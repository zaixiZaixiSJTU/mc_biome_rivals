#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BiomeRivals.Content;
using BiomeRivals.Core;
using BiomeRivals.Networking;
using UnityEngine;

namespace BiomeRivals.Demo
{
    // Ordinary shuffled decks and accepted commands only; barriers contain public facts, never hands.
    public sealed class DemoOnlineGoatProbe : IDisposable
    {
        private readonly IMatchGateway _gateway;
        private readonly MatchStateStore _store;
        private readonly DemoOnlineMatchSession _session;
        private readonly CardContentRegistry _registry;
        private MatchEventDto _target, _goat, _move;
        private int _moveRevision;
        private Exception _failure;
        private DemoAuthoritativeMatchView View => _session.View;
        private bool IsMover => View.ViewerIndex == 0;
        private string Role => IsMover ? "mover" : "observer";

        public DemoOnlineGoatProbe(IMatchGateway gateway, MatchStateStore store, DemoOnlineMatchSession session, CardContentRegistry registry)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _gateway.EventBatchReceived += ObserveBatch;
            _gateway.Faulted += ObserveFault;
        }

        public static int[] SelectMulligan(string[] hand)
        {
            hand = hand ?? Array.Empty<string>();
            var target = Array.IndexOf(hand, "si_002");
            var goat = Array.IndexOf(hand, "si_004");
            return Enumerable.Range(0, hand.Length).Where(index => index != target && index != goat).ToArray();
        }

