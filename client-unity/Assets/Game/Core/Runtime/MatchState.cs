using System;

namespace BiomeRivals.Core
{
    [Serializable]
    public sealed class PlayerStateDto
    {
        public string playerId = string.Empty;
        public int life;
        public int armor;
        public int redstone;
        public int redstoneCapacity;
    }

    [Serializable]
    public sealed class MatchStateDto
    {
        public string matchId = string.Empty;
        public string rulesetVersion = string.Empty;
        public int revision;
        public long lastEventId;
        public string status = string.Empty;
        public int turn;
        public int activePlayerIndex;
        public PlayerStateDto[] players = Array.Empty<PlayerStateDto>();
        public string winnerPlayerId = string.Empty;
    }

    public sealed class MatchStateStore
    {
        public MatchStateDto Current { get; private set; }

        public void Replace(MatchStateDto snapshot)
        {
            Current = snapshot ?? throw new ArgumentNullException(nameof(snapshot));
        }

        public void Clear()
        {
            Current = null;
        }
    }
}
