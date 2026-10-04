#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;

namespace BiomeRivals.Networking
{
    // Receive-side latency diagnostics only. Commands still go to the real transport unchanged.
    public interface IMatchInboundDeliveryDiagnostics
    {
        IDisposable HoldIncomingMatchMessages();
        int HeldIncomingMessageCount { get; }
    }
}
#endif
