using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BiomeRivals.Demo
{
    public sealed class DemoBattlefieldPointerController : MonoBehaviour
    {
        private DemoBattlefield3D _battlefield;
        private Action<bool, DemoSlotKind, int> _onSlotClicked;
        private Action<bool, DemoSlotKind, int, bool> _onSlotHovered;
        private Action<bool, DemoSlotKind, int, bool> _onSlotPressed;
        private DemoBattlefieldSlotTarget _hovered;
        private DemoBattlefieldSlotTarget _pressed;

        public void Configure(
            DemoBattlefield3D battlefield,
            Action<bool, DemoSlotKind, int> onSlotClicked,
            Action<bool, DemoSlotKind, int, bool> onSlotHovered = null,
            Action<bool, DemoSlotKind, int, bool> onSlotPressed = null)
        {
            _battlefield = battlefield;
            _onSlotClicked = onSlotClicked;
            _onSlotHovered = onSlotHovered;
            _onSlotPressed = onSlotPressed;
        }

        private void Update()
        {
            if (_battlefield == null) return;
            var blockedByUi = EventSystem.current != null && EventSystem.current.IsPointerOverGameObject();
            var target = !blockedByUi && _battlefield.TryRaycastSlot(Input.mousePosition, out var hit)
                ? hit
                : null;
            SetHovered(target);

            if (Input.GetMouseButtonDown(0)) SetPressed(target);
            if (!Input.GetMouseButtonUp(0)) return;
            var clicked = _pressed != null && _pressed == target ? _pressed : null;
            SetPressed(null);
            if (clicked != null) _onSlotClicked?.Invoke(clicked.Player, clicked.Kind, clicked.Index);
        }

        private void SetHovered(DemoBattlefieldSlotTarget target)
        {
            if (_hovered == target) return;
            if (_hovered != null) SetHoveredState(_hovered, false);
            _hovered = target;
            if (_hovered != null) SetHoveredState(_hovered, true);
        }

        private void SetPressed(DemoBattlefieldSlotTarget target)
        {
            if (_pressed == target) return;
            if (_pressed != null) SetPressedState(_pressed, false);
            _pressed = target;
            if (_pressed != null) SetPressedState(_pressed, true);
        }

        private void SetHoveredState(DemoBattlefieldSlotTarget target, bool hovered)
        {
            if (_onSlotHovered != null) _onSlotHovered(target.Player, target.Kind, target.Index, hovered);
            else _battlefield.SetSlotHovered(target.Player, target.Kind, target.Index, hovered);
        }

        private void SetPressedState(DemoBattlefieldSlotTarget target, bool pressed)
        {
            if (_onSlotPressed != null) _onSlotPressed(target.Player, target.Kind, target.Index, pressed);
            else _battlefield.SetSlotPressed(target.Player, target.Kind, target.Index, pressed);
        }

        private void OnDisable()
        {
            if (_battlefield == null) return;
            SetHovered(null);
            SetPressed(null);
        }
    }
}
