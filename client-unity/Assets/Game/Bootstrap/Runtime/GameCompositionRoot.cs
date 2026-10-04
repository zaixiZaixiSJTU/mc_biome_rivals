using BiomeRivals.Core;
using BiomeRivals.Networking;
using BiomeRivals.Presentation;
using UnityEngine;

namespace BiomeRivals.Bootstrap
{
    public sealed class GameCompositionRoot : MonoBehaviour
    {
        private IMatchGateway _matchGateway;
        private IPlayerAccountService _playerAccountService;
        private PresentationQueue _presentationQueue;
        private readonly MatchStateStore _matchStateStore = new MatchStateStore();

        public static GameCompositionRoot Instance { get; private set; }
        public MatchStateStore MatchStateStore => _matchStateStore;
        public IMatchGateway MatchGateway => _matchGateway;
        public IPlayerAccountService PlayerAccountService => EnsurePlayerAccountService();
        public PresentationQueue PresentationQueue => _presentationQueue;

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.BeforeSceneLoad)]
        private static void EnsureCreated()
        {
            if (Instance != null) return;
            var root = new GameObject("[BiomeRivals]");
            DontDestroyOnLoad(root);
            root.AddComponent<GameCompositionRoot>();
        }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            _presentationQueue = gameObject.AddComponent<PresentationQueue>();
            BindGateway(new AuthoritativeMatchGateway(new UnavailableMatchTransport()));
        }

        private void OnDestroy()
        {
            UnbindGateway();
            _playerAccountService?.Dispose();
            _playerAccountService = null;
            if (Instance == this) Instance = null;
        }

        public void RegisterOnlineTransport(IMatchTransport transport)
        {
            UnbindGateway();
            _matchStateStore.Clear();
            _presentationQueue.Reset();
            BindGateway(new AuthoritativeMatchGateway(transport));
        }

        public IMatchGateway RegisterDefaultOnlineTransport(
            string factionId,
            int cardContentVersion,
            int implementedEffectRegistryVersion)
        {
            var settings = NakamaConnectionSettings.Load();
            var accountService = EnsurePlayerAccountService(settings);
            var sessionProvider = accountService as IPlayerAccountSessionProvider ??
                throw new System.InvalidOperationException("The configured account service cannot provide a matchmaking session.");
            RegisterOnlineTransport(new NakamaMatchTransport(
                settings,
                new MatchmakingPreferences(factionId, cardContentVersion, implementedEffectRegistryVersion),
                sessionProvider));
            return _matchGateway;
        }

        private IPlayerAccountService EnsurePlayerAccountService(NakamaConnectionSettings settings = null)
        {
            if (_playerAccountService != null) return _playerAccountService;
            _playerAccountService = new PlayerAccountService(
                new NakamaPlayerAccountBackend(settings ?? NakamaConnectionSettings.Load()));
            return _playerAccountService;
        }

        private void BindGateway(IMatchGateway gateway)
        {
            _matchGateway = gateway;
            _matchGateway.SnapshotReceived += HandleSnapshot;
            _matchGateway.EventBatchReceived += _matchStateStore.Apply;
            _matchGateway.EventBatchReceived += _presentationQueue.Enqueue;
            _matchGateway.CommandRejected += HandleCommandRejected;
            _matchGateway.Faulted += HandleGatewayFault;
            _matchGateway.ConnectionStateChanged += HandleConnectionState;
        }

        private void UnbindGateway()
        {
            if (_matchGateway == null) return;
            _matchGateway.SnapshotReceived -= HandleSnapshot;
            _matchGateway.EventBatchReceived -= _matchStateStore.Apply;
            _matchGateway.EventBatchReceived -= _presentationQueue.Enqueue;
            _matchGateway.CommandRejected -= HandleCommandRejected;
            _matchGateway.Faulted -= HandleGatewayFault;
            _matchGateway.ConnectionStateChanged -= HandleConnectionState;
            _matchGateway.Dispose();
            _matchGateway = null;
        }

        private void HandleCommandRejected(CommandRejectionDto rejection)
        {
            Debug.LogWarning(
                $"Command {rejection.commandId} rejected: {rejection.code} - {rejection.message}",
                this);
        }

        private void HandleSnapshot(MatchStateDto snapshot)
        {
            _matchStateStore.Replace(snapshot);
            _presentationQueue.Reset(snapshot.lastEventId);
        }

        private void HandleGatewayFault(System.Exception exception) => Debug.LogException(exception, this);

        private void HandleConnectionState(MatchConnectionStatus status) =>
            Debug.Log($"Match connection: {status.Phase} {status.Detail}", this);
    }
}
