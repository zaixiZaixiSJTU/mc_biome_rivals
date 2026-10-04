using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BiomeRivals.Content;
using UnityEngine;
using UnityEngine.UI;

namespace BiomeRivals.Demo
{
    public sealed partial class DemoSceneController
    {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        [Serializable]
        private sealed class CardPaperAuditReport
        {
            public bool success;
            public int registeredCards, visitedCards, themes, screenWidth, screenHeight;
            public string[] cardIds, screenshots;
        }

        private IEnumerator PrepareCardPaperCapture()
        {
            if (IsOnlineBoard) throw new InvalidOperationException("Card paper fixtures cannot replace a live match.");
            var capture = GetCommandLineValue("-captureDemo");
            var root = Path.Combine(Path.GetDirectoryName(capture), Path.GetFileNameWithoutExtension(capture) + "-cards");
            if (Directory.Exists(root)) throw new InvalidOperationException("Refusing to overwrite card paper evidence.");
            Directory.CreateDirectory(root);
            var definitions = JsonUtility.FromJson<CardDefinitionRegistryDocument>(Resources.Load<TextAsset>("CardContent/card-definition-registry.v1").text).entries;
            var visited = new HashSet<string>(StringComparer.Ordinal);
            var screenshots = new List<string>();
            var groups = definitions.GroupBy(card => card.themeId).ToArray();
            foreach (var group in groups)
            {
                SelectFaction(group.Key);
                SelectOpponentFaction(group.Key);
                var cards = group.ToArray();
                for (var start = 0; start < cards.Length; start += 7)
                {
                    var batch = cards.Skip(start).Take(7).Select(card => card.id).ToArray();
                    var fullHand = Enumerable.Range(0, 7).Select(index => batch[index % batch.Length]).ToArray();
                    _match.ResetHand(fullHand);
                    SelectFirstHandCard();
                    RefreshAll();
                    _titleText.text = "全卡纸面 · 本地验收示例";
                    ShowStatus("卡面是居中预览；图鉴右侧保留完整规则。本场景仅用于排版验收。", false);
                    yield return null;
                    yield return null;
                    Canvas.ForceUpdateCanvases();
                    AuditResponsiveCanvasBounds();
                    foreach (var handCard in _handRoot.GetComponentsInChildren<CardUI>()) AuditSceneCardPaper(handCard);
                    var handPath = Path.Combine(root, group.Key + "-hand-" + (start / 7 + 1) + ".png");
                    WriteDemoScreenshot(handPath);
                    screenshots.Add(handPath);
                    var revision = MatchView.Revision;
                    var energy = MatchView.Energy;
                    var identities = MatchView.HandCards.Select(card => card.handCardInstanceId).ToArray();
                    var selected = _selectedHandCardInstanceId;
                    if (!ClickButtonThroughEventSystem(_handInspectionButton)) throw new InvalidOperationException("All-card reader entry failed.");
                    yield return null;
                    for (var index = 0; index < batch.Length; index++)
                    {
                        if (index > 0)
                        {
                            if (!ClickButtonThroughEventSystem(_handInspectionNext)) throw new InvalidOperationException("All-card actual pagination failed.");
                            yield return null;
                        }
                        yield return null;
                        Canvas.ForceUpdateCanvases();
                        var card = _handInspectionDetails.CurrentCard;
                        _registry.TryGetText(batch[index], out var registered);
                        if (card.CardId != batch[index] || !visited.Add(card.CardId) ||
                            card.HandCardInstanceId != identities[index] || card.FullRulesText != registered.rulesText ||
                            _handInspectionRules.text != registered.rulesText || !InspectedRulesFitPaper(card) ||
                            _handInspectionRules.fontSize * _canvasRoot.GetComponent<Canvas>().scaleFactor < 12f ||
                            _handCanvasGroup.interactable || GetComponent<DemoBattlefieldPointerController>().InputEnabled)
                            throw new InvalidOperationException("All-card full text, identity, paper or read-only gate failed: " + batch[index]);
                        AuditSceneCardPaper(card);
                        var needsScroll = _handInspectionScroll.content.rect.height > _handInspectionScroll.viewport.rect.height + 0.01f;
                        if (_handInspectionScroll.verticalScrollbar.gameObject.activeSelf != needsScroll)
                            throw new InvalidOperationException("Actual card reader scrollbar visibility is incorrect.");
                        var readingPath = Path.Combine(root, card.CardId + "-reading.png");
                        WriteDemoScreenshot(readingPath);
                        screenshots.Add(readingPath);
                    }
                    if (!ClickButtonThroughEventSystem(_handInspectionOverlay.Find("ReadingPanel/Close").GetComponent<Button>()))
                        throw new InvalidOperationException("All-card reader return failed.");
                    yield return null;
                    if (MatchView.Revision != revision || MatchView.Energy != energy || _selectedHandCardInstanceId != selected ||
                        !MatchView.HandCards.Select(card => card.handCardInstanceId).SequenceEqual(identities) ||
                        !_handCanvasGroup.interactable || !GetComponent<DemoBattlefieldPointerController>().InputEnabled)
                        throw new InvalidOperationException("Reading changed gameplay or did not restore legal input.");
                }
            }
            var report = new CardPaperAuditReport { success = visited.Count == definitions.Length && groups.Length == 7,
                registeredCards = definitions.Length, visitedCards = visited.Count, themes = groups.Length,
                screenWidth = Screen.width, screenHeight = Screen.height, cardIds = visited.OrderBy(id => id).ToArray(), screenshots = screenshots.ToArray() };
            if (!report.success) throw new InvalidOperationException("Card paper coverage is incomplete.");
            File.WriteAllText(Path.Combine(root, "coverage.json"), JsonUtility.ToJson(report, true));
            _titleText.text = "全卡纸面 · 本地验收完成";
            Debug.Log("All-card paper settled: True; 74 registered cards; 7 themes; actual entry/pagination/return; full rules; unchanged reading state.");
        }

