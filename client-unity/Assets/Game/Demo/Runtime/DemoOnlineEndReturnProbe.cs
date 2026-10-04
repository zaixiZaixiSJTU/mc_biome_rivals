#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
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
    /// <summary>
    /// Development Player acceptance probe. Issues ordinary session commands only.
    /// The local barrier synchronizes test clients; it never supplies game state.
    /// </summary>
    public sealed class DemoOnlineEndReturnProbe : IDisposable
    {
        private readonly IMatchGateway _gateway;
        private readonly MatchStateStore _store;
        private readonly DemoOnlineMatchSession _session;
        private readonly CardContentRegistry _registry;
        private readonly string _sourceCardId;
        private readonly DemoOnlineEndReturnReport _report = new DemoOnlineEndReturnReport();
        private MatchEventPayloadDto _returned;
        private Exception _failure;
        private int _expiryCount;
        private string _sourceHandInstanceId;
        private string _targetInstanceId;
        private int _energyBefore;
        private HandCardStateDto[] _otherHandBefore;

        public DemoOnlineEndReturnProbe(IMatchGateway gateway, MatchStateStore store,
            DemoOnlineMatchSession session, CardContentRegistry registry, string sourceCardId)
        {
            Require(sourceCardId == "ed_002" || sourceCardId == "ed_005", "Unsupported return probe card.");
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));
            _sourceCardId = sourceCardId;
            _gateway.EventBatchReceived += ObserveBatch;
            _gateway.Faulted += ObserveFault;
        }

        public static int[] SelectMulliganIndices(string[] hand, string sourceCardId)
        {
            hand = hand ?? Array.Empty<string>();
            var sourceIndex = Array.IndexOf(hand, sourceCardId);
            var unitIndex = Array.FindIndex(hand, IsSafeUnit);
            return Enumerable.Range(0, hand.Length).Where(index => index != sourceIndex && index != unitIndex).ToArray();
        }

        private static bool IsSafeUnit(string cardId) => cardId == "ed_001" || cardId == "ed_004";
        private int ExpectedModifier => _sourceCardId == "ed_002" ? -1 : -2;
        private DemoAuthoritativeMatchView View => _session.View;
        private bool IsOwner => _report.role == "owner";

        public async Task<DemoOnlineEndReturnReport> RunAsync(float deadline, string barrierDirectory,
            Func<float, Task<bool>> reconnect, Func<string, string, int, float, Task<bool>> inspectCard,
            Func<string, string, DemoBattlefieldObject, float, Task<bool>> playThroughUi)
        {
            Require(_store.Current != null && View.PlayerFactionId == FactionIds.End &&
                View.OpponentFactionId == FactionIds.End, "Return probe requires two authoritative End decks.");
            Require(!string.IsNullOrWhiteSpace(barrierDirectory) && Directory.Exists(barrierDirectory),
                "Return probe requires an existing unique evidence directory.");
            _report.matchId = _store.Current.matchId;
            _report.viewerPlayerId = _store.Current.viewerPlayerId;
            _report.sourceCardId = _sourceCardId;
            _report.protocolVersion = GameVersions.Protocol;
            _report.rulesetVersion = GameVersions.Ruleset;
            _report.role = View.ViewerIndex == 1 ? "owner" : "observer";
            if (IsOwner && View.IsMulligan)
                await WaitUntil(() => View.OpponentMulliganCompleted, deadline, "Observer mulligan");
            if (View.IsMulligan && !View.PlayerMulliganCompleted)
                await Accept(_session.MulliganAsync(IsOwner
                    ? SelectMulliganIndices(View.Hand.ToArray(), _sourceCardId) : Array.Empty<int>()), "MULLIGAN");
            await WaitUntil(() => !View.IsMulligan, deadline, "Opening hand selection");

            // Seat 1 has four starting cards. No fixture, deck rewrite, or resource injection.
            while (_returned == null)
            {
                CheckDeadline(deadline, "Drawing a return card and living unit");
                Require(!View.IsFinished, "Match finished before a return could be performed.");
                if (!_session.CanIssueCommand || !View.IsPlayerTurn) { await Task.Yield(); continue; }
                if (View.Phase == DemoTurnPhase.Main)
                {
                    if (IsOwner && await TryReturnOrDeploy(deadline, playThroughUi)) continue;
                    await Accept(_session.EnterCombatAsync(), "ENTER_COMBAT");
                }
                else await Accept(_session.EndTurnAsync(), "END_TURN");
            }

            await WaitUntil(() => View.Revision == _report.returnRevision && !_session.HasPendingCommand,
                deadline, "Applying the return batch");
            ValidateReturnState();
            if (IsOwner)
            {
                _report.returnedHandCardInstanceId = _returned.returnedHandCardInstanceId;
                _report.costModifier = ExpectedModifier;
                _report.discountedCost = EffectiveReturnedCost(ExpectedModifier);
                _report.discountUiVerified = await inspectCard(_returned.cardId,
                    _returned.returnedHandCardInstanceId, _report.discountedCost, deadline);
                Require(_report.discountUiVerified, "Returned instance UI did not display its discounted cost.");
            }
            _report.reconnectRecovered = await reconnect(deadline);
            Require(_report.reconnectRecovered && _store.Current.matchId == _report.matchId &&
                View.Revision == _report.returnRevision, "Recovery did not restore the same return revision.");
            ValidateReturnState();
            _report.recoveredRevision = View.Revision;
            if (IsOwner)
                Require(await inspectCard(_returned.cardId, _returned.returnedHandCardInstanceId,
                    _report.discountedCost, deadline), "Discounted UI was not restored after reconnect.");
            _report.recoveryStateVerified = true;
            await Rendezvous(barrierDirectory, deadline);

            if (IsOwner)
            {
                Require(View.IsPlayerTurn && View.Phase == DemoTurnPhase.Main, "Return changed the actor's turn.");
                await Accept(_session.EnterCombatAsync(), "ENTER_COMBAT after recovery");
                await Accept(_session.EndTurnAsync(), "END_TURN after recovery");
            }
            await WaitUntil(() => _expiryCount > 0 && !_session.HasPendingCommand, deadline, "Discount expiry");
            Require(_expiryCount == 1 && _report.expiryRevision > _report.returnRevision,
                "The return discount did not expire exactly once.");
            ValidateHiddenHands(_store.Current);
            if (IsOwner)
            {
                var card = View.HandCards.Single(value => value.handCardInstanceId == _returned.returnedHandCardInstanceId);
                Require(card.cardId == _returned.cardId && card.costModifier == 0 &&
                    string.IsNullOrEmpty(card.expiresAtEndOfTurnPlayerId), "Expiry did not clear the exact returned instance.");
                _report.baseCost = EffectiveReturnedCost(0);
                _report.expiredUiVerified = await inspectCard(card.cardId, card.handCardInstanceId,
                    _report.baseCost, deadline);
                Require(_report.expiredUiVerified, "The actual card UI retained an expired discount.");
            }
            _report.expiryStateVerified = true;
            // A second barrier prevents concession/terminal UI from hiding the observer's expiry check.
            await Rendezvous(barrierDirectory, deadline, "expired");
            if (IsOwner) await Accept(_session.ConcedeAsync(), "CONCEDE");
            await WaitUntil(() => View.IsFinished, deadline, "Final convergence");
            CheckFailure();
            Require(_expiryCount == 1, "Duplicate discount expiry after completion.");
            _report.revision = View.Revision;
            _report.matchStatus = _store.Current.status;
            _report.winnerPlayerId = _store.Current.winnerPlayerId;
            _report.playerFaction = View.PlayerFactionId;
            _report.opponentFaction = View.OpponentFactionId;
            _report.playerLife = View.PlayerLife;
            _report.opponentLife = View.OpponentLife;
            _report.playerUnitCount = View.PlayerBattlefield.Count(value => value.SlotKind == DemoSlotKind.Unit);
            _report.opponentUnitCount = View.OpponentBattlefield.Count(value => value.SlotKind == DemoSlotKind.Unit);
            _report.expiryCount = _expiryCount;
            _report.ok = true;
            return _report;
        }

        private async Task<bool> TryReturnOrDeploy(float deadline,
            Func<string, string, DemoBattlefieldObject, float, Task<bool>> playThroughUi)
        {
            var source = View.HandCards.FirstOrDefault(card => card.cardId == _sourceCardId);
            var target = View.PlayerBattlefield.FirstOrDefault(value => value.Health > 0 &&
                value.SlotKind == DemoSlotKind.Unit && IsSafeUnit(value.CardId));
            if (source != null && target != null && View.Energy >= Cost(source))
            {
                _sourceHandInstanceId = source.handCardInstanceId;
                _targetInstanceId = target.InstanceId;
                _energyBefore = View.Energy;
                _otherHandBefore = View.HandCards.Where(value => value.handCardInstanceId != _sourceHandInstanceId)
                    .Select(value => new HandCardStateDto { handCardInstanceId = value.handCardInstanceId,
                        cardId = value.cardId, costModifier = value.costModifier,
                        expiresAtEndOfTurnPlayerId = value.expiresAtEndOfTurnPlayerId }).ToArray();
                _report.performedUiCardPlay = await playThroughUi(source.cardId, source.handCardInstanceId, target, deadline);
                Require(_report.performedUiCardPlay, "Return was not accepted through real UI and world pointer dispatch.");
                CheckFailure();
                Require(_returned != null && _returned.instanceId == _targetInstanceId, "Accepted return command had no return event.");
                Require(View.Energy == _energyBefore - Cost(source) &&
                    !View.HandCards.Any(value => value.handCardInstanceId == _sourceHandInstanceId),
                    "Return payment or exact played instance consumption was incorrect.");
                _report.paymentVerified = true;
                return true;
            }
            var slot = Array.FindIndex(View.UnitSlots, string.IsNullOrEmpty);
            var unit = View.HandCards.FirstOrDefault(card => IsSafeUnit(card.cardId) && Cost(card) <= View.Energy);
            if (slot >= 0 && unit != null && (target == null || source == null))
            {
                await Accept(_session.DeployAsync(unit.cardId, DemoSlotKind.Unit, slot, unit.handCardInstanceId), "DEPLOY_CARD");
                return true;
            }
            return false;
        }

        private int Cost(HandCardStateDto card)
        {
            Require(_registry.TryGetDefinition(card.cardId, out var definition), "Unknown probe card definition.");
            return View.GetEffectiveCost(definition, card.handCardInstanceId);
        }

        private int EffectiveReturnedCost(int modifier)
        {
            Require(_registry.TryGetDefinition(_returned.cardId, out var definition), "Unknown returned card.");
            return Math.Max(0, definition.cost + modifier);
        }

        private void ObserveFault(Exception error) => _failure = error;

        private void ObserveBatch(MatchEventBatchDto batch)
        {
            try
            {
                foreach (var item in batch.events)
                {
                    if (item.type == MatchEventTypes.ObjectReturned && item.payload.sourceCardId == _sourceCardId)
                    {
                        Require(_returned == null, "More than one return event in this probe.");
                        ValidateReturnBatch(batch, _store.Current.viewerPlayerId, _sourceCardId);
                        _returned = item.payload;
                        _report.returnRevision = batch.revision;
                        _report.returnEventId = item.eventId;
                        _report.targetInstanceId = _returned.instanceId;
                        _report.returnedCardId = _returned.cardId;
                        _report.ownerPlayerId = _returned.ownerPlayerId;
                        _report.returnEventOrderHash = EventOrderHash(batch);
                        _report.causalOrderVerified = true;
                    }
                    if (item.type == MatchEventTypes.HandCardCostModifierExpired && _returned != null &&
                        item.payload.playerId == _returned.ownerPlayerId)
                    {
                        ValidateExpiry(item.payload, _returned, _store.Current.viewerPlayerId, ExpectedModifier);
                        _expiryCount++;
                        _report.expiryRevision = batch.revision;
                        _report.expiryEventId = item.eventId;
                        _report.expiryEventOrderHash = EventOrderHash(batch);
                    }
                }
            }
            catch (Exception error) { _failure = error; }
        }

        public static void ValidateReturnBatch(MatchEventBatchDto batch, string viewerPlayerId, string sourceCardId)
        {
            Require(batch?.events != null && batch.handProjection != null, "Return batch is incomplete.");
            var index = Array.FindIndex(batch.events, value => value.type == MatchEventTypes.ObjectReturned);
            Require(index > 0, "Return has no preceding card play.");
            var returned = batch.events[index].payload;
            var played = batch.events[index - 1];
            Require(played.type == MatchEventTypes.CardPlayed && played.payload.cardId == sourceCardId &&
                played.payload.playerId == returned.sourcePlayerId &&
                returned.sourceInstanceId == "effect-" + played.eventId &&
                returned.effectId == "effect." + sourceCardId + ".01",
                "CARD_PLAYED and OBJECT_RETURNED are not causally adjacent.");
            Require(returned.sourceCardId == sourceCardId && returned.destination == "HAND" &&
                returned.ownerPlayerId == returned.controllerPlayerId && returned.sourcePlayerId == returned.ownerPlayerId &&
                returned.fromSlotKind == "UNIT" && returned.fromSlotIndex >= 0 && IsSafeUnit(returned.cardId) &&
                !string.IsNullOrEmpty(returned.instanceId), "Return provenance or destination is incorrect.");
            var projection = batch.handProjection;
            Require(projection.ownPlayerId == viewerPlayerId, "Hand projection routed to the wrong viewer.");
            if (viewerPlayerId == returned.ownerPlayerId)
            {
                var modifier = sourceCardId == "ed_002" ? -1 : -2;
                Require(!string.IsNullOrWhiteSpace(returned.returnedHandCardInstanceId) &&
                    returned.costModifier == modifier && returned.expiresAtEndOfTurnPlayerId == returned.ownerPlayerId,
                    "Owner return instance, modifier, or expiry is incorrect.");
                var card = projection.ownHandCards.SingleOrDefault(value =>
                    value.handCardInstanceId == returned.returnedHandCardInstanceId);
                Require(card != null && card.cardId == returned.cardId && card.costModifier == modifier &&
                    card.expiresAtEndOfTurnPlayerId == returned.ownerPlayerId &&
                    projection.ownHandCards.Length == returned.ownerHandCount, "Owner private hand did not include the returned instance.");
            }
            else
            {
                Require(returned.returnedHandCardInstanceId == null && returned.expiresAtEndOfTurnPlayerId == null &&
                    returned.costModifier == 0 && projection.opponentPlayerId == returned.ownerPlayerId &&
                    projection.opponentHandCount == returned.ownerHandCount, "Observer return projection leaked private fields or incorrect counts.");
            }
        }

        public static void ValidateExpiry(MatchEventPayloadDto expired, MatchEventPayloadDto returned,
            string viewerPlayerId, int expectedModifier)
        {
            Require(expired.playerId == returned.ownerPlayerId &&
                expired.expiredAtEndOfTurnPlayerId == returned.ownerPlayerId, "Expiry attributed to the wrong player.");
            if (viewerPlayerId == returned.ownerPlayerId)
                Require(expired.handCardInstanceId == returned.returnedHandCardInstanceId &&
                    expired.cardId == returned.cardId && expired.expiredCostModifier == expectedModifier &&
                    expired.costModifier == 0, "Expiry did not reference the precise discounted instance.");
            else
                Require(expired.handCardInstanceId == null && expired.cardId == null &&
                    expired.expiredCostModifier == 0 && expired.costModifier == 0 && expired.effectiveCost == 0,
                    "Observer expiry event leaked a private instance or fee.");
        }

        public static void ValidateHiddenHands(MatchStateDto state)
        {
            Require(state?.players != null && state.players.Length == 2, "Private state is incomplete.");
            var opponent = state.players.Single(value => value.playerId != state.viewerPlayerId);
            Require(opponent.hand != null && opponent.handCards != null &&
                opponent.hand.Length == opponent.handCards.Length && opponent.hand.All(value => value == null) &&
                opponent.handCards.All(value => value == null), "Opponent hand is not fully redacted.");
        }

        private void ValidateReturnState()
        {
            CheckFailure();
            ValidateHiddenHands(_store.Current);
            var owner = _store.Current.players.Single(value => value.playerId == _returned.ownerPlayerId);
            Require(owner.battlefield.All(value => value.instanceId != _returned.instanceId) &&
                string.IsNullOrEmpty(owner.unitSlots[_returned.fromSlotIndex]) &&
                owner.hand.Length == _returned.ownerHandCount && owner.discardPile.Length == _returned.ownerDiscardCount,
                "Return did not clear the battlefield slot or synchronize owner counts.");
            if (IsOwner)
            {
                var card = View.HandCards.Single(value => value.handCardInstanceId == _returned.returnedHandCardInstanceId);
                Require(card.cardId == _returned.cardId && card.costModifier == ExpectedModifier &&
                    card.expiresAtEndOfTurnPlayerId == _returned.ownerPlayerId, "Store lost the exact return discount.");
                foreach (var previous in _otherHandBefore)
                {
                    var actual = View.HandCards.Single(value => value.handCardInstanceId == previous.handCardInstanceId);
                    Require(actual.cardId == previous.cardId && actual.costModifier == previous.costModifier &&
                        actual.expiresAtEndOfTurnPlayerId == previous.expiresAtEndOfTurnPlayerId,
                        "Return changed an unrelated hand instance.");
                }
            }
            _report.privateProjectionVerified = true;
            _report.boardRemovalVerified = true;
        }

        private async Task Rendezvous(string directory, float deadline, string stage = "recovered")
        {
            var ownPath = Path.Combine(directory, $"{_sourceCardId}-{stage}-{_report.role}.json");
            var peerPath = Path.Combine(directory, $"{_sourceCardId}-{stage}-{(IsOwner ? "observer" : "owner")}.json");
            Require(!File.Exists(ownPath), "Probe barrier would overwrite existing evidence.");
            var marker = new BarrierMarker { matchId = _report.matchId, sourceCardId = _sourceCardId,
                returnRevision = _report.returnRevision, stage = stage, role = _report.role };
            // Only public facts. Write completes before peer reads; no private hand IDs on this channel.
            using (var file = new FileStream(ownPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file)) writer.Write(JsonUtility.ToJson(marker));
            BarrierMarker peer = null;
            await WaitUntil(() =>
            {
                if (!File.Exists(peerPath)) return false;
                try { peer = JsonUtility.FromJson<BarrierMarker>(File.ReadAllText(peerPath)); }
                catch (IOException) { return false; }
                return peer != null;
            }, deadline, $"Peer {stage} barrier");
            Require(peer.matchId == marker.matchId && peer.sourceCardId == marker.sourceCardId &&
                peer.returnRevision == marker.returnRevision && peer.stage == stage &&
                peer.role != marker.role, "Probe clients recovered different public state.");
        }

        private async Task WaitUntil(Func<bool> predicate, float deadline, string stage)
        {
            while (!predicate()) { CheckDeadline(deadline, stage); await Task.Yield(); }
            CheckFailure();
        }

        private void CheckDeadline(float deadline, string stage)
        {
            CheckFailure();
            if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException(stage + " timed out.");
        }
        private void CheckFailure() { if (_failure != null) throw new InvalidOperationException("Return probe event validation failed.", _failure); }
        private static async Task Accept(Task<MatchCommandDispatchResult> command, string label)
        {
            var result = await command;
            Require(result.Outcome == MatchCommandOutcome.Accepted,
                label + " was not acknowledged: " + result.Outcome + "/" + result.Code + "/" + result.Message);
        }
        private static void Require(bool condition, string error) { if (!condition) throw new InvalidOperationException(error); }
        private static string EventOrderHash(MatchEventBatchDto batch)
        {
            using (var hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(string.Join("|",
                    batch.events.Select(value => $"{value.eventId}:{value.type}"))))).Replace("-", "");
        }
        public void Dispose() { _gateway.EventBatchReceived -= ObserveBatch; _gateway.Faulted -= ObserveFault; }

        [Serializable]
        private sealed class BarrierMarker
        {
            public string matchId, sourceCardId, stage, role;
            public int returnRevision;
        }
    }

    [Serializable]
    public sealed class DemoOnlineEndReturnReport
    {
        public bool ok;
        public string matchId, viewerPlayerId, role, sourceCardId, ownerPlayerId, targetInstanceId, returnedCardId;
        public string returnedHandCardInstanceId, returnEventOrderHash, expiryEventOrderHash;
        public string matchStatus, winnerPlayerId, playerFaction, opponentFaction;
        public string accountPhase, accountUserId, accountDisplayName, rulesetVersion;
        public int protocolVersion, revision, returnRevision, recoveredRevision, expiryRevision, expiryCount;
        public long returnEventId, expiryEventId;
        public int costModifier, discountedCost, baseCost, playerLife, opponentLife, playerUnitCount, opponentUnitCount;
        public bool causalOrderVerified, performedUiCardPlay, paymentVerified, privateProjectionVerified, boardRemovalVerified;
        public bool reconnectRecovered, recoveryStateVerified, discountUiVerified, expiredUiVerified, expiryStateVerified;
    }
}
#endif
