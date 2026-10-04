using System;
using System.Collections.Generic;
using System.IO;
using System.Text.RegularExpressions;
using BiomeRivals.Content;
using UnityEngine;

namespace BiomeRivals.Demo
{
    public static class DemoCardArtProvider
    {
        private static readonly Dictionary<string, Sprite> Cache = new Dictionary<string, Sprite>();
        private static HashSet<string> _registeredIds;
        private static readonly HashSet<string> Missing = new HashSet<string>();
        public static bool LocalArtEnabled { get; set; } = true;

        public static Sprite Load(string cardId)
        {
            if (!LocalArtEnabled) return null;
            if (!IsSafeCardId(cardId) || !RegisteredIds.Contains(cardId)) return null;
            if (Cache.TryGetValue(cardId, out var cached) && cached != null) return cached;

            foreach (var path in CandidatePaths(cardId))
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
                        DemoUiMetrics.PixelsPerUnit);
                    sprite.name = "DemoArt_" + cardId;
                    Cache[cardId] = sprite;
                    return sprite;
                }
                UnityEngine.Object.Destroy(texture);
            }
            if (Missing.Add(cardId)) Debug.LogWarning($"Registered Minecraft card art is missing or invalid: {cardId}. Rebuild/verify the Player's MinecraftCardIcons package.");
            return null;
        }

        public static bool IsSafeCardId(string cardId) => cardId != null && Regex.IsMatch(cardId, @"\A[a-z]{2}_[0-9]{3}\z");

        public static string GetPackagedIconPath(string dataPath, string cardId)
        {
            if (!IsSafeCardId(cardId)) throw new ArgumentException("Card art ID is not a safe registered-ID filename.", nameof(cardId));
            if (string.IsNullOrWhiteSpace(dataPath) || !Path.IsPathRooted(dataPath))
                throw new ArgumentException("Player data directory must be absolute.", nameof(dataPath));
            var parent = Directory.GetParent(Path.GetFullPath(dataPath)) ?? throw new ArgumentException("Player data directory has no parent.", nameof(dataPath));
            return Path.Combine(parent.FullName, "MinecraftCardIcons", cardId + ".png");
        }

        private static IEnumerable<string> CandidatePaths(string cardId)
        {
#if UNITY_EDITOR
            yield return Path.Combine(Application.dataPath, "Generated", "MinecraftCardIcons", cardId + ".png");
#endif
            yield return GetPackagedIconPath(Application.dataPath, cardId);
        }

        private static HashSet<string> RegisteredIds
        {
            get
            {
                if (_registeredIds != null) return _registeredIds;
                var names = Resources.Load<TextAsset>("CardContent/card-name-registry.zh-CN.v1")
                    ?? throw new InvalidOperationException("Card art requires the registered card name resource.");
                var document = JsonUtility.FromJson<CardNameRegistryDocument>(names.text);
                if (document?.schemaVersion != 1 || document.entries == null)
                    throw new FormatException("Card art name registry is incomplete or unsupported.");
                var ids = new HashSet<string>(StringComparer.Ordinal);
                foreach (var entry in document.entries)
                    if (entry == null || !IsSafeCardId(entry.id) || !ids.Add(entry.id))
                        throw new FormatException("Card art registry contains an unsafe or duplicate ID.");
                _registeredIds = ids;
                return ids;
            }
        }

        [RuntimeInitializeOnLoadMethod(RuntimeInitializeLoadType.SubsystemRegistration)]
        private static void ResetForPlaySession() { LocalArtEnabled = true; ClearCache(); }

        public static void ClearCache()
        {
            foreach (var sprite in Cache.Values)
            {
                if (sprite == null) continue;
                var texture = sprite.texture;
                if (Application.isPlaying) { UnityEngine.Object.Destroy(sprite); UnityEngine.Object.Destroy(texture); }
                else { UnityEngine.Object.DestroyImmediate(sprite); UnityEngine.Object.DestroyImmediate(texture); }
            }
            Cache.Clear(); Missing.Clear(); _registeredIds = null;
        }
    }
}
