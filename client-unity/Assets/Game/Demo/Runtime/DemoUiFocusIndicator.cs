using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BiomeRivals.Demo
{
    // Pixel brackets identify focus by shape, with a dark backing on light biome frames.
    // This is a non-interactive UI graphic, never a battlefield overlay or gameplay command.
    public sealed class DemoUiFocusIndicator : MaskableGraphic
    {
        private Selectable _owner;
        private float _lastScale = -1f;
        public bool HasVisibleFocus { get; private set; }

        public static DemoUiFocusIndicator Attach(Selectable owner)
        {
            var marker = owner.GetComponentsInChildren<DemoUiFocusIndicator>(true)
                .FirstOrDefault(item => item.transform.parent == owner.transform && item.gameObject.activeSelf);
            if (marker == null)
            {
                var child = new GameObject("FocusIndicator", typeof(RectTransform));
                child.transform.SetParent(owner.transform, false);
                marker = child.AddComponent<DemoUiFocusIndicator>();
            }
            marker._owner = owner;
            marker.raycastTarget = false;
            marker.color = new Color32(244, 235, 209, 255);
            marker.rectTransform.anchorMin = Vector2.zero;
            marker.rectTransform.anchorMax = Vector2.one;
            marker.rectTransform.sizeDelta = Vector2.zero;
            marker.rectTransform.anchoredPosition = Vector2.zero;
            marker.transform.SetAsLastSibling();
            marker.RefreshVisual();
            return marker;
        }

        public void RefreshVisual()
        {
            var visible = _owner != null && _owner.IsActive() && _owner.IsInteractable() &&
                EventSystem.current != null && EventSystem.current.currentSelectedGameObject == _owner.gameObject;
            var scale = canvas != null ? Mathf.Max(0.01f, canvas.rootCanvas.scaleFactor) : 1f;
            if (visible == HasVisibleFocus && Mathf.Abs(scale - _lastScale) < 0.0001f) return;
            HasVisibleFocus = visible;
            _lastScale = scale;
            SetVerticesDirty();
        }

        private void LateUpdate() => RefreshVisual();

        protected override void OnPopulateMesh(VertexHelper helper)
        {
            helper.Clear();
            if (!HasVisibleFocus) return;
            var rect = rectTransform.rect;
            var scale = Mathf.Max(0.01f, _lastScale);
            var length = Mathf.Min(8f / scale, Mathf.Min(rect.width, rect.height) * 0.28f);
            var thickness = Mathf.Min(2f / scale, length * 0.4f);
            var inset = 3f / scale;
            var backing = 1f / scale;
            var bars = new Rect[8];
            var index = 0;
            foreach (var right in new[] { false, true })
            foreach (var top in new[] { false, true })
            {
                var x = right ? rect.xMax - inset - length : rect.xMin + inset;
                var y = top ? rect.yMax - inset - length : rect.yMin + inset;
                bars[index++] = new Rect(x, top ? y + length - thickness : y, length, thickness);
                bars[index++] = new Rect(right ? x + length - thickness : x, y, thickness, length);
            }
            foreach (var bar in bars) AddQuad(helper, new Rect(bar.x - backing, bar.y - backing,
                bar.width + backing * 2f, bar.height + backing * 2f), new Color32(14, 16, 14, 255));
            foreach (var bar in bars) AddQuad(helper, bar, color);
        }

        private static void AddQuad(VertexHelper helper, Rect rect, Color32 tint)
        {
            var start = helper.currentVertCount;
            var vertex = UIVertex.simpleVert;
            vertex.color = tint;
            vertex.position = new Vector2(rect.xMin, rect.yMin); helper.AddVert(vertex);
            vertex.position = new Vector2(rect.xMin, rect.yMax); helper.AddVert(vertex);
            vertex.position = new Vector2(rect.xMax, rect.yMax); helper.AddVert(vertex);
            vertex.position = new Vector2(rect.xMax, rect.yMin); helper.AddVert(vertex);
            helper.AddTriangle(start, start + 1, start + 2);
            helper.AddTriangle(start, start + 2, start + 3);
        }
    }
}
