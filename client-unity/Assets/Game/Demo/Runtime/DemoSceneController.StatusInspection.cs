using System;
using System.Collections;
using System.Linq;
using BiomeRivals.Networking;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BiomeRivals.Demo
{
    public sealed partial class DemoSceneController
    {
        private void ShowCompatibilityFailure(ServerCompatibilityFailure failure)
        {
            if (_statusText == null) return;
            if (_statusSummary != null) _statusSummary.SetFullText(failure.UserDetails, failure.UserSummary);
            else _statusText.text = failure.UserSummary;
            _statusText.color = Danger;
            RefreshStatusInspection();
        }

        private void ShowOnlineException(Exception exception, bool connecting)
        {
            if (exception is ServerCompatibilityException compatibility) ShowCompatibilityFailure(compatibility.Failure);
            else ShowStatus(DemoOnlineFeedback.FormatException(exception, connecting), true);
        }

        private RectTransform _statusInspectionOverlay;
        private Text _statusInspectionBody;
        private Button _statusInspectionButton;
        private Button _statusInspectionClose;
        private ScrollRect _statusInspectionScroll;
        private GameObject _statusPreviousFocus;
        private bool _statusInspectionOpen;
        private string _statusInspectionDisplayed;
        private DemoReadableSummary _statusSummary;
        private Button _cardNotesButton;
        private bool _readingCardNotes;
        private Text _statusInspectionTitle;
        private bool IsReadOnlyOverlayOpen => IsHandInspectionOpen || _statusInspectionOpen;

        private void BuildStatusInspection()
        {
            _statusInspectionButton = CreateSecondaryButton(_canvasRoot, "InspectStatus", new Vector2(814, -522), new Vector2(212, 32), "查看完整提示", 15);
            _statusInspectionButton.onClick.AddListener(OpenStatusInspection);
            _statusInspectionOverlay = CreateRect(_canvasRoot, "StatusInspectionOverlay", Vector2.zero, new Vector2(ReferenceWidth, ReferenceHeight));
            var blocker = _statusInspectionOverlay.gameObject.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.72f);
            var panel = CreateBasePanel(_statusInspectionOverlay, "ReadingPanel", Vector2.zero, new Vector2(900, 630));
            _statusInspectionTitle = CreateText(panel, "Title", new Vector2(-90, 266), new Vector2(570, 42), "操作提示 · 完整说明", 26, Pale, TextAnchor.MiddleLeft, FontStyle.Bold);
            _statusInspectionClose = CreateSecondaryButton(panel, "Close", new Vector2(360, 266), new Vector2(110, 42), "返回", 18);
            _statusInspectionClose.onClick.AddListener(CloseStatusInspection);
            var viewport = CreateRect(panel, "Viewport", new Vector2(-8, -5), new Vector2(786, 440));
            viewport.gameObject.AddComponent<Image>().color = new Color(1f, 1f, 1f, 0f);
            viewport.gameObject.AddComponent<RectMask2D>();
            _statusInspectionBody = CreateText(viewport, "FullMessage", Vector2.zero, new Vector2(760, 0), string.Empty, 24, Pale, TextAnchor.UpperLeft, FontStyle.Normal);
            _statusInspectionBody.supportRichText = false;
            _statusInspectionBody.verticalOverflow = VerticalWrapMode.Overflow;
            _statusInspectionBody.lineSpacing = 1.15f;
            var body = _statusInspectionBody.rectTransform;
            body.anchorMin = new Vector2(0f, 1f);
            body.anchorMax = new Vector2(1f, 1f);
            body.pivot = new Vector2(0.5f, 1f);
            body.sizeDelta = new Vector2(-24f, 0f);
            _statusInspectionBody.gameObject.AddComponent<DemoHudTypography>().Configure(24);
            _statusInspectionBody.gameObject.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            _statusInspectionScroll = viewport.gameObject.AddComponent<ScrollRect>();
            _statusInspectionScroll.viewport = viewport;
            _statusInspectionScroll.content = body;
            _statusInspectionScroll.horizontal = false;
            _statusInspectionScroll.movementType = ScrollRect.MovementType.Clamped;
            _statusInspectionScroll.scrollSensitivity = 36f;
            var track = CreatePanel(panel, "ScrollTrack", new Vector2(406, -5), new Vector2(12, 440), Ink);
            var handle = CreatePanel(track.transform, "Handle", Vector2.zero, new Vector2(12, 50), Muted);
            handle.rectTransform.sizeDelta = Vector2.zero;
            handle.rectTransform.anchorMin = Vector2.zero;
            handle.rectTransform.anchorMax = Vector2.one;
            var scrollbar = track.gameObject.AddComponent<Scrollbar>();
            scrollbar.handleRect = handle.rectTransform;
            scrollbar.targetGraphic = handle;
            scrollbar.direction = Scrollbar.Direction.BottomToTop;
            _statusInspectionScroll.verticalScrollbar = scrollbar;
            _statusInspectionScroll.verticalScrollbarVisibility = ScrollRect.ScrollbarVisibility.AutoHide;
            CreateText(panel, "ReadOnlyHint", new Vector2(0, -270), new Vector2(770, 34),
                "↑/↓/PgUp/PgDn 滚动 · Home/End 首尾 · Esc / 右键返回（只读）", 15, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            DemoUiNavigation.DisableDirectionalNavigation(_statusInspectionClose, _statusInspectionScroll.verticalScrollbar);
            _statusInspectionOverlay.gameObject.SetActive(false);
        }

        private void OpenStatusInspection()
        {
            OpenReadingPanel(false);
        }

        private void OpenCardNotes()
        {
            OpenReadingPanel(true);
        }

        private string CurrentReadingText => _readingCardNotes
            ? GetCardNotesText()
            : _statusSummary.FullText;

        private string GetCardNotesText()
        {
            var notes = string.Join("\n\n", _inspectorRoot.GetComponentsInChildren<DemoReadableSummary>()
                .Where(summary => summary.name != "Header")
                .Select(summary => summary.FullText).Where(value => !string.IsNullOrWhiteSpace(value)));
            if (MatchView.Phase == DemoTurnPhase.Main && !MatchView.IsFinished &&
                !string.IsNullOrWhiteSpace(_selectedCardId) && _registry.TryGetText(_selectedCardId, out var card))
                return $"{card.name}\n\n{card.rulesText}\n\n{notes}";
            return notes;
        }

        private void OpenReadingPanel(bool cardNotes)
        {
            if (IsReadOnlyOverlayOpen || MatchView.IsMulligan || MatchView.PendingChoice != null) return;
            _readingCardNotes = cardNotes;
            if (string.IsNullOrWhiteSpace(CurrentReadingText)) return;
            _statusPreviousFocus = EventSystem.current != null ? EventSystem.current.currentSelectedGameObject : null;
            _statusInspectionOpen = true;
            _statusInspectionDisplayed = null;
            _statusInspectionOverlay.SetAsLastSibling();
            RefreshAllInternal(false);
            EventSystem.current?.SetSelectedGameObject(_statusInspectionClose.gameObject);
        }

        private void CloseStatusInspection()
        {
            if (!_statusInspectionOpen) return;
            _statusInspectionOpen = false;
            RefreshAllInternal(false);
            DemoUiNavigation.RestoreFocus(EventSystem.current, _statusPreviousFocus,
                _readingCardNotes ? _cardNotesButton : _statusInspectionButton, _statusInspectionButton);
            _statusPreviousFocus = null;
        }

        private void RefreshStatusInspection()
        {
            if (_statusInspectionOverlay == null) return;
            var hasCardNotes = _inspectorRoot.GetComponentsInChildren<DemoReadableSummary>().Any(summary => !string.IsNullOrWhiteSpace(summary.FullText));
            _cardNotesButton.gameObject.SetActive(!_showRuleDiagnostics && hasCardNotes);
            _cardNotesButton.interactable = !IsReadOnlyOverlayOpen && !MatchView.IsMulligan && MatchView.PendingChoice == null &&
                hasCardNotes;
            if (MatchView.IsMulligan || MatchView.PendingChoice != null) _statusInspectionOpen = false;
            _statusInspectionButton.interactable = !IsReadOnlyOverlayOpen && !MatchView.IsMulligan && MatchView.PendingChoice == null && !string.IsNullOrWhiteSpace(_statusSummary.FullText);
            _statusInspectionButton.gameObject.SetActive(!MatchView.IsMulligan && MatchView.PendingChoice == null);
            _statusInspectionOverlay.gameObject.SetActive(_statusInspectionOpen);
            if (!_statusInspectionOpen) return;
            _statusInspectionBody.color = Pale;
            _statusInspectionTitle.color = _readingCardNotes ? Pale : _statusText.color;
            _statusInspectionTitle.text = _readingCardNotes ? "卡牌与操作 · 完整说明" : "操作提示 · 完整说明";
            var fullMessage = CurrentReadingText;
            if (_statusInspectionDisplayed == fullMessage) return;
            _statusInspectionDisplayed = fullMessage;
            _statusInspectionBody.text = _statusInspectionDisplayed;
            _statusInspectionBody.GetComponent<DemoHudTypography>().ApplyScale(_canvasRoot.GetComponent<Canvas>().scaleFactor);
            LayoutRebuilder.ForceRebuildLayoutImmediate(_statusInspectionBody.rectTransform);
            _statusInspectionScroll.StopMovement();
            _statusInspectionScroll.verticalNormalizedPosition = 1f;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private IEnumerator PrepareStatusInspectionCapture()
        {
            var revision = MatchView.Revision;
            var identities = MatchView.HandCards.Select(card => card.handCardInstanceId).ToArray();
            var message = "阅读验收示例 · 本面板不执行游戏命令\n\n" + string.Join("\n\n", Enumerable.Range(1, 10).Select(index => $"{index}. 部署与风险说明：请选择合法的地表格。红石不足、占格越界或目标已经离场时，操作会被拒绝且不扣除资源。移动后可能承受守卫者伤害；取消选择不会支付费用。服务器版本不兼容时，请更新两端，再重新连接。"));
            ShowStatus(message, false);
            yield return null;
            if (!ClickButtonThroughEventSystem(_statusInspectionButton)) throw new InvalidOperationException("Status reading real open click failed.");
            yield return null;
            Canvas.ForceUpdateCanvases();
            if (!_statusInspectionOpen || _statusInspectionBody.text != message || _handCanvasGroup.interactable || GetComponent<DemoBattlefieldPointerController>().InputEnabled || _statusInspectionScroll.content.rect.height <= _statusInspectionScroll.viewport.rect.height)
                throw new InvalidOperationException("Status full-text/layout/modal-input gate failed.");
            var before = _statusInspectionScroll.verticalNormalizedPosition;
            ExecuteEvents.Execute(_statusInspectionScroll.gameObject, new PointerEventData(EventSystem.current) { scrollDelta = new Vector2(0, -5) }, ExecuteEvents.scrollHandler);
            yield return null;
            if (_statusInspectionScroll.verticalNormalizedPosition >= before) throw new InvalidOperationException("Status reading actual wheel failed.");
            if (!ClickButtonThroughEventSystem(_statusInspectionClose)) throw new InvalidOperationException("Status reading real return click failed.");
            yield return null;
            if (_statusInspectionOpen || MatchView.Revision != revision || !MatchView.HandCards.Select(card => card.handCardInstanceId).SequenceEqual(identities))
                throw new InvalidOperationException("Status reading changed gameplay or did not close.");
            if (!ClickButtonThroughEventSystem(_statusInspectionButton)) throw new InvalidOperationException("Status reading real reopen failed.");
            yield return null;
            Debug.Log("Status inspection settled: True; actual entry/scroll/return/reopen; full message; unchanged revision/hand; gameplay locked.");
        }
#else
        private IEnumerator PrepareStatusInspectionCapture() { throw new NotSupportedException("Status capture diagnostics are unavailable in release builds."); }
#endif
    }
}
