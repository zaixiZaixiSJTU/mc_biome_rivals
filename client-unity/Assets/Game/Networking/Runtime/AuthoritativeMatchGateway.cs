using System;
using System.Threading.Tasks;
using BiomeRivals.Core;
using UnityEngine;

namespace BiomeRivals.Networking
{
    public sealed partial class AuthoritativeMatchGateway : IMatchGateway, IMatchReconnectDiagnostics
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        , IMatchTestFixtureDiagnostics, IMatchInboundDeliveryDiagnostics
#endif
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

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public event Action<MatchTestFixtureResult> TestFixtureResultReceived;
        public Task RequestSimultaneousDefeatFixtureAsync(bool readableHand = false)
        {
            if (_disposed || _compatibilityFailed || !_currentStatus.CanSendCommands)
                throw new InvalidOperationException("Test fixture diagnostics require a compatible ready match.");
            return _transport.SendAsync(255, readableHand
                ? "{\"fixture\":\"simultaneous-defeat-readable\"}"
                : "{\"fixture\":\"simultaneous-defeat\"}");
        }
#endif

        public Task SimulateUnexpectedDisconnectAsync()
        {
            if (_transport is IMatchReconnectDiagnostics diagnostics)
                return diagnostics.SimulateUnexpectedDisconnectAsync();
            return Task.FromException(new NotSupportedException(
                "The configured match transport does not expose reconnect diagnostics."));
        }

        private void HandleMessage(int opcode, string json)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            lock (_inboundDeliveryLock)
            {
                if (_disposed) return;
                if (_holdingInbound || _drainingInbound)
                {
                    if (_heldInbound.Count >= 64 || _heldInboundCharacters + (json?.Length ?? 0) > 2 * 1024 * 1024)
                    {
                        CancelInboundDeliveryHold();
                        HandleFault(new InvalidOperationException("Development inbound hold exceeded its bounded receive buffer."));
                        return;
                    }
                    _heldInbound.Enqueue(new HeldInboundMessage(opcode, json));
                    _heldInboundCharacters += json?.Length ?? 0;
                    return;
                }
                DeliverMessage(opcode, json);
            }
#else
            DeliverMessage(opcode, json);
#endif
        }

        private void DeliverMessage(int opcode, string json)
        {
            try
            {
                switch (opcode)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    case 5:
                        var fixture = MatchWireJson.Deserialize<MatchTestFixtureResult>(json);
                        if (fixture == null || !fixture.ok || fixture.revision < 1 ||
                            fixture.reason != "SIMULTANEOUS_DEFEAT" || !string.IsNullOrEmpty(fixture.winnerPlayerId))
                            throw new InvalidOperationException("Invalid test fixture acknowledgement.");
                        TestFixtureResultReceived?.Invoke(fixture);
                        break;
#endif
                    case MatchOpcodes.EventBatch:
                        var batch = MatchWireJson.Deserialize<MatchEventBatchDto>(json);
                        if (batch == null || !HasCompatibleVersion(batch.protocolVersion, batch.rulesetVersion))
                        {
                            FailCompatibility("Server event protocol or ruleset version is unsupported.");
                            return;
                        }
                        EventBatchReceived?.Invoke(batch);
                        break;
                    case MatchOpcodes.Rejection:
                        CommandRejected?.Invoke(MatchWireJson.Deserialize<CommandRejectionDto>(json));
                        break;
                    case MatchOpcodes.Snapshot:
                        var snapshot = MatchWireJson.Deserialize<MatchStateDto>(json);
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
                HandleFault(exception);
            }
        }

        private void HandleFault(Exception exception)
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CancelInboundDeliveryHold();
#endif
            Faulted?.Invoke(exception);
        }

        private void HandleConnectionState(MatchConnectionStatus status)
        {
            if (_compatibilityFailed) return;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            if (!status.CanSendCommands || status.MatchId != _currentStatus.MatchId) CancelInboundDeliveryHold();
#endif
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
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CancelInboundDeliveryHold();
#endif
            var exception = new InvalidOperationException(message);
            _currentStatus = new MatchConnectionStatus(
                MatchConnectionPhase.Failed,
                message,
                _currentStatus.MatchId,
                _currentStatus.Attempt);
            ConnectionStateChanged?.Invoke(_currentStatus);
            Faulted?.Invoke(exception);
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            CancelInboundDeliveryHold();
#endif
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
