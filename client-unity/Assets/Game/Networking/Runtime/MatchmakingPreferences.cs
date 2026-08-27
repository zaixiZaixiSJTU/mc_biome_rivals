using System;
using System.Collections.Generic;
using BiomeRivals.Core;

namespace BiomeRivals.Networking
{
    public sealed class MatchmakingPreferences
    {
        public const string FactionProperty = "factionId";

        public MatchmakingPreferences(string factionId)
        {
            if (!FactionIds.IsSupported(factionId))
                throw new ArgumentOutOfRangeException(nameof(factionId), factionId, "A supported faction id is required for matchmaking.");
            FactionId = factionId;
        }

        public string FactionId { get; }

        public Dictionary<string, string> ToStringProperties() =>
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [FactionProperty] = FactionId
            };
    }
}
