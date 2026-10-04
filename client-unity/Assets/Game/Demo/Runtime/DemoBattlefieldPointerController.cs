using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

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
        private bool _inputEnabled = true;
        private readonly List<RaycastResult> _uiRaycastResults = new List<RaycastResult>(8);

        public bool InputEnabled => _inputEnabled;

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
            ProcessPointerFrame(Input.mousePosition, blockedByUi,
                Input.GetMouseButtonDown(0), Input.GetMouseButtonUp(0));
        }

        public DemoBattlefieldSlotTarget ProcessPointerFrame(
            Vector2 screenPosition,
            bool blockedByUi,
            bool primaryButtonDown,
            bool primaryButtonUp)
        {
            if (_battlefield == null) return null;
            if (!_inputEnabled)
            {
                SetHovered(null);
                SetPressed(null);
                return null;
            }
            var target = !blockedByUi && _battlefield.TryRaycastSlot(screenPosition, out var hit)
                ? hit
                : null;
            SetHovered(target);

            if (primaryButtonDown) SetPressed(target);
            if (!primaryButtonUp) return target;
            var clicked = _pressed != null && _pressed == target ? _pressed : null;
            SetPressed(null);
            if (clicked != null) _onSlotClicked?.Invoke(clicked.Player, clicked.Kind, clicked.Index);
            return target;
        }

        public void SetInputEnabled(bool enabled)
        {
            if (_inputEnabled == enabled) return;
            _inputEnabled = enabled;
            if (enabled) return;
            SetHovered(null);
            SetPressed(null);
        }

        public DemoBattlefieldSlotTarget ProcessPointerFrame(
            Vector2 screenPosition,
            bool primaryButtonDown,
            bool primaryButtonUp)
        {
            return ProcessPointerFrame(screenPosition, IsPointerOverUiAtPosition(screenPosition),
                primaryButtonDown, primaryButtonUp);
        }

        public bool IsPointerOverUiAtPosition(Vector2 screenPosition)
        {
            var eventSystem = EventSystem.current;
            if (eventSystem == null) return false;

            var pointer = new PointerEventData(eventSystem) { position = screenPosition };
            _uiRaycastResults.Clear();
            eventSystem.RaycastAll(pointer, _uiRaycastResults);
            foreach (var result in _uiRaycastResults)
                if (result.module is GraphicRaycaster) return true;
            return false;
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
