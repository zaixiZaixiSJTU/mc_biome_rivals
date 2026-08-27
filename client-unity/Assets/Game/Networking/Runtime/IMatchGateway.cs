using System;
using System.Threading.Tasks;
using BiomeRivals.Core;

namespace BiomeRivals.Networking
{
    public interface IMatchGateway : IDisposable
    {
        event Action<MatchEventBatchDto> EventBatchReceived;
        event Action<MatchStateDto> SnapshotReceived;
        event Action<CommandRejectionDto> CommandRejected;
        event Action<Exception> Faulted;
        event Action<MatchConnectionStatus> ConnectionStateChanged;

        MatchConnectionStatus CurrentStatus { get; }

        Task ConnectAsync();
        Task SendCommandAsync(MatchCommandDto command);
        Task DisconnectAsync();
    }
}
