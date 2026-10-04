using System.Reflection;
using BiomeRivals.Bootstrap;
using BiomeRivals.Content;
using BiomeRivals.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoStatusInspectionTests
    {
        private GameObject _root;
        private DemoSceneController _controller;
        private object _previousComposition;
        private EventSystem _registeredEventSystem;
        private static readonly PropertyInfo CompositionInstance = typeof(GameCompositionRoot).GetProperty("Instance");

        [SetUp]
        public void SetUp()
        {
            _previousComposition = CompositionInstance.GetValue(null);
            CompositionInstance.GetSetMethod(true).Invoke(null, new object[] { null });
            _root = new GameObject("StatusInspectionTest");
            _controller = _root.AddComponent<DemoSceneController>();
            _controller.BuildNow();
            if (EventSystem.current == null)
            {
                // EventSystem does not execute its play-mode OnEnable in EditMode.
                // Register using that lifecycle, not its setter (which rejects unknown instances).
                _registeredEventSystem = _root.GetComponentInChildren<EventSystem>();
                typeof(EventSystem).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(_registeredEventSystem, null);
            }
            var canvas = _root.transform.Find("DemoCanvas").GetComponent<Canvas>();
            canvas.GetComponent<CanvasScaler>().enabled = false;
            canvas.scaleFactor = 1f;
        }

        [TearDown]
        public void TearDown()
        {
            if (_registeredEventSystem != null)
                typeof(EventSystem).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(_registeredEventSystem, null);
            _registeredEventSystem = null;
            Object.DestroyImmediate(_root);
            CompositionInstance.GetSetMethod(true).Invoke(null, new[] { _previousComposition });
        }

        private void Invoke(string method, params object[] arguments) => typeof(DemoSceneController)
            .GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_controller, arguments);
        private T Find<T>(string path) where T : Component => _root.transform.Find("DemoCanvas/" + path).GetComponent<T>();

        [Test]
        public void FullTextIsLiteralAndUpdatingDoesNotKeepThePreviousMessage()
        {
            const string original = "localhost:17350\n更新两端后重试\n<color=red>完整拒绝原因</color>";
            Invoke("ShowStatus", original, true);
            Invoke("OpenStatusInspection");
            var body = Find<Text>("StatusInspectionOverlay/ReadingPanel/Viewport/FullMessage");
            Assert.That(body.text, Is.EqualTo(original));
            Assert.That(body.supportRichText, Is.False);
            Assert.That(body.resizeTextForBestFit, Is.False);
            Invoke("ShowStatus", "新的提示全文", false);
            Assert.That(body.text, Is.EqualTo("新的提示全文"));
            Assert.That(body.color, Is.EqualTo(Find<Text>("StatusPlate/Status").color));
            Invoke("ShowStatus", "新的提示全文", true);
            Assert.That(body.color, Is.EqualTo(Find<Text>("StatusPlate/Status").color), "Severity must update even when text is identical.");
        }

        [Test]
        public void ReadingLocksOriginalInputAndRestoresItWhenClosed()
        {
            var before = Find<CanvasGroup>("HandPlate/HandCards").interactable;
            Invoke("OpenStatusInspection");
            Assert.That(Find<CanvasGroup>("HandPlate/HandCards").interactable, Is.False);
            Assert.That(_root.GetComponent<DemoBattlefieldPointerController>().InputEnabled, Is.False);
            Assert.That(Find<Button>("EndTurnButton").interactable, Is.False);
            Assert.That(Find<Button>("InspectHand").interactable, Is.False);
            Invoke("OpenHandInspection");
            Assert.That(Find<RectTransform>("HandInspectionOverlay").gameObject.activeSelf, Is.False);
            Invoke("CloseStatusInspection");
            Assert.That(Find<CanvasGroup>("HandPlate/HandCards").interactable, Is.EqualTo(before));
            Assert.That(Find<RectTransform>("StatusInspectionOverlay").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ModalFocusReturnsToTheEntryButton()
        {
            var entry = Find<Button>("InspectStatus");
            EventSystem.current.SetSelectedGameObject(entry.gameObject);
            Invoke("OpenStatusInspection");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Find<Button>("StatusInspectionOverlay/ReadingPanel/Close").gameObject));
            Invoke("CloseStatusInspection");
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(entry.gameObject));
        }

        [TestCase(1f)]
        [TestCase(2f / 3f)]
        public void CompactSummaryKeepsTheFullMessageAtReadableSize(float scale)
        {
            var canvas = _root.transform.Find("DemoCanvas").GetComponent<Canvas>();
            canvas.scaleFactor = scale;
            var text = Find<Text>("StatusPlate/Status");
            var summary = text.GetComponent<DemoReadableSummary>();
            var message = string.Join("\n", new string[50]).Replace("\n", "完整拒绝原因与操作建议\n");
            Invoke("ShowStatus", message, true);
            Assert.That(summary.FullText, Is.EqualTo(message));
            Assert.That(summary.IsAbbreviated, Is.True);
            Assert.That(text.text, Does.EndWith("…"));
            Assert.That(text.resizeTextForBestFit, Is.False);
            Assert.That(text.fontSize * scale, Is.GreaterThanOrEqualTo(12f));
            Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 0.5f));
            Invoke("OpenStatusInspection");
            Assert.That(Find<Text>("StatusInspectionOverlay/ReadingPanel/Viewport/FullMessage").text, Is.EqualTo(message));
        }

        [Test]
        public void CardNotesReuseTheReadOnlyPanelWithoutChangingTheStatusMessage()
        {
            Invoke("ShowStatus", "当前对局提示", false);
            var notes = _root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent").GetComponentsInChildren<DemoReadableSummary>();
            Assert.That(notes, Is.Not.Empty);
            Invoke("OpenCardNotes");
            Assert.That(Find<RectTransform>("StatusInspectionOverlay").gameObject.activeSelf, Is.True);
            Assert.That(Find<Text>("StatusInspectionOverlay/ReadingPanel/Title").text, Is.EqualTo("卡牌与操作 · 完整说明"));
            Assert.That(Find<Text>("StatusInspectionOverlay/ReadingPanel/Viewport/FullMessage").text, Is.Not.Empty);
            Assert.That(Find<CanvasGroup>("HandPlate/HandCards").interactable, Is.False);
            Assert.That(_root.GetComponent<DemoBattlefieldPointerController>().InputEnabled, Is.False);
            Invoke("CloseStatusInspection");
            Invoke("OpenStatusInspection");
            Assert.That(Find<Text>("StatusInspectionOverlay/ReadingPanel/Viewport/FullMessage").text, Is.EqualTo("当前对局提示"));
        }

        [TestCase(1f)]
        [TestCase(2f / 3f)]
        public void EquippedTemporaryResourceAndBuriedStateStayReadable(float scale)
        {
            _root.transform.Find("DemoCanvas").GetComponent<Canvas>().scaleFactor = scale;
            Invoke("SetupHudResourceScenario");
            Invoke("OpenCardNotes");
            Assert.That(Find<Text>("StatusInspectionOverlay/ReadingPanel/Viewport/FullMessage").text, Does.StartWith("炽足兽\n\n"));
            Invoke("CloseStatusInspection");
            foreach (var typography in _root.GetComponentsInChildren<DemoHudTypography>()) typography.ApplyScale(scale);
            foreach (var path in new[] { "EnergyPlate/Energy", "PlayerEquipment/Label", "HandLabel" })
            {
                var text = Find<Text>(path);
                Assert.That(text.fontSize * scale, Is.GreaterThanOrEqualTo(12f), path);
                Assert.That(text.preferredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 0.5f), path);
                Assert.That(text.resizeTextForBestFit, Is.False, path);
            }
            Assert.That(Find<Text>("EnergyPlate/Energy").text, Is.EqualTo("红石 ◆ 12/10\n临时 +2"));
            Assert.That(Find<Text>("PlayerEquipment/Label").text, Is.EqualTo("激流三叉戟\n攻击 2 · 耐久 3/3"));
            Assert.That(Find<Text>("HandLabel").text, Does.Contain("掩埋 10"));
        }

        private T Field<T>(string name) => (T)typeof(DemoSceneController)
            .GetField(name, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(_controller);

        [TestCase(1f)]
        [TestCase(2f / 3f)]
        public void NarrowCardPreviewFitsAndItsReadingEntryKeepsFullRules(float scale)
        {
            _root.transform.Find("DemoCanvas").GetComponent<Canvas>().scaleFactor = scale;
            Invoke("SetupHudResourceScenario");
            var view = Field<CardDetailsView>("_cardDetailsView");
            var rules = view.CurrentCard.transform.Find("Rules").GetComponent<Text>();
            Assert.That(view.CurrentCard.IsCompact, Is.False, "Preview must retain the detail card layout.");
            Assert.That(rules.alignment, Is.EqualTo(TextAnchor.MiddleCenter));
            Assert.That(rules.rectTransform.rect.height, Is.LessThan(view.CurrentCard.RectTransform.rect.height * 0.23f),
                "The preview must fit the actual light-paper region, not just an oversized text Rect.");
            Assert.That(rules.resizeTextMinSize * scale, Is.GreaterThanOrEqualTo(12f));
            var settings = rules.GetGenerationSettings(rules.rectTransform.rect.size);
            settings.resizeTextForBestFit = false;
            settings.fontSize = rules.resizeTextMinSize;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            Assert.That(new TextGenerator().GetPreferredHeight(rules.text, settings) / rules.pixelsPerUnit,
                Is.LessThanOrEqualTo(rules.rectTransform.rect.height + 0.5f));
            if (scale < 1f) Assert.That(rules.text, Does.EndWith("…"));
            var registry = Field<CardContentRegistry>("_registry");
            Assert.That(registry.TryGetText("nt_004", out var card), Is.True);
            Invoke("OpenCardNotes");
            Assert.That(Find<Text>("StatusInspectionOverlay/ReadingPanel/Viewport/FullMessage").text, Does.Contain(card.rulesText));
        }

        [Test]
        public void ClosingTerminalReadingDoesNotUnlockGameplay()
        {
            Invoke("SetupHudResourceScenario");
            var match = Field<DemoLocalMatch>("_match");
            match.ResetOpponentLife(2);
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            Assert.That(match.ApplyAttack(match.CreateAttackCommand(MatchAttackerIds.Hero, "HERO")).Accepted, Is.True);
            Invoke("RefreshAll");
            Assert.That(match.IsFinished, Is.True);
            Invoke("OpenStatusInspection");
            Assert.That(Find<Text>("StatusInspectionOverlay/ReadingPanel/Viewport/FullMessage").text, Does.Contain("所有操作已锁定"));
            Invoke("CloseStatusInspection");
            Assert.That(Find<CanvasGroup>("HandPlate/HandCards").interactable, Is.False);
            Assert.That(Find<Button>("EndTurnButton").interactable, Is.False);
            Assert.That(_root.GetComponent<DemoBattlefieldPointerController>().InputEnabled, Is.False);
        }

        [Test]
        public void IncomingMovementChoiceClosesReadingAndCannotBeCoveredByIt()
        {
            Invoke("SetupHudResourceScenario");
            var registry = Field<CardContentRegistry>("_registry");
            Assert.That(registry.TryGetDefinition("si_005", out var bear), Is.True);
            var match = Field<DemoLocalMatch>("_match");
            match.ResetOpponent(new[] { bear });
            Invoke("OpenStatusInspection");
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            var target = match.OpponentBattlefield[0];
            Assert.That(match.ApplyAttack(match.CreateAttackCommand(MatchAttackerIds.Hero, "UNIT", target.InstanceId)).Accepted, Is.True);
            Assert.That(match.PendingChoice, Is.Not.Null);
            Invoke("RefreshAll");
            Assert.That(Find<RectTransform>("StatusInspectionOverlay").gameObject.activeSelf, Is.False);
            Invoke("OpenStatusInspection");
            Invoke("OpenCardNotes");
            Assert.That(Find<RectTransform>("StatusInspectionOverlay").gameObject.activeSelf, Is.False);
            Assert.That(Find<CanvasGroup>("HandPlate/HandCards").interactable, Is.False);
            Assert.That(Find<Button>("EndTurnButton").interactable, Is.False);
        }

        [TestCase(1f)]
        [TestCase(2f / 3f)]
        public void LongMessagesUseScrollContentRatherThanShrinkingOrTruncation(float scale)
        {
            var canvas = _root.transform.Find("DemoCanvas").GetComponent<Canvas>();
            canvas.scaleFactor = scale;
            foreach (var typography in _root.GetComponentsInChildren<DemoHudTypography>(true)) typography.ApplyScale(scale);
            var entry = Find<Button>("InspectStatus").GetComponentInChildren<Text>();
            Assert.That(entry.preferredHeight, Is.LessThanOrEqualTo(entry.rectTransform.rect.height + 0.5f));
            var message = string.Join("\n", new string[50]).Replace("\n", "完整拒绝原因与操作建议\n");
            Invoke("ShowStatus", message, false);
            Invoke("OpenStatusInspection");
            Canvas.ForceUpdateCanvases();
            var body = Find<Text>("StatusInspectionOverlay/ReadingPanel/Viewport/FullMessage");
            var scroll = Find<ScrollRect>("StatusInspectionOverlay/ReadingPanel/Viewport");
            Assert.That(body.text, Is.EqualTo(message));
            Assert.That(body.fontSize * scale, Is.GreaterThanOrEqualTo(12f));
            Assert.That(body.verticalOverflow, Is.EqualTo(VerticalWrapMode.Overflow));
            Assert.That(scroll.content.rect.height, Is.GreaterThan(scroll.viewport.rect.height));
            Assert.That(scroll.verticalScrollbar, Is.Not.Null);
        }
    }
}
