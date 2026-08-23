using UnityEngine;
using UnityEngine.EventSystems;

namespace BiomeRivals.Demo
{
    public sealed class DemoHoverScale : MonoBehaviour, IPointerEnterHandler, IPointerExitHandler
    {
        [SerializeField] private float hoverScale = 1.07f;
        [SerializeField] private float response = 14f;
        private Vector3 _target = Vector3.one;

        public void Configure(float scale, float speed)
        {
            hoverScale = scale;
            response = speed;
        }

        public void OnPointerEnter(PointerEventData eventData) => _target = Vector3.one * hoverScale;
        public void OnPointerExit(PointerEventData eventData) => _target = Vector3.one;

        private void Update()
        {
            transform.localScale = Vector3.Lerp(transform.localScale, _target, 1f - Mathf.Exp(-response * Time.unscaledDeltaTime));
        }
    }
}
