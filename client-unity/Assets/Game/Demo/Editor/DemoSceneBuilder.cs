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
            DemoUiPrefabBuilder.Rebuild();
            Directory.CreateDirectory("Assets/Game/Demo/Scenes");

            var sceneAlreadyExists = File.Exists(ScenePath);
            var scene = sceneAlreadyExists
                ? EditorSceneManager.OpenScene(ScenePath, OpenSceneMode.Single)
                : EditorSceneManager.NewScene(NewSceneSetup.EmptyScene, NewSceneMode.Single);
            if (!sceneAlreadyExists) scene.name = "Demo";
            GameObject root = null;
            foreach (var candidate in scene.GetRootGameObjects())
            {
                if (candidate.name == "[Demo] Biome Rivals Local Match")
                {
                    root = candidate;
                    break;
                }
            }
            if (root == null) root = new GameObject("[Demo] Biome Rivals Local Match");
            var battlefield = root.GetComponent<DemoBattlefield3D>();
            if (battlefield == null) battlefield = root.AddComponent<DemoBattlefield3D>();
            var blockShader = Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit");
            if (blockShader == null) throw new MissingReferenceException("A tracked block shader is required by the 2.5D demo.");
            var backdropShader = Shader.Find("BiomeRivals/Demo/CompositeBackdrop");
            if (backdropShader == null) throw new MissingReferenceException("The composite battlefield shader is required by the 2.5D demo.");
            var groundSurfaceShader = Shader.Find("BiomeRivals/Demo/GroundSurface");
            if (groundSurfaceShader == null) throw new MissingReferenceException("The interactive ground surface shader is required by the 2.5D demo.");
            var backdrop = AssetDatabase.LoadAssetAtPath<Texture2D>(BackgroundPath);
            if (backdrop == null) throw new FileNotFoundException("The illustrated battlefield backdrop is missing.", BackgroundPath);
            battlefield.Configure(blockShader, backdropShader, groundSurfaceShader, backdrop);
            if (root.GetComponent<DemoSceneController>() == null) root.AddComponent<DemoSceneController>();

            if (!EditorSceneManager.SaveScene(scene, ScenePath))
                throw new IOException("Failed to save demo scene: " + ScenePath);

            EditorBuildSettings.scenes = new[] { new EditorBuildSettingsScene(ScenePath, true) };
            AssetDatabase.SaveAssets();
            AssetDatabase.Refresh();
        }

    }
}
