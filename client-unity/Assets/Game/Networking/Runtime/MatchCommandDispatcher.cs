using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using BiomeRivals.Core;

namespace BiomeRivals.Networking
{
    public enum MatchCommandOutcome
    {
        Accepted,
        Rejected,
        TransportFailed,
        TimedOut
    }

    public readonly struct MatchCommandDispatchResult
    {
        public readonly string CommandId;
        public readonly MatchCommandOutcome Outcome;
        public readonly string Code;
        public readonly string Message;
        public readonly int Revision;

        public MatchCommandDispatchResult(string commandId, MatchCommandOutcome outcome, string code, string message, int revision)
        {
            CommandId = commandId ?? string.Empty;
            Outcome = outcome;
            Code = code ?? string.Empty;
            Message = message ?? string.Empty;
            Revision = revision;
        }
    }

    public sealed class MatchCommandDispatcher : IDisposable
    {
        private readonly IMatchGateway _gateway;
        private readonly Dictionary<string, TaskCompletionSource<MatchCommandDispatchResult>> _pending =
            new Dictionary<string, TaskCompletionSource<MatchCommandDispatchResult>>(StringComparer.Ordinal);
        private bool _disposed;

        public event Action<string> CommandPending;
        public event Action<MatchCommandDispatchResult> CommandCompleted;

        public int PendingCount => _pending.Count;

        public MatchCommandDispatcher(IMatchGateway gateway)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _gateway.EventBatchReceived += HandleAccepted;
            _gateway.CommandRejected += HandleRejected;
            _gateway.Faulted += HandleFault;
            _gateway.ConnectionStateChanged += HandleConnectionState;
        }

        public async Task<MatchCommandDispatchResult> SendAndWaitAsync(MatchCommandDto command, TimeSpan timeout)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(MatchCommandDispatcher));
            if (command == null) throw new ArgumentNullException(nameof(command));
            if (string.IsNullOrWhiteSpace(command.commandId)) throw new ArgumentException("Command ID is required.", nameof(command));
            if (timeout <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(timeout));
            if (!_gateway.CurrentStatus.CanSendCommands)
                return CompleteImmediately(command.commandId, MatchCommandOutcome.TransportFailed, "NOT_CONNECTED", "The authoritative match is not ready.", command.expectedRevision);
            if (_pending.ContainsKey(command.commandId))
                throw new InvalidOperationException("A command with the same ID is already pending: " + command.commandId);

            var source = new TaskCompletionSource<MatchCommandDispatchResult>(TaskCreationOptions.RunContinuationsAsynchronously);
            _pending.Add(command.commandId, source);
            CommandPending?.Invoke(command.commandId);
            try
            {
                await _gateway.SendCommandAsync(command);
            }
            catch (Exception exception)
            {
                return Complete(command.commandId, MatchCommandOutcome.TransportFailed, "SEND_FAILED", exception.Message, command.expectedRevision);
            }

            var completed = await Task.WhenAny(source.Task, Task.Delay(timeout));
            if (completed == source.Task) return await source.Task;
            return Complete(command.commandId, MatchCommandOutcome.TimedOut, "ACK_TIMEOUT", "The server did not acknowledge the command in time.", command.expectedRevision);
        }

        private void HandleAccepted(MatchEventBatchDto batch)
        {
            if (batch == null || string.IsNullOrEmpty(batch.acknowledgedCommandId)) return;
            Complete(batch.acknowledgedCommandId, MatchCommandOutcome.Accepted, string.Empty, string.Empty, batch.revision);
        }

        private void HandleRejected(CommandRejectionDto rejection)
        {
            if (rejection == null || string.IsNullOrEmpty(rejection.commandId)) return;
            Complete(rejection.commandId, MatchCommandOutcome.Rejected, rejection.code, rejection.message, rejection.revision);
        }

        private void HandleFault(Exception exception)
        {
            var commandIds = new List<string>(_pending.Keys);
            foreach (var commandId in commandIds)
                Complete(commandId, MatchCommandOutcome.TransportFailed, "TRANSPORT_FAULT", exception?.Message ?? "Unknown transport fault.", 0);
        }

        private void HandleConnectionState(MatchConnectionStatus status)
        {
            if (status.Phase != MatchConnectionPhase.Disconnecting &&
                status.Phase != MatchConnectionPhase.Offline &&
                status.Phase != MatchConnectionPhase.Failed) return;
            HandleFault(new InvalidOperationException(
                string.IsNullOrEmpty(status.Detail) ? "The authoritative connection is no longer available." : status.Detail));
        }

        private MatchCommandDispatchResult CompleteImmediately(string commandId, MatchCommandOutcome outcome, string code, string message, int revision)
        {
            var result = new MatchCommandDispatchResult(commandId, outcome, code, message, revision);
            CommandCompleted?.Invoke(result);
            return result;
        }

        private MatchCommandDispatchResult Complete(string commandId, MatchCommandOutcome outcome, string code, string message, int revision)
        {
            var result = new MatchCommandDispatchResult(commandId, outcome, code, message, revision);
            if (!_pending.TryGetValue(commandId, out var source)) return result;
            _pending.Remove(commandId);
            source.TrySetResult(result);
            CommandCompleted?.Invoke(result);
            return result;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _gateway.EventBatchReceived -= HandleAccepted;
            _gateway.CommandRejected -= HandleRejected;
            _gateway.Faulted -= HandleFault;
            _gateway.ConnectionStateChanged -= HandleConnectionState;
            HandleFault(new ObjectDisposedException(nameof(MatchCommandDispatcher)));
        }
    }
}
