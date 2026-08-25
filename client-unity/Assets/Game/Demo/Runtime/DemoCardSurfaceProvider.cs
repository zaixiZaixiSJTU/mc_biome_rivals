using UnityEngine;

namespace BiomeRivals.Demo
{
    internal static class DemoCardSurfaceProvider
    {
        private static Sprite _artSurface;

        public static Sprite LoadArtSurface()
        {
            if (_artSurface != null) return _artSurface;
            var texture = DemoWorldAssetProvider.LoadBlockTexture("polished_blackstone_bricks") ?? CreateFallbackTexture();
            texture.filterMode = FilterMode.Point;
            texture.wrapMode = TextureWrapMode.Repeat;
            _artSurface = Sprite.Create(
                texture,
                new Rect(0f, 0f, texture.width, texture.height),
                new Vector2(0.5f, 0.5f),
                DemoUiMetrics.PixelsPerUnit,
                0,
                SpriteMeshType.FullRect);
            _artSurface.name = "CardArtSurface_polished_blackstone_bricks";
            return _artSurface;
        }

        private static Texture2D CreateFallbackTexture()
        {
            var texture = new Texture2D(16, 16, TextureFormat.RGBA32, false)
            {
                name = "RuntimeCardSurface_polished_blackstone_bricks",
                filterMode = FilterMode.Point,
                wrapMode = TextureWrapMode.Repeat,
                hideFlags = HideFlags.DontSave
            };
            for (var y = 0; y < 16; y++)
            {
                for (var x = 0; x < 16; x++)
                {
                    var seam = y % 5 == 0 || (x + (y / 5 % 2) * 4) % 8 == 0;
                    var noise = ((x * 13 + y * 29 + x * y) & 7) / 7f;
                    texture.SetPixel(x, y, seam
                        ? new Color32(17, 18, 20, 255)
                        : Color.Lerp(new Color32(37, 38, 42, 255), new Color32(67, 64, 70, 255), noise * 0.46f));
                }
            }
            texture.Apply(false, false);
            return texture;
        }
    }
}
