using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BiomeRivals.Demo
{
    public sealed class DemoSlotPointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        private Action<bool> _onHoverChanged;

        public void Configure(Action<bool> onHoverChanged) => _onHoverChanged = onHoverChanged;

        public void OnPointerEnter(PointerEventData eventData) => _onHoverChanged?.Invoke(true);

        public void OnPointerExit(PointerEventData eventData) => _onHoverChanged?.Invoke(false);

        private void OnDisable() => _onHoverChanged?.Invoke(false);
    }
}
