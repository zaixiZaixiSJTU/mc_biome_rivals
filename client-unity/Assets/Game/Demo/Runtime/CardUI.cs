using System;
using BiomeRivals.Content;
using UnityEngine;
using UnityEngine.UI;

namespace BiomeRivals.Demo
{
    public sealed class CardUI : MonoBehaviour
    {
        public const string LayoutId = "card-ui-v1";
        private static readonly Color Ink = Hex("#0E100E");
        private static readonly Color Pale = Hex("#F1E6CB");

        public string CardId { get; private set; }
        public bool IsCompact { get; private set; }
        public RectTransform RectTransform => (RectTransform)transform;

        public void Bind(CardContentRegistry registry, string cardId, Vector2 size, bool compact, Font font, Action onClick)
        {
            if (!registry.TryGetDefinition(cardId, out var definition) || !registry.TryGetText(cardId, out var text))
                throw new InvalidOperationException("Card content is not registered: " + cardId);
            registry.TryGetTheme(definition.themeId, out var theme);
            ClearChildren();
            CardId = cardId;
            IsCompact = compact;
            gameObject.name = "Card_" + cardId;
            RectTransform.anchorMin = RectTransform.anchorMax = RectTransform.pivot = new Vector2(0.5f, 0.5f);
            RectTransform.sizeDelta = size;
            RectTransform.anchoredPosition = Vector2.zero;

            var rootImage = GetComponent<Image>() ?? gameObject.AddComponent<Image>();
            var frameSprite = DemoCardFrameProvider.Load(definition.themeId);
            var usesStudyFrame = frameSprite != null;
            rootImage.sprite = frameSprite;
            rootImage.type = Image.Type.Simple;
            rootImage.preserveAspect = false;
            rootImage.color = usesStudyFrame ? Color.white : theme.FrameDark;

            var button = GetComponent<Button>();
            if (onClick != null)
            {
                if (button == null) button = gameObject.AddComponent<Button>();
                button.targetGraphic = rootImage;
                ConfigureButtonColors(button, usesStudyFrame ? Color.white : theme.FrameDark, theme.Accent);
                button.onClick.RemoveAllListeners();
                button.onClick.AddListener(() => onClick());
            }
            else if (button != null)
            {
                button.onClick.RemoveAllListeners();
                button.interactable = false;
            }

            var h = size.y;
            var w = size.x;
            var titleHeight = compact ? 31f : 39f;
            var titleY = h * 0.5f - titleHeight * 0.72f;
            var artHeight = compact ? h * 0.36f : h * 0.39f;
            var artY = compact ? h * 0.105f : h * 0.11f;

            if (!usesStudyFrame)
            {
                CreateImage("Frame", Vector2.zero, size - new Vector2(10, 10), theme.FrameBase);
                CreateImage("TitleBand", new Vector2(0, titleY), new Vector2(w - 14, titleHeight), theme.FrameDark);
            }

            var artSurface = CreateImage("ArtSurface", new Vector2(0, artY), new Vector2(w - 24f, artHeight + 2f), Color.Lerp(theme.FrameDark, Color.white, 0.16f));
            artSurface.sprite = DemoCardSurfaceProvider.LoadArtSurface();
            artSurface.type = Image.Type.Tiled;
            artSurface.pixelsPerUnitMultiplier = 1f;

            var sprite = DemoCardArtProvider.Load(cardId);
            if (sprite != null)
            {
                var art = CreateImage("Art", new Vector2(0, artY), new Vector2(Mathf.Min(w * 0.54f, artHeight * 0.78f), artHeight * 0.78f), Color.white);
                art.sprite = sprite;
                art.preserveAspect = true;
            }
            else
            {
                CreateText("ArtFallback", new Vector2(0, artY), new Vector2(w - 28, artHeight - 10), "◆", compact ? 34 : 52, theme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold, font);
            }

            var costSize = compact ? 34f : 43f;
            var costVisualSize = costSize * 1.55f;
            var costPosition = new Vector2(-w * 0.5f + costVisualSize * 0.52f, h * 0.5f - costVisualSize * 0.52f);
            CreateSocket("CostSocketFrame", costPosition, costVisualSize, DemoCardFrameProvider.LoadCostSocket(definition.themeId), theme.Accent);
            CreateText("Name", new Vector2(usesStudyFrame ? 12f : 7f, titleY), new Vector2(w - (usesStudyFrame ? 64f : 50f), titleHeight - 4), text.name, compact ? 15 : 20, theme.TitleText, TextAnchor.MiddleCenter, FontStyle.Bold, font);
            CreateText("Cost", costPosition, new Vector2(costSize, costSize), definition.cost.ToString(), compact ? 18 : 23, usesStudyFrame ? Pale : Ink, TextAnchor.MiddleCenter, FontStyle.Bold, font);

            var rulesHeight = compact ? h * 0.31f : h * 0.29f;
            var rulesY = usesStudyFrame ? -h * 0.21f : -h * 0.235f;
            if (!usesStudyFrame) CreateImage("RulesSurface", new Vector2(0, rulesY), new Vector2(w - 18, rulesHeight), theme.RulesSurface);
            var rules = CreateText("Rules", new Vector2(0, rulesY), new Vector2(w - (usesStudyFrame ? 38 : 30), rulesHeight - (usesStudyFrame ? 15 : 8)), text.rulesText, compact ? 11 : 14, theme.BodyText, TextAnchor.MiddleCenter, FontStyle.Normal, font);
            rules.alignByGeometry = usesStudyFrame;
            rules.resizeTextForBestFit = compact;
            rules.resizeTextMinSize = 9;
            rules.resizeTextMaxSize = compact ? 11 : 14;

            var typeY = -h * 0.5f + (compact ? 20f : 24f);
            CreateText("Type", new Vector2(0, typeY), new Vector2(w - 54, compact ? 20 : 24), text.typeLabel, compact ? 10 : 12, theme.TitleText, TextAnchor.MiddleCenter, FontStyle.Bold, font);
            var statSocketSize = costSize * 1.35f;
            if (definition.hasAttack)
                CreateStat("Attack", new Vector2(-w * 0.5f + 22, -h * 0.5f + 21), definition.attack.ToString(), statSocketSize, DemoCardFrameProvider.LoadAttackSocket(definition.themeId), theme.Accent, compact, font);
            if (definition.hasHealth)
                CreateStat("Health", new Vector2(w * 0.5f - 22, -h * 0.5f + 21), definition.health.ToString(), statSocketSize, DemoCardFrameProvider.LoadHealthSocket(definition.themeId), theme.Accent, compact, font);
            if (definition.hasDurability)
                CreateStat("Durability", new Vector2(w * 0.5f - 22, -h * 0.5f + 21), definition.durability.ToString(), statSocketSize, DemoCardFrameProvider.LoadHealthSocket(definition.themeId), theme.Accent, compact, font);
        }

