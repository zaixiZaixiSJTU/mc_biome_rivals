using System;
using System.Collections;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;

namespace BiomeRivals.Demo
{
    public sealed partial class DemoSceneController
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private IEnumerator PrepareResponsiveHandInspectionCapture()
        {
            var finalSize = new Vector2Int(GetCaptureDimension("-captureWidth", 1920), GetCaptureDimension("-captureHeight", 1080));
            var sizes = new[] { new Vector2Int(1424, 714), new Vector2Int(1024, 768), new Vector2Int(1280, 720), new Vector2Int(1920, 1080), finalSize };
            var revision = MatchView.Revision;
            var instances = MatchView.HandCards.Select(card => card.handCardInstanceId).ToArray();
            foreach (var size in sizes)
            {
                Screen.SetResolution(size.x, size.y, FullScreenMode.Windowed);
                var deadline = Time.realtimeSinceStartup + 8f;
                while (Screen.width != size.x || Screen.height != size.y)
                {
                    if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException($"Responsive Player never reached {size.x}x{size.y}.");
                    yield return null;
                }
                yield return null;
                yield return null; // Let CanvasScaler, viewport Update and dynamic card type sizes settle.
                Canvas.ForceUpdateCanvases();
                AuditResponsiveCanvasBounds();
                if (IsHandInspectionOpen)
                {
                    if (!ClickButtonThroughEventSystem(_handInspectionOverlay.Find("ReadingPanel/Close").GetComponent<Button>()))
                        throw new InvalidOperationException("Resized reading modal close-button raycast failed.");
                    yield return null;
                }
                yield return PrepareHandInspectionCapture();
                AuditResponsiveCanvasBounds();
                if (MatchView.Revision != revision || !MatchView.HandCards.Select(card => card.handCardInstanceId).SequenceEqual(instances))
                    throw new InvalidOperationException("Window resize changed gameplay state or private hand identities.");
                Debug.Log($"Responsive hand inspection settled: True; screen={Screen.width}x{Screen.height}; scale={_canvasRoot.GetComponent<Canvas>().scaleFactor:F6}; actual close/open/pagination; full rules; unchanged state; gameplay locked.");
            }
        }

        private void AuditResponsiveCanvasBounds()
        {
            var canvas = _canvasRoot.GetComponent<Canvas>();
            var expectedScale = Mathf.Min(Screen.width / ReferenceWidth, Screen.height / ReferenceHeight);
            if (Screen.width >= 1280 && Screen.height >= 720)
            {
                foreach (var typography in _canvasRoot.GetComponentsInChildren<DemoHudTypography>())
                {
                    var text = typography.GetComponent<Text>();
                    if (text.fontSize * canvas.scaleFactor < DemoHudTypography.MinimumScreenFontSize - 0.01f ||
                        text.resizeTextForBestFit || text.preferredHeight > text.rectTransform.rect.height + 0.5f)
                        throw new InvalidOperationException($"HUD reading gate failed: {text.transform.parent.name}/{text.name}; font={text.fontSize}; scale={canvas.scaleFactor}; height={text.preferredHeight}/{text.rectTransform.rect.height}.");
                }
                Debug.Log($"HUD reading gate passed: screen={Screen.width}x{Screen.height}; selected labels >=12px; full text height; no auto-shrink.");
            }
            if (canvas.renderMode != RenderMode.ScreenSpaceOverlay ||
                _canvasRoot.GetComponent<CanvasScaler>().screenMatchMode != CanvasScaler.ScreenMatchMode.Expand ||
                Mathf.Abs(canvas.scaleFactor - expectedScale) > 0.001f)
                throw new InvalidOperationException("Responsive Canvas is not using full-content fit scaling.");
            var names = new[] { "TitlePlate", "FactionRail", "HandPlate", "CardDetailsPanel", "EndTurnButton", "InspectHand", "OnlineStatusPanel" };
            var corners = new Vector3[4];
            foreach (var name in names)
            {
                var rect = _canvasRoot.Find(name) as RectTransform;
                if (rect == null) throw new InvalidOperationException("Missing responsive UI target: " + name);
                rect.GetWorldCorners(corners);
                foreach (var corner in corners)
                {
                    var screen = RectTransformUtility.WorldToScreenPoint(null, corner);
                    if (screen.x < -1f || screen.y < -1f || screen.x > Screen.width + 1f || screen.y > Screen.height + 1f)
                        throw new InvalidOperationException($"UI escaped resized screen: {name}, point={screen}, size={Screen.width}x{Screen.height}.");
                }
            }
            var camera = _battlefield.BoardCamera;
            var expectedViewport = DemoBattlefield3D.CalculateAspectViewport(Screen.width / (float)Screen.height);
            if (Vector4.Distance(new Vector4(camera.rect.x, camera.rect.y, camera.rect.width, camera.rect.height),
                new Vector4(expectedViewport.x, expectedViewport.y, expectedViewport.width, expectedViewport.height)) > 0.001f)
                throw new InvalidOperationException("Resized battlefield viewport has not settled.");
            Physics.SyncTransforms();
            foreach (var player in new[] { true, false })
            foreach (var kind in new[] { DemoSlotKind.Unit, DemoSlotKind.Building })
            for (var index = 0; index < _battlefield.GetSlotCount(kind); index++)
            {
                var screen = camera.WorldToScreenPoint(_battlefield.GetSlotInteractionWorldPosition(player, kind, index));
                if (screen.z <= 0 || !_battlefield.TryRaycastSlot(screen, out var hit) || hit.Player != player || hit.Kind != kind || hit.Index != index)
                    throw new InvalidOperationException($"Resized world raycast mismatch: {player}/{kind}/{index}.");
            }
        }
#else
        private IEnumerator PrepareResponsiveHandInspectionCapture()
        {
            throw new NotSupportedException("Responsive capture diagnostics are unavailable in release builds.");
        }
#endif
    }
}
