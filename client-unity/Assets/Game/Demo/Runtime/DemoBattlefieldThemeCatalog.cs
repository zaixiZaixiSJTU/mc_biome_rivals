using System;
using System.Collections.Generic;
using UnityEngine;

namespace BiomeRivals.Demo
{
    public readonly struct DemoBattlefieldTheme
    {
        public readonly string FactionId;
        /// <summary>Dominant voxel ground texture, also used for deploy slot pads.</summary>
        public readonly string PrimaryTextureKey;
        public readonly string SecondaryTextureKey;
        public readonly string TertiaryTextureKey;
        public readonly string FoundationTextureKey;
        public readonly Color GroundColor;
        public readonly Color FoundationColor;
        public readonly Color EnvironmentLight;
        public readonly Color UiTint;

        public DemoBattlefieldTheme(
            string factionId,
            string primaryTextureKey,
            string secondaryTextureKey,
            string tertiaryTextureKey,
            string foundationTextureKey,
            string groundColor,
            string foundationColor,
            string environmentLight,
            string uiTint)
        {
            FactionId = factionId;
            PrimaryTextureKey = primaryTextureKey;
            SecondaryTextureKey = secondaryTextureKey;
            TertiaryTextureKey = tertiaryTextureKey;
            FoundationTextureKey = foundationTextureKey;
            GroundColor = Parse(groundColor);
            FoundationColor = Parse(foundationColor);
            EnvironmentLight = Parse(environmentLight);
            UiTint = Parse(uiTint);
        }

        private static Color Parse(string value)
        {
            if (ColorUtility.TryParseHtmlString(value, out var color)) return color;
            throw new FormatException("Invalid battlefield theme color: " + value);
        }
    }

    /// <summary>
    /// Maps each faction to voxel terrain textures extracted from the local
    /// Minecraft installation. The battlefield is fully 3D, so themes swap
    /// ground blocks, slot pads and environment lights instead of painted
    /// half-module images.
    /// </summary>
    public static class DemoBattlefieldThemeCatalog
    {
        private static readonly Dictionary<string, DemoBattlefieldTheme> Themes =
            new Dictionary<string, DemoBattlefieldTheme>(StringComparer.Ordinal)
            {
                {
                    "plains_forest", new DemoBattlefieldTheme(
                        "plains_forest", "grass_block_top", "mossy_stone_bricks", "oak_planks", "dirt",
                        "#6A873E", "#6C4D32", "#8FC7B7", "#4F8D59")
                },
                {
                    "desert_badlands", new DemoBattlefieldTheme(
                        "desert_badlands", "red_sandstone", "sandstone", "cut_sandstone", "sandstone",
                        "#C17A43", "#D8BE78", "#F1B86A", "#B96E32")
                },
                {
                    "snow_ice", new DemoBattlefieldTheme(
                        "snow_ice", "snow_block", "packed_ice", "stone_bricks", "dirt",
                        "#DFEDF2", "#6C4D32", "#A7D8EF", "#619BB9")
                },
                {
                    "cave_dark_forest", new DemoBattlefieldTheme(
                        "cave_dark_forest", "mossy_stone_bricks", "stone_bricks", "cobblestone", "cobblestone",
                        "#61754B", "#77746E", "#52B9A5", "#315C58")
                },
                {
                    "ocean_river", new DemoBattlefieldTheme(
                        "ocean_river", "prismarine_bricks", "dark_prismarine", "sea_lantern", "dark_prismarine",
                        "#4A8F7C", "#315D59", "#52BBD2", "#287B91")
                },
                {
                    "nether", new DemoBattlefieldTheme(
                        "nether", "netherrack", "nether_bricks", "basalt_top", "netherrack",
                        "#6A2E2C", "#5A2428", "#FF6A2B", "#9D3529")
                },
                {
                    "end", new DemoBattlefieldTheme(
                        "end", "purpur_block", "obsidian", "deepslate_bricks", "obsidian",
                        "#A97BA9", "#17121E", "#C59BE8", "#6E4A8E")
                }
            };

        public static DemoBattlefieldTheme Get(string factionId)
        {
            if (factionId != null && Themes.TryGetValue(factionId, out var theme)) return theme;
            throw new ArgumentException("Unknown battlefield faction: " + factionId, nameof(factionId));
        }

        public static Color GetSkyColor(string playerFactionId, string opponentFactionId)
        {
            var playerLight = Get(playerFactionId).EnvironmentLight;
            var opponentLight = Get(opponentFactionId).EnvironmentLight;
            var combinedLight = Color.Lerp(playerLight, opponentLight, 0.5f);
            Color baseSky = new Color32(9, 11, 10, 255);
            return Color.Lerp(baseSky, combinedLight, 0.16f);
        }
    }
}
