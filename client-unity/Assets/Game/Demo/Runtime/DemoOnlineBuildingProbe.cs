#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BiomeRivals.Content;
using BiomeRivals.Core;
using BiomeRivals.Networking;
using UnityEngine;

namespace BiomeRivals.Demo
{
    // Uses ordinary decks/commands. Barriers carry public frozen-state facts only.
    public sealed class DemoOnlineBuildingProbe : IDisposable
    {
        private readonly IMatchGateway _gateway;
        private readonly MatchStateStore _store;
        private readonly DemoOnlineMatchSession _session;
        private readonly CardContentRegistry _registry;
        private readonly Dictionary<int, MatchEventDto> _deployed = new Dictionary<int, MatchEventDto>();
        private Exception _failure;
        private DemoAuthoritativeMatchView View => _session.View;
        private int Seat => View.ViewerIndex;
        private string CardId => Seat == 0 ? "cd_004" : "cd_007";
        private string Role => Seat == 0 ? "single" : "structure";
        private int Width => Seat == 0 ? 1 : 2;

        public DemoOnlineBuildingProbe(IMatchGateway gateway, MatchStateStore store, DemoOnlineMatchSession session, CardContentRegistry registry)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _gateway.EventBatchReceived += ObserveBatch;
            _gateway.Faulted += ObserveFault;
        }

        public static int[] SelectMulligan(string[] hand, string cardId)
        {
            hand = hand ?? Array.Empty<string>();
            var keep = Array.IndexOf(hand, cardId);
            return Enumerable.Range(0, hand.Length).Where(index => index != keep).ToArray();
        }

