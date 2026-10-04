#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BiomeRivals.Core;
using BiomeRivals.Networking;
using UnityEngine;

namespace BiomeRivals.Demo
{
    // Ordinary mulligan/end-turn commands and shuffled decks. No server fixture or local state injection.
    public sealed class DemoOnlinePendingProbe : IDisposable
    {
        private readonly IMatchGateway _gateway;
        private readonly MatchStateStore _store;
        private readonly DemoOnlineMatchSession _session;
        private Exception _failure;
        private string _pendingId;
        private DemoAuthoritativeMatchView View => _session.View;

        public DemoOnlinePendingProbe(IMatchGateway gateway, MatchStateStore store, DemoOnlineMatchSession session)
        {
            _gateway = gateway; _store = store; _session = session;
            _gateway.Faulted += Fault;
            _session.CommandPending += Pending;
        }

        public async Task<DemoOnlinePendingReport> RunAsync(float deadline, string directory, string capturePath,
            Func<float, string, Task<bool>> inspectUi)
        {
            var report = new DemoOnlinePendingReport { matchId = _store.Current.matchId, viewerPlayerId = _store.Current.viewerPlayerId };
            if (View.ViewerIndex == 1) await Wait(() => View.OpponentMulliganCompleted, deadline);
            if (View.IsMulligan && !View.PlayerMulliganCompleted) await Accept(_session.MulliganAsync(Array.Empty<int>()));
            await Wait(() => !View.IsMulligan && !_session.HasPendingCommand, deadline);
            while (_store.Current.players.Any(player => player.hand.Length < 7))
            {
                Check(deadline);
                if (View.IsPlayerTurn) await Accept(_session.EndTurnAsync());
                else await Task.Yield();
            }
            // Freeze both clients at each public revision before allowing the next ordinary turn.
            await Barrier(directory, "full", deadline);
            for (var stage = 0; stage < 2; stage++)
            {
                if (View.IsPlayerTurn)
                {
                    Require(!report.pendingUiVerified, "The same viewer cannot inspect both pending turns.");
                    var diagnostics = _gateway as IMatchInboundDeliveryDiagnostics ?? throw new NotSupportedException("Development receive diagnostics missing.");
                    var before = _store.Current.revision;
                    var instances = View.HandCards.Select(card => card.handCardInstanceId).ToArray();
                    Require(instances.Length == 7, "Ordinary draw preparation did not fill the hand.");
                    Task<MatchCommandDispatchResult> command;
                    using (diagnostics.HoldIncomingMatchMessages())
                    {
                        command = _session.EndTurnAsync();
                        var pendingDeadline = Mathf.Min(deadline, Time.realtimeSinceStartup + 4f);
                        await Wait(() => diagnostics.HeldIncomingMessageCount > 0, pendingDeadline);
                        Require(_session.HasPendingCommand && !_session.CanIssueCommand && !command.IsCompleted,
                            "Real response did not leave the command pending behind the receive fence.");
                        report.pendingCommandId = _pendingId;
                        report.pendingRevision = before;
                        report.pendingHandInstanceIds = instances;
                        report.pendingHandCount = instances.Length;
                        report.pendingUiVerified = await inspectUi(pendingDeadline, capturePath);
                        Require(report.pendingUiVerified && _session.HasPendingCommand && !command.IsCompleted &&
                            _store.Current.revision == before && View.HandCards.Select(card => card.handCardInstanceId).SequenceEqual(instances),
                            "Pending UI inspection changed revision, hand, or command completion.");
                        report.heldResponseCount = diagnostics.HeldIncomingMessageCount;
                    }
                    var accepted = await command;
                    Require(accepted.Outcome == MatchCommandOutcome.Accepted && accepted.CommandId == report.pendingCommandId &&
                        accepted.Revision == before + 1, "Releasing real responses did not acknowledge the same ordinary command.");
                    await Wait(() => _store.Current.revision == accepted.Revision && !_session.HasPendingCommand, deadline);
                    report.ackRevision = accepted.Revision;
                    report.actualAcknowledgementVerified = true;
                    WriteMarker(directory, "done-" + stage, accepted.Revision);
                }
                else
                {
                    var marker = await ReadMarker(directory, "done-" + stage, deadline);
                    await Wait(() => _store.Current.revision == marker.revision && !_session.HasPendingCommand, deadline);
                }
                DemoOnlineEndReturnProbe.ValidateHiddenHands(_store.Current);
                await Barrier(directory, "turn-" + stage, deadline);
            }
            Require(report.pendingUiVerified && report.actualAcknowledgementVerified, "Viewer never inspected its own pending command.");
            if (View.ViewerIndex == 0) await Accept(_session.ConcedeAsync());
            await Wait(() => View.IsFinished && !_session.HasPendingCommand, deadline);
            DemoOnlineEndReturnProbe.ValidateHiddenHands(_store.Current);
            report.protocolVersion = GameVersions.Protocol;
            report.rulesetVersion = GameVersions.Ruleset;
            report.revision = _store.Current.revision;
            report.matchStatus = _store.Current.status;
            report.winnerPlayerId = _store.Current.winnerPlayerId;
            report.playerFaction = View.PlayerFactionId; report.opponentFaction = View.OpponentFactionId;
            report.playerLife = View.PlayerLife; report.opponentLife = View.OpponentLife;
            report.playerUnitCount = View.PlayerBattlefield.Count; report.opponentUnitCount = View.OpponentBattlefield.Count;
            report.privateProjectionVerified = true;
            report.ok = true;
            return report;
        }

