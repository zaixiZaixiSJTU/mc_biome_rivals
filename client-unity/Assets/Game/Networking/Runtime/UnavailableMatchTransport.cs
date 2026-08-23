using System;
using System.Threading.Tasks;

namespace BiomeRivals.Networking
{
    public sealed class UnavailableMatchTransport : IMatchTransport
    {
        public event Action<int, string> MessageReceived;
        public event Action<Exception> Faulted;

        public Task ConnectAsync() => Task.FromException(NotConfigured());
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
