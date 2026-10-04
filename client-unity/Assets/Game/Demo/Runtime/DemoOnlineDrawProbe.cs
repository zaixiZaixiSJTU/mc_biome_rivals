#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using BiomeRivals.Core;
using BiomeRivals.Networking;
using UnityEngine;

namespace BiomeRivals.Demo
{
    public sealed class DemoOnlineDrawProbe : IDisposable
    {
        private readonly IMatchGateway _gateway;
        private readonly MatchStateStore _store;
        private readonly DemoOnlineMatchSession _session;
        private readonly IMatchTestFixtureDiagnostics _diagnostics;
        private readonly DemoOnlineDrawReport _report = new DemoOnlineDrawReport();
        private Exception _failure;
        private int _preparedCount;
        private int _terminalCount;
        private MatchTestFixtureResult _ack;

        public DemoOnlineDrawProbe(IMatchGateway gateway, MatchStateStore store, DemoOnlineMatchSession session)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            _session = session ?? throw new ArgumentNullException(nameof(session));
            _diagnostics = gateway as IMatchTestFixtureDiagnostics ??
                throw new NotSupportedException("Draw probe requires Development fixture diagnostics.");
            _gateway.SnapshotReceived += ObserveSnapshot;
            _gateway.EventBatchReceived += ObserveBatch;
            _gateway.CommandRejected += ObserveRejection;
            _gateway.Faulted += ObserveFault;
            _diagnostics.TestFixtureResultReceived += ObserveAck;
        }

        public async Task<DemoOnlineDrawReport> RunAsync(float deadline,
            Func<float, Task<bool>> inspectUi, Func<float, Task<bool>> reconnect, bool readableHand = false)
        {
            _report.matchId = _store.Current.matchId;
            _report.readableHand = readableHand;
            _report.viewerPlayerId = _store.Current.viewerPlayerId;
            _report.role = _session.View.ViewerIndex == 0 ? "requester" : "observer";
            _report.protocolVersion = GameVersions.Protocol;
            _report.rulesetVersion = GameVersions.Ruleset;
            // Serialize opening commands, then never change gameplay state locally.
            if (_report.role == "observer")
                await WaitUntil(() => _session.View.OpponentMulliganCompleted, deadline, "Requester mulligan");
            if (_session.View.IsMulligan && !_session.View.PlayerMulliganCompleted)
            {
                var result = await _session.MulliganAsync(Array.Empty<int>());
                Require(result.Outcome == MatchCommandOutcome.Accepted, "Mulligan was rejected.");
            }
            await WaitUntil(() => !_session.View.IsMulligan && !_session.HasPendingCommand, deadline, "Match start");
            Require(_report.openingRevision > 0, "Authoritative match-start event was not observed.");
            if (_report.role == "requester")
                await _diagnostics.RequestSimultaneousDefeatFixtureAsync(readableHand);
            await WaitUntil(() => _session.View.IsFinished && _terminalCount == 1 &&
                (_report.role != "requester" || _ack != null), deadline, "Authoritative draw");
            ValidateState();
            Require(_preparedCount == 1 && _report.preparedRevision == _report.openingRevision,
                "Fixture did not synchronize exactly one private baseline.");
            if (_report.role == "requester")
            {
                Require(_ack.ok && _ack.revision == _store.Current.revision, "Fixture ack and draw revision disagree.");
                _report.fixtureAcknowledged = true;
            }
            _report.uiVerifiedBeforeReconnect = await inspectUi(deadline);
            Require(_report.uiVerifiedBeforeReconnect, "Draw UI or input lock failed before reconnect.");
            var revision = _store.Current.revision;
            _report.reconnectRecovered = await reconnect(deadline);
            Require(_report.reconnectRecovered && _store.Current.matchId == _report.matchId &&
                _store.Current.revision == revision, "Draw reconnect changed the authoritative match or revision.");
            ValidateState();
            _report.uiVerifiedAfterReconnect = await inspectUi(deadline);
            Require(_report.uiVerifiedAfterReconnect, "Draw UI or input lock was not recovered.");
            _report.revision = _store.Current.revision;
            _report.matchStatus = _store.Current.status;
            _report.winnerPlayerId = _store.Current.winnerPlayerId;
            _report.playerFaction = _session.View.PlayerFactionId;
            _report.opponentFaction = _session.View.OpponentFactionId;
            _report.playerLife = _session.View.PlayerLife;
            _report.opponentLife = _session.View.OpponentLife;
            _report.playerUnitCount = _session.View.PlayerBattlefield.Count;
            _report.opponentUnitCount = _session.View.OpponentBattlefield.Count;
            _report.terminalEventCount = _terminalCount;
            _report.playerHandCount = _session.View.HandCards.Count;
            _report.ownHandInstanceIds = _session.View.HandCards.Select(card => card.handCardInstanceId).ToArray();
            _report.privateProjectionVerified = true;
            _report.ok = true;
            return _report;
        }

