using UnityEngine;

namespace BiomeRivals.Demo
{
    public static class DemoUiMetrics
    {
        public const float PixelsPerUnit = 16f;
        public const float FrameBorderPixels = 5f;
        public const float PanelContentInset = 7f;

        public static int GetScreenReadableFontSize(int authoredFontSize, float minimumScreenPixels, float canvasScaleFactor)
        {
            var safeScaleFactor = Mathf.Max(0.01f, canvasScaleFactor);
            var scaledMinimumFontSize = Mathf.CeilToInt(Mathf.Max(1f, minimumScreenPixels) / safeScaleFactor);
            return Mathf.Max(authoredFontSize, scaledMinimumFontSize);
        }
    }
}
