using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;

namespace BiomeRivals.Demo
{
    internal sealed class DemoHudMaterialFactory : IDisposable
    {
        private readonly Color _rivetHighlight;
        private readonly Dictionary<string, Texture2D> _textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
        private readonly Dictionary<string, Sprite> _sprites = new Dictionary<string, Sprite>(StringComparer.Ordinal);
        private readonly HashSet<Texture2D> _generatedTextures = new HashSet<Texture2D>();

        public DemoHudMaterialFactory(Color rivetHighlight)
        {
            _rivetHighlight = rivetHighlight;
        }

        public void Decorate(RectTransform root, Vector2 size, DemoUiStyleClass styleClass)
        {
            const float bevelOffset = 2f;
            var frameTexture = GetTexture(DemoUiStyleCatalog.GetFrameTextureKey(styleClass));
            var bodyTexture = GetTexture("polished_blackstone_bricks");
            var contentInset = DemoUiMetrics.FrameBorderPixels;
            var bodySize = new Vector2(Mathf.Max(0, size.x - contentInset * 2f), Mathf.Max(0, size.y - contentInset * 2f));
            var frameTint = DemoUiStyleCatalog.GetFrameTint(styleClass);
            var bodyTint = DemoUiStyleCatalog.GetBodyTint(styleClass);

            CreateSlicedPanel(root, "FrameSlice", Vector2.zero, size, GetSprite(frameTexture, true), frameTint);
            CreateTiledPanel(root, "MaterialFill", Vector2.zero, bodySize, GetSprite(bodyTexture, false), bodyTint);

            var bevelWidth = Mathf.Max(0, size.x - contentInset * 2f);
            CreateSolidPanel(root, "InnerBevelTop", new Vector2(0, size.y * 0.5f - contentInset - bevelOffset), new Vector2(bevelWidth, 1f), new Color(frameTint.r, frameTint.g, frameTint.b, 0.30f));
            CreateSolidPanel(root, "InnerBevelBottom", new Vector2(0, -size.y * 0.5f + contentInset + bevelOffset), new Vector2(bevelWidth, 2f), new Color(0, 0, 0, 0.38f));

            CreateRivet(root, "NW", new Vector2(-size.x * 0.5f + 3f, size.y * 0.5f - 3f), frameTint);
            CreateRivet(root, "NE", new Vector2(size.x * 0.5f - 3f, size.y * 0.5f - 3f), frameTint);
            CreateRivet(root, "SW", new Vector2(-size.x * 0.5f + 3f, -size.y * 0.5f + 3f), frameTint);
            CreateRivet(root, "SE", new Vector2(size.x * 0.5f - 3f, -size.y * 0.5f + 3f), frameTint);
        }

        public void Dispose()
        {
            foreach (var sprite in _sprites.Values)
            {
                if (sprite == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(sprite);
                else UnityEngine.Object.DestroyImmediate(sprite);
            }
            _sprites.Clear();
            foreach (var texture in _generatedTextures)
            {
                if (texture == null) continue;
                if (Application.isPlaying) UnityEngine.Object.Destroy(texture);
                else UnityEngine.Object.DestroyImmediate(texture);
            }
            _generatedTextures.Clear();
            _textures.Clear();
        }

        private void CreateRivet(Transform parent, string suffix, Vector2 position, Color edge)
        {
            CreateSolidPanel(parent, "Rivet" + suffix, position, new Vector2(3f, 3f), Color.Lerp(edge, _rivetHighlight, 0.52f));
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

        private Sprite GetSprite(Texture2D texture, bool sliced)
        {
            var key = texture.name + (sliced ? ":sliced" : ":tile");
            Sprite cached;
            if (_sprites.TryGetValue(key, out cached) && cached != null) return cached;
            var border = sliced
                ? Vector4.one * DemoUiMetrics.FrameBorderPixels
                : Vector4.zero;
            var sprite = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                DemoUiMetrics.PixelsPerUnit,
                0,
                SpriteMeshType.FullRect,
                border);
            sprite.name = "RuntimeHudSprite_" + key;
            _sprites[key] = sprite;
            return sprite;
        }

        private static Image CreateSlicedPanel(Transform parent, string name, Vector2 position, Vector2 size, Sprite sprite, Color tint)
        {
            var image = CreateSpritePanel(parent, name, position, size, sprite, tint);
            image.type = Image.Type.Sliced;
            image.fillCenter = true;
            image.pixelsPerUnitMultiplier = 1f;
            return image;
        }

        private static Image CreateTiledPanel(Transform parent, string name, Vector2 position, Vector2 size, Sprite sprite, Color tint)
        {
            var image = CreateSpritePanel(parent, name, position, size, sprite, tint);
            image.type = Image.Type.Tiled;
            image.pixelsPerUnitMultiplier = 1f;
            return image;
        }

        private static Image CreateSpritePanel(Transform parent, string name, Vector2 position, Vector2 size, Sprite sprite, Color tint)
        {
            var root = CreateRect(parent, name, position, size);
            var image = root.gameObject.AddComponent<Image>();
            image.sprite = sprite;
            image.color = tint;
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

    }
}
