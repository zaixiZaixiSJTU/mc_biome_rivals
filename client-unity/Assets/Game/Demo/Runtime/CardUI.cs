using System;
using BiomeRivals.Content;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BiomeRivals.Demo
{
    public sealed class CardUI : MonoBehaviour, IBeginDragHandler, IDragHandler, IEndDragHandler
    {
        public const string LayoutId = "card-ui-v1";
        private const float CompactRulesMinScreenFontSize = 12f;
        private const float DetailRulesMinScreenFontSize = 12f;
        private const float RulesMaxScreenFontSize = 15f;
        private const float CostModifierMinScreenFontSize = 10f;
        private const float CostModifierMaxScreenFontSize = 12f;
        private const float UnaffordableCardAlpha = 0.8f;
        private const float DefaultArrivalAnimationDuration = 0.24f;
        private static readonly Color Ink = Hex("#0E100E");
        private static readonly Color Pale = Hex("#F1E6CB");
        private Text _rulesText;
        private Text _costModifierText;
        private string _fullRulesText = string.Empty;
        private bool _compactRules;
        private float _appliedCanvasScale;
        private Action _onDragBegin;
        private Action<Vector2> _onDragUpdate;
        private Action<Vector2> _onDragEnd;
        private CanvasGroup _dragCanvasGroup;
        private RectTransform _dragCanvasRect;
        private DemoHoverScale _hoverScale;
        private Vector3 _dragGrabOffset;
        private Vector3 _dragStartLocalPosition;
        private Vector3 _dragStartLocalScale;
        private Quaternion _dragStartLocalRotation;
        private int _dragStartSiblingIndex;
        private bool _dragStartBlocksRaycasts;
        private bool _dragging;
        private CanvasGroup _arrivalCanvasGroup;
        private float _arrivalElapsed;
        private float _arrivalDuration;
        private float _arrivalTargetAlpha;
        private bool _arrivalWasInteractable;
        private bool _arrivalWasBlocksRaycasts;

        public string CardId { get; private set; }
        public bool IsCompact { get; private set; }
        public bool IsArrivalAnimating { get; private set; }
        public float ArrivalAnimationAlpha => _arrivalCanvasGroup != null ? _arrivalCanvasGroup.alpha : 1f;
        public bool IsResourceAffordable { get; private set; } = true;
        public string HandCardInstanceId { get; private set; } = string.Empty;
        public int BaseCost { get; private set; }
        public int DisplayedCost { get; private set; }
        public RectTransform RectTransform => (RectTransform)transform;

        public void Bind(CardContentRegistry registry, string cardId, Vector2 size, bool compact, Font font, Action onClick,
            int? costOverride = null, string handCardInstanceId = "", Action onDragBegin = null,
            Action<Vector2> onDragUpdate = null, Action<Vector2> onDragEnd = null)
        {
            CancelArrivalAnimation();
            IsResourceAffordable = true;
            var existingCanvasGroup = GetComponent<CanvasGroup>();
            if (existingCanvasGroup != null) existingCanvasGroup.alpha = 1f;
            if (!registry.TryGetDefinition(cardId, out var definition) || !registry.TryGetText(cardId, out var text))
                throw new InvalidOperationException("Card content is not registered: " + cardId);
            registry.TryGetTheme(definition.themeId, out var theme);
            ClearChildren();
            CardId = cardId;
            IsCompact = compact;
            HandCardInstanceId = handCardInstanceId ?? string.Empty;
            _onDragBegin = onDragBegin;
            _onDragUpdate = onDragUpdate;
            _onDragEnd = onDragEnd;
            BaseCost = definition.cost;
            DisplayedCost = costOverride ?? BaseCost;
            gameObject.name = "Card_" + cardId +
                (string.IsNullOrEmpty(HandCardInstanceId) ? string.Empty : "_" + HandCardInstanceId);
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
            var artHeight = compact ? h * 0.34f : h * 0.33f;
            var artY = compact ? h * 0.11f : h * 0.11f;

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
            var titleLeft = costPosition.x + costVisualSize * 0.5f + (compact ? 5f : 8f);
            var titleRight = w * 0.5f - (compact ? 8f : 10f);
            var titleWidth = Mathf.Max(1f, titleRight - titleLeft);
            var titleName = CreateText("Name", new Vector2((titleLeft + titleRight) * 0.5f, titleY),
                new Vector2(titleWidth, titleHeight - 2f), text.name, compact ? 15 : 20, theme.TitleText,
                TextAnchor.MiddleCenter, FontStyle.Bold, font);
            titleName.resizeTextForBestFit = true;
            titleName.resizeTextMinSize = compact ? 14 : 18;
            titleName.resizeTextMaxSize = compact ? 15 : 20;
            var isDiscounted = DisplayedCost < BaseCost;
            CreateText("Cost", costPosition, new Vector2(costSize, costSize), DisplayedCost.ToString(), compact ? 18 : 23, isDiscounted ? Hex("#9CDC72") : usesStudyFrame ? Pale : Ink, TextAnchor.MiddleCenter, FontStyle.Bold, font);
            if (isDiscounted)
            {
                var modifierPosition = costPosition + new Vector2(costVisualSize * 0.35f, -costVisualSize * 0.34f);
                var modifierSize = new Vector2(compact ? 32f : 40f, compact ? 24f : 26f);
                var reduction = BaseCost - DisplayedCost;
                var modifierLabel = "-" + reduction;
                var badge = CreateImage("CostModifierBadge", modifierPosition, modifierSize, Hex("#173821"));
                badge.sprite = DemoCardSurfaceProvider.LoadArtSurface();
                badge.type = Image.Type.Tiled;
                badge.pixelsPerUnitMultiplier = 1f;
                _costModifierText = CreateText("CostModifier", modifierPosition, modifierSize, modifierLabel,
                    compact ? 10 : 12, Hex("#D9FFB5"), TextAnchor.MiddleCenter, FontStyle.Bold, font);
                _costModifierText.resizeTextForBestFit = true;
            }

            var rulesHeight = compact ? h * 0.34f : h * 0.35f;
            var rulesY = usesStudyFrame ? -h * (compact ? 0.224f : 0.21f) : -h * 0.235f;
            if (!usesStudyFrame) CreateImage("RulesSurface", new Vector2(0, rulesY), new Vector2(w - 18, rulesHeight), theme.RulesSurface);
            var rulesWidth = usesStudyFrame ? w - (compact ? 28 : 38) : w - 30;
            var rulesHeightInset = usesStudyFrame ? (compact ? 14 : 15) : 8;
            var rules = CreateText("Rules", new Vector2(0, rulesY), new Vector2(rulesWidth, rulesHeight - rulesHeightInset), text.rulesText, compact ? 12 : 14, theme.BodyText, TextAnchor.MiddleCenter, FontStyle.Normal, font);
            rules.alignByGeometry = usesStudyFrame;
            rules.resizeTextForBestFit = true;
            _rulesText = rules;
            _fullRulesText = text.rulesText;
            _compactRules = compact;
            _appliedCanvasScale = 0f;
            RefreshRuleTypography(true);

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

        public void SetResourceAffordable(bool affordable)
        {
            IsResourceAffordable = affordable;
            var canvasGroup = GetComponent<CanvasGroup>();
            if (canvasGroup == null) canvasGroup = gameObject.AddComponent<CanvasGroup>();
            canvasGroup.alpha = affordable ? 1f : UnaffordableCardAlpha;
            // Affordability is a visual hint only: unaffordable cards remain selectable for inspection.
            canvasGroup.interactable = true;
            canvasGroup.blocksRaycasts = true;
        }

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (_dragging || _onDragBegin == null || _onDragEnd == null || eventData == null) return;

            _hoverScale = GetComponent<DemoHoverScale>();
            _hoverScale?.BeginDrag();
            _dragStartLocalPosition = transform.localPosition;
            _dragStartLocalScale = transform.localScale;
            _dragStartLocalRotation = transform.localRotation;
            _dragStartSiblingIndex = transform.GetSiblingIndex();

            var canvas = GetComponentInParent<Canvas>();
            canvas = canvas != null ? canvas.rootCanvas : null;
            _dragCanvasRect = canvas != null ? canvas.transform as RectTransform : null;
            if (_dragCanvasRect != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    _dragCanvasRect, eventData.position, canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
                    out var pointerWorldPosition))
                _dragGrabOffset = transform.position - pointerWorldPosition;
            else
                _dragGrabOffset = Vector3.zero;

            _dragCanvasGroup = GetComponent<CanvasGroup>();
            if (_dragCanvasGroup == null) _dragCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            _dragStartBlocksRaycasts = _dragCanvasGroup.blocksRaycasts;
            _dragCanvasGroup.blocksRaycasts = false;
            transform.SetAsLastSibling();
            _dragging = true;
            _onDragBegin();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (!_dragging || eventData == null) return;
            var canvas = GetComponentInParent<Canvas>();
            canvas = canvas != null ? canvas.rootCanvas : null;
            var eventCamera = eventData.pressEventCamera ??
                (canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay ? canvas.worldCamera : null);
            if (_dragCanvasRect != null && RectTransformUtility.ScreenPointToWorldPointInRectangle(
                    _dragCanvasRect, eventData.position, eventCamera, out var pointerWorldPosition))
                transform.position = pointerWorldPosition + _dragGrabOffset;
            _onDragUpdate?.Invoke(eventData.position);
        }

        public void OnEndDrag(PointerEventData eventData)
        {
            if (!_dragging) return;
            var dropPosition = eventData != null ? eventData.position : Vector2.zero;

            transform.localPosition = _dragStartLocalPosition;
            transform.localScale = _dragStartLocalScale;
            transform.localRotation = _dragStartLocalRotation;
            if (transform.parent != null)
                transform.SetSiblingIndex(Mathf.Clamp(_dragStartSiblingIndex, 0, transform.parent.childCount - 1));
            if (_dragCanvasGroup != null) _dragCanvasGroup.blocksRaycasts = _dragStartBlocksRaycasts;
            _hoverScale?.EndDrag();
            _dragging = false;

            // The drop callback can synchronously resolve a play and rebuild the hand.
            // Do not touch this card again after invoking it.
            _onDragEnd?.Invoke(dropPosition);
        }

        public void PlayArrivalAnimation(float duration = DefaultArrivalAnimationDuration)
        {
            CancelArrivalAnimation();
            if (duration <= 0f) return;

            _arrivalCanvasGroup = GetComponent<CanvasGroup>();
            if (_arrivalCanvasGroup == null) _arrivalCanvasGroup = gameObject.AddComponent<CanvasGroup>();
            _arrivalTargetAlpha = _arrivalCanvasGroup.alpha;
            _arrivalWasInteractable = _arrivalCanvasGroup.interactable;
            _arrivalWasBlocksRaycasts = _arrivalCanvasGroup.blocksRaycasts;
            _arrivalCanvasGroup.alpha = 0f;
            _arrivalCanvasGroup.interactable = false;
            _arrivalCanvasGroup.blocksRaycasts = false;
            _arrivalElapsed = 0f;
            _arrivalDuration = duration;
            IsArrivalAnimating = true;
        }

        private void Update() => AdvanceArrivalAnimation(Time.unscaledDeltaTime);

        private void AdvanceArrivalAnimation(float deltaTime)
        {
            if (!IsArrivalAnimating || _arrivalCanvasGroup == null) return;
            _arrivalElapsed += Mathf.Max(0f, deltaTime);
            var progress = Mathf.Clamp01(_arrivalElapsed / _arrivalDuration);
            var eased = 1f - Mathf.Pow(1f - progress, 3f);
            _arrivalCanvasGroup.alpha = Mathf.Lerp(0f, _arrivalTargetAlpha, eased);
            if (progress >= 1f) CancelArrivalAnimation();
        }

        private void CancelArrivalAnimation()
        {
            if (!IsArrivalAnimating) return;
            IsArrivalAnimating = false;
            _arrivalElapsed = 0f;
            _arrivalDuration = 0f;
            if (_arrivalCanvasGroup == null) return;
            _arrivalCanvasGroup.alpha = _arrivalTargetAlpha;
            _arrivalCanvasGroup.interactable = _arrivalWasInteractable;
            _arrivalCanvasGroup.blocksRaycasts = _arrivalWasBlocksRaycasts;
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

        private static string CreateCompactRulesPreview(Text rules, string fullText)
        {
            if (string.IsNullOrEmpty(fullText) || rules.font == null) return fullText ?? string.Empty;

            var rect = rules.rectTransform.rect;
            if (rect.width <= 0f || rect.height <= 0f) return fullText;

            var settings = rules.GetGenerationSettings(rect.size);
            settings.resizeTextForBestFit = false;
            settings.fontSize = rules.resizeTextMinSize;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;

            var generator = new TextGenerator();
            if (generator.GetPreferredHeight(fullText, settings) <= rect.height + 0.5f) return fullText;

            var low = 0;
            var high = fullText.Length;
            while (low < high)
            {
                var middle = low + (high - low + 1) / 2;
                var candidate = AddPreviewEllipsis(fullText, middle);
                if (generator.GetPreferredHeight(candidate, settings) <= rect.height + 0.5f) low = middle;
                else high = middle - 1;
            }

            return AddPreviewEllipsis(fullText, low);
        }

        private static string AddPreviewEllipsis(string fullText, int prefixLength)
        {
            var prefix = fullText.Substring(0, Mathf.Clamp(prefixLength, 0, fullText.Length))
                .TrimEnd(' ', '，', '；', '。', '、', ',', ';', '.');
            return prefix + "…";
        }

        private static float GetCanvasScaleFactor(Graphic graphic)
        {
            var canvas = graphic != null ? graphic.canvas : null;
            if (canvas == null) return 1f;
            var rootCanvas = canvas.rootCanvas;
            return rootCanvas != null ? Mathf.Max(0.01f, rootCanvas.scaleFactor) : 1f;
        }

        private void LateUpdate() => RefreshRuleTypography(false);

        private void RefreshRuleTypography(bool force)
        {
            if (_rulesText == null && _costModifierText == null) return;
            var scaleReference = _rulesText != null ? (Graphic)_rulesText : _costModifierText;
            var canvasScale = GetCanvasScaleFactor(scaleReference);
            if (!force && Mathf.Abs(canvasScale - _appliedCanvasScale) < 0.001f) return;

            if (_rulesText != null)
            {
                var minimumScreenFontSize = _compactRules ? CompactRulesMinScreenFontSize : DetailRulesMinScreenFontSize;
                _rulesText.resizeTextMinSize = Mathf.Max(1, Mathf.CeilToInt(minimumScreenFontSize / canvasScale));
                _rulesText.resizeTextMaxSize = Mathf.Max(_rulesText.resizeTextMinSize,
                    Mathf.Max(1, Mathf.CeilToInt(RulesMaxScreenFontSize / canvasScale)));
                if (_compactRules) _rulesText.text = CreateCompactRulesPreview(_rulesText, _fullRulesText);
                _rulesText.SetAllDirty();
            }

            if (_costModifierText != null)
            {
                _costModifierText.resizeTextMinSize = Mathf.Max(1,
                    Mathf.CeilToInt(CostModifierMinScreenFontSize / canvasScale));
                _costModifierText.resizeTextMaxSize = Mathf.Max(_costModifierText.resizeTextMinSize,
                    Mathf.CeilToInt(CostModifierMaxScreenFontSize / canvasScale));
                _costModifierText.SetAllDirty();
            }

            _appliedCanvasScale = canvasScale;
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
            _rulesText = null;
            _costModifierText = null;
            _fullRulesText = string.Empty;
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
            // Keep the textured reading surface opaque; input gating belongs to the Button/CanvasGroup.
            colors.disabledColor = new Color(normal.r * 0.9f, normal.g * 0.9f, normal.b * 0.9f, normal.a);
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static Color Hex(string value) =>
            ColorUtility.TryParseHtmlString(value, out var color) ? color : Color.magenta;
    }
}
