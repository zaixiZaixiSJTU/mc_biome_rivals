using System;
using System.Threading.Tasks;
using BiomeRivals.Core;
using UnityEngine;

namespace BiomeRivals.Networking
{
    public sealed class AuthoritativeMatchGateway : IMatchGateway, IMatchReconnectDiagnostics
    {
        private readonly IMatchTransport _transport;
        private MatchConnectionStatus _currentStatus;
        private bool _compatibilityFailed;
        private bool _disposed;

        public event Action<MatchEventBatchDto> EventBatchReceived;
        public event Action<MatchStateDto> SnapshotReceived;
        public event Action<CommandRejectionDto> CommandRejected;
        public event Action<Exception> Faulted;
        public event Action<MatchConnectionStatus> ConnectionStateChanged;

        public MatchConnectionStatus CurrentStatus => _currentStatus;

        public AuthoritativeMatchGateway(IMatchTransport transport)
        {
            _transport = transport ?? throw new ArgumentNullException(nameof(transport));
            _currentStatus = _transport.CurrentStatus;
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
            if (!string.Equals(command.rulesetVersion, GameVersions.Ruleset, StringComparison.Ordinal))
                throw new InvalidOperationException("Command ruleset version does not match this client.");
            if (_compatibilityFailed)
                throw new InvalidOperationException("The authoritative match uses an incompatible protocol or ruleset.");
            return _transport.SendAsync(MatchOpcodes.Command, SerializeCommand(command));
        }

