using UnityEngine;
using UnityEngine.Rendering;

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

        public static Material CreateEntityMaterial(string name, Color fallback, string textureKey, Shader preferredShader = null)
        {
            var material = CreateBlockMaterial(name, fallback, textureKey, Color.black, preferredShader);
            material.enableInstancing = false;
            if (LoadBlockTexture(textureKey) != null)
            {
                SetColor(material, "_BaseColor", Color.white);
                SetColor(material, "_Color", Color.white);
            }
            if (material.HasProperty("_Cutoff")) material.SetFloat("_Cutoff", 0.1f);
            material.EnableKeyword("_ALPHATEST_ON");
            material.renderQueue = 2450;
            return material;
        }

        public static Material CreateSurfaceHighlightMaterial(string name, Shader preferredShader = null)
        {
            var material = CreateBlockMaterial(name, new Color(0f, 0f, 0f, 0f), string.Empty, Color.black, preferredShader);
            if (material.HasProperty("_Mode")) material.SetFloat("_Mode", 3f);
            if (material.HasProperty("_SrcBlend")) material.SetInt("_SrcBlend", (int)BlendMode.SrcAlpha);
            if (material.HasProperty("_DstBlend")) material.SetInt("_DstBlend", (int)BlendMode.OneMinusSrcAlpha);
            if (material.HasProperty("_ZWrite")) material.SetInt("_ZWrite", 0);
            material.DisableKeyword("_ALPHATEST_ON");
            material.EnableKeyword("_ALPHABLEND_ON");
            material.DisableKeyword("_ALPHAPREMULTIPLY_ON");
            if (material.HasProperty("_Metallic")) material.SetFloat("_Metallic", 0f);
            if (material.HasProperty("_Glossiness")) material.SetFloat("_Glossiness", 0.08f);
            material.renderQueue = (int)RenderQueue.Transparent;
            return material;
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
