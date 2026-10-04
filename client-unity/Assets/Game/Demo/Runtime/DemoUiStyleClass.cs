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
                case DemoUiStyleClass.PrimaryActionButton: return new Color32(52, 29, 24, 255);
                default: return new Color32(27, 30, 27, 240);
            }
        }

        public static Color GetFrameTint(DemoUiStyleClass styleClass)
        {
            switch (styleClass)
            {
                case DemoUiStyleClass.SecondaryButton: return new Color32(112, 113, 104, 248);
                case DemoUiStyleClass.PrimaryActionButton: return new Color32(180, 74, 44, 255);
                default: return new Color32(105, 107, 100, 248);
            }
        }

        public static Color GetBodyTint(DemoUiStyleClass styleClass)
        {
            switch (styleClass)
            {
                // Opaque interior: stable text contrast on every biome, without
                // compositing a second, stretched brick pattern underneath.
                case DemoUiStyleClass.SecondaryButton: return new Color32(72, 76, 71, 255);
                case DemoUiStyleClass.PrimaryActionButton: return new Color32(112, 45, 28, 255);
                default: return new Color32(52, 57, 53, 255);
            }
        }

        public static Color GetInteractionTint(DemoUiStyleClass styleClass) =>
            styleClass == DemoUiStyleClass.PrimaryActionButton
                ? new Color32(244, 168, 95, 255)
                : new Color32(207, 201, 176, 255);

        public static string GetFrameTextureKey(DemoUiStyleClass styleClass) => "stone_bricks";
    }
}
