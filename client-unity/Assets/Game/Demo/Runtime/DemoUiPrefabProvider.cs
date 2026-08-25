using UnityEngine;

namespace BiomeRivals.Demo
{
    internal static class DemoUiPrefabProvider
    {
        private const string ResourceFolder = "DemoUI/Prefabs/";

        public static GameObject Load(DemoUiStyleClass styleClass) =>
            Resources.Load<GameObject>(ResourceFolder + styleClass);
    }
}
