using System;
using System.Threading.Tasks;
using BiomeRivals.Core;
using UnityEngine;

namespace BiomeRivals.Networking
{
    public sealed class AuthoritativeMatchGateway : IMatchGateway
    {
        private readonly IMatchTransport _transport;
        private bool _disposed;

        public event Action<MatchEventBatchDto> EventBatchReceived;
        public event Action<MatchStateDto> SnapshotReceived;
        public event Action<CommandRejectionDto> CommandRejected;
        public event Action<Exception> Faulted;

        public AuthoritativeMatchGateway(IMatchTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _transport.MessageReceived += HandleMessage;
            _transport.Faulted += HandleFault;
        }

        public Task ConnectAsync() => _transport.ConnectAsync();

        public Task SendCommandAsync(MatchCommandDto command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (command.protocolVersion != GameVersions.Protocol)
                throw new InvalidOperationException("Command protocol version does not match this client.");
            return _transport.SendAsync(MatchOpcodes.Command, JsonUtility.ToJson(command));
        }

        public Task DisconnectAsync() => _transport.DisconnectAsync();

        private void HandleMessage(int opcode, string json)
        {
            try
            {
                switch (opcode)
                {
                    case MatchOpcodes.EventBatch:
                        var batch = JsonUtility.FromJson<MatchEventBatchDto>(json);
                        if (batch.protocolVersion != GameVersions.Protocol)
                            throw new InvalidOperationException("Server protocol version is unsupported.");
                        EventBatchReceived?.Invoke(batch);
                        break;
                    case MatchOpcodes.Rejection:
                        CommandRejected?.Invoke(JsonUtility.FromJson<CommandRejectionDto>(json));
                        break;
                    case MatchOpcodes.Snapshot:
                        var snapshot = JsonUtility.FromJson<MatchStateDto>(json);
                        if (snapshot == null || snapshot.protocolVersion != GameVersions.Protocol || snapshot.rulesetVersion != GameVersions.Ruleset)
                            throw new InvalidOperationException("Server snapshot protocol or ruleset version is unsupported.");
                        SnapshotReceived?.Invoke(snapshot);
                        break;
                }
            }
            catch (Exception exception)
            {
                Faulted?.Invoke(exception);
            }
        }

        private void HandleFault(Exception exception) => Faulted?.Invoke(exception);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _transport.MessageReceived -= HandleMessage;
            _transport.Faulted -= HandleFault;
            _transport.Dispose();
        }
    }
}
