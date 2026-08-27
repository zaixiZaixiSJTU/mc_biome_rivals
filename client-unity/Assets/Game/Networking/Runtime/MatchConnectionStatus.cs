using System;

namespace BiomeRivals.Networking
{
    public enum MatchConnectionPhase
    {
        Offline,
        Authenticating,
        Connecting,
        Matchmaking,
        Joining,
        Ready,
        Reconnecting,
        Disconnecting,
        Failed
    }

    public readonly struct MatchConnectionStatus
    {
        public readonly MatchConnectionPhase Phase;
        public readonly string Detail;
        public readonly string MatchId;
        public readonly int Attempt;

        public MatchConnectionStatus(MatchConnectionPhase phase, string detail = "", string matchId = "", int attempt = 0)
        {
            Phase = phase;
            Detail = detail ?? string.Empty;
            MatchId = matchId ?? string.Empty;
            Attempt = attempt;
        }

        public bool CanSendCommands => Phase == MatchConnectionPhase.Ready && !string.IsNullOrEmpty(MatchId);
    }
}
