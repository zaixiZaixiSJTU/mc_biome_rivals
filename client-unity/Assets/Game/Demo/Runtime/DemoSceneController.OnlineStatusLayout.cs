using UnityEngine;

namespace BiomeRivals.Demo
{
    public sealed partial class DemoSceneController
    {
        private bool VerifyOnlineStatusTextLayout()
        {
            if (_onlineStatusText == null) return false;
            Canvas.ForceUpdateCanvases();
            var text = _onlineStatusText;
            text.GetComponent<DemoHudTypography>()?.ApplyScale(_canvasRoot.GetComponent<Canvas>().scaleFactor);
            var bounds = text.rectTransform.rect.size;
            var settings = text.GetGenerationSettings(bounds);
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            var generator = new TextGenerator();
            foreach (var line in text.text.Split('\n'))
                if (generator.GetPreferredWidth(line, settings) / text.pixelsPerUnit > bounds.x)
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    Debug.Log($"Online status layout rejected width: size={bounds}; font={text.fontSize}; scale={_canvasRoot.GetComponent<Canvas>().scaleFactor}; ppu={text.pixelsPerUnit}; width={generator.GetPreferredWidth(line, settings) / text.pixelsPerUnit}");
#endif
                    return false;
                }
            if (text.preferredHeight > bounds.y) return false;
            var actual = new TextGenerator();
            if (!actual.Populate(text.text, text.GetGenerationSettings(bounds))) return false;
            var expectedLines = text.text.Split('\n').Length;
            return actual.lineCount == expectedLines && text.fontSize ==
                DemoUiMetrics.GetScreenReadableFontSize(15, DemoHudTypography.MinimumScreenFontSize,
                    _canvasRoot.GetComponent<Canvas>().scaleFactor) && !text.resizeTextForBestFit;
        }
    }
}