        public async Task<DemoOnlineBuildingReport> RunAsync(float deadline, string directory,
            Func<HandCardStateDto, DemoSlotKind, int, float, Task> deployUi,
            Func<float, Task<bool>> inspectWorld, Func<float, Task<bool>> reconnect, DemoOnlineDeploymentAudit deploymentAudit = null)
        {
            Require(_store.Current != null && Seat >= 0 && View.PlayerFactionId == FactionIds.CaveDarkForest &&
                View.OpponentFactionId == FactionIds.CaveDarkForest, "Building probe requires two ordinary Cave decks.");
            Require(Directory.Exists(directory), "Building probe needs an existing unique evidence directory.");
            var matchId = _store.Current.matchId;
            // Serialize mulligans so the normal revision contract does not race.
            if (Seat == 1 && View.IsMulligan) await Wait(() => View.OpponentMulliganCompleted, deadline);
            if (View.IsMulligan && !View.PlayerMulliganCompleted)
                await Accept(_session.MulliganAsync(deploymentAudit != null
                    ? DemoOnlineDeploymentAudit.SelectMulligan(View.Hand.ToArray(), Seat) : SelectMulligan(View.Hand.ToArray(), CardId)));
            await Wait(() => !View.IsMulligan, deadline);
            var uiAccepted = false;
            while (_deployed.Count < 2 || deploymentAudit != null && !deploymentAudit.BothPlayersCompleted)
            {
                Check(deadline);
                Require(!View.IsFinished, "Match ended before both buildings were deployed.");
                if (!View.IsPlayerTurn || !_session.CanIssueCommand) { await Task.Yield(); continue; }
                if (View.PendingChoice != null)
                {
                    var option = View.PendingChoice.options.FirstOrDefault(value => value != null && value.selectable);
                    Require(option != null && View.IsChoiceOwner, "Cannot resolve normal draw-preparation choice.");
                    await Accept(_session.ResolveChoiceAsync(View.PendingChoice.choiceId, option.optionIndex));
                    continue;
                }
                if (View.Phase != DemoTurnPhase.Main)
                {
                    if (deploymentAudit != null && View.Phase == DemoTurnPhase.Combat && await deploymentAudit.TryPrepareCombatAsync()) continue;
                    await Accept(_session.EndTurnAsync());
                    continue;
                }
                if (deploymentAudit != null && !deploymentAudit.UnitVerified)
                {
                    var unitCard = deploymentAudit.FindUnitCard();
                    if (unitCard != null)
                    {
                        await deploymentAudit.BeforeUnitAsync(unitCard, deadline);
                        await deployUi(unitCard, DemoSlotKind.Unit, View.UnitSlots.Length - 1, deadline);
                        deploymentAudit.UnitAccepted();
                        continue;
                    }
                }
                if (deploymentAudit != null && await deploymentAudit.TryOccupiedAsync(deadline)) continue;
                var card = View.HandCards.FirstOrDefault(value => value != null && value.cardId == CardId && Cost(value) <= View.Energy);
                if (!_deployed.ContainsKey(Seat) && card != null && (deploymentAudit == null || deploymentAudit.UnitVerified))
                {
                    if (deploymentAudit != null) await deploymentAudit.BeforeBuildingAsync(card, deadline);
                    await deployUi(card, DemoSlotKind.Building, View.BuildingSlots.Length - Width, deadline);
                    deploymentAudit?.BuildingAccepted();
                    Require(_deployed.ContainsKey(Seat), "UI acknowledgement did not contain the expected building deployment.");
                    uiAccepted = true;
                    continue;
                }
                // Keep legal draw room without rewriting the deck or manufacturing resources.
                // Never use the reserved building cells for preparation.
                if ((!_deployed.ContainsKey(Seat) || deploymentAudit != null && !deploymentAudit.BothPlayersCompleted) && View.Hand.Count >= 6)
                {
                    // Death drops and structure production also occupy real hand slots.
                    // Consume them through normal targeted material commands; never discard
                    // or rewrite a hand to make this development scenario pass.
                    if (TrySelectPreparationMaterial(View, out var material, out var materialTarget) && Cost(material) <= View.Energy)
                    {
                        var revision = View.Revision;
                        await Accept(_session.PlayCardAsync(material.cardId, material.handCardInstanceId,
                            materialTarget.SlotKind == DemoSlotKind.Unit ? "UNIT" : "BUILDING", materialTarget.InstanceId));
                        Require(View.Revision == revision + 1 && !View.HandCards.Any(value => value?.handCardInstanceId == material.handCardInstanceId),
                            "Normal preparation material did not consume its exact hand instance.");
                        continue;
                    }
                    var unit = View.HandCards.FirstOrDefault(value => value != null &&
                        (value.cardId == "cd_001" || value.cardId == "cd_002" || value.cardId == "cd_003" || value.cardId == "cd_005") &&
                        (deploymentAudit == null || deploymentAudit.CanPrepareUnit(value)) && Cost(value) <= View.Energy);
                    var free = Array.FindIndex(View.UnitSlots, string.IsNullOrEmpty);
                    if (unit != null && free >= 0)
                    {
                        await Accept(_session.DeployAsync(unit.cardId, DemoSlotKind.Unit, free, unit.handCardInstanceId));
                        continue;
                    }
                    var darkness = View.HandCards.FirstOrDefault(value => value != null && value.cardId == "cd_006" && Cost(value) <= View.Energy);
                    if (darkness != null) { await Accept(_session.PlayCardAsync(darkness.cardId, darkness.handCardInstanceId)); continue; }
                }
                await Accept(_session.EnterCombatAsync());
            }
            await Wait(() => !_session.HasPendingCommand, deadline);
            Require(uiAccepted, "This viewer never deployed its building through UI.");
            var frozenRevision = View.Revision;
            ValidateBuildings();
            DemoOnlineEndReturnProbe.ValidateHiddenHands(_store.Current);
            Require(await inspectWorld(deadline), "Building world footprint/mesh alignment failed.");
            var hash = PublicBoardHash(_store.Current);
            await Barrier(directory, "deployed", matchId, frozenRevision, hash, deadline);
            Require(await reconnect(deadline) && _store.Current.matchId == matchId && View.Revision == frozenRevision,
                "Reconnect did not recover the exact frozen building revision.");
            ValidateBuildings();
            DemoOnlineEndReturnProbe.ValidateHiddenHands(_store.Current);
            Require(PublicBoardHash(_store.Current) == hash && await inspectWorld(deadline), "Building state/model changed after reconnect.");
            await Barrier(directory, "recovered", matchId, frozenRevision, hash, deadline);
            if (Seat == 1) await Accept(_session.ConcedeAsync());
            await Wait(() => View.IsFinished, deadline);
            Require(View.Revision == frozenRevision + 1 && _store.Current.winnerPlayerId == _store.Current.players[0].playerId,
                "Terminal settlement changed the expected frozen revision/winner.");
            ValidateBuildings();
            Require(await inspectWorld(deadline), "Terminal world geometry lost a building.");
            return new DemoOnlineBuildingReport
            {
                ok = true, role = Role, matchId = matchId, viewerPlayerId = _store.Current.viewerPlayerId,
                deploymentRejectionsVerified = deploymentAudit?.LocalComplete == true,
                deploymentRejections = deploymentAudit?.Cases ?? Array.Empty<DemoDeploymentRejectionProof>(),
                arenaId = View.ArenaId, unitSlotCount = View.UnitSlots.Length, buildingSlotCount = View.BuildingSlots.Length,
                revision = View.Revision, frozenRevision = frozenRevision, recoveredRevision = frozenRevision,
                singleInstanceId = _deployed[0].payload.instanceId, structureInstanceId = _deployed[1].payload.instanceId,
                singleSlotIndex = View.BuildingSlots.Length - 1, structureSlotIndex = View.BuildingSlots.Length - 2,
                singleEventId = _deployed[0].eventId, structureEventId = _deployed[1].eventId,
                publicBoardHash = hash, deploymentEventHash = Hash(string.Join("|", _deployed.Values.OrderBy(value => value.eventId)
                    .Select(value => $"{value.eventId}:{value.payload.playerId}:{value.payload.cardId}:{value.payload.instanceId}:{value.payload.slotIndex}:{value.payload.occupiedSlots}"))),
                performedUiDeploy = true, paymentVerified = true, worldVerified = true, privateProjectionVerified = true,
                reconnectRecovered = true, recoveryStateVerified = true, matchStatus = _store.Current.status,
                winnerPlayerId = _store.Current.winnerPlayerId, playerFaction = View.PlayerFactionId, opponentFaction = View.OpponentFactionId,
                playerLife = View.PlayerLife, opponentLife = View.OpponentLife,
                playerUnitCount = View.PlayerBattlefield.Count(value => value.SlotKind == DemoSlotKind.Unit && value.Health > 0),
                opponentUnitCount = View.OpponentBattlefield.Count(value => value.SlotKind == DemoSlotKind.Unit && value.Health > 0)
            };
        }