        private void CreateStat(string prefix, Vector2 position, string value, float size, Sprite socket, Color fallbackTint, bool compact, Font font)
        {
            CreateSocket(prefix + "SocketFrame", position, size, socket, fallbackTint);
            CreateText(prefix, position, new Vector2(size * 0.72f, size * 0.72f), value, compact ? 15 : 20, Pale, TextAnchor.MiddleCenter, FontStyle.Bold, font);
        }

        private Image CreateSocket(string name, Vector2 position, float size, Sprite sprite, Color fallbackTint)
        {
            var image = CreateImage(name, position, new Vector2(size, size), sprite != null ? Color.white : fallbackTint);
            image.sprite = sprite ?? DemoCardSurfaceProvider.LoadArtSurface();
            image.type = sprite != null ? Image.Type.Simple : Image.Type.Tiled;
            image.preserveAspect = sprite != null;
            return image;
        }

        private Image CreateImage(string name, Vector2 position, Vector2 size, Color color)
        {
            var rect = CreateRect(name, position, size);
            var image = rect.gameObject.AddComponent<Image>();
            image.color = color;
            image.raycastTarget = false;
            return image;
        }

        private Text CreateText(string name, Vector2 position, Vector2 size, string value, int fontSize, Color color, TextAnchor alignment, FontStyle style, Font font)
        {
            var rect = CreateRect(name, position, size);
            var text = rect.gameObject.AddComponent<Text>();
            text.font = font;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private RectTransform CreateRect(string name, Vector2 position, Vector2 size)
        {
            var child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(transform, false);
            var rect = child.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private void ClearChildren()
        {
            for (var index = transform.childCount - 1; index >= 0; index--)
            {
                var child = transform.GetChild(index).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        private static void ConfigureButtonColors(Button button, Color normal, Color accent)
        {
            var colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = Color.Lerp(normal, accent, 0.22f);
            colors.pressedColor = Color.Lerp(normal, accent, 0.38f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.42f);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static Color Hex(string value) =>
            ColorUtility.TryParseHtmlString(value, out var color) ? color : Color.magenta;
    }
}
