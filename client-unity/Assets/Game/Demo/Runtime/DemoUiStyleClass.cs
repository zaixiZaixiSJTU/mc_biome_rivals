using UnityEngine;

namespace BiomeRivals.Demo
{
    public enum DemoUiStyleClass
    {
        BasePanel,
        SecondaryButton,
        PrimaryActionButton
    }

    public abstract class DemoUiStyleComponent : MonoBehaviour
    {
        public abstract DemoUiStyleClass StyleClass { get; }
    }

    public static class DemoUiStyleCatalog
    {
        public static Color GetRootFill(DemoUiStyleClass styleClass)
        {
            switch (styleClass)
            {
                case DemoUiStyleClass.SecondaryButton: return new Color32(36, 39, 35, 250);
                case DemoUiStyleClass.PrimaryActionButton: return new Color32(31, 82, 71, 255);
                default: return new Color32(27, 30, 27, 240);
            }
        }

        public static Color GetFrameTint(DemoUiStyleClass styleClass)
        {
            switch (styleClass)
            {
                case DemoUiStyleClass.SecondaryButton: return new Color32(112, 113, 104, 248);
                case DemoUiStyleClass.PrimaryActionButton: return new Color32(91, 174, 159, 255);
                default: return new Color32(105, 107, 100, 248);
            }
        }

        public static Color GetBodyTint(DemoUiStyleClass styleClass)
        {
            switch (styleClass)
            {
                case DemoUiStyleClass.SecondaryButton: return new Color32(126, 128, 118, 104);
                case DemoUiStyleClass.PrimaryActionButton: return new Color32(82, 147, 134, 142);
                default: return new Color32(111, 113, 105, 92);
            }
        }

        public static string GetFrameTextureKey(DemoUiStyleClass styleClass) =>
            styleClass == DemoUiStyleClass.PrimaryActionButton ? "prismarine_bricks" : "stone_bricks";
    }
}
