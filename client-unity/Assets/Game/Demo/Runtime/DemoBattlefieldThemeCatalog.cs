using System;
using System.Collections.Generic;
using UnityEngine;

namespace BiomeRivals.Demo
{
    public readonly struct DemoBattlefieldTheme
    {
        public readonly string FactionId;
        public readonly string NearResourcePath;
        public readonly string FarResourcePath;
        public readonly Color EnvironmentLight;
        public readonly Color UiTint;

        public DemoBattlefieldTheme(string factionId, string nearResourcePath, string farResourcePath, string environmentLight, string uiTint)
        {
            FactionId = factionId;
            NearResourcePath = nearResourcePath;
            FarResourcePath = farResourcePath;
            EnvironmentLight = Parse(environmentLight);
            UiTint = Parse(uiTint);
        }

        private static Color Parse(string value)
        {
            if (ColorUtility.TryParseHtmlString(value, out var color)) return color;
            throw new FormatException("Invalid battlefield theme color: " + value);
        }
    }

    public static class DemoBattlefieldThemeCatalog
    {
        private const string ResourceRoot = "DemoBattlefields/";

        private static readonly Dictionary<string, DemoBattlefieldTheme> Themes =
            new Dictionary<string, DemoBattlefieldTheme>(StringComparer.Ordinal)
            {
                { "plains_forest", Create("plains_forest", "#8FC7B7", "#4F8D59") },
                { "desert_badlands", Create("desert_badlands", "#F1B86A", "#B96E32") },
                { "snow_ice", Create("snow_ice", "#A7D8EF", "#619BB9") },
                { "cave_dark_forest", Create("cave_dark_forest", "#52B9A5", "#315C58") },
                { "ocean_river", Create("ocean_river", "#52BBD2", "#287B91") },
                { "nether", Create("nether", "#FF6A2B", "#9D3529") },
                { "end", Create("end", "#C59BE8", "#6E4A8E") }
            };

        public static DemoBattlefieldTheme Get(string factionId)
        {
            if (factionId != null && Themes.TryGetValue(factionId, out var theme)) return theme;
            throw new ArgumentException("Unknown battlefield faction: " + factionId, nameof(factionId));
        }

        public static Texture2D LoadNearTexture(string factionId) => LoadTexture(Get(factionId).NearResourcePath);

        public static Texture2D LoadFarTexture(string factionId) => LoadTexture(Get(factionId).FarResourcePath);

        private static Texture2D LoadTexture(string resourcePath)
        {
            var texture = Resources.Load<Texture2D>(resourcePath);
            if (texture == null)
                throw new MissingReferenceException($"Battlefield half-module is missing: Resources/{resourcePath}.png");
            return texture;
        }

        private static DemoBattlefieldTheme Create(string factionId, string environmentLight, string uiTint) =>
            new DemoBattlefieldTheme(
                factionId,
                ResourceRoot + "field-" + factionId + "-v1",
                ResourceRoot + "field-" + factionId + "-far-v1",
                environmentLight,
                uiTint);
    }
}
