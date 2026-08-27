using System;
using System.Threading.Tasks;
using BiomeRivals.Core;
using BiomeRivals.Networking;

namespace BiomeRivals.Demo
{
    public sealed class DemoOnlineMatchSession : IDisposable
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
        public bool CanIssueCommand => HasAuthoritativeState && _gateway.CurrentStatus.CanSendCommands && _dispatcher.PendingCount == 0;
        public bool HasPendingCommand => _dispatcher.PendingCount > 0;

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

        public Task<MatchCommandDispatchResult> DeployAsync(string cardId, DemoSlotKind kind, int slotIndex) =>
            Send(MatchCommandFactory.DeployCard(NewCommandId(), Revision, cardId, kind == DemoSlotKind.Unit ? "UNIT" : "BUILDING", slotIndex));

        public Task<MatchCommandDispatchResult> PlayCardAsync(string cardId, string targetType = "", string targetInstanceId = "") =>
            Send(MatchCommandFactory.PlayCard(NewCommandId(), Revision, cardId, targetType, targetInstanceId));

        public Task<MatchCommandDispatchResult> EnterCombatAsync() =>
            Send(MatchCommandFactory.EnterCombat(NewCommandId(), Revision));

        public Task<MatchCommandDispatchResult> AttackAsync(string attackerInstanceId, string targetType, string targetInstanceId = "") =>
            Send(MatchCommandFactory.Attack(NewCommandId(), Revision, attackerInstanceId, targetType, targetInstanceId));

        public Task<MatchCommandDispatchResult> EndTurnAsync() =>
            Send(MatchCommandFactory.EndTurn(NewCommandId(), Revision));

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