        private int Cost(HandCardStateDto value) => _registry.TryGetDefinition(value.cardId, out var definition)
            ? View.GetEffectiveCost(definition, value.handCardInstanceId) : int.MaxValue;

        private void ObserveBatch(MatchEventBatchDto batch)
        {
            try
            {
                foreach (var value in batch.events ?? Array.Empty<MatchEventDto>())
                {
                    if (value?.type != MatchEventTypes.CardDeployed) continue;
                    var seat = Array.FindIndex(_store.Current.players, player => player.playerId == value.payload.playerId);
                    if (seat < 0 || value.payload.cardId != (seat == 0 ? "cd_004" : "cd_007")) continue;
                    Require(value.payload.slotKind == "BUILDING" && value.payload.slotIndex == View.BuildingSlots.Length - (seat == 0 ? 1 : 2) &&
                        value.payload.occupiedSlots == (seat == 0 ? 1 : 2) && !_deployed.ContainsKey(seat), "Unexpected critical building deployment.");
                    _deployed.Add(seat, value);
                }
            }
            catch (Exception exception) { _failure = exception; }
        }

        public static bool TrySelectPreparationMaterial(DemoAuthoritativeMatchView view,
            out HandCardStateDto card, out DemoBattlefieldObject target)
        {
            card = null;
            target = null;
            if (!view.IsPlayerTurn || view.IsFinished || view.IsMulligan || view.Phase != DemoTurnPhase.Main || view.PendingChoice != null)
                return false;
            foreach (var candidate in view.HandCards)
            {
                if (candidate == null || (candidate.cardId != "tk_009" && candidate.cardId != "tk_010")) continue;
                var kind = candidate.cardId == "tk_009" ? DemoSlotKind.Unit : DemoSlotKind.Building;
                var friendly = view.PlayerBattlefield.OrderBy(value => value.SlotIndex)
                    .FirstOrDefault(value => value.SlotKind == kind && value.Health > 0);
                if (friendly == null) continue;
                card = candidate;
                target = friendly;
                return true;
            }
            return false;
        }