        public async Task<DemoOnlineGoatReport> RunAsync(float deadline, string directory,
            Func<HandCardStateDto, DemoSlotKind, int, float, Task> deployUi,
            Func<HandCardStateDto, int, string, float, Task> deployGoatUi,
            Func<string, string, float, Task<bool>> inspectWorld, Func<float, Task<bool>> reconnect)
        {
            Require(_store.Current != null && View.ViewerIndex >= 0 && View.PlayerFactionId == FactionIds.SnowIce &&
                View.OpponentFactionId == FactionIds.SnowIce && Directory.Exists(directory), "Goat probe requires ordinary Snow decks and an evidence directory.");
            var matchId = _store.Current.matchId;
            if (!IsMover && View.IsMulligan) await Wait(() => View.OpponentMulliganCompleted, deadline);
            if (View.IsMulligan && !View.PlayerMulliganCompleted)
                await Accept(_session.MulliganAsync(IsMover ? SelectMulligan(View.Hand.ToArray()) : Array.Empty<int>()));
            await Wait(() => !View.IsMulligan, deadline);
            var targetUi = false;
            var goatUi = false;
            while (_move == null)
            {
                Check(deadline);
                Require(!View.IsFinished, "Match ended before the goat movement.");
                if (!View.IsPlayerTurn || !_session.CanIssueCommand) { await Task.Yield(); continue; }
                if (View.PendingChoice != null)
                {
                    var option = View.PendingChoice.options.FirstOrDefault(value => value != null && value.selectable);
                    Require(option != null && View.IsChoiceOwner, "Cannot resolve normal preparation choice.");
                    await Accept(_session.ResolveChoiceAsync(View.PendingChoice.choiceId, option.optionIndex));
                    continue;
                }
                if (View.Phase != DemoTurnPhase.Main) { await Accept(_session.EndTurnAsync()); continue; }
                if (IsMover)
                {
                    var target = View.HandCards.FirstOrDefault(value => value?.cardId == "si_002" && Cost(value) <= View.Energy);
                    if (_target == null && target != null)
                    {
                        await deployUi(target, DemoSlotKind.Unit, View.UnitSlots.Length - 3, deadline);
                        Require(_target != null, "Target UI deployment event missing.");
                        targetUi = true;
                        continue;
                    }
                    var goat = View.HandCards.FirstOrDefault(value => value?.cardId == "si_004" && Cost(value) <= View.Energy);
                    if (_target != null && goat != null)
                    {
                        await deployGoatUi(goat, View.UnitSlots.Length - 2, _target.payload.instanceId, deadline);
                        Require(_goat != null && _move != null, "Targeted goat UI deployment did not produce movement.");
                        goatUi = true;
                        continue;
                    }
                }
                // Draw room is made through legal commands. The three critical cells stay reserved.
                if (View.Hand.Count >= 6 || !IsMover && View.PlayerBattlefield.Count == 0)
                    if (await PrepareDrawRoom()) continue;
                await Accept(_session.EnterCombatAsync());
            }
            await Wait(() => !_session.HasPendingCommand, deadline);
            Require(!IsMover || targetUi && goatUi, "Mover never used both real UI deployment paths.");
            Require(View.Revision == _moveRevision, "A command changed the frozen movement revision.");
            var frozenRevision = View.Revision;
            ValidateMovement(_store.Current, _target.payload.instanceId, _goat.payload.instanceId);
            DemoOnlineEndReturnProbe.ValidateHiddenHands(_store.Current);
            Require(await inspectWorld(_target.payload.instanceId, _goat.payload.instanceId, deadline), "Moved 3D models are not aligned.");
            var hash = DemoOnlineBuildingProbe.PublicBoardHash(_store.Current);
            await Barrier(directory, "moved", matchId, frozenRevision, hash, deadline);
            Require(await reconnect(deadline) && _store.Current.matchId == matchId && View.Revision == frozenRevision,
                "Reconnect did not recover the exact movement revision.");
            ValidateMovement(_store.Current, _target.payload.instanceId, _goat.payload.instanceId);
            DemoOnlineEndReturnProbe.ValidateHiddenHands(_store.Current);
            Require(DemoOnlineBuildingProbe.PublicBoardHash(_store.Current) == hash &&
                await inspectWorld(_target.payload.instanceId, _goat.payload.instanceId, deadline), "Recovered movement state or models differ.");
            await Barrier(directory, "recovered", matchId, frozenRevision, hash, deadline);
            if (!IsMover) await Accept(_session.ConcedeAsync());
            await Wait(() => View.IsFinished, deadline);
            Require(View.Revision == frozenRevision + 1 && _store.Current.winnerPlayerId == _store.Current.players[0].playerId,
                "Unexpected terminal movement settlement.");
            ValidateMovement(_store.Current, _target.payload.instanceId, _goat.payload.instanceId);
            Require(await inspectWorld(_target.payload.instanceId, _goat.payload.instanceId, deadline), "Terminal movement models differ.");
            return new DemoOnlineGoatReport
            {
                ok = true, role = Role, matchId = matchId, viewerPlayerId = _store.Current.viewerPlayerId,
                arenaId = View.ArenaId, unitSlotCount = View.UnitSlots.Length, buildingSlotCount = View.BuildingSlots.Length,
                revision = View.Revision, frozenRevision = frozenRevision, recoveredRevision = frozenRevision,
                targetInstanceId = _target.payload.instanceId, goatInstanceId = _goat.payload.instanceId,
                targetEventId = _target.eventId, goatEventId = _goat.eventId, movementEventId = _move.eventId,
                fromSlotIndex = _move.payload.fromSlotIndex, toSlotIndex = _move.payload.toSlotIndex, goatSlotIndex = _goat.payload.slotIndex,
                movementSourceInstanceId = _move.payload.sourceInstanceId, movementSourceCardId = _move.payload.sourceCardId,
                movementEffectId = _move.payload.effectId, movementEventCount = 1, publicBoardHash = hash,
                performedTargetUiDeploy = targetUi, performedGoatUiDeploy = goatUi, paymentVerified = IsMover && targetUi && goatUi,
                eventCausalityVerified = true, footprintVerified = true, worldVerified = true, privateProjectionVerified = true,
                reconnectRecovered = true, recoveryStateVerified = true,
                matchStatus = _store.Current.status, winnerPlayerId = _store.Current.winnerPlayerId,
                playerFaction = View.PlayerFactionId, opponentFaction = View.OpponentFactionId,
                playerLife = View.PlayerLife, opponentLife = View.OpponentLife,
                playerUnitCount = View.PlayerBattlefield.Count(value => value.SlotKind == DemoSlotKind.Unit && value.Health > 0),
                opponentUnitCount = View.OpponentBattlefield.Count(value => value.SlotKind == DemoSlotKind.Unit && value.Health > 0)
            };
        }

