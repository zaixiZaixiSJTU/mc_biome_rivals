using System;
using System.Collections.Generic;
using UnityEngine;

namespace BiomeRivals.Demo
{
    internal static class DemoCardFrameProvider
    {
        private const string ResourcePath = "DemoCardFrames/card-frame-theme-study-v1";

        private static readonly string[] ThemeOrder =
        {
            "plains_forest",
            "desert_badlands",
            "snow_ice",
            "cave_dark_forest",
            "ocean_river",
            "nether",
            "end"
        };

        private static readonly Rect[] FrameRects =
        {
            new Rect(24, 201, 218, 520),
            new Rect(257, 201, 220, 520),
            new Rect(488, 201, 220, 520),
            new Rect(721, 201, 219, 520),
            new Rect(950, 201, 221, 520),
            new Rect(1184, 201, 221, 520),
            new Rect(1416, 201, 222, 520)
        };

        private static readonly Rect[] CostSocketRects =
        {
            new Rect(24, 640, 75, 81),
            new Rect(257, 640, 76, 81),
            new Rect(488, 638, 78, 83),
            new Rect(721, 639, 77, 82),
            new Rect(950, 638, 79, 83),
            new Rect(1184, 638, 80, 83),
            new Rect(1416, 638, 79, 83)
        };

        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>(StringComparer.Ordinal);

        public static Sprite Load(string themeId)
        {
            return LoadSlice(themeId, FrameRects, "CardFrame_", string.Empty);
        }

        public static Sprite LoadCostSocket(string themeId)
        {
            return LoadSlice(themeId, CostSocketRects, "CardCostSocket_", "_cost");
        }

        private static Sprite LoadSlice(string themeId, IReadOnlyList<Rect> rects, string spritePrefix, string cacheSuffix)
        {
            if (string.IsNullOrWhiteSpace(themeId)) return null;
            Sprite cached;
            var cacheKey = themeId + cacheSuffix;
            if (Cache.TryGetValue(cacheKey, out cached) && cached != null) return cached;
            var themeIndex = Array.IndexOf(ThemeOrder, themeId);
            if (themeIndex < 0) return null;
            var texture = Resources.Load<Texture2D>(ResourcePath);
            if (texture == null || texture.width < 1638 || texture.height < 946) return null;
            texture.filterMode = FilterMode.Bilinear;
            texture.wrapMode = TextureWrapMode.Clamp;
            var sprite = Sprite.Create(texture, rects[themeIndex], new Vector2(0.5f, 0.5f), DemoUiMetrics.PixelsPerUnit, 0, SpriteMeshType.FullRect);
            sprite.name = spritePrefix + themeId;
            Cache[cacheKey] = sprite;
            return sprite;
        }
    }
}