        private void ValidateState()
        {
            CheckFailure();
            Require(_session.View.IsFinished && !_session.View.HasWinner && !_session.View.IsPlayerWinner &&
                _store.Current.players.All(player => player.life == 0), "State/view is not a winnerless simultaneous defeat.");
            DemoOnlineEndReturnProbe.ValidateHiddenHands(_store.Current);
            if (_report.readableHand)
                Require(_session.View.HandCards.Count == 7 && _store.Current.players.All(player => player.hand.Length == 7),
                    "Readable draw fixture did not retain full hands.");
        }
        private void ObserveSnapshot(MatchStateDto snapshot)
        {
            if (snapshot.status == "ACTIVE" && snapshot.players.All(player => player.life == 0))
            {
                _preparedCount++;
                _report.preparedRevision = snapshot.revision;
                try { DemoOnlineEndReturnProbe.ValidateHiddenHands(snapshot); }
                catch (Exception error) { _failure = error; }
            }
        }
        private void ObserveBatch(MatchEventBatchDto batch)
        {
            if (batch.events.Any(item => item.type == MatchEventTypes.MatchStarted))
                _report.openingRevision = batch.revision;
            var ended = batch.events.FirstOrDefault(item => item.type == MatchEventTypes.MatchEnded);
            if (ended == null) return;
            try
            {
                Require(ended.payload.reason == "SIMULTANEOUS_DEFEAT" &&
                    string.IsNullOrEmpty(ended.payload.winnerPlayerId), "Unexpected winner or terminal reason.");
                _terminalCount++;
                _report.terminalReason = ended.payload.reason;
                _report.terminalEventId = ended.eventId;
                using (var hash = SHA256.Create())
                    _report.publicEventOrderHash = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(
                        string.Join("|", batch.events.Select(item => $"{item.eventId}:{item.type}"))))).Replace("-", "");
            }
            catch (Exception error) { _failure = error; }
        }
        private void ObserveAck(MatchTestFixtureResult result) => _ack = result;
        private void ObserveFault(Exception error) => _failure = error;
        private void ObserveRejection(CommandRejectionDto rejection) =>
            _failure = new InvalidOperationException("Draw probe command rejected: " + rejection.code + "/" + rejection.message);
        private async Task WaitUntil(Func<bool> condition, float deadline, string stage)
        {
            while (!condition())
            {
                CheckFailure();
                if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException(stage + " timed out.");
                await Task.Yield();
            }
            CheckFailure();
        }
        private void CheckFailure() { if (_failure != null) throw new InvalidOperationException("Draw probe failed.", _failure); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        public void Dispose()
        {
            _gateway.SnapshotReceived -= ObserveSnapshot;
            _gateway.EventBatchReceived -= ObserveBatch;
            _gateway.CommandRejected -= ObserveRejection;
            _gateway.Faulted -= ObserveFault;
            _diagnostics.TestFixtureResultReceived -= ObserveAck;
        }
    }

    [Serializable]
    public sealed class DemoOnlineDrawReport
    {
        public bool ok, fixtureAcknowledged, privateProjectionVerified, reconnectRecovered;
        public bool uiVerifiedBeforeReconnect, uiVerifiedAfterReconnect;
        public bool readableHand;
        public int playerHandCount;
        public string[] ownHandInstanceIds;
        public string matchId, viewerPlayerId, role, terminalReason, publicEventOrderHash, rulesetVersion;
        public string matchStatus, winnerPlayerId, playerFaction, opponentFaction;
        public string accountPhase, accountUserId, accountDisplayName;
        public int protocolVersion, revision, openingRevision, preparedRevision, terminalEventCount;
        public int playerLife, opponentLife, playerUnitCount, opponentUnitCount;
        public long terminalEventId;
    }
}
#endif
