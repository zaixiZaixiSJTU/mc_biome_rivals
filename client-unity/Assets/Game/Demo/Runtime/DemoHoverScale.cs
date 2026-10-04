using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BiomeRivals.Demo
{
    public sealed class DemoHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler,
        IPointerDownHandler, IPointerUpHandler
    {
        public float TargetScale => _target.x;
        public bool PreviewHoverPinned => _previewHoverPinned;

        [SerializeField] private float hoverScale = 1.07f;
        [SerializeField] private float response = 14f;
        [SerializeField] private float hoverLift;
        [SerializeField] private float pressScale = 0.97f;
        [SerializeField] private float pressDepth = 1.5f;
        private Selectable _selectable;
        private bool _raiseToFrontOnHover;
        private bool _previewHoverPinned;
        private bool _raisedOnHover;
        private bool _dragging;
        private bool _pointerHovered;
        private bool _pointerHeld;
        private int _siblingIndexBeforeHover;
        private Vector3 _target = Vector3.one;
        private Vector3 _restingLocalPosition;
        private Vector3 _targetLocalPosition;

        private void Awake()
        {
            _selectable = GetComponent<Selectable>();
            _restingLocalPosition = transform.localPosition;
            _targetLocalPosition = _restingLocalPosition;
        }

        public void Configure(float scale, float speed, bool raiseToFrontOnHover = false, float lift = 0f)
        {
            hoverScale = scale;
            response = speed;
            _raiseToFrontOnHover = raiseToFrontOnHover;
            hoverLift = Mathf.Max(0f, lift);
            _restingLocalPosition = transform.localPosition;
            _targetLocalPosition = _restingLocalPosition;
        }

        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_dragging || !CanInteract()) return;
            _pointerHovered = true;
            RefreshTarget();
            if (!_raiseToFrontOnHover || transform.parent == null) return;

            _siblingIndexBeforeHover = transform.GetSiblingIndex();
            transform.SetAsLastSibling();
            _raisedOnHover = true;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            if (_dragging || _previewHoverPinned) return;
            _pointerHovered = false;
            RefreshTarget();
            if (!_raisedOnHover || transform.parent == null) return;

            var lastValidIndex = transform.parent.childCount - 1;
            transform.SetSiblingIndex(Mathf.Clamp(_siblingIndexBeforeHover, 0, lastValidIndex));
            _raisedOnHover = false;
        }

        public void OnPointerDown(PointerEventData eventData)
        {
            if (_dragging || !CanInteract()) return;
            _pointerHeld = true;
            RefreshTarget();
        }

        public void OnPointerUp(PointerEventData eventData)
        {
            if (_dragging) return;
            _pointerHeld = false;
            RefreshTarget();
        }

        public void PinHoverForPreview()
        {
            _previewHoverPinned = true;
            OnPointerEnter(null);
            AdvanceMotion(1f);
        }

        public void BeginDrag()
        {
            _dragging = true;
            _previewHoverPinned = false;
            _pointerHovered = false;
            _pointerHeld = false;
            _target = Vector3.one;
            _targetLocalPosition = _restingLocalPosition;
            transform.localPosition = _restingLocalPosition;
            transform.localScale = Vector3.one;
            if (_raisedOnHover && transform.parent != null)
                transform.SetSiblingIndex(Mathf.Clamp(_siblingIndexBeforeHover, 0, transform.parent.childCount - 1));
            _raisedOnHover = false;
        }

        public void EndDrag()
        {
            _dragging = false;
            RefreshTarget();
        }

        private void Update()
        {
            if (!CanInteract() && (_pointerHovered || _pointerHeld))
            {
                _pointerHovered = false;
                _pointerHeld = false;
                RefreshTarget();
                RestoreSiblingOrder();
            }
            AdvanceMotion(Time.unscaledDeltaTime);
        }

        private bool CanInteract()
        {
            var selectable = _selectable != null ? _selectable : GetComponent<Selectable>();
            return selectable == null || selectable.IsInteractable();
        }

        private void RefreshTarget()
        {
            var hovered = _pointerHovered || _previewHoverPinned;
            var pressed = _pointerHeld && _pointerHovered;
            _target = Vector3.one * (pressed ? pressScale : hovered ? hoverScale : 1f);
            _targetLocalPosition = _restingLocalPosition +
                Vector3.up * (hovered ? hoverLift : 0f) + Vector3.down * (pressed ? pressDepth : 0f);
        }

        private void RestoreSiblingOrder()
        {
            if (!_raisedOnHover || transform.parent == null) return;
            var lastValidIndex = transform.parent.childCount - 1;
            transform.SetSiblingIndex(Mathf.Clamp(_siblingIndexBeforeHover, 0, lastValidIndex));
            _raisedOnHover = false;
        }

        private void AdvanceMotion(float deltaTime)
        {
            if (_dragging) return;
            var blend = 1f - Mathf.Exp(-response * deltaTime);
            transform.localScale = Vector3.Lerp(transform.localScale, _target, blend);
            transform.localPosition = Vector3.Lerp(transform.localPosition, _targetLocalPosition, blend);
        }
    }
}
