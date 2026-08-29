using BiomeRivals.Content;
using UnityEngine;

namespace BiomeRivals.Demo
{
    public sealed class CardDetailsView : MonoBehaviour
    {
        private CardContentRegistry _registry;
        private Font _font;

        public CardUI CurrentCard { get; private set; }

        public void Configure(CardContentRegistry registry, Font font)
        {
            _registry = registry;
            _font = font;
        }

        public CardUI ShowCard(string cardId, Vector2 size, Vector2 position, int? costOverride = null)
        {
            if (_registry == null) throw new System.InvalidOperationException("CardDetailsView is not configured.");
            CurrentCard = DemoCardUiFactory.Create(transform, _registry, cardId, size, false, _font, null, costOverride);
            CurrentCard.RectTransform.anchoredPosition = position;
            return CurrentCard;
        }

        public void Clear() => CurrentCard = null;
    }
}
