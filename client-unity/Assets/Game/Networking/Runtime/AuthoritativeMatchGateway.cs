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
        public event Action<MatchConnectionStatus> ConnectionStateChanged;

        public MatchConnectionStatus CurrentStatus => _transport.CurrentStatus;

        public AuthoritativeMatchGateway(IMatchTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _transport.MessageReceived += HandleMessage;
            _transport.Faulted += HandleFault;
            _transport.ConnectionStateChanged += HandleConnectionState;
        }

        public Task ConnectAsync() => _transport.ConnectAsync();

        public Task SendCommandAsync(MatchCommandDto command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (command.protocolVersion != GameVersions.Protocol)
                throw new InvalidOperationException("Command protocol version does not match this client.");
            return _transport.SendAsync(MatchOpcodes.Command, SerializeCommand(command));
        }

        public static string SerializeCommand(MatchCommandDto command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            switch (command.type)
            {
                case MatchCommandTypes.Mulligan:
                    return JsonUtility.ToJson(new MulliganCommandWire(command));
                case MatchCommandTypes.DeployCard:
                    return JsonUtility.ToJson(new DeployCommandWire(command));
                case MatchCommandTypes.PlayCard:
                    return string.IsNullOrEmpty(command.payload.targetType)
                        ? JsonUtility.ToJson(new PlayCardCommandWire(command))
                        : JsonUtility.ToJson(new TargetedPlayCardCommandWire(command));
                case MatchCommandTypes.Attack:
                    return command.payload.targetType == "HERO"
                        ? JsonUtility.ToJson(new HeroAttackCommandWire(command))
                        : JsonUtility.ToJson(new AttackCommandWire(command));
                case MatchCommandTypes.EnterCombat:
                case MatchCommandTypes.EndTurn:
                case MatchCommandTypes.Concede:
                    return JsonUtility.ToJson(new EmptyCommandWire(command));
                default:
                    throw new InvalidOperationException($"Unsupported command type '{command.type}'.");
            }
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

        private void HandleConnectionState(MatchConnectionStatus status) => ConnectionStateChanged?.Invoke(status);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _transport.MessageReceived -= HandleMessage;
            _transport.Faulted -= HandleFault;
            _transport.ConnectionStateChanged -= HandleConnectionState;
            _transport.Dispose();
        }

        [Serializable]
        private sealed class EmptyWirePayload { }

        [Serializable]
        private sealed class MulliganWirePayload
        {
            public int[] cardIndices;
        }

        [Serializable]
        private sealed class DeployWirePayload
        {
            public string cardId;
            public string slotKind;
            public int slotIndex;
            public string paymentMethod;
        }

        [Serializable]
        private sealed class PlayWirePayload
        {
            public string cardId;
        }

        [Serializable]
        private sealed class TargetedPlayWirePayload
        {
            public string cardId;
            public string targetType;
            public string targetInstanceId;
        }

        [Serializable]
        private sealed class AttackWirePayload
        {
            public string attackerInstanceId;
            public string targetType;
            public string targetInstanceId;
        }

        [Serializable]
        private sealed class HeroAttackWirePayload
        {
            public string attackerInstanceId;
            public string targetType;
        }

        [Serializable]
        private abstract class CommandWireBase
        {
            public int protocolVersion;
            public string rulesetVersion;
            public string commandId;
            public int expectedRevision;
            public string type;

            protected CommandWireBase(MatchCommandDto command)
            {
                protocolVersion = command.protocolVersion;
                rulesetVersion = command.rulesetVersion;
                commandId = command.commandId;
                expectedRevision = command.expectedRevision;
                type = command.type;
            }
        }

        [Serializable]
        private sealed class EmptyCommandWire : CommandWireBase
        {
            public EmptyWirePayload payload = new EmptyWirePayload();
            public EmptyCommandWire(MatchCommandDto command) : base(command) { }
        }

        [Serializable]
        private sealed class MulliganCommandWire : CommandWireBase
        {
            public MulliganWirePayload payload;

            public MulliganCommandWire(MatchCommandDto command) : base(command)
            {
                payload = new MulliganWirePayload { cardIndices = command.payload.cardIndices ?? Array.Empty<int>() };
            }
        }

        [Serializable]
        private sealed class DeployCommandWire : CommandWireBase
        {
            public DeployWirePayload payload;

            public DeployCommandWire(MatchCommandDto command) : base(command)
            {
                payload = new DeployWirePayload
                {
                    cardId = command.payload.cardId,
                    slotKind = command.payload.slotKind,
                    slotIndex = command.payload.slotIndex,
                    paymentMethod = command.payload.paymentMethod
                };
            }
        }

        [Serializable]
        private sealed class PlayCardCommandWire : CommandWireBase
        {
            public PlayWirePayload payload;

            public PlayCardCommandWire(MatchCommandDto command) : base(command)
            {
                payload = new PlayWirePayload { cardId = command.payload.cardId };
            }
        }

        [Serializable]
        private sealed class TargetedPlayCardCommandWire : CommandWireBase
        {
            public TargetedPlayWirePayload payload;

            public TargetedPlayCardCommandWire(MatchCommandDto command) : base(command)
            {
                payload = new TargetedPlayWirePayload
                {
                    cardId = command.payload.cardId,
                    targetType = command.payload.targetType,
                    targetInstanceId = command.payload.targetInstanceId
                };
            }
        }

        [Serializable]
        private sealed class AttackCommandWire : CommandWireBase
        {
            public AttackWirePayload payload;

            public AttackCommandWire(MatchCommandDto command) : base(command)
            {
                payload = new AttackWirePayload
                {
                    attackerInstanceId = command.payload.attackerInstanceId,
                    targetType = command.payload.targetType,
                    targetInstanceId = command.payload.targetInstanceId
                };
            }
        }

        [Serializable]
        private sealed class HeroAttackCommandWire : CommandWireBase
        {
            public HeroAttackWirePayload payload;

            public HeroAttackCommandWire(MatchCommandDto command) : base(command)
            {
                payload = new HeroAttackWirePayload
                {
                    attackerInstanceId = command.payload.attackerInstanceId,
                    targetType = command.payload.targetType
                };
            }
        }
    }
}