        private async Task<bool> PrepareDrawRoom()
        {
            var freeUnit = Array.FindIndex(View.UnitSlots, string.IsNullOrEmpty);
            if (IsMover && freeUnit >= View.UnitSlots.Length - 3) freeUnit = -1;
            var unit = View.HandCards.FirstOrDefault(value => value != null &&
                (value.cardId == "si_005" || !IsMover && value.cardId == "si_002") && Cost(value) <= View.Energy);
            if (unit != null && freeUnit >= 0)
            { await Accept(_session.DeployAsync(unit.cardId, DemoSlotKind.Unit, freeUnit, unit.handCardInstanceId)); return true; }
            var hut = View.HandCards.FirstOrDefault(value => value?.cardId == "si_007" && Cost(value) <= View.Energy);
            var freeBuilding = Array.FindIndex(View.BuildingSlots, string.IsNullOrEmpty);
            if (hut != null && freeBuilding >= 0)
            { await Accept(_session.DeployAsync(hut.cardId, DemoSlotKind.Building, freeBuilding, hut.handCardInstanceId)); return true; }
            var spire = IsMover ? View.HandCards.FirstOrDefault(value => value?.cardId == "si_008" && Cost(value) <= View.Energy) : null;
            var consecutive = Enumerable.Range(0, View.BuildingSlots.Length - 1).Where(index =>
                string.IsNullOrEmpty(View.BuildingSlots[index]) && string.IsNullOrEmpty(View.BuildingSlots[index + 1])).DefaultIfEmpty(-1).First();
            if (spire != null && consecutive >= 0)
            { await Accept(_session.DeployAsync(spire.cardId, DemoSlotKind.Building, consecutive, spire.handCardInstanceId)); return true; }
            var enemy = View.OpponentBattlefield.FirstOrDefault(value => value.SlotKind == DemoSlotKind.Unit && value.Health > 0);
            var snow = View.HandCards.FirstOrDefault(value => value != null && (value.cardId == "si_001" || value.cardId == "si_006") && Cost(value) <= View.Energy);
            // Observer must never damage the mover's critical object.
            if (IsMover && enemy != null && snow != null)
            { await Accept(_session.PlayCardAsync(snow.cardId, snow.handCardInstanceId, "UNIT", enemy.InstanceId)); return true; }
            return false;
        }

        private int Cost(HandCardStateDto value) => _registry.TryGetDefinition(value.cardId, out var definition)
            ? View.GetEffectiveCost(definition, value.handCardInstanceId) : int.MaxValue;

        private void ObserveBatch(MatchEventBatchDto batch)
        {
            try
            {
                foreach (var value in batch.events ?? Array.Empty<MatchEventDto>())
                {
                    var payload = value?.payload;
                    if (payload == null || payload.playerId != _store.Current.players[0].playerId) continue;
                    if (value.type == MatchEventTypes.CardDeployed && payload.cardId == "si_002" && payload.slotIndex == View.UnitSlots.Length - 3)
                    { Require(_target == null && payload.slotKind == "UNIT" && payload.occupiedSlots == 1, "Duplicate/invalid target deployment."); _target = value; }
                    if (value.type == MatchEventTypes.CardDeployed && payload.cardId == "si_004")
                    { Require(_goat == null && _target != null && payload.slotIndex == View.UnitSlots.Length - 2 && payload.occupiedSlots == 1, "Unexpected goat deployment."); _goat = value; }
                    if (value.type != MatchEventTypes.ObjectMoved || payload.effectId != "effect.si_004.01") continue;
                    Require(_move == null && _target != null && _goat != null && _target.eventId < _goat.eventId && _goat.eventId < value.eventId &&
                        payload.instanceId == _target.payload.instanceId && payload.cardId == "si_002" &&
                        payload.sourceInstanceId == _goat.payload.instanceId && payload.sourceCardId == "si_004" &&
                        payload.sourcePlayerId == payload.playerId && payload.fromSlotIndex == View.UnitSlots.Length - 3 &&
                        payload.toSlotIndex == View.UnitSlots.Length - 1, "Goat movement event causality is invalid.");
                    _move = value;
                    _moveRevision = batch.revision;
                }
            }
            catch (Exception exception) { _failure = exception; }
        }