        private void ValidateBuildings()
        {
            Require(_deployed.Count == 2, "Both critical deployments are required.");
            foreach (var entry in _deployed)
            {
                var owner = _store.Current.players[entry.Key];
                var payload = entry.Value.payload;
                var value = owner.battlefield.SingleOrDefault(candidate => candidate.instanceId == payload.instanceId);
                Require(value != null && value.ownerPlayerId == owner.playerId && value.cardId == payload.cardId &&
                    value.slotKind == "BUILDING" && value.slotIndex == payload.slotIndex && value.occupiedSlots == payload.occupiedSlots && value.health > 0,
                    "Authoritative building identity/anchor/width changed.");
                Require(owner.buildingSlots.Count(id => id == value.instanceId) == value.occupiedSlots,
                    "Building did not occupy exactly its declared number of cells.");
                for (var offset = 0; offset < value.occupiedSlots; offset++)
                    Require(owner.buildingSlots[value.slotIndex + offset] == value.instanceId, "Structure footprint is not contiguous.");
            }
        }

        public static string PublicBoardHash(MatchStateDto state) => Hash(JsonUtility.ToJson(new PublicBoard
        {
            arenaId = state.arenaId,
            players = state.players.Select(player => new PublicPlayer { playerId = player.playerId,
                unitSlots = player.unitSlots, buildingSlots = player.buildingSlots, battlefield = player.battlefield }).ToArray()
        }));

        private async Task Barrier(string directory, string stage, string matchId, int revision, string hash, float deadline)
        {
            var path = Path.Combine(directory, $"building-{stage}-{Role}.json");
            using (var file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file))
                writer.Write(JsonUtility.ToJson(new Marker { matchId = matchId, stage = stage, role = Role, revision = revision, hash = hash }));
            var peerPath = Path.Combine(directory, $"building-{stage}-{(Seat == 0 ? "structure" : "single")}.json");
            Marker peer = null;
            await Wait(() =>
            {
                if (!File.Exists(peerPath)) return false;
                try { peer = JsonUtility.FromJson<Marker>(File.ReadAllText(peerPath)); }
                catch (IOException) { return false; }
                return peer != null;
            }, deadline);
            Require(peer.matchId == matchId && peer.stage == stage && peer.role != Role && peer.revision == revision && peer.hash == hash,
                "Players did not freeze/recover the same public building state.");
        }

        private async Task Wait(Func<bool> condition, float deadline)
        {
            while (!condition()) { Check(deadline); await Task.Yield(); }
            Check(deadline);
        }
        private void Check(float deadline)
        {
            if (_failure != null) throw new InvalidOperationException("Building probe event failure.", _failure);
            if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException("Building probe deadline expired.");
        }
        private void ObserveFault(Exception value) => _failure = value;
        private static async Task Accept(Task<MatchCommandDispatchResult> command) =>
            Require((await command).Outcome == MatchCommandOutcome.Accepted, "Normal building preparation command was not accepted.");
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private static string Hash(string value)
        {
            using (var sha = SHA256.Create()) return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(value))).Replace("-", string.Empty);
        }
        public void Dispose() { _gateway.EventBatchReceived -= ObserveBatch; _gateway.Faulted -= ObserveFault; }
        [Serializable] private sealed class Marker { public string matchId, stage, role, hash; public int revision; }
        [Serializable] private sealed class PublicBoard { public string arenaId; public PublicPlayer[] players; }
        [Serializable] private sealed class PublicPlayer { public string playerId; public string[] unitSlots, buildingSlots; public BattlefieldObjectStateDto[] battlefield; }
    }

    [Serializable]
    public sealed class DemoOnlineBuildingReport
    {
        public bool deploymentRejectionsVerified;
        public DemoDeploymentRejectionProof[] deploymentRejections = Array.Empty<DemoDeploymentRejectionProof>();
        public bool ok, performedUiDeploy, paymentVerified, worldVerified, privateProjectionVerified, reconnectRecovered, recoveryStateVerified;
        public string role, matchId, viewerPlayerId, arenaId, singleInstanceId, structureInstanceId, publicBoardHash, deploymentEventHash;
        public string matchStatus, winnerPlayerId, playerFaction, opponentFaction, accountPhase, accountUserId, accountDisplayName;
        public int revision, frozenRevision, recoveredRevision, unitSlotCount, buildingSlotCount, singleSlotIndex, structureSlotIndex;
        public int playerLife, opponentLife, playerUnitCount, opponentUnitCount;
        public long singleEventId, structureEventId;
    }
}
#endif
