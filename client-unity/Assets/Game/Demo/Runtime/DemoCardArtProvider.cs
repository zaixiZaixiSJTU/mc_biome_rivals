using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace BiomeRivals.Demo
{
    public static class DemoCardArtProvider
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        public static bool LocalArtEnabled { get; set; } = true;

        public static Sprite Load(string cardId)
        {
            if (Cache.TryGetValue(cardId, out var cached)) return cached;
            if (!LocalArtEnabled) return null;

#if UNITY_EDITOR || DEVELOPMENT_BUILD
            var relative = Path.Combine("Generated", "MinecraftCardIcons", cardId + ".png");
            var candidates = new[]
            {
                Path.Combine(Application.dataPath, relative),
                Path.GetFullPath(Path.Combine(Application.dataPath, "..", "..", "..", "Assets", relative)),
                Path.GetFullPath(Path.Combine(Directory.GetCurrentDirectory(), "client-unity", "Assets", relative))
            };
            foreach (var path in candidates)
            {
                if (!File.Exists(path)) continue;
                var texture = new Texture2D(2, 2, TextureFormat.RGBA32, false)
                {
                    name = "DemoArt_" + cardId,
                    filterMode = FilterMode.Point,
                    wrapMode = TextureWrapMode.Clamp
                };
                if (texture.LoadImage(File.ReadAllBytes(path), false))
                {
                    var sprite = Sprite.Create(
                        texture,
                        new Rect(0, 0, texture.width, texture.height),
                        new Vector2(0.5f, 0.5f),
                        16f);
                    sprite.name = "DemoArt_" + cardId;
                    Cache.Add(cardId, sprite);
                    return sprite;
                }
                Object.Destroy(texture);
            }
#endif
            Cache.Add(cardId, null);
            return null;
        }
    }
}
