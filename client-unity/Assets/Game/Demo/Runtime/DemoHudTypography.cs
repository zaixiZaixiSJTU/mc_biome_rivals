using UnityEngine;
using UnityEngine.UI;

namespace BiomeRivals.Demo
{
    // HUD only. CardUI owns card typography and summary/full-text rules separately.
    [DisallowMultipleComponent]
    [RequireComponent(typeof(Text))]
    public sealed class DemoHudTypography : MonoBehaviour
    {
        public const float MinimumScreenFontSize = 12f;
        private Text _text;
        private Canvas _canvas;
        private int _authoredFontSize;
        private float _lastScale = -1f;

        public int AuthoredFontSize => _authoredFontSize;

        public void Configure(int authoredFontSize)
        {
            _text = GetComponent<Text>();
            _canvas = GetComponentInParent<Canvas>();
            _authoredFontSize = Mathf.Max(1, authoredFontSize);
            _lastScale = -1f;
            ApplyScale(_canvas != null ? _canvas.scaleFactor : 1f);
        }

        // Exposed for deterministic typography tests; never issues gameplay commands.
        public void ApplyScale(float canvasScale)
        {
            if (_text == null || _authoredFontSize == 0) return;
            var safeScale = Mathf.Max(0.01f, canvasScale);
            if (Mathf.Abs(_lastScale - safeScale) < 0.0001f) return;
            _lastScale = safeScale;
            _text.resizeTextForBestFit = false;
            _text.fontSize = DemoUiMetrics.GetScreenReadableFontSize(
                _authoredFontSize, MinimumScreenFontSize, safeScale);
        }

        private void LateUpdate()
        {
            if (_canvas == null) _canvas = GetComponentInParent<Canvas>();
            ApplyScale(_canvas != null ? _canvas.scaleFactor : 1f);
        }
    }
}