        public static string SerializeCommand(MatchCommandDto command)
        {
            if (command == null) throw new ArgumentNullException(nameof(command));
            if ((command.type == MatchCommandTypes.DeployCard || command.type == MatchCommandTypes.PlayCard) &&
                (command.payload == null || !IsValidHandCardInstanceId(command.payload.handCardInstanceId)))
                throw new InvalidOperationException("Deploy and play commands require a valid handCardInstanceId.");
            switch (command.type)
            {
                case MatchCommandTypes.Mulligan:
                    return JsonUtility.ToJson(new MulliganCommandWire(command));
                case MatchCommandTypes.DeployCard:
                    return string.IsNullOrEmpty(command.payload.targetType)
                        ? JsonUtility.ToJson(new DeployCommandWire(command))
                        : JsonUtility.ToJson(new TargetedDeployCommandWire(command));
                case MatchCommandTypes.PlayCard:
                    if (command.payload.targetInstanceIds != null && command.payload.targetInstanceIds.Length > 0)
                        return JsonUtility.ToJson(new MultiTargetedPlayCardCommandWire(command));
                    return string.IsNullOrEmpty(command.payload.targetType)
                        ? JsonUtility.ToJson(new PlayCardCommandWire(command))
                        : JsonUtility.ToJson(new TargetedPlayCardCommandWire(command));
                case MatchCommandTypes.ResolveChoice:
                    return JsonUtility.ToJson(new ResolveChoiceCommandWire(command));
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

        private static bool IsValidHandCardInstanceId(string value)
        {
            const string prefix = "hand-";
            if (string.IsNullOrEmpty(value) || !value.StartsWith(prefix, StringComparison.Ordinal) || value.Length == prefix.Length)
                return false;
            for (var index = prefix.Length; index < value.Length; index++)
                if (value[index] < '0' || value[index] > '9') return false;
            return true;
        }

        public Task DisconnectAsync() => _transport.DisconnectAsync();

        public Task SimulateUnexpectedDisconnectAsync()
        {
            if (_transport is IMatchReconnectDiagnostics diagnostics)
                return diagnostics.SimulateUnexpectedDisconnectAsync();
            return Task.FromException(new NotSupportedException(
                "The configured match transport does not expose reconnect diagnostics."));
        }

        private void HandleMessage(int opcode, string json)
        {
            try
            {
                switch (opcode)
                {
                    case MatchOpcodes.EventBatch:
                        var batch = JsonUtility.FromJson<MatchEventBatchDto>(json);
                        if (batch == null || !HasCompatibleVersion(batch.protocolVersion, batch.rulesetVersion))
                        {
                            FailCompatibility("Server event protocol or ruleset version is unsupported.");
                            return;
                        }
                        EventBatchReceived?.Invoke(batch);
                        break;
                    case MatchOpcodes.Rejection:
                        CommandRejected?.Invoke(JsonUtility.FromJson<CommandRejectionDto>(json));
                        break;
                    case MatchOpcodes.Snapshot:
                        var snapshot = JsonUtility.FromJson<MatchStateDto>(json);
                        if (snapshot != null && IsExplicitJsonNull(json, "pendingChoice")) snapshot.pendingChoice = null;
                        if (snapshot == null || !HasCompatibleVersion(snapshot.protocolVersion, snapshot.rulesetVersion))
                        {
                            FailCompatibility("Server snapshot protocol or ruleset version is unsupported.");
                            return;
                        }
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

        private void HandleConnectionState(MatchConnectionStatus status)
        {
            if (_compatibilityFailed) return;
            _currentStatus = status;
            ConnectionStateChanged?.Invoke(status);
        }

        private static bool HasCompatibleVersion(int protocolVersion, string rulesetVersion) =>
            protocolVersion == GameVersions.Protocol &&
            string.Equals(rulesetVersion, GameVersions.Ruleset, StringComparison.Ordinal);

        private void FailCompatibility(string message)
        {
            if (_compatibilityFailed) return;
            _compatibilityFailed = true;
            var exception = new InvalidOperationException(message);
            _currentStatus = new MatchConnectionStatus(
                MatchConnectionPhase.Failed,
                message,
                _currentStatus.MatchId,
                _currentStatus.Attempt);
            ConnectionStateChanged?.Invoke(_currentStatus);
            Faulted?.Invoke(exception);
        }

        private static bool IsExplicitJsonNull(string json, string propertyName)
        {
            if (string.IsNullOrEmpty(json) || string.IsNullOrEmpty(propertyName)) return false;
            var propertyIndex = json.IndexOf("\"" + propertyName + "\"", StringComparison.Ordinal);
            if (propertyIndex < 0) return false;
            var colonIndex = json.IndexOf(':', propertyIndex + propertyName.Length + 2);
            if (colonIndex < 0) return false;
            var valueIndex = colonIndex + 1;
            while (valueIndex < json.Length && char.IsWhiteSpace(json[valueIndex])) valueIndex++;
            return valueIndex + 4 <= json.Length && string.CompareOrdinal(json, valueIndex, "null", 0, 4) == 0;
        }

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
            public string handCardInstanceId;
            public string slotKind;
            public int slotIndex;
            public string paymentMethod;
        }

        [Serializable]
        private sealed class TargetedDeployWirePayload
        {
            public string cardId;
            public string handCardInstanceId;
            public string slotKind;
            public int slotIndex;
            public string paymentMethod;
            public string targetType;
            public string targetInstanceId;
        }

        [Serializable]
        private sealed class PlayWirePayload
        {
            public string cardId;
            public string handCardInstanceId;
        }

        [Serializable]
        private sealed class TargetedPlayWirePayload
        {
            public string cardId;
            public string handCardInstanceId;
            public string targetType;
            public string targetInstanceId;
        }

        [Serializable]
        private sealed class MultiTargetedPlayWirePayload
        {
            public string cardId;
            public string handCardInstanceId;
            public string targetType;
            public string[] targetInstanceIds;
        }

        [Serializable]
        private sealed class ResolveChoiceWirePayload
        {
            public string choiceId;
            public int selectedOptionIndex;
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
                    handCardInstanceId = command.payload.handCardInstanceId,
                    slotKind = command.payload.slotKind,
                    slotIndex = command.payload.slotIndex,
                    paymentMethod = command.payload.paymentMethod
                };
            }
        }

        [Serializable]
        private sealed class TargetedDeployCommandWire : CommandWireBase
        {
            public TargetedDeployWirePayload payload;

            public TargetedDeployCommandWire(MatchCommandDto command) : base(command)
            {
                payload = new TargetedDeployWirePayload
                {
                    cardId = command.payload.cardId,
                    handCardInstanceId = command.payload.handCardInstanceId,
                    slotKind = command.payload.slotKind,
                    slotIndex = command.payload.slotIndex,
                    paymentMethod = command.payload.paymentMethod,
                    targetType = command.payload.targetType,
                    targetInstanceId = command.payload.targetInstanceId
                };
            }
        }

        [Serializable]
        private sealed class PlayCardCommandWire : CommandWireBase
        {
            public PlayWirePayload payload;

            public PlayCardCommandWire(MatchCommandDto command) : base(command)
            {
                payload = new PlayWirePayload
                {
                    cardId = command.payload.cardId,
                    handCardInstanceId = command.payload.handCardInstanceId
                };
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
                    handCardInstanceId = command.payload.handCardInstanceId,
                    targetType = command.payload.targetType,
                    targetInstanceId = command.payload.targetInstanceId
                };
            }
        }

        [Serializable]
        private sealed class MultiTargetedPlayCardCommandWire : CommandWireBase
        {
            public MultiTargetedPlayWirePayload payload;

            public MultiTargetedPlayCardCommandWire(MatchCommandDto command) : base(command)
            {
                payload = new MultiTargetedPlayWirePayload
                {
                    cardId = command.payload.cardId,
                    handCardInstanceId = command.payload.handCardInstanceId,
                    targetType = command.payload.targetType,
                    targetInstanceIds = command.payload.targetInstanceIds ?? Array.Empty<string>()
                };
            }
        }

        [Serializable]
        private sealed class ResolveChoiceCommandWire : CommandWireBase
        {
            public ResolveChoiceWirePayload payload;

            public ResolveChoiceCommandWire(MatchCommandDto command) : base(command)
            {
                payload = new ResolveChoiceWirePayload
                {
                    choiceId = command.payload.choiceId,
                    selectedOptionIndex = command.payload.selectedOptionIndex
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