        private string MarkerPath(string directory, string stage) => Path.Combine(directory, "pending-" + stage + ".json");
        private void WriteMarker(string directory, string stage, int revision)
        {
            using (var file = new FileStream(MarkerPath(directory, stage), FileMode.CreateNew, FileAccess.Write, FileShare.None))
            using (var writer = new StreamWriter(file))
                writer.Write(JsonUtility.ToJson(new Marker { matchId = _store.Current.matchId, revision = revision }));
        }
        private async Task<Marker> ReadMarker(string directory, string stage, float deadline)
        {
            Marker marker = null;
            await Wait(() =>
            {
                if (!File.Exists(MarkerPath(directory, stage))) return false;
                try { marker = JsonUtility.FromJson<Marker>(File.ReadAllText(MarkerPath(directory, stage))); }
                catch (IOException) { return false; }
                return marker != null;
            }, deadline);
            Require(marker.matchId == _store.Current.matchId, "Cross-match pending barrier.");
            return marker;
        }
        private async Task Barrier(string directory, string stage, float deadline)
        {
            var revision = _store.Current.revision;
            WriteMarker(directory, stage + "-" + View.ViewerIndex, revision);
            var peer = await ReadMarker(directory, stage + "-" + (1 - View.ViewerIndex), deadline);
            Require(peer.revision == revision, "Pending peers disagree on public revision.");
        }
        private async Task Wait(Func<bool> condition, float deadline)
        { while (!condition()) { Check(deadline); await Task.Yield(); } Check(deadline); }
        private void Check(float deadline)
        {
            if (_failure != null) throw new InvalidOperationException("Pending probe transport failure.", _failure);
            if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException("Pending probe deadline expired.");
        }
        private static async Task Accept(Task<MatchCommandDispatchResult> task)
        { var result = await task; Require(result.Outcome == MatchCommandOutcome.Accepted, "Ordinary preparation command rejected: " + result.Code); }
        private static void Require(bool condition, string message) { if (!condition) throw new InvalidOperationException(message); }
        private void Fault(Exception error) => _failure = error;
        private void Pending(string id) => _pendingId = id;
        public void Dispose() { _gateway.Faulted -= Fault; _session.CommandPending -= Pending; }
        [Serializable] private sealed class Marker { public string matchId; public int revision; }
    }

    [Serializable]
    public sealed class DemoOnlinePendingReport
    {
        public bool ok, pendingUiVerified, actualAcknowledgementVerified, privateProjectionVerified;
        public string matchId, viewerPlayerId, pendingCommandId, rulesetVersion, matchStatus, winnerPlayerId;
        public string playerFaction, opponentFaction, accountPhase, accountUserId, accountDisplayName;
        public string[] pendingHandInstanceIds;
        public int protocolVersion, revision, pendingRevision, ackRevision, heldResponseCount, pendingHandCount;
        public int playerLife, opponentLife, playerUnitCount, opponentUnitCount;
    }
}
#endif
