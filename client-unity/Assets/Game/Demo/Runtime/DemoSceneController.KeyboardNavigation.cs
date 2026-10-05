using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BiomeRivals.Demo
{
    public sealed partial class DemoSceneController
    {
        private static readonly KeyCode[] ReadingKeys = { KeyCode.LeftArrow, KeyCode.RightArrow,
            KeyCode.UpArrow, KeyCode.DownArrow, KeyCode.PageUp, KeyCode.PageDown, KeyCode.Home, KeyCode.End };

        private bool HandleReadingKey(KeyCode key, bool reverseTab = false)
        {
            if (!IsReadOnlyOverlayOpen) return false;
            if (key == KeyCode.Escape)
            {
                if (_statusInspectionOpen) CloseStatusInspection(); else CloseHandInspection();
                return true;
            }
            if (key == KeyCode.Tab)
            {
                var candidates = _statusInspectionOpen ? new Selectable[] { _statusInspectionClose } :
                    new Selectable[] { _handInspectionOverlay.Find("ReadingPanel/Close").GetComponent<Button>(),
                        _handInspectionPrevious, _handInspectionNext };
                DemoUiNavigation.FocusNext(EventSystem.current, candidates, reverseTab);
                return true;
            }
            if (key == KeyCode.LeftArrow || key == KeyCode.RightArrow)
            {
                if (IsHandInspectionOpen) MoveHandInspection(key == KeyCode.LeftArrow ? -1 : 1);
                return true;
            }
            var scroll = _statusInspectionOpen ? _statusInspectionScroll : _handInspectionScroll;
            switch (key)
            {
                case KeyCode.UpArrow: DemoUiNavigation.ScrollVertical(scroll, -48f); return true;
                case KeyCode.DownArrow: DemoUiNavigation.ScrollVertical(scroll, 48f); return true;
                case KeyCode.PageUp: DemoUiNavigation.ScrollVertical(scroll, -scroll.viewport.rect.height * 0.85f); return true;
                case KeyCode.PageDown: DemoUiNavigation.ScrollVertical(scroll, scroll.viewport.rect.height * 0.85f); return true;
                case KeyCode.Home: DemoUiNavigation.ScrollToEdge(scroll, false); return true;
                case KeyCode.End: DemoUiNavigation.ScrollToEdge(scroll, true); return true;
                default: return false;
            }
        }
    }
}
