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

            var scene = EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            scene.name = "Demo";
            var root = new GameObject("[Demo] Biome Rivals Local Match");
            var battlefield = root.AddComponent<DemoBattlefield3D>();
            var blockShader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (blockShader == null) throw new MissingReferenceException("A tracked block shader is required by the 2.5D demo.");
            var backdropShader = Shader.Find("Unlit/Texture");
            if (backdropShader == null) throw new MissingReferenceException("The unlit backdrop shader is required by the 2.5D demo.");
            var backdrop = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            if (backdrop == null) throw new FileNotFoundException("The illustrated battlefield backdrop is missing.", BackgroundPath);
            battlefield.Configure(blockShader, backdropShader, backdrop);
            root.AddComponent<DemoSceneController>();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Failed to save demo scene: " + ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }
}
