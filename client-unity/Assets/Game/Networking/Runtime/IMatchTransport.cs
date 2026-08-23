using System;
using System.Threading.Tasks;

namespace BiomeRivals.Networking
{
    public interface IMatchTransport : IDisposable
    {
        event Action<int, string> MessageReceived;
        event Action<Exception> Faulted;

        Task ConnectAsync();
        Task SendAsync(int opcode, string json);
        Task DisconnectAsync();
    }
}
