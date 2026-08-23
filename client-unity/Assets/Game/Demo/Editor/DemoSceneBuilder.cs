using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace BiomeRivals.Demo.Editor
{
    public static class DemoSceneBuilder
    {
        public const string ScenePath = "Assets/Game/Demo/Scenes/Demo.unity";
        public const string BackgroundPath = "Assets/Game/Demo/Art/demo-battlefield-bg-v1.png";

        [MenuItem("Biome Rivals/Build and Open Demo Scene")]
        public static void BuildAndOpen()
        {
            BuildScene();
            EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single);
        }

        public static void BuildFromCommandLine()
        {
            BuildScene();
            Debug.Log("Biome Rivals demo scene generated successfully.");
        }

        private static void BuildScene()
        {
            Directory.CreateDirectory("Assets/Game/Demo/Scenes");
            ConfigureBackgroundImporter();

            var background = AssetDatabase.LoadAssetAtPath<Sprite>(BackgroundPath);
            if (background == null) throw new FileNotFoundException("Demo background sprite could not be imported.", BackgroundPath);

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Demo";
            var root = new GameObject("[Demo] Biome Rivals Local Match");
            var controller = root.AddComponent<DemoSceneController>();
            controller.Configure(background);

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Failed to save demo scene: " + ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

        private static void ConfigureBackgroundImporter()
        {
            AssetDatabase.ImportAsset(BackgroundPath, ImportAssetOptions.ForceSynchronousImport);
            var importer = AssetImporter.GetAtPath(BackgroundPath) as TextureImporter;
            if (importer == null) throw new FileNotFoundException("Demo background texture importer was not found.", BackgroundPath);
            var changed = importer.textureType != TextureImporterType.Sprite || importer.mipmapEnabled ||
                          importer.filterMode != FilterMode.Bilinear || importer.wrapMode != TextureWrapMode.Clamp ||
                          importer.maxTextureSize != 2048;
            if (!changed) return;
            importer.textureType = TextureImporterType.Sprite;
            importer.spriteImportMode = SpriteImportMode.Single;
            importer.mipmapEnabled = false;
            importer.filterMode = FilterMode.Bilinear;
            importer.wrapMode = TextureWrapMode.Clamp;
            importer.maxTextureSize = 2048;
            importer.textureCompression = TextureImporterCompression.CompressedHQ;
            importer.SaveAndReimport();
        }
    }
}
