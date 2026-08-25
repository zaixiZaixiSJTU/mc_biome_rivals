using System;
using BiomeRivals.Content;
using UnityEngine;

namespace BiomeRivals.Demo
{
    internal static class DemoCardUiFactory
    {
        public static CardUI Create(
            Transform parent,
            CardContentRegistry registry,
            string cardId,
            Vector2 size,
            bool compact,
            Font font,
            Action onClick)
        {
            var prefab = DemoUiPrefabProvider.LoadCardUI();
            var rootObject = prefab != null
                ? UnityEngine.Object.Instantiate(prefab, parent, false)
                : new GameObject("CardUI", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(CardUI));
            if (rootObject.transform.parent != parent) rootObject.transform.SetParent(parent, false);
            var card = rootObject.GetComponent<CardUI>() ?? rootObject.AddComponent<CardUI>();
            card.Bind(registry, cardId, size, compact, font, onClick);
            return card;
        }
    }
}
