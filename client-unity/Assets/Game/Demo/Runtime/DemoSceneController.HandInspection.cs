using System;
using System.Collections;
using System.Linq;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.UI;
using BiomeRivals.Content;
using BiomeRivals.Core;

namespace BiomeRivals.Demo
{
    // A presentation-only browser: never selects a gameplay card or calls a Session command.
    public sealed partial class DemoSceneController
    {
        private RectTransform _handInspectionOverlay;
        private RectTransform _handInspectionContent;
        private CardDetailsView _handInspectionDetails;
        private Button _handInspectionButton;
        private Text _handInspectionPosition;
        private string _handInspectionInstanceId;
        private bool IsHandInspectionOpen => !string.IsNullOrEmpty(_handInspectionInstanceId);

        private void BuildHandInspection()
        {
            _handInspectionButton = CreateSecondaryButton(_canvasRoot, "InspectHand", new Vector2(480, -510),
                new Vector2(132, 32), "查看手牌", 15);
            _handInspectionButton.onClick.AddListener(OpenHandInspection);
            _handInspectionOverlay = CreateRect(_canvasRoot, "HandInspectionOverlay", Vector2.zero,
                new Vector2(ReferenceWidth, ReferenceHeight));
            var blocker = _handInspectionOverlay.gameObject.AddComponent<Image>();
            blocker.color = new Color(0f, 0f, 0f, 0.65f);
            blocker.raycastTarget = true;
            var panel = CreateBasePanel(_handInspectionOverlay, "ReadingPanel", Vector2.zero, new Vector2(640, 700));
            panel.GetComponent<Image>().raycastTarget = true;
            CreateText(panel, "Title", new Vector2(0, 307), new Vector2(370, 40), "手牌图鉴 · 只读", 23,
                Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
            var close = CreateSecondaryButton(panel, "Close", new Vector2(251, 307), new Vector2(85, 40), "关闭", 16);
            close.onClick.AddListener(CloseHandInspection);
            _handInspectionContent = CreateRect(panel, "CardContent", new Vector2(0, 25), new Vector2(360, 480));
            _handInspectionDetails = _handInspectionContent.gameObject.AddComponent<CardDetailsView>();
            _handInspectionDetails.Configure(_registry, UiFont);
            _handInspectionPosition = CreateText(panel, "Position", new Vector2(0, -254), new Vector2(360, 30),
                string.Empty, 17, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
            CreateText(panel, "ReadOnlyHint", new Vector2(0, -318), new Vector2(390, 28), "仅查看说明，不会出牌 · Esc 关闭", 15,
                Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            var previous = CreateSecondaryButton(panel, "Previous", new Vector2(-203, -274), new Vector2(120, 42), "上一张", 16);
            var next = CreateSecondaryButton(panel, "Next", new Vector2(203, -274), new Vector2(120, 42), "下一张", 16);
            previous.onClick.AddListener(() => MoveHandInspection(-1));
            next.onClick.AddListener(() => MoveHandInspection(1));
            _handInspectionOverlay.gameObject.SetActive(false);
        }

        private void OpenHandInspection()
        {
            var match = MatchView;
            if (match.IsMulligan || match.PendingChoice != null || match.HandCards.Count == 0) return;
            _handInspectionInstanceId = match.HandCards[0].handCardInstanceId;
            _handInspectionOverlay.SetAsLastSibling();
            RefreshAllInternal(false);
        }

        private void MoveHandInspection(int offset)
        {
            var cards = MatchView.HandCards;
            if (!IsHandInspectionOpen || cards.Count == 0) return;
            var index = cards.ToList().FindIndex(card => card.handCardInstanceId == _handInspectionInstanceId);
            if (index < 0) { CloseHandInspection(); return; }
            _handInspectionInstanceId = cards[(index + offset % cards.Count + cards.Count) % cards.Count].handCardInstanceId;
            RefreshHandInspection();
        }

        private void CloseHandInspection()
        {
            _handInspectionInstanceId = null;
            RefreshAllInternal(false);
        }

        private void RefreshHandInspection()
        {
            if (_handInspectionOverlay == null) return;
            var match = MatchView;
            _handInspectionButton.interactable = !match.IsMulligan && match.PendingChoice == null && match.HandCards.Count > 0;
            var card = match.HandCards.FirstOrDefault(value => value.handCardInstanceId == _handInspectionInstanceId);
            if (card == null || match.IsMulligan || match.PendingChoice != null) _handInspectionInstanceId = null;
            _handInspectionOverlay.gameObject.SetActive(IsHandInspectionOpen);
            foreach (Transform child in _handInspectionContent) child.gameObject.SetActive(false);
            ClearChildren(_handInspectionContent);
            _handInspectionDetails.Clear();
            if (!IsHandInspectionOpen) return;
            if (!_registry.TryGetDefinition(card.cardId, out var definition)) throw new InvalidOperationException("Unregistered inspected hand card.");
            _handInspectionDetails.ShowCard(card.cardId, new Vector2(360, 480), Vector2.zero,
                match.GetEffectiveCost(definition, card.handCardInstanceId), card.handCardInstanceId);
            _handInspectionPosition.text = $"{match.HandCards.ToList().FindIndex(value => value.handCardInstanceId == card.handCardInstanceId) + 1} / {match.HandCards.Count}";
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private async Task<bool> InspectOnlinePendingUi(float deadline, string path)
        {
            if (!_onlineSession.HasPendingCommand || _onlineSession.CanIssueCommand) return false;
            var longest = MatchView.HandCards.OrderByDescending(card =>
                _registry.TryGetText(card.cardId, out var text) ? text.rulesText.Length : 0).First().cardId;
            if (!await InspectOnlineFullHandReadability(deadline, longest) || !_onlineSession.HasPendingCommand ||
                _onlineSession.CanIssueCommand || Time.realtimeSinceStartup >= deadline) return false;
            WriteDemoScreenshot(path);
            var result = _onlineSession.HasPendingCommand && !_onlineSession.CanIssueCommand && Time.realtimeSinceStartup < deadline;
            Debug.Log($"Real pending hand readability captured: {result}; longest={longest}; revision={MatchView.Revision}.");
            return result;
        }

        private async Task<bool> InspectOnlineFullHandReadability(float deadline, string longCardId = "ed_008")
        {
            var instances = MatchView.HandCards.Select(card => card.handCardInstanceId).ToArray();
            var longCardIndex = MatchView.HandCards.ToList().FindIndex(card => card.cardId == longCardId);
            var revision = MatchView.Revision;
            if (instances.Length != 7 || longCardIndex < 0 || Time.realtimeSinceStartup >= deadline)
                throw new InvalidOperationException($"Hand readability precondition failed: cards={instances.Length}; index={longCardIndex}; remaining={deadline - Time.realtimeSinceStartup:F3}s.");
            await Task.Yield();
            if (!ClickButtonThroughEventSystem(_handInspectionButton))
                throw new InvalidOperationException("Hand readability real open-button raycast failed.");
            try
            {
                await Task.Yield();
                var next = _handInspectionOverlay.Find("ReadingPanel/Next").GetComponent<Button>();
                for (var index = 0; index < longCardIndex; index++)
                {
                    if (Time.realtimeSinceStartup >= deadline || !ClickButtonThroughEventSystem(next))
                        throw new InvalidOperationException($"Hand readability pagination failed: index={index}; remaining={deadline - Time.realtimeSinceStartup:F3}s.");
                    await Task.Yield();
                }
                Canvas.ForceUpdateCanvases();
                var readingCard = _handInspectionDetails.CurrentCard;
                var rules = readingCard.transform.Find("Rules").GetComponent<Text>();
                _registry.TryGetText(longCardId, out var registered);
                if (readingCard.CardId != longCardId || rules.text != registered.rulesText ||
                    rules.preferredHeight > rules.rectTransform.rect.height)
                    throw new InvalidOperationException($"Hand readability rules failed: card={readingCard.CardId}; expected={longCardId}; fullText={rules.text == registered.rulesText}; height={rules.preferredHeight}/{rules.rectTransform.rect.height}.");
                var close = _handInspectionOverlay.Find("ReadingPanel/Close").GetComponent<Button>();
                if (!ClickButtonThroughEventSystem(close)) throw new InvalidOperationException("Hand readability real close-button raycast failed.");
                await Task.Yield();
                Canvas.ForceUpdateCanvases();
                var cards = _handRoot.GetComponentsInChildren<CardUI>();
                var readable = cards.Length == 7 && cards.All(card => Mathf.Approximately(card.GetComponent<Button>().colors.disabledColor.a, 1f)) &&
                    cards.SelectMany(card => card.GetComponentsInChildren<Text>()).All(text =>
                        text.color.a * text.GetComponentsInParent<CanvasGroup>().Aggregate(1f, (alpha, group) => alpha * group.alpha) >= 0.799f);
                var result = readable && IsFullHandLayoutWithinPlate() && MatchView.Revision == revision &&
                    MatchView.HandCards.Select(card => card.handCardInstanceId).SequenceEqual(instances) &&
                    !IsHandInspectionOpen && !_handCanvasGroup.interactable && !_handCanvasGroup.blocksRaycasts &&
                    !_endTurnButton.interactable && !_playerHeroButton.interactable && !_opponentHeroTargetButton.interactable &&
                    GetComponent<DemoBattlefieldPointerController>()?.InputEnabled == false;
                Debug.Log($"Online full-hand draw readability settled: {result}; revision={revision}; cards={cards.Length}; real UI opened/paginated/closed.");
                return result;
            }
            finally { if (IsHandInspectionOpen) CloseHandInspection(); }
        }
#endif

        private void SetupHandReadabilityStatePreview(string state)
        {
            if (state != "opponent" && state != "win" && state != "loss")
                throw new ArgumentException("Unsupported local hand readability state: " + state);
            SelectFaction("end");
            SelectOpponentFaction("nether");
            _match.ResetOpponent(Array.Empty<CardDefinitionEntry>());
            var fullHand = new[] { "ed_002", "ed_003", "ed_004", "ed_005", "ed_006", "ed_007", "ed_008" };
            if (state == "win")
            {
                if (!_registry.TryGetDefinition("or_006", out var trident)) throw new InvalidOperationException("Missing preview weapon.");
                _match.ResetHand(new[] { trident.id });
                _match.ResetPlayerRedstoneForScenario(trident.cost, trident.cost);
                _match.ResetOpponentLife(2);
                var equipped = _match.ApplyPlayCard(trident, _match.CreatePlayCardCommand(trident.id));
                if (!equipped.Accepted) throw new InvalidOperationException(equipped.Message);
                _match.ResetHand(fullHand); // Establish the visual fixture BEFORE the legal lethal action, never after terminal resolution.
                var combat = _match.ApplyEnterCombat(_match.CreateEnterCombatCommand());
                var lethal = _match.ApplyAttack(_match.CreateAttackCommand(MatchAttackerIds.Hero, "HERO"));
                if (!combat.Accepted || !lethal.Accepted) throw new InvalidOperationException("Lethal preview command failed.");
            }
            else
            {
                _match.ResetDeckAndHand(fullHand, Array.Empty<string>());
                if (state == "loss") _match.ResetPlayerLife(1);
                var ended = _match.ApplyEndTurn(_match.CreateEndTurnCommand());
                if (!ended.Accepted) throw new InvalidOperationException(ended.Message);
                if (state == "loss")
                {
                    var draw = _match.BeginNextPlayerTurn();
                    if (draw.Outcome != DemoDrawOutcome.Fatigue || draw.FatigueDamage != 1)
                        throw new InvalidOperationException("Loss preview must resolve normal fatigue, not force a terminal flag.");
                }
            }
            SelectFirstHandCard();
            RefreshAll();
            if (state != "opponent") TryShowLocalMatchOutcome();
            ShowStatus("只读审查：满手七张；结束/对手回合操作保持锁定。", false);
        }

        private IEnumerator AuditHandReadabilityStateCapture(string state)
        {
            yield return PrepareHandInspectionCapture();
            var close = _handInspectionOverlay.Find("ReadingPanel/Close").GetComponent<Button>();
            if (!ClickButtonThroughEventSystem(close)) throw new InvalidOperationException("Read-only Close UI click failed.");
            yield return null;
            Canvas.ForceUpdateCanvases();
            var outcomeMatches = state == "opponent" ? !MatchView.IsFinished && !MatchView.IsPlayerTurn :
                MatchView.IsFinished && MatchView.HasWinner && MatchView.IsPlayerWinner == (state == "win");
            var settled = outcomeMatches && IsFullHandLayoutWithinPlate() && !IsHandInspectionOpen &&
                Mathf.Approximately(_handCanvasGroup.alpha, 1f) && !_handCanvasGroup.interactable && !_handCanvasGroup.blocksRaycasts &&
                !_endTurnButton.interactable && !_playerHeroButton.interactable && !_opponentHeroTargetButton.interactable &&
                GetComponent<DemoBattlefieldPointerController>()?.InputEnabled == false;
            Debug.Log($"Hand state readability preview settled: {settled} (state={state}; cards=7; read-only UI opened/paginated/closed; gameplay locked).");
            if (!settled) throw new InvalidOperationException("Hand-state opacity, layout or input gates failed.");
        }

        private IEnumerator PrepareHandInspectionCapture()
        {
            var revision = MatchView.Revision;
            var selected = _selectedHandCardInstanceId;
            var instances = MatchView.HandCards.Select(value => value.handCardInstanceId).ToArray();
            yield return null; // Register newly created Graphics before using the real EventSystem raycast.
            if (!ClickButtonThroughEventSystem(_handInspectionButton))
                throw new InvalidOperationException("Read-only hand button did not receive an actual UI click.");
            yield return null;
            var next = _handInspectionOverlay.Find("ReadingPanel/Next").GetComponent<Button>();
            for (var index = 1; index < instances.Length; index++)
            {
                if (!ClickButtonThroughEventSystem(next)) throw new InvalidOperationException("Read-only pagination UI click failed.");
                yield return null;
            }
            Canvas.ForceUpdateCanvases();
            var card = _handInspectionDetails.CurrentCard;
            var rules = card.transform.Find("Rules").GetComponent<Text>();
            _registry.TryGetText("ed_008", out var text);
            var settled = instances.Length == 7 && card.CardId == "ed_008" && rules.text == text.rulesText &&
                rules.preferredHeight <= rules.rectTransform.rect.height &&
                MatchView.Revision == revision && _selectedHandCardInstanceId == selected &&
                MatchView.HandCards.Select(value => value.handCardInstanceId).SequenceEqual(instances) &&
                !_handCanvasGroup.interactable && !_handCanvasGroup.blocksRaycasts && !_endTurnButton.interactable &&
                GetComponent<DemoBattlefieldPointerController>()?.InputEnabled == false;
            Debug.Log($"Hand inspection preview settled: {settled} (cards=7; last=ed_008; full rules; actual UI clicks; gameplay locked).");
            if (!settled) throw new InvalidOperationException("Read-only hand inspection did not preserve full text and gameplay locks.");
        }
    }
}
