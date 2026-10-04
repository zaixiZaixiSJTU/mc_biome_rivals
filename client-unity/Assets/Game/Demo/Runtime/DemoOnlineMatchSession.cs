using System;
using System.Threading.Tasks;
using BiomeRivals.Core;
using BiomeRivals.Networking;

namespace BiomeRivals.Demo
{
    public sealed class DemoOnlineMatchSession : IDisposable, IPlayerOperations
    {
        private static readonly TimeSpan CommandTimeout = TimeSpan.FromSeconds(8);
        private readonly IMatchGateway _gateway;
        private readonly MatchStateStore _store;
        private readonly MatchCommandDispatcher _dispatcher;
        private bool _disposed;

        public event Action StateChanged;
        public event Action<string> CommandPending;
        public event Action<MatchCommandDispatchResult> CommandCompleted;

        public DemoAuthoritativeMatchView View { get; }
        public bool HasAuthoritativeState =>
            _store.Current != null &&
            _gateway.CurrentStatus.Phase != MatchConnectionPhase.Offline &&
            _gateway.CurrentStatus.Phase != MatchConnectionPhase.Disconnecting &&
            _gateway.CurrentStatus.Phase != MatchConnectionPhase.Failed;
        public bool CanIssueCommand => HasAuthoritativeState && !View.IsFinished &&
            _gateway.CurrentStatus.CanSendCommands && _dispatcher.PendingCount == 0;
        public bool HasPendingCommand => _dispatcher.PendingCount > 0;

        public PlayerObservation Observe()
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DemoOnlineMatchSession));
            return PlayerObservation.FromSnapshot(HasAuthoritativeState ? _store.Current : null, CanIssueCommand);
        }

        public Task<MatchCommandDispatchResult> ExecuteAsync(PlayerActionRequest action)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DemoOnlineMatchSession));
            if (action == null) throw new ArgumentNullException(nameof(action));
            var id = NewCommandId();
            if (!CanIssueCommand) return RejectAction(id, "NOT_READY", "The player session is not ready.");
            if (action.matchId != _store.Current.matchId)
                return RejectAction(id, "MATCH_MISMATCH", "The action belongs to a different match.");
            if (action.expectedRevision != Revision)
                return RejectAction(id, "STALE_OBSERVATION", "Observe the current state before deciding again.");
            switch (action.type)
            {
                case MatchCommandTypes.Mulligan:
                case MatchCommandTypes.DeployCard:
                case MatchCommandTypes.PlayCard:
                case MatchCommandTypes.ResolveChoice:
                case MatchCommandTypes.EnterCombat:
                case MatchCommandTypes.Attack:
                case MatchCommandTypes.EndTurn:
                case MatchCommandTypes.Concede: break;
                default: return RejectAction(id, "UNKNOWN_ACTION", "Unsupported player operation.");
            }
            if (action.payload == null) return RejectAction(id, "INVALID_PAYLOAD", "An action payload is required.");
            // Freeze caller-owned arrays before asynchronous transport starts.
            var payload = UnityEngine.JsonUtility.FromJson<MatchCommandPayloadDto>(
                UnityEngine.JsonUtility.ToJson(action.payload));
            return Send(new MatchCommandDto { protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset, commandId = id, expectedRevision = action.expectedRevision,
                type = action.type, payload = payload });
        }

        private Task<MatchCommandDispatchResult> RejectAction(string id, string code, string message) =>
            Task.FromResult(new MatchCommandDispatchResult(id, MatchCommandOutcome.Rejected,
                code, message, _store.Current?.revision ?? -1));

        public DemoOnlineMatchSession(IMatchGateway gateway, MatchStateStore store)
        {
            _gateway = gateway ?? throw new ArgumentNullException(nameof(gateway));
            _store = store ?? throw new ArgumentNullException(nameof(store));
            View = new DemoAuthoritativeMatchView(store);
            _dispatcher = new MatchCommandDispatcher(gateway);
            _store.Changed += HandleStateChanged;
            _gateway.ConnectionStateChanged += HandleConnectionStateChanged;
            _dispatcher.CommandPending += HandleCommandPending;
            _dispatcher.CommandCompleted += HandleCommandCompleted;
        }

        public Task<MatchCommandDispatchResult> DeployAsync(
            string cardId,
            DemoSlotKind kind,
            int slotIndex,
            string handCardInstanceId,
            string paymentMethod = MatchPaymentMethods.Redstone,
            string targetType = "",
            string targetInstanceId = "") =>
            Send(MatchCommandFactory.DeployCard(
                NewCommandId(), Revision, cardId, kind == DemoSlotKind.Unit ? "UNIT" : "BUILDING", slotIndex,
                handCardInstanceId, paymentMethod, targetType, targetInstanceId));

        public Task<MatchCommandDispatchResult> MulliganAsync(int[] cardIndices) =>
            Send(MatchCommandFactory.Mulligan(NewCommandId(), Revision, cardIndices));

        public Task<MatchCommandDispatchResult> PlayCardAsync(
            string cardId,
            string handCardInstanceId,
            string targetType = "",
            string targetInstanceId = "",
            string[] targetInstanceIds = null) =>
            Send(MatchCommandFactory.PlayCard(NewCommandId(), Revision, cardId, handCardInstanceId, targetType, targetInstanceId, targetInstanceIds));

        public Task<MatchCommandDispatchResult> ResolveChoiceAsync(string choiceId, int selectedOptionIndex) =>
            Send(MatchCommandFactory.ResolveChoice(NewCommandId(), Revision, choiceId, selectedOptionIndex));

        public Task<MatchCommandDispatchResult> EnterCombatAsync() =>
            Send(MatchCommandFactory.EnterCombat(NewCommandId(), Revision));

        public Task<MatchCommandDispatchResult> AttackAsync(string attackerInstanceId, string targetType, string targetInstanceId = "") =>
            Send(MatchCommandFactory.Attack(NewCommandId(), Revision, attackerInstanceId, targetType, targetInstanceId));

        public Task<MatchCommandDispatchResult> EndTurnAsync() =>
            Send(MatchCommandFactory.EndTurn(NewCommandId(), Revision));

        public Task<MatchCommandDispatchResult> ConcedeAsync() =>
            Send(MatchCommandFactory.Concede(NewCommandId(), Revision));

        private int Revision
        {
            get
            {
                if (!HasAuthoritativeState) throw new InvalidOperationException("An authoritative snapshot is required before issuing commands.");
                return _store.Current.revision;
            }
        }

        private Task<MatchCommandDispatchResult> Send(MatchCommandDto command)
        {
            if (_disposed) throw new ObjectDisposedException(nameof(DemoOnlineMatchSession));
            if (!CanIssueCommand) throw new InvalidOperationException("The authoritative match is not ready or another command is pending.");
            return _dispatcher.SendAndWaitAsync(command, CommandTimeout);
        }

        private static string NewCommandId() => "online-" + Guid.NewGuid().ToString("N");
        private void HandleStateChanged(MatchStateDto _) => StateChanged?.Invoke();
        private void HandleConnectionStateChanged(MatchConnectionStatus _) => StateChanged?.Invoke();
        private void HandleCommandPending(string commandId) => CommandPending?.Invoke(commandId);
        private void HandleCommandCompleted(MatchCommandDispatchResult result) => CommandCompleted?.Invoke(result);

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _store.Changed -= HandleStateChanged;
            _gateway.ConnectionStateChanged -= HandleConnectionStateChanged;
            _dispatcher.CommandPending -= HandleCommandPending;
            _dispatcher.CommandCompleted -= HandleCommandCompleted;
            _dispatcher.Dispose();
        }
    }
}