        public static void ValidateMovement(MatchStateDto state, string targetId, string goatId)
        {
            Require(state?.players?.Length == 2 && !string.IsNullOrEmpty(targetId) && targetId != goatId, "Movement identities missing.");
            var owner = state.players[0];
            var slots = owner.unitSlots;
            Require(slots != null && slots.Length >= 3, "Movement capacity missing.");
            var target = owner.battlefield.SingleOrDefault(value => value.instanceId == targetId);
            var goat = owner.battlefield.SingleOrDefault(value => value.instanceId == goatId);
            Require(target != null && goat != null && target.cardId == "si_002" && goat.cardId == "si_004" &&
                target.ownerPlayerId == owner.playerId && goat.ownerPlayerId == owner.playerId &&
                target.slotKind == "UNIT" && goat.slotKind == "UNIT" && target.occupiedSlots == 1 && goat.occupiedSlots == 1 &&
                target.attack == 1 && target.health == 4 && target.maxHealth == 4 && goat.health == 2 &&
                target.slotIndex == slots.Length - 1 && goat.slotIndex == slots.Length - 2 &&
                string.IsNullOrEmpty(slots[slots.Length - 3]) && slots[slots.Length - 2] == goatId && slots[slots.Length - 1] == targetId &&
                slots.Count(value => value == targetId) == 1 && slots.Count(value => value == goatId) == 1 &&
                goat.attack == 4 && goat.temporaryAttackModifier == 1 && goat.temporaryAttackModifierExpiresOnTurn == state.turn,
                "Moved identity/empty origin/exact destination/goat battlecry state is inconsistent.");
        }

        private async Task Barrier(string directory, string stage, string matchId, int revision, string hash, float deadline)
        {
            using (var file = new FileStream(Path.Combine(directory, $"goat-{stage}-{Role}.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file))
                writer.Write(JsonUtility.ToJson(new Marker { matchId = matchId, stage = stage, role = Role, revision = revision, hash = hash }));
            Marker peer = null;
            var path = Path.Combine(directory, $"goat-{stage}-{(IsMover ? "observer" : "mover")}.json");
            await Wait(() =>
            {
                if (!File.Exists(path)) return false;
                try { peer = JsonUtility.FromJson<Marker>(File.ReadAllText(path)); } catch (IOException) { return false; }
                return peer != null;
            }, deadline);
            Require(peer.matchId == matchId && peer.stage == stage && peer.role == (IsMover ? "observer" : "mover") &&
                peer.revision == revision && peer.hash == hash, "Players disagree on the frozen/recovered public movement state.");
        }
        private async Task Wait(Func<bool> condition, float deadline)
        { while (!condition()) { Check(deadline); await Task.Yield(); } Check(deadline); }
        private void Check(float deadline)
        {
            if (_failure != null) throw new InvalidOperationException("Goat probe event failure.", _failure);
            if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException("Goat probe deadline expired.");
        }
        private void ObserveFault(Exception exception) => _failure = exception;
        private static async Task Accept(Task<MatchCommandDispatchResult> command)
        {
            var result = await command;
            Require(result.Outcome == MatchCommandOutcome.Accepted,
                $"Ordinary goat preparation command failed: {result.Outcome}, {result.Code} - {result.Message}.");
        }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        public void Dispose() { _gateway.EventBatchReceived -= ObserveBatch; _gateway.Faulted -= ObserveFault; }
        [Serializable] private sealed class Marker { public string matchId, stage, role, hash; public int revision; }
    }

    [Serializable]
    public sealed class DemoOnlineGoatReport
    {
        public bool ok, performedTargetUiDeploy, performedGoatUiDeploy, paymentVerified, eventCausalityVerified, footprintVerified;
        public bool worldVerified, privateProjectionVerified, reconnectRecovered, recoveryStateVerified;
        public string role, matchId, viewerPlayerId, arenaId, targetInstanceId, goatInstanceId, publicBoardHash;
        public string movementSourceInstanceId, movementSourceCardId, movementEffectId;
        public string matchStatus, winnerPlayerId, playerFaction, opponentFaction, accountPhase, accountUserId, accountDisplayName;
        public int revision, frozenRevision, recoveredRevision, unitSlotCount, buildingSlotCount, fromSlotIndex, toSlotIndex, goatSlotIndex, movementEventCount;
        public int playerLife, opponentLife, playerUnitCount, opponentUnitCount;
        public long targetEventId, goatEventId, movementEventId;
    }
}
#endif
