using UnityEngine;

namespace BiomeRivals.Demo
{
    public static class DemoWorldAssetProvider
    {
        private const string ResourceRoot = "DemoWorld/";

        public static bool LocalAssetsEnabled { get; set; } = true;

        public static Texture2D LoadBlockTexture(string textureKey)
        {
            if (!LocalAssetsEnabled || string.IsNullOrWhiteSpace(textureKey)) return null;
            var texture = Resources.Load<Texture2D>(ResourceRoot + textureKey);
            if (texture != null) texture.filterMode = FilterMode.Point;
            return texture;
        }

        public static GameObject LoadCardPrefab(string cardId)
        {
            if (!LocalAssetsEnabled || string.IsNullOrWhiteSpace(cardId)) return null;
            return Resources.Load<GameObject>(ResourceRoot + "Prefabs/" + cardId);
        }

        public static Material CreateBlockMaterial(string name, Color fallback, string textureKey, Color emission, Shader preferredShader = null)
        {
            var shader = preferredShader ??
                         Shader.Find("Universal Render Pipeline/Lit") ??
                         Shader.Find("Standard") ??
                         Shader.Find("Unlit/Texture") ??
                         Shader.Find("Unlit/Color");
            if (shader == null) throw new MissingReferenceException("No tracked demo block shader is available.");
            var material = new Material(shader) { name = name, enableInstancing = true };
            var texture = LoadBlockTexture(textureKey);

            SetColor(material, "_BaseColor", fallback);
            SetColor(material, "_Color", fallback);
            if (texture != null)
            {
                SetTexture(material, "_BaseMap", texture);
                SetTexture(material, "_MainTex", texture);
            }

            if (emission.maxColorComponent > 0.001f)
            {
                material.EnableKeyword("_EMISSION");
                SetColor(material, "_EmissionColor", emission);
            }
            return material;
        }

        /// <summary>
        /// Vanilla-style entity cutout material: unlit shading with baked
        /// per-face vertex colors plus an optional emissive lift used by fire
        /// creatures (blaze, magma cube).
        /// </summary>
        public static Material CreateEntityMaterial(string name, Color fallback, string textureKey, Shader preferredShader = null, float emissiveBoost = 0f)
        {
            var shader = preferredShader ??
                         Shader.Find("BiomeRivals/Demo/Entity") ??
                         Shader.Find("Standard") ??
                         Shader.Find("Universal Render Pipeline/Lit");
            if (shader == null) throw new MissingReferenceException("No tracked demo entity shader is available.");
            var layered = DemoMinecraftModelFactory.TryGetSurfaceBaseTextureKey(textureKey, out var baseKey);
            Texture2D baseTexture = null;
            Texture2D surfaceTexture = null;
            if (layered)
            {
                baseTexture = LoadBlockTexture(baseKey);
                surfaceTexture = LoadBlockTexture(textureKey);
                if (baseTexture == null || surfaceTexture == null)
                    throw new MissingReferenceException($"Entity surface '{textureKey}' requires both '{baseKey}' skin and biome texture.");
                if (baseTexture.width != surfaceTexture.width || baseTexture.height != surfaceTexture.height)
                    throw new System.FormatException("Registered entity surface layers must share an atlas size.");
            }
            var material = CreateBlockMaterial(name, fallback, textureKey, Color.black, shader);
            if (layered)
            {
                if (!material.HasProperty("_SurfaceOverlayTex") || !material.HasProperty("_UseSurfaceOverlay"))
                {
                    if (Application.isPlaying) Object.Destroy(material); else Object.DestroyImmediate(material);
                    throw new MissingReferenceException("Entity shader does not support registered surface layers.");
                }
                SetTexture(material, "_MainTex", baseTexture);
                SetTexture(material, "_BaseMap", baseTexture);
                SetTexture(material, "_SurfaceOverlayTex", surfaceTexture);
                material.SetFloat("_UseSurfaceOverlay", 1f);
            }
            material.enableInstancing = false;
            var alphaColorMask = DemoMinecraftModelFactory.UsesAlphaColorMask(textureKey);
            if (alphaColorMask && !material.HasProperty("_UseAlphaColorMask"))
            {
                if (Application.isPlaying) Object.Destroy(material); else Object.DestroyImmediate(material);
                throw new MissingReferenceException("Registered color-mask skin requires the entity shader.");
            }
            if (material.HasProperty("_UseAlphaColorMask")) material.SetFloat("_UseAlphaColorMask", alphaColorMask ? 1f : 0f);
            var lowAlphaEmission = DemoMinecraftModelFactory.UsesLowAlphaEmission(textureKey);
            if (lowAlphaEmission && !material.HasProperty("_UseLowAlphaEmission"))
            {
                if (Application.isPlaying) Object.Destroy(material); else Object.DestroyImmediate(material);
                throw new MissingReferenceException("Registered low-alpha emission requires the entity shader.");
            }
            // Keep zero-alpha atlas padding transparent, but retain nonzero 8-bit eye pixels.
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", lowAlphaEmission ? 0.5f / 255f : 0.1f);
            if (material.HasProperty("_UseLowAlphaEmission")) material.SetFloat("_UseLowAlphaEmission", lowAlphaEmission ? 1f : 0f);
            if (material.HasProperty("_EmissiveBoost")) material.SetFloat("_EmissiveBoost", emissiveBoost);
            material.SetOverrideTag("RenderType", "TransparentCutout");
            material.renderQueue = 2450;
            return material;
        }

        public static Material CreateGroundSurfaceMaterial(string name, Texture texture, bool useScreenProjection, Shader preferredShader = null)
        {
            var shader = preferredShader ?? Shader.Find("BiomeRivals/Demo/GroundSurface") ?? Shader.Find("Unlit/Texture");
            if (shader == null) throw new MissingReferenceException("No tracked interactive ground shader is available.");
            var material = new Material(shader) { name = name, enableInstancing = true };
            material.mainTexture = texture ?? Texture2D.whiteTexture;
            SetColor(material, "_Color", Color.white);
            SetColor(material, "_HighlightColor", Color.black);
            if (material.HasProperty("_HighlightStrength")) material.SetFloat("_HighlightStrength", 0f);
            if (material.HasProperty("_UseScreenProjection")) material.SetFloat("_UseScreenProjection", useScreenProjection ? 1f : 0f);
            return material;
        }

        public static void SetMaterialColor(Material material, Color value)
        {
            SetColor(material, "_Color", value);
            SetColor(material, "_BaseColor", value);
        }

        private static void SetColor(Material material, string property, Color value)
        {
            if (material.HasProperty(property)) material.SetColor(property, value);
        }

        private static void SetTexture(Material material, string property, Texture value)
        {
            if (material.HasProperty(property)) material.SetTexture(property, value);
        }
    }
}
