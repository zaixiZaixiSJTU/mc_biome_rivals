using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BiomeRivals.Demo
{
    public sealed class DemoSlotPointerRelay : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler, IPointerDownHandler, IPointerUpHandler
    {
        private Action<bool> _onHoverChanged;
        private Action<bool> _onPressedChanged;

        public void Configure(Action<bool> onHoverChanged, Action<bool> onPressedChanged)
        {
            _onHoverChanged = onHoverChanged;
            _onPressedChanged = onPressedChanged;
        }

        public void OnPointerEnter(PointerEventData eventData) => _onHoverChanged?.Invoke(true);

        public void OnPointerExit(PointerEventData eventData)
        {
            _onHoverChanged?.Invoke(false);
            _onPressedChanged?.Invoke(false);
        }

        public void OnPointerDown(PointerEventData eventData) => _onPressedChanged?.Invoke(true);

        public void OnPointerUp(PointerEventData eventData) => _onPressedChanged?.Invoke(false);

        private void OnDisable()
        {
            _onHoverChanged?.Invoke(false);
            _onPressedChanged?.Invoke(false);
        }
    }
}
