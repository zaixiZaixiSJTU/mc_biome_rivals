using System;
using BiomeRivals.Content;
using UnityEngine;
using UnityEngine.UI;

namespace BiomeRivals.Presentation
{
    /// <summary>
    /// A data-driven card face. Layout belongs to the prefab; names and colors belong
    /// to the content registries; gameplay values belong to the supplied view model.
    /// </summary>
    public sealed class CardFaceView : MonoBehaviour
    {
        [Header("Surfaces")]
        [SerializeField] private Image frameSurface;
        [SerializeField] private Image frameShadow;
        [SerializeField] private Image accentSurface;
        [SerializeField] private Image rulesSurface;
        [SerializeField] private Image artwork;

        [Header("Text")]
        [SerializeField] private Text costText;
        [SerializeField] private Text titleText;
        [SerializeField] private Text rulesText;
        [SerializeField] private Text typeText;

        [Header("Optional stats")]
        [SerializeField] private GameObject attackRoot;
        [SerializeField] private Text attackText;
        [SerializeField] private GameObject healthRoot;
        [SerializeField] private Text healthText;
        [SerializeField] private GameObject durabilityRoot;
        [SerializeField] private Text durabilityText;

        public void Render(CardFaceViewModel model, Sprite art = null)
        {
            if (model == null) throw new ArgumentNullException(nameof(model));

            var registry = CardContentLoader.Current;
            if (!registry.TryGetName(model.cardId, out var localizedName))
                throw new InvalidOperationException($"Unregistered card id: {model.cardId}");
            if (!registry.TryGetTheme(model.themeId, out var palette))
                throw new InvalidOperationException($"Unregistered card theme: {model.themeId}");

            RequireBindings();
            frameSurface.color = palette.FrameBase;
            frameShadow.color = palette.FrameDark;
            accentSurface.color = palette.Accent;
            rulesSurface.color = palette.RulesSurface;
            titleText.color = palette.TitleText;
            rulesText.color = palette.BodyText;
            typeText.color = palette.BodyText;

            costText.text = model.cost.ToString();
            titleText.text = localizedName;
            rulesText.text = model.rulesText;
            typeText.text = model.typeText;
            artwork.sprite = art;
            artwork.enabled = art != null;

            RenderStat(attackRoot, attackText, model.showAttack, model.attack);
            RenderStat(healthRoot, healthText, model.showHealth, model.health);
            RenderStat(durabilityRoot, durabilityText, model.showDurability, model.durability);
        }

        public void RenderRegistered(string cardId, Sprite art = null)
        {
            var registry = CardContentLoader.Current;
            if (!registry.TryGetDefinition(cardId, out var definition))
                throw new InvalidOperationException($"Unregistered card definition: {cardId}");
            if (!registry.TryGetText(cardId, out var text))
                throw new InvalidOperationException($"Unregistered localized card text: {cardId}");

            Render(new CardFaceViewModel
            {
                cardId = definition.id,
                themeId = definition.themeId,
                rulesText = text.rulesText,
                typeText = text.typeLabel,
                cost = definition.cost,
                showAttack = definition.hasAttack,
                attack = definition.attack,
                showHealth = definition.hasHealth,
                health = definition.health,
                showDurability = definition.hasDurability,
                durability = definition.durability
            }, art);
        }

        private static void RenderStat(GameObject root, Text label, bool visible, int value)
        {
            if (root == null) return;
            root.SetActive(visible);
            if (visible && label != null) label.text = value.ToString();
        }

        private void RequireBindings()
        {
            if (frameSurface == null || frameShadow == null || accentSurface == null ||
                rulesSurface == null || artwork == null || costText == null ||
                titleText == null || rulesText == null || typeText == null)
            {
                throw new InvalidOperationException($"CardFaceView '{name}' has incomplete required bindings.");
            }
        }
    }
}
