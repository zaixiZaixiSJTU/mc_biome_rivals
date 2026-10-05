using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BiomeRivals.Demo
{
    public static class DemoUiNavigation
    {
        private static bool IsEligible(Selectable item) => item != null && item.IsActive() && item.IsInteractable();

        public static bool FocusNext(EventSystem system, IEnumerable<Selectable> candidates, bool reverse)
        {
            if (system == null) return false;
            var eligible = candidates.Where(IsEligible).Distinct().ToList();
            if (eligible.Count == 0) { system.SetSelectedGameObject(null); return false; }
            var index = eligible.FindIndex(item => item.gameObject == system.currentSelectedGameObject);
            var next = index < 0 ? reverse ? eligible.Count - 1 : 0 :
                (index + (reverse ? -1 : 1) + eligible.Count) % eligible.Count;
            system.SetSelectedGameObject(eligible[next].gameObject);
            return true;
        }

        public static void RestoreFocus(EventSystem system, GameObject previous, params Selectable[] fallback)
        {
            if (system == null) return;
            var original = previous != null ? previous.GetComponent<Selectable>() : null;
            var target = IsEligible(original) ? original : fallback.FirstOrDefault(IsEligible);
            system.SetSelectedGameObject(target != null ? target.gameObject : null);
        }

        // Read-only keys own direction/scroll routing; UGUI must not also move underlying focus.
        public static void DisableDirectionalNavigation(params Selectable[] items)
        {
            foreach (var item in items.Where(value => value != null))
            {
                var navigation = item.navigation;
                navigation.mode = Navigation.Mode.None;
                item.navigation = navigation;
            }
        }

        public static void ScrollVertical(ScrollRect scroll, float delta)
        {
            if (scroll == null || scroll.content == null || scroll.viewport == null) return;
            scroll.StopMovement();
            var overflow = scroll.content.rect.height - scroll.viewport.rect.height;
            scroll.verticalNormalizedPosition = overflow > 0.01f
                ? Mathf.Clamp01(scroll.verticalNormalizedPosition - delta / overflow) : 1f;
        }

        public static void ScrollToEdge(ScrollRect scroll, bool end)
        {
            if (scroll == null || scroll.content == null || scroll.viewport == null) return;
            scroll.StopMovement();
            scroll.verticalNormalizedPosition = end && scroll.content.rect.height > scroll.viewport.rect.height ? 0f : 1f;
        }
    }
}
