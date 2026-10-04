using System.Globalization;
using UnityEngine;
using UnityEngine.UI;

namespace BiomeRivals.Demo
{
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class DemoReadableSummary : MonoBehaviour
    {
        private Text _text;
        private Canvas _canvas;
        private string _fullText = string.Empty;
        private string _preview = string.Empty;
        private int _authoredSize;
        private float _lastScale = -1f;
        private Vector2 _lastBounds;
        private bool _dirty;
        public string FullText { get { SynchronizeExternalText(); return _fullText; } }
        public bool IsAbbreviated => _preview != _fullText;

        public void Configure(int authoredSize)
        {
            _text = GetComponent<Text>();
            _canvas = GetComponentInParent<Canvas>();
            _authoredSize = Mathf.Max(15, authoredSize);
            SetFullText(_text.text);
        }

        public void SetFullText(string value)
        {
            _fullText = value ?? string.Empty;
            _dirty = true;
            Refresh();
        }

        private void SynchronizeExternalText()
        {
            if (_text != null && _text.text != _preview)
            {
                _fullText = _text.text;
                _dirty = true;
            }
        }

        public void Refresh()
        {
            if (_text == null) return;
            var scale = _canvas != null ? Mathf.Max(0.01f, _canvas.scaleFactor) : 1f;
            var bounds = _text.rectTransform.rect.size;
            if (!_dirty && Mathf.Abs(scale - _lastScale) < 0.0001f && bounds == _lastBounds) return;
            _dirty = false;
            _lastScale = scale;
            _lastBounds = bounds;
            _text.resizeTextForBestFit = false;
            _text.supportRichText = false;
            _text.fontSize = DemoUiMetrics.GetScreenReadableFontSize(_authoredSize, 12f, scale);
            _preview = FitPreview(_fullText, _text);
            _text.text = _preview;
        }

        // Preserve grapheme boundaries: a summary must not split surrogate pairs or combining marks.
        public static string FitPreview(string value, Text text, int? fontSize = null)
        {
            value = value ?? string.Empty;
            if (text.font == null || text.rectTransform.rect.width <= 0f || text.rectTransform.rect.height <= 0f) return value;
            var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
            settings.resizeTextForBestFit = false;
            if (fontSize.HasValue) settings.fontSize = fontSize.Value;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            var generator = new TextGenerator();
            bool Fits(string candidate) => generator.GetPreferredHeight(candidate, settings) / text.pixelsPerUnit <= text.rectTransform.rect.height + 0.5f;
            if (Fits(value)) return value;
            var starts = StringInfo.ParseCombiningCharacters(value);
            var low = 0;
            var high = starts.Length;
            string Candidate(int elements) => value.Substring(0, elements >= starts.Length ? value.Length : starts[elements]).TrimEnd() + "…";
            while (low < high)
            {
                var middle = low + (high - low + 1) / 2;
                if (Fits(Candidate(middle))) low = middle;
                else high = middle - 1;
            }
            return Candidate(low);
        }

        private void LateUpdate() { SynchronizeExternalText(); Refresh(); }
    }
}
