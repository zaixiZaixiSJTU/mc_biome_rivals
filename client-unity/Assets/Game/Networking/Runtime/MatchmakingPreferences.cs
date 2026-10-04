using System;
using System.Collections.Generic;
using BiomeRivals.Core;

namespace BiomeRivals.Networking
{
    public sealed class MatchmakingPreferences
    {
        public const string FactionProperty = "factionId";

        public MatchmakingPreferences(
            string factionId,
            int cardContentVersion,
            int implementedEffectRegistryVersion)
        {
            if (!FactionIds.IsSupported(factionId))
                throw new ArgumentOutOfRangeException(nameof(factionId), factionId, "A supported faction id is required for matchmaking.");
            if (cardContentVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(cardContentVersion), cardContentVersion, "A positive card content version is required for matchmaking.");
            if (implementedEffectRegistryVersion <= 0)
                throw new ArgumentOutOfRangeException(nameof(implementedEffectRegistryVersion), implementedEffectRegistryVersion, "A positive implemented effect registry version is required for matchmaking.");
            FactionId = factionId;
            CardContentVersion = cardContentVersion;
            ImplementedEffectRegistryVersion = implementedEffectRegistryVersion;
        }

        public string FactionId { get; }
        public int CardContentVersion { get; }
        public int ImplementedEffectRegistryVersion { get; }

        public string GetIncompatibilityMessage(
            int protocolVersion,
            string rulesetVersion,
            int cardContentVersion,
            int implementedEffectRegistryVersion)
        {
            if (protocolVersion != GameVersions.Protocol)
                return $"protocol version mismatch (client {GameVersions.Protocol}, server {protocolVersion})";
            if (!string.Equals(rulesetVersion, GameVersions.Ruleset, StringComparison.Ordinal))
                return $"ruleset version mismatch (client {GameVersions.Ruleset}, server {rulesetVersion ?? "<missing>"})";
            if (cardContentVersion != CardContentVersion)
                return $"card data version mismatch (client {CardContentVersion}, server {cardContentVersion})";
            if (implementedEffectRegistryVersion != ImplementedEffectRegistryVersion)
                return $"implemented effect registry version mismatch (client {ImplementedEffectRegistryVersion}, server {implementedEffectRegistryVersion})";
            return string.Empty;
        }

        public Dictionary<string, string> ToStringProperties() =>
            new Dictionary<string, string>(StringComparer.Ordinal)
            {
                [FactionProperty] = FactionId
            };
    }
}
