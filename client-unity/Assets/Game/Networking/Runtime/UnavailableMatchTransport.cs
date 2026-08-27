using System;
using System.Threading.Tasks;

namespace BiomeRivals.Networking
{
    public sealed class UnavailableMatchTransport : IMatchTransport
    {
        public event Action<int, string> MessageReceived;
        public event Action<Exception> Faulted;
        public event Action<MatchConnectionStatus> ConnectionStateChanged;

        public MatchConnectionStatus CurrentStatus { get; private set; } =
            new MatchConnectionStatus(MatchConnectionPhase.Offline, "Online transport is not configured.");

        public Task ConnectAsync()
        {
            var exception = NotConfigured();
            CurrentStatus = new MatchConnectionStatus(MatchConnectionPhase.Failed, exception.Message);
            ConnectionStateChanged?.Invoke(CurrentStatus);
            Faulted?.Invoke(exception);
            return Task.FromException(exception);
        }
        public Task SendAsync(int opcode, string json) => Task.FromException(NotConfigured());
        public Task DisconnectAsync() => Task.CompletedTask;
        public void Dispose() { }

        private static InvalidOperationException NotConfigured()
        {
            return new InvalidOperationException(
                "No online transport is registered. Install the pinned Nakama Unity SDK and register its IMatchTransport adapter in GameCompositionRoot.");
        }
    }
}
