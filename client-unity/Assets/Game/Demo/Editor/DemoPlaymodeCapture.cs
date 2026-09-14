using UnityEditor;
using UnityEditor.SceneManagement;

namespace BiomeRivals.Demo.Editor
{
    /// <summary>
    /// Command-line entry that opens the demo scene and enters play mode so the
    /// runtime `-captureDemo <path>` / `-preview*` flags can render a screenshot
    /// headlessly, e.g.:
    /// Unity.exe -projectPath client-unity -executeMethod
    /// BiomeRivals.Demo.Editor.DemoPlaymodeCapture.RunFromCommandLine
    /// -captureDemo demo-preview.png -logFile -
    /// </summary>
    public static class DemoPlaymodeCapture
    {
        public static void RunFromCommandLine()
        {
            EditorSceneManager.OpenScene(DemoSceneBuilder.ScenePath, OpenSceneMode.Single);
            EditorApplication.isPlaying = true;
        }
    }
}
