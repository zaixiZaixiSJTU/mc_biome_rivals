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
    // Optional development audit for the ordinary building probe. No injected cards or resources.
    public sealed class DemoOnlineDeploymentAudit : IDisposable
    {
        private readonly IMatchGateway _gateway;
        private readonly MatchStateStore _store;
        private readonly DemoOnlineMatchSession _session;
        private readonly CardContentRegistry _registry;
        private readonly Func<float, Task<bool>> _recover;
        private readonly string _directory;
        private readonly List<DemoDeploymentRejectionProof> _cases = new List<DemoDeploymentRejectionProof>();
        private readonly Dictionary<string, CommandRejectionDto> _wire = new Dictionary<string, CommandRejectionDto>();
        private int _batches;
        private bool _unitLegal, _buildingLegal, _followupLegal, _marked;
        private string _criticalUnitId;
        private bool _unitMarked;
        private Exception _failure;
        private DemoAuthoritativeMatchView View => _session.View;
        private int Seat => View.ViewerIndex;
        private string Role => Seat == 0 ? "single" : "structure";
        public bool UnitVerified => _unitLegal;
        public bool LocalComplete => _unitLegal && _buildingLegal && _followupLegal &&
            _cases.Count == (Seat == 0 ? 7 : 8);
        public DemoDeploymentRejectionProof[] Cases => _cases.ToArray();

        public DemoOnlineDeploymentAudit(IMatchGateway gateway, MatchStateStore store, DemoOnlineMatchSession session,
            CardContentRegistry registry, string directory, Func<float, Task<bool>> recover)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _recover = recover ?? throw new ArgumentNullException(nameof(recover));
            _directory = directory;
            Require(Directory.Exists(directory), "Deployment audit needs a unique existing evidence directory.");
            _gateway.CommandRejected += ObserveRejection;
            _gateway.EventBatchReceived += ObserveBatch;
        }

        public static int[] SelectMulligan(string[] hand, int seat)
        {
            hand = hand ?? Array.Empty<string>();
            var keep = new HashSet<int>();
            var buildings = new[] { "cd_004", "cd_007" };
            foreach (var id in buildings)
            {
                var index = Enumerable.Range(0, hand.Length).Where(value => hand[value] == id && !keep.Contains(value)).DefaultIfEmpty(-1).First();
                if (index >= 0) keep.Add(index);
            }
            foreach (var index in Enumerable.Range(0, hand.Length).Where(value => IsSafeUnit(hand[value])).Take(2)) keep.Add(index);
            return Enumerable.Range(0, hand.Length).Where(index => !keep.Contains(index)).ToArray();
        }

        public bool BothPlayersCompleted
        {
            get
            {
                Check();
                if (!LocalComplete || !_marked) return false;
                var path = Path.Combine(_directory, $"deployment-audit-done-{(Seat == 0 ? "structure" : "single")}.json");
                if (!File.Exists(path)) return false;
                Marker peer;
                try { peer = JsonUtility.FromJson<Marker>(File.ReadAllText(path)); }
                catch (IOException) { return false; }
                Require(peer != null && peer.matchId == _store.Current.matchId && peer.role == (Seat == 0 ? "structure" : "single") &&
                    peer.caseCount == (Seat == 0 ? 8 : 7) && peer.revision > 0, "Invalid peer deployment-audit completion marker.");
                // The file may arrive before that player's accepted follow-up event on the socket.
                return View.Revision >= peer.revision;
            }
        }

        public HandCardStateDto FindUnitCard() => View.HandCards.FirstOrDefault(value => value != null && IsSafeUnit(value.cardId) && Cost(value) <= View.Energy);
        public bool CanPrepareUnit(HandCardStateDto card)
        {
            if (card == null || !IsSafeUnit(card.cardId)) return false;
            if (!_unitLegal || _cases.Any(value => value.label == "unit-occupied")) return true;
            var reserved = View.HandCards.FirstOrDefault(value => value != null && IsSafeUnit(value.cardId));
            return card.handCardInstanceId != reserved?.handCardInstanceId;
        }
        // Ordinary unit consumption is safe during the protected pre-combat prefix.
        // Later random deathrattles are legal gameplay, not rejection mutations.
        private static bool IsSafeUnit(string id) => id == "cd_001" || id == "cd_002" || id == "cd_003" || id == "cd_005";
        private int Cost(HandCardStateDto value) => _registry.TryGetDefinition(value.cardId, out var definition)
            ? View.GetEffectiveCost(definition, value.handCardInstanceId) : int.MaxValue;

        public static bool TrySelectPreparationCombat(DemoAuthoritativeMatchView view,
            out DemoBattlefieldObject attacker, out DemoBattlefieldObject target)
        {
            attacker = view.PlayerBattlefield.OrderBy(value => value.SlotIndex).FirstOrDefault(value =>
                value.SlotKind == DemoSlotKind.Unit && value.SlotIndex < view.UnitSlots.Length - 1 && view.CanAttackWith(value, out _));
            target = view.OpponentBattlefield.OrderBy(value => value.SlotIndex).FirstOrDefault(value =>
                value.SlotKind == DemoSlotKind.Unit && value.SlotIndex < view.UnitSlots.Length - 1 && view.CanAttackTarget(value, "UNIT", out _));
            return attacker != null && target != null;
        }

        public async Task<bool> TryPrepareCombatAsync()
        {
            // CD-003 deathrattles can hit a last-cell unit indirectly. Finish both
            // occupancy rejections before ANY preparation combat can cause damage.
            if (!BothUnitPrefixesVerified) return false;
            // Normal combat frees preparation cells/hand room. Never attack either
            // critical last-cell unit, any building, or a hero to manufacture a victory.
            if (!TrySelectPreparationCombat(View, out var attacker, out var target)) return false;
            var revision = View.Revision;
            var result = await _session.AttackAsync(attacker.InstanceId, "UNIT", target.InstanceId);
            Require(result.Outcome == MatchCommandOutcome.Accepted && View.Revision == revision + 1,
                "Normal preparation combat was not accepted.");
            return true;
        }

        public bool BothUnitPrefixesVerified
        {
            get
            {
                if (!_unitMarked) return false;
                var peerRole = Seat == 0 ? "structure" : "single";
                var path = Path.Combine(_directory, $"deployment-unit-done-{peerRole}.json");
                if (!File.Exists(path)) return false;
                try { return UnitPrefixMarkerHasConverged(File.ReadAllText(path), _store.Current.matchId, peerRole, View.Revision); }
                catch (IOException) { return false; }
            }
        }

        public static bool UnitPrefixMarkerHasConverged(string json, string matchId, string role, int revision)
        {
            var peer = JsonUtility.FromJson<Marker>(json);
            Require(peer != null && peer.matchId == matchId && peer.role == role &&
                peer.caseCount == 4 && peer.revision > 0, "Invalid peer unit-prefix marker.");
            return revision >= peer.revision;
        }

        private void MarkUnitPrefix()
        {
            Require(!_unitMarked && _cases.Count(value => value.label.StartsWith("unit-", StringComparison.Ordinal)) == 4,
                "Unit prefix is incomplete or duplicated.");
            using (var file = new FileStream(Path.Combine(_directory, $"deployment-unit-done-{Role}.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file))
                writer.Write(JsonUtility.ToJson(new Marker { matchId = _store.Current.matchId, role = Role, caseCount = 4, revision = View.Revision }));
            _unitMarked = true;
        }

        public async Task BeforeUnitAsync(HandCardStateDto card, float deadline)
        {
            Require(!_unitLegal && !_cases.Any(value => value.label == "unit-negative"), "Duplicate unit boundary audit.");
            await Reject(card, DemoSlotKind.Unit, -1, "unit-negative", "INVALID_TARGET", deadline);
            await Reject(card, DemoSlotKind.Unit, View.UnitSlots.Length, "unit-capacity", "INVALID_TARGET", deadline);
            await Reject(card, DemoSlotKind.Unit, View.UnitSlots.Length + 1, "unit-capacity-plus-one", "INVALID_TARGET", deadline);
        }
        public void UnitAccepted()
        {
            Require(_cases.Count == 3, "Unit audit cases missing.");
            _criticalUnitId = View.GetObject(true, DemoSlotKind.Unit, View.UnitSlots.Length - 1)?.InstanceId;
            Require(!string.IsNullOrEmpty(_criticalUnitId), "Accepted last-cell audit unit is missing.");
            _unitLegal = true;
        }

        public async Task BeforeBuildingAsync(HandCardStateDto card, float deadline)
        {
            Require(_unitLegal && !_buildingLegal, "Building audit needs its accepted boundary unit.");
            await Reject(card, DemoSlotKind.Building, -1, "building-negative", "INVALID_TARGET", deadline);
            await Reject(card, DemoSlotKind.Building, View.BuildingSlots.Length, "building-capacity", "INVALID_TARGET", deadline);
            if (Seat == 1)
                await Reject(card, DemoSlotKind.Building, View.BuildingSlots.Length - 1, "structure-edge", "INVALID_TARGET", deadline);
        }
        public void BuildingAccepted() => _buildingLegal = true;

        public static bool CriticalUnitOccupiesLastCell(DemoAuthoritativeMatchView view, string instanceId)
        {
            var unit = view.GetObject(true, DemoSlotKind.Unit, view.UnitSlots.Length - 1);
            return !string.IsNullOrEmpty(instanceId) && unit != null && unit.InstanceId == instanceId && unit.Health > 0;
        }

        public async Task<bool> TryOccupiedAsync(float deadline)
        {
            if (!_unitLegal || _followupLegal) return false;
            if (!_cases.Any(value => value.label == "unit-occupied"))
            {
                var unit = FindUnitCard();
                if (unit != null)
                {
                    Require(CriticalUnitOccupiesLastCell(View, _criticalUnitId),
                        "The original last-cell audit unit died/moved; occupancy rejection precondition is not met.");
                    await Reject(unit, DemoSlotKind.Unit, View.UnitSlots.Length - 1, "unit-occupied", "SLOT_OCCUPIED", deadline);
                    MarkUnitPrefix();
                }
            }
            if (!_buildingLegal) return false;
            var occupancyLabel = Seat == 0 ? "structure-occupied" : "building-occupied";
            if (!_cases.Any(value => value.label == occupancyLabel))
            {
                var overlapCard = View.HandCards.FirstOrDefault(value => value?.cardId == (Seat == 0 ? "cd_007" : "cd_004") && Cost(value) <= View.Energy);
                if (overlapCard != null)
                {
                    var anchor = View.BuildingSlots.Length - 2;
                    Require(Seat == 0 ? string.IsNullOrEmpty(View.BuildingSlots[anchor]) && !string.IsNullOrEmpty(View.BuildingSlots[anchor + 1])
                        : !string.IsNullOrEmpty(View.BuildingSlots[anchor]) && View.BuildingSlots[anchor] == View.BuildingSlots[anchor + 1],
                        "Structure occupancy precondition does not test the declared first/second cell.");
                    await Reject(overlapCard, DemoSlotKind.Building, anchor, occupancyLabel, "SLOT_OCCUPIED", deadline);
                }
            }
            if (_cases.Count != (Seat == 0 ? 7 : 8)) return false;
            var revision = View.Revision;
            var result = await _session.EnterCombatAsync();
            Require(result.Outcome == MatchCommandOutcome.Accepted && View.Revision == revision + 1 && View.Phase == DemoTurnPhase.Combat,
                "A legal follow-up did not work after occupied-cell rejections.");
            _followupLegal = true;
            Require(LocalComplete, "Deployment audit did not complete all cases and positive follow-ups.");
            using (var file = new FileStream(Path.Combine(_directory, $"deployment-audit-done-{Role}.json"), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file))
                writer.Write(JsonUtility.ToJson(new Marker { matchId = _store.Current.matchId, role = Role, caseCount = _cases.Count, revision = View.Revision }));
            _marked = true;
            return true;
        }

        private async Task Reject(HandCardStateDto card, DemoSlotKind kind, int slot, string label, string code, float deadline)
        {
            Check();
            Require(!_cases.Any(value => value.label == label) && View.IsPlayerTurn && View.Phase == DemoTurnPhase.Main &&
                _session.CanIssueCommand && Cost(card) <= View.Energy, "Invalid/duplicate rejection-audit precondition.");
            // Start from a server snapshot, not a possibly differently-defaulted event replay DTO.
            Require(await _recover(deadline), "Could not establish the baseline server snapshot.");
            var before = ProjectionHash(_store.Current);
            var revision = View.Revision;
            var batches = _batches;
            var energy = View.Energy;
            var cost = Cost(card);
            var result = await _session.DeployAsync(card.cardId, kind, slot, card.handCardInstanceId);
            Check();
            Require(result.Outcome == MatchCommandOutcome.Rejected && result.Code == code && result.Revision == revision &&
                _wire.TryGetValue(result.CommandId, out var wire) && wire.code == code && wire.revision == revision &&
                _batches == batches && ProjectionHash(_store.Current) == before && !_session.HasPendingCommand && _session.CanIssueCommand,
                $"Rejection was not atomic/correlated: {label}, {result.Outcome}, {result.Code} - {result.Message}.");
            Require(await _recover(deadline), "Could not recover server state after a rejection.");
            Check();
            Require(View.Revision == revision && ProjectionHash(_store.Current) == before && _batches == batches &&
                View.HandCards.Any(value => value?.handCardInstanceId == card.handCardInstanceId && value.cardId == card.cardId),
                "Fresh authoritative recovery exposed a mutation after rejection: " + label);
            _cases.Add(new DemoDeploymentRejectionProof { label = label, commandId = result.CommandId, code = code, cardId = card.cardId,
                slotKind = kind == DemoSlotKind.Unit ? "UNIT" : "BUILDING", slotIndex = slot,
                revision = revision, recoveredRevision = View.Revision, effectiveCost = cost, energyBefore = energy, energyAfter = View.Energy,
                beforeHash = before, afterHash = ProjectionHash(_store.Current), rejected = true, noEvents = true, freshSnapshotVerified = true });
            Debug.Log($"Unity deployment rejection verified: {Role}/{label}, {code}, revision {revision}, fresh full-state snapshot unchanged.");
        }

        public static string ProjectionHash(MatchStateDto state)
        {
            using (var sha = SHA256.Create())
                return BitConverter.ToString(sha.ComputeHash(Encoding.UTF8.GetBytes(JsonUtility.ToJson(state)))).Replace("-", string.Empty);
        }
        private void ObserveBatch(MatchEventBatchDto _) => _batches++;
        private void ObserveRejection(CommandRejectionDto value)
        {
            if (value == null || string.IsNullOrEmpty(value.commandId) || _wire.ContainsKey(value.commandId))
            { _failure = new InvalidOperationException("Duplicate/invalid wire rejection."); return; }
            _wire.Add(value.commandId, value);
        }
        private void Check() { if (_failure != null) throw new InvalidOperationException("Deployment rejection audit failed.", _failure); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        public void Dispose() { _gateway.CommandRejected -= ObserveRejection; _gateway.EventBatchReceived -= ObserveBatch; }
        [Serializable] private sealed class Marker { public string matchId, role; public int caseCount, revision; }
    }

    [Serializable]
    public sealed class DemoDeploymentRejectionProof
    {
        public string label, commandId, code, cardId, slotKind, beforeHash, afterHash;
        public int slotIndex, revision, recoveredRevision, effectiveCost, energyBefore, energyAfter;
        public bool rejected, noEvents, freshSnapshotVerified;
    }
}
#endif
