#if UNITY_EDITOR || DEVELOPMENT_BUILD
using System;
using System.Threading.Tasks;

namespace BiomeRivals.Networking
{
    // Deliberately absent from release Players. The server additionally requires its explicit gate.
    public interface IMatchTestFixtureDiagnostics
    {
        event Action<MatchTestFixtureResult> TestFixtureResultReceived;
        Task RequestSimultaneousDefeatFixtureAsync(bool readableHand = false);
    }

    [Serializable]
    public sealed class MatchTestFixtureResult
    {
        public bool ok;
        public int revision;
        public string winnerPlayerId;
        public string reason;
    }
}
#endif
