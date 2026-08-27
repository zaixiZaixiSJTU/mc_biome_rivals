using System;

namespace BiomeRivals.Core
{
    public static class FactionIds
    {
        public const string PlainsForest = "plains_forest";
        public const string DesertBadlands = "desert_badlands";
        public const string SnowIce = "snow_ice";
        public const string CaveDarkForest = "cave_dark_forest";
        public const string OceanRiver = "ocean_river";
        public const string Nether = "nether";
        public const string End = "end";

        public static readonly string[] All =
        {
            PlainsForest, DesertBadlands, SnowIce, CaveDarkForest, OceanRiver, Nether, End
        };

        public static bool IsSupported(string value)
        {
            foreach (var factionId in All)
                if (string.Equals(factionId, value, StringComparison.Ordinal)) return true;
            return false;
        }

        public static string CardPrefix(string factionId)
        {
            switch (factionId)
            {
                case PlainsForest: return "pf";
                case DesertBadlands: return "db";
                case SnowIce: return "si";
                case CaveDarkForest: return "cd";
                case OceanRiver: return "or";
                case Nether: return "nt";
                case End: return "ed";
                default: throw new ArgumentOutOfRangeException(nameof(factionId), factionId, "Unsupported faction id.");
            }
        }
    }
}
