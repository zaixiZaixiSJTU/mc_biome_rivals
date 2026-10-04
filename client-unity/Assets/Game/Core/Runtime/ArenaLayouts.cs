using System;
using System.Collections.Generic;

namespace BiomeRivals.Core
{
    public sealed class ArenaLayoutDefinition
    {
        public ArenaLayoutDefinition(string id, int unitSlotCount, int buildingSlotCount)
        {
            Id = id;
            UnitSlotCount = unitSlotCount;
            BuildingSlotCount = buildingSlotCount;
        }

        public string Id { get; }
        public int UnitSlotCount { get; }
        public int BuildingSlotCount { get; }
    }

    public static class ArenaLayouts
    {
        public const string DefaultArenaId = "standard_meadow";
        private static readonly Dictionary<string, ArenaLayoutDefinition> Layouts =
            new Dictionary<string, ArenaLayoutDefinition>(StringComparer.Ordinal)
            {
                [DefaultArenaId] = new ArenaLayoutDefinition(DefaultArenaId, 4, 3),
                ["plains_sunrise"] = new ArenaLayoutDefinition("plains_sunrise", 4, 3),
                ["deep_caverns"] = new ArenaLayoutDefinition("deep_caverns", 5, 2),
                ["nether_lava_sea"] = new ArenaLayoutDefinition("nether_lava_sea", 3, 4),
                ["end_void"] = new ArenaLayoutDefinition("end_void", 3, 4),
                ["deep_ocean"] = new ArenaLayoutDefinition("deep_ocean", 4, 3),
                ["desert_storm"] = new ArenaLayoutDefinition("desert_storm", 4, 3)
            };

        public static bool TryGet(string arenaId, out ArenaLayoutDefinition layout)
        {
            layout = null;
            if (string.IsNullOrWhiteSpace(arenaId)) return false;
            return Layouts.TryGetValue(arenaId, out layout);
        }

        public static ArenaLayoutDefinition Default => Layouts[DefaultArenaId];
        public static IReadOnlyCollection<string> Ids => Layouts.Keys;
    }
}
