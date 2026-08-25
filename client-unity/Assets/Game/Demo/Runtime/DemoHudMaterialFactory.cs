using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BiomeRivals.Demo
{
    internal sealed class DemoHudMaterialFactory : IDisposable
    {
        private readonly Color _stoneEdge;
        private readonly Color _oakEdge;
        private readonly Color _emberEdge;
        private readonly Color _prismarineEdge;
        private readonly Color _rivetHighlight;
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly HashSet<Texture2D> _generatedTextures = new HashSet<Texture2D>();

        public DemoHudMaterialFactory(Color stoneEdge, Color oakEdge, Color emberEdge, Color prismarineEdge, Color rivetHighlight)
        {
            _stoneEdge = stoneEdge;
            _oakEdge = oakEdge;
            _emberEdge = emberEdge;
            _prismarineEdge = prismarineEdge;
            _rivetHighlight = rivetHighlight;
        }

        public void Decorate(RectTransform root, Vector2 size, Color fill, Color edge)
        {
            const float railThickness = 10f;
            const float cornerSize = 15f;
            const float inset = 4f;
            var frameTexture = ResolveFrameTexture(edge);
            var bodyTexture = GetTexture("polished_blackstone_bricks");
            var bodySize = new Vector2(Mathf.Max(0, size.x - railThickness * 2f), Mathf.Max(0, size.y - railThickness * 2f));
            var frameTint = Color.Lerp(Color.white, edge, 0.24f);
            frameTint.a = 0.96f;
            var bodyTint = Color.Lerp(Color.white, fill, 0.42f);
            bodyTint.a = 0.34f;

            CreateRawTexturePanel(root, "MaterialFill", Vector2.zero, bodySize, bodyTexture, bodyTint, 48f);
            CreateRawTexturePanel(root, "FrameTop", new Vector2(0, size.y * 0.5f - railThickness * 0.5f), new Vector2(Mathf.Max(0, size.x - cornerSize * 1.35f), railThickness), frameTexture, frameTint, 34f);
            CreateRawTexturePanel(root, "FrameBottom", new Vector2(0, -size.y * 0.5f + railThickness * 0.5f), new Vector2(Mathf.Max(0, size.x - cornerSize * 1.35f), railThickness), frameTexture, Color.Lerp(frameTint, Color.black, 0.22f), 34f);
            CreateRawTexturePanel(root, "FrameLeft", new Vector2(-size.x * 0.5f + railThickness * 0.5f, 0), new Vector2(railThickness, Mathf.Max(0, size.y - cornerSize * 1.35f)), frameTexture, frameTint, 34f);
            CreateRawTexturePanel(root, "FrameRight", new Vector2(size.x * 0.5f - railThickness * 0.5f, 0), new Vector2(railThickness, Mathf.Max(0, size.y - cornerSize * 1.35f)), frameTexture, Color.Lerp(frameTint, Color.black, 0.12f), 34f);

            CreateCorner(root, "NW", new Vector2(-size.x * 0.5f + cornerSize * 0.5f, size.y * 0.5f - cornerSize * 0.5f), cornerSize, frameTexture, frameTint, edge);
            CreateCorner(root, "NE", new Vector2(size.x * 0.5f - cornerSize * 0.5f, size.y * 0.5f - cornerSize * 0.5f), cornerSize, frameTexture, frameTint, edge);
            CreateCorner(root, "SW", new Vector2(-size.x * 0.5f + cornerSize * 0.5f, -size.y * 0.5f + cornerSize * 0.5f), cornerSize, frameTexture, Color.Lerp(frameTint, Color.black, 0.18f), edge);
            CreateCorner(root, "SE", new Vector2(size.x * 0.5f - cornerSize * 0.5f, -size.y * 0.5f + cornerSize * 0.5f), cornerSize, frameTexture, Color.Lerp(frameTint, Color.black, 0.22f), edge);

            CreateSolidPanel(root, "InnerBevelTop", new Vector2(0, size.y * 0.5f - railThickness - inset * 0.5f), new Vector2(Mathf.Max(0, size.x - railThickness * 2f), 2f), new Color(edge.r, edge.g, edge.b, 0.30f));
            CreateSolidPanel(root, "InnerBevelBottom", new Vector2(0, -size.y * 0.5f + railThickness + inset * 0.5f), new Vector2(Mathf.Max(0, size.x - railThickness * 2f), 3f), new Color(0, 0, 0, 0.38f));
        }

        public void Dispose()
        {
            foreach (var texture in _generatedTextures)
            {
                if (texture == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                else UnityEngine.Object.DestroyImmediate(texture);
            }
            _generatedTextures.Clear();
            _textures.Clear();
        }

        private void CreateCorner(Transform parent, string suffix, Vector2 position, float size, Texture texture, Color tint, Color edge)
        {
            CreateRawTexturePanel(parent, "FrameCorner" + suffix, position, new Vector2(size, size), texture, tint, 30f);
            var rivetPosition = position + new Vector2(suffix.Contains("W") ? -1f : 1f, suffix.Contains("N") ? 1f : -1f);
            CreateSolidPanel(parent, "Rivet" + suffix, rivetPosition, new Vector2(4f, 4f), Color.Lerp(edge, _rivetHighlight, 0.52f));
        }

        private Texture2D ResolveFrameTexture(Color edge)
        {
            var key = "stone_bricks";
            var nearest = ColorDistance(edge, _stoneEdge);
            SelectNearest(edge, _oakEdge, "oak_planks", ref nearest, ref key);
            SelectNearest(edge, _emberEdge, "nether_bricks", ref nearest, ref key);
            SelectNearest(edge, _prismarineEdge, "prismarine_bricks", ref nearest, ref key);
            return GetTexture(key);
        }

        private static void SelectNearest(Color value, Color candidate, string candidateKey, ref float nearest, ref string key)
        {
            var distance = ColorDistance(value, candidate);
            if (distance >= nearest) return;
            nearest = distance;
            key = candidateKey;
        }

        private Texture2D GetTexture(string key)
        {
            Texture2D cached;
            if (_textures.TryGetValue(key, out cached) && cached != null) return cached;
            var texture = DemoWorldAssetProvider.LoadBlockTexture(key);
            if (texture == null)
            {
                texture = CreateFallbackTexture(key);
                _generatedTextures.Add(texture);
            }
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            _textures[key] = texture;
            return texture;
        }

        private static RawImage CreateRawTexturePanel(Transform parent, string name, Vector2 position, Vector2 size, Texture texture, Color tint, float tileSize)
        {
            var root = CreateRect(parent, name, position, size);
            var image = root.gameObject.AddComponent<RawImage>();
            image.texture = texture;
            image.color = tint;
            image.uvRect = new Rect(0f, 0f, Mathf.Max(0.1f, size.x / tileSize), Mathf.Max(0.1f, size.y / tileSize));
            image.raycastTarget = false;
            return image;
        }

        private static Image CreateSolidPanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var root = CreateRect(parent, name, position, size);
            var image = root.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private static RectTransform CreateRect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var gameObject = new GameObject(name, typeof(RectTransform));
            gameObject.transform.SetParent(parent, false);
            var rect = gameObject.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private static Texture2D CreateFallbackTexture(string key)
        {
            var palette = GetFallbackPalette(key);
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
            {
                name = "RuntimeHudMaterial_" + key,
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.DontSave
            };
            for (var y = 0; y < 16; y++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var plank = key == "oak_planks";
                    var seam = plank
                        ? y % 5 == 0 || (x == ((y / 5) % 2) * 8 && y % 5 != 0)
                        : y % 5 == 0 || (x + (y / 5 % 2) * 4) % 8 == 0;
                    var noise = ((x * 17 + y * 31 + x * y * 3) & 7) / 7f;
                    texture.SetPixel(x, y, seam ? palette[2] : Color.Lerp(palette[0], palette[1], noise * 0.52f));
                }
            }
            texture.Apply(false, false);
            return texture;
        }

        private static Color[] GetFallbackPalette(string key)
        {
            switch (key)
            {
                case "oak_planks": return new[] { ParseColor("#6F4B2B"), ParseColor("#A97843"), ParseColor("#3D291B") };
                case "nether_bricks": return new[] { ParseColor("#331D21"), ParseColor("#5A2B2D"), ParseColor("#170F12") };
                case "prismarine_bricks": return new[] { ParseColor("#315E58"), ParseColor("#5B8A77"), ParseColor("#193A39") };
                case "stone_bricks": return new[] { ParseColor("#55564F"), ParseColor("#77786B"), ParseColor("#292C29") };
                default: return new[] { ParseColor("#27272A"), ParseColor("#454148"), ParseColor("#111214") };
            }
        }

        private static Color ParseColor(string value)
        {
            Color color;
            return ColorUtility.TryParseHtmlString(value, out color) ? color : Color.magenta;
        }

        private static float ColorDistance(Color left, Color right)
        {
            var red = left.r - right.r;
            var green = left.g - right.g;
            var blue = left.b - right.b;
            return red * red + green * green + blue * blue;
        }
    }
}
