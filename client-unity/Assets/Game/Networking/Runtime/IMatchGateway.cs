using System;
using System.Threading.Tasks;
using BiomeRivals.Core;

namespace BiomeRivals.Networking
{
    public interface IMatchGateway : IDisposable
    {
        event Action<MatchEventBatchDto> EventBatchReceived;
        event Action<CommandRejectionDto> CommandRejected;
        event Action<Exception> Faulted;

        Task ConnectAsync();
        Task SendCommandAsync(MatchCommandDto command);
        Task DisconnectAsync();
    }
}
