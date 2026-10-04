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
            var bounds = text.rectTransform.rect.size;
            var settings = text.GetGenerationSettings(bounds);
            settings.horizontalOverflow = HorizontalWrapMode.Overflow;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            var generator = new TextGenerator();
            foreach (var line in text.text.Split('\n'))
                if (generator.GetPreferredWidth(line, settings) / text.pixelsPerUnit > bounds.x) return false;
            if (text.preferredHeight > bounds.y) return false;
            var actual = new TextGenerator();
            if (!actual.Populate(text.text, text.GetGenerationSettings(bounds))) return false;
            var expectedLines = text.text.Split('\n').Length;
            return actual.lineCount == expectedLines && text.fontSize == 15 && !text.resizeTextForBestFit;
        }
    }
}
