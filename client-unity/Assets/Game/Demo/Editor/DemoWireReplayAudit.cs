using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using BiomeRivals.Core;
using BiomeRivals.Networking;
using UnityEngine;

namespace BiomeRivals.Demo.Editor
{
    /// <summary>Replays actual Nakama probe wire captures through the production gateway and store.</summary>
    public static class DemoWireReplayAudit
    {
        public static void ValidateDrawFromCommandLine()
        {
            var arguments = Environment.GetCommandLineArgs();
            var source = Argument(arguments, "-wireReplaySource");
            var output = Argument(arguments, "-wireReplayResult");
            if (File.Exists(output)) throw new InvalidOperationException("Refusing to overwrite replay audit evidence.");
            var report = new AuditResult { sourcePath = source, unityVersion = Application.unityVersion };
            try
            {
                using (var hash = SHA256.Create())
                    report.sourceSha256 = BitConverter.ToString(hash.ComputeHash(File.ReadAllBytes(source))).Replace("-", "");
                var capture = JsonUtility.FromJson<Capture>(File.ReadAllText(source));
                if (capture == null || !capture.ok || capture.traces == null || capture.traces.Length != 2)
                    throw new InvalidOperationException("Expected a successful two-client draw capture.");
                foreach (var trace in capture.traces)
                {
                    using (var transport = new ReplayTransport())
                    using (var gateway = new AuthoritativeMatchGateway(transport))
                    {
                        var store = new MatchStateStore();
                        Exception failure = null;
                        var terminalBatchCount = 0;
                        var preparedSnapshotCount = 0;
                        gateway.SnapshotReceived += store.Replace;
                        gateway.EventBatchReceived += store.Apply;
                        gateway.SnapshotReceived += snapshot =>
                        {
                            if (snapshot.status == "ACTIVE" && snapshot.players.All(player => player.life == 0))
                                preparedSnapshotCount++;
                        };
                        gateway.EventBatchReceived += batch =>
                        {
                            if (batch.events.Any(item => item.type == MatchEventTypes.MatchEnded &&
                                item.payload.reason == "SIMULTANEOUS_DEFEAT")) terminalBatchCount++;
                        };
                        gateway.Faulted += error => failure = error;
                        foreach (var message in trace.messages)
                        {
                            transport.Deliver(message.opCode, message.json);
                            if (failure != null)
                                throw new InvalidOperationException($"Wire replay failed for {trace.viewerPlayerId} at opcode {message.opCode}.", failure);
                        }
                        var state = store.Current;
                        var view = new DemoAuthoritativeMatchView(store);
                        if (preparedSnapshotCount != 1 || terminalBatchCount != 1 ||
                            state == null || state.matchId != capture.matchId || state.viewerPlayerId != trace.viewerPlayerId ||
                            state.revision != capture.drawRevision || state.status != "FINISHED" ||
                            !string.IsNullOrEmpty(state.winnerPlayerId) || state.players.Any(player => player.life != 0) ||
                            !view.IsFinished || view.HasWinner || view.IsPlayerWinner)
                            throw new InvalidOperationException("Replayed authoritative state/view is not the captured draw.");
                        report.replayedClients++;
                        report.replayedMessages += trace.messages.Length;
                        report.matchId = state.matchId;
                        report.revision = state.revision;
                    }
                }
                report.ok = true;
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                Debug.Log($"Actual wire draw replay passed: {report.replayedClients} clients, {report.replayedMessages} messages, {report.matchId}, revision {report.revision}.");
            }
            catch (Exception error)
            {
                report.error = error.ToString();
                File.WriteAllText(output, JsonUtility.ToJson(report, true));
                throw;
            }
        }

        private static string Argument(string[] arguments, string key)
        {
            var index = Array.IndexOf(arguments, key);
            if (index < 0 || index + 1 >= arguments.Length || string.IsNullOrWhiteSpace(arguments[index + 1]))
                throw new ArgumentException("Missing " + key);
            return Path.GetFullPath(arguments[index + 1]);
        }

        [Serializable] private sealed class Capture
        {
            public bool ok;
            public string matchId;
            public int drawRevision;
            public Trace[] traces;
        }
        [Serializable] private sealed class Trace { public string viewerPlayerId; public Message[] messages; }
        [Serializable] private sealed class Message { public int opCode; public string json; }
        [Serializable] private sealed class AuditResult
        {
            public bool ok;
            public string error, sourcePath, sourceSha256, unityVersion, matchId;
            public int revision, replayedClients, replayedMessages;
        }
        private sealed class ReplayTransport : IMatchTransport
        {
            public event Action<int, string> MessageReceived;
            public event Action<Exception> Faulted { add { } remove { } }
            public event Action<MatchConnectionStatus> ConnectionStateChanged { add { } remove { } }
            public MatchConnectionStatus CurrentStatus => new MatchConnectionStatus(MatchConnectionPhase.Ready);
            public void Deliver(int opcode, string json) => MessageReceived?.Invoke(opcode, json);
            public Task ConnectAsync() => Task.CompletedTask;
            public Task SendAsync(int opcode, string json) => Task.FromException(new NotSupportedException("Read-only wire replay."));
            public Task DisconnectAsync() => Task.CompletedTask;
            public void Dispose() { }
        }
    }
}