        private void AuditSceneCardPaper(CardUI card)
        {
            _registry.TryGetDefinition(card.CardId, out var definition);
            var text = card.transform.Find("Rules").GetComponent<Text>();
            var bounds = DemoCardFrameProvider.GetRulesPaperBounds(definition.themeId, card.RectTransform.rect.size);
            if (Vector2.Distance(text.rectTransform.sizeDelta, bounds.size) > 0.01f ||
                Vector2.Distance(text.rectTransform.anchoredPosition, bounds.center) > 0.01f ||
                text.resizeTextMinSize * _canvasRoot.GetComponent<Canvas>().scaleFactor < 12f ||
                text.alignment != TextAnchor.MiddleCenter ||
                DemoReadableSummary.FitPreview(card.FullRulesText, text, text.resizeTextMinSize) != text.text)
                throw new InvalidOperationException("Rendered card escaped its paper registration: " + card.CardId);
        }

        private IEnumerator PrepareChoiceRulesCapture()
        {
            var choice = MatchView.PendingChoice;
            if (choice == null || !MatchView.IsChoiceOwner) throw new InvalidOperationException("Choice reader needs a visible owned card choice.");
            var selected = _selectedChoiceOptionIndex;
            var revision = MatchView.Revision;
            var energy = MatchView.Energy;
            var identities = MatchView.HandCards.Select(card => card.handCardInstanceId).ToArray();
            var entry = _choiceCardsRoot.Find("ChoiceSlot1/ReadRules").GetComponent<Button>();
            if (!ClickButtonThroughEventSystem(entry)) throw new InvalidOperationException("Pending rules entry actual click failed.");
            yield return null;
            Canvas.ForceUpdateCanvases();
            var card = _handInspectionDetails.CurrentCard;
            _registry.TryGetText(card.CardId, out var registered);
            if (_handInspectionRules.text != registered.rulesText || !InspectedRulesFitPaper(card) ||
                _choiceOverlayCanvasGroup.interactable || _choiceConfirmButton.IsInteractable() ||
                GetComponent<DemoBattlefieldPointerController>().InputEnabled)
                throw new InvalidOperationException("Pending reader full text or input lock failed.");
            if (!ClickButtonThroughEventSystem(_handInspectionOverlay.Find("ReadingPanel/Close").GetComponent<Button>()))
                throw new InvalidOperationException("Pending reader actual return failed.");
            yield return null;
            if (!_choiceOverlayCanvasGroup.interactable || MatchView.PendingChoice?.choiceId != choice.choiceId ||
                _selectedChoiceOptionIndex != selected || MatchView.Revision != revision || MatchView.Energy != energy ||
                !MatchView.HandCards.Select(value => value.handCardInstanceId).SequenceEqual(identities))
                throw new InvalidOperationException("Pending reader changed the choice or game state.");
            if (!ClickButtonThroughEventSystem(_choiceCardsRoot.Find("ChoiceSlot1/ReadRules").GetComponent<Button>()))
                throw new InvalidOperationException("Pending reader reopen failed.");
            yield return null;
            Debug.Log("Pending rules reading settled: True; actual entry/return/reopen; full rules; unchanged choice/revision/energy/hand; gameplay locked.");
        }
#else
        private IEnumerator PrepareCardPaperCapture() { throw new NotSupportedException("Card paper audit is unavailable in release builds."); }
        private IEnumerator PrepareChoiceRulesCapture() { throw new NotSupportedException("Choice reading audit is unavailable in release builds."); }
#endif
    }
}
