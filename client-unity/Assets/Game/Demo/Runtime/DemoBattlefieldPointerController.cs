using System;
using UnityEngine;
using UnityEngine.EventSystems;

namespace BiomeRivals.Demo
{
    public sealed class DemoBattlefieldPointerController : MonoBehaviour
    {
        private DemoBattlefield3D _battlefield;
        private Action<bool, DemoSlotKind, int> _onSlotClicked;
        private DemoBattlefieldSlotTarget _hovered;
        private DemoBattlefieldSlotTarget _pressed;

        public void Configure(DemoBattlefield3D battlefield, Action<bool, DemoSlotKind, int> onSlotClicked)
        {
            _battlefield = battlefield;
            _onSlotClicked = onSlotClicked;
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
            if (_hovered != null) _battlefield.SetSlotHovered(_hovered.Player, _hovered.Kind, _hovered.Index, false);
            _hovered = target;
            if (_hovered != null) _battlefield.SetSlotHovered(_hovered.Player, _hovered.Kind, _hovered.Index, true);
        }

        private void SetPressed(DemoBattlefieldSlotTarget target)
        {
            if (_pressed == target) return;
            if (_pressed != null) _battlefield.SetSlotPressed(_pressed.Player, _pressed.Kind, _pressed.Index, false);
            _pressed = target;
            if (_pressed != null) _battlefield.SetSlotPressed(_pressed.Player, _pressed.Kind, _pressed.Index, true);
        }

        private void OnDisable()
        {
            if (_battlefield == null) return;
            SetHovered(null);
            SetPressed(null);
        }
    }
}
