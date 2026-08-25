using System.IO;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BiomeRivals.Demo.Editor
{
    public static class DemoUiPrefabBuilder
    {
        public const string PrefabFolder = "Assets/Game/Demo/UI/Resources/DemoUI/Prefabs";

        [MenuItem("Biome Rivals/Rebuild Demo UI Prefabs")]
        public static void Rebuild()
        {
            Directory.CreateDirectory(PrefabFolder);
            SavePrefab<BasePanel>(DemoUiStyleClass.BasePanel, false);
            SavePrefab<SecondaryButton>(DemoUiStyleClass.SecondaryButton, true);
            SavePrefab<PrimaryActionButton>(DemoUiStyleClass.PrimaryActionButton, true);
            SaveCardUiPrefab();
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void SaveCardUiPrefab()
        {
            var root = new GameObject("CardUI", typeof(RectTransform), typeof(Image), typeof(Shadow), typeof(CardUI));
            try
            {
                var image = root.GetComponent<Image>();
                image.color = Color.white;
                var shadow = root.GetComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.52f);
                shadow.effectDistance = new Vector2(4f, -4f);
                var path = $"{PrefabFolder}/CardUI.prefab";
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new IOException("Failed to save card UI prefab: " + path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void SavePrefab<TStyle>(DemoUiStyleClass styleClass, bool isButton)
            where TStyle : DemoUiStyleComponent
        {
            var root = new GameObject(styleClass.ToString(), typeof(RectTransform), typeof(Image), typeof(Shadow), typeof(TStyle));
            try
            {
                var image = root.GetComponent<Image>();
                image.color = DemoUiStyleCatalog.GetRootFill(styleClass);
                var shadow = root.GetComponent<Shadow>();
                shadow.effectColor = new Color(0f, 0f, 0f, 0.46f);
                shadow.effectDistance = new Vector2(5f, -5f);
                if (isButton)
                {
                    var button = root.AddComponent<Button>();
                    button.targetGraphic = image;
                }

                var path = $"{PrefabFolder}/{styleClass}.prefab";
                if (PrefabUtility.SaveAsPrefabAsset(root, path) == null)
                    throw new IOException("Failed to save demo UI prefab: " + path);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
