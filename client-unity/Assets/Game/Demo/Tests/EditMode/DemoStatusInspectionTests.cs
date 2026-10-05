using System.Reflection;
using System.Linq;
using BiomeRivals.Bootstrap;
using BiomeRivals.Content;
using BiomeRivals.Core;
using BiomeRivals.Networking;
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

        private bool ReadingKey(KeyCode key, bool reverse = false) => (bool)typeof(DemoSceneController)
            .GetMethod("HandleReadingKey", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_controller, new object[] { key, reverse });

        [Test]
        public void ReadingKeysStayInsideModalAndPageDuplicateInstancesWithoutGameplayChanges()
        {
            var match = Field<DemoLocalMatch>("_match");
            match.ResetHand(new[] { "nt_002", "nt_002" }); Invoke("RefreshAll");
            var entry = Find<Button>("InspectHand"); EventSystem.current.SetSelectedGameObject(entry.gameObject);
            var revision = match.Revision; var energy = match.Energy;
            var instances = match.HandCards.Select(card => card.handCardInstanceId).ToArray();
            Invoke("OpenHandInspection");
            var close = Find<Button>("HandInspectionOverlay/ReadingPanel/Close");
            var previous = Find<Button>("HandInspectionOverlay/ReadingPanel/Previous");
            var next = Find<Button>("HandInspectionOverlay/ReadingPanel/Next");
            Assert.That(ReadingKey(KeyCode.Tab), Is.True);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(previous.gameObject));
            ReadingKey(KeyCode.Tab); Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(next.gameObject));
            ReadingKey(KeyCode.Tab); Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(close.gameObject));
            ReadingKey(KeyCode.Tab, true); Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(next.gameObject));
            Assert.That(close.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            Assert.That(next.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            ReadingKey(KeyCode.RightArrow);
            Assert.That(Field<CardDetailsView>("_handInspectionDetails").CurrentCard.HandCardInstanceId, Is.EqualTo(instances[1]));
            ReadingKey(KeyCode.LeftArrow);
            Assert.That(Field<CardDetailsView>("_handInspectionDetails").CurrentCard.HandCardInstanceId, Is.EqualTo(instances[0]));
            ExecuteEvents.Execute(next.gameObject, new BaseEventData(EventSystem.current), ExecuteEvents.submitHandler);
            Assert.That(Field<CardDetailsView>("_handInspectionDetails").CurrentCard.HandCardInstanceId, Is.EqualTo(instances[1]));
            Assert.That(Field<CanvasGroup>("_handCanvasGroup").interactable, Is.False);
            ReadingKey(KeyCode.Escape);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(entry.gameObject));
            Assert.That(match.Revision, Is.EqualTo(revision)); Assert.That(match.Energy, Is.EqualTo(energy));
            Assert.That(match.HandCards.Select(card => card.handCardInstanceId), Is.EqualTo(instances));
            Assert.That(ReadingKey(KeyCode.RightArrow), Is.False);
        }

        [Test]
        public void StatusKeysScrollLongBodyAndTabCannotReachGameplay()
        {
            Invoke("ShowStatus", string.Join("\n", Enumerable.Repeat("完整消息与操作说明", 100)), false);
            Invoke("OpenStatusInspection"); Canvas.ForceUpdateCanvases();
            var scroll = Find<ScrollRect>("StatusInspectionOverlay/ReadingPanel/Viewport");
            var close = Find<Button>("StatusInspectionOverlay/ReadingPanel/Close");
            ReadingKey(KeyCode.Tab); ReadingKey(KeyCode.Tab, true);
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(close.gameObject));
            ReadingKey(KeyCode.End); Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(0.01f));
            ReadingKey(KeyCode.Home); Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.01f));
            ReadingKey(KeyCode.DownArrow); Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(1f));
            ReadingKey(KeyCode.UpArrow); Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.01f));
            ReadingKey(KeyCode.PageDown); Assert.That(scroll.verticalNormalizedPosition, Is.LessThan(1f));
            ReadingKey(KeyCode.PageUp); Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.01f));
            Assert.That(Find<Button>("EndTurnButton").interactable, Is.False);
            ReadingKey(KeyCode.Escape);
            Assert.That(Find<RectTransform>("StatusInspectionOverlay").gameObject.activeSelf, Is.False);
        }

        [Test]
        public void ChoiceReaderRestoresTheExactDuplicateOptionAfterUiRebuildWithoutConfirming()
        {
            var match = Field<DemoLocalMatch>("_match");
            var pending = new PendingChoiceDto { choiceId = "duplicate-reader", playerId = "local-player", kind = "ARCHAEOLOGY_TOP_3",
                options = new[] { new PendingChoiceOptionDto { optionIndex = 0, cardId = "nt_002", selectable = true },
                    new PendingChoiceOptionDto { optionIndex = 1, cardId = "nt_002", selectable = true } } };
            typeof(DemoLocalMatch).GetProperty("PendingChoice").GetSetMethod(true).Invoke(match, new object[] { pending });
            Invoke("RefreshAll"); Invoke("AdvanceChoiceOverlayEntrance", 1f);
            var root = Field<RectTransform>("_choiceCardsRoot");
            var entry = root.Find("ChoiceSlot1/ReadRules").GetComponent<Button>();
            EventSystem.current.SetSelectedGameObject(entry.gameObject);
            var revision = match.Revision; var energy = match.Energy; var selected = Field<int>("_selectedChoiceOptionIndex");
            entry.onClick.Invoke();
            Assert.That(Field<int>("_choiceInspectionOptionIndex"), Is.EqualTo(1));
            ReadingKey(KeyCode.RightArrow); ReadingKey(KeyCode.Tab); ReadingKey(KeyCode.Tab, true);
            Assert.That(Field<int>("_choiceInspectionOptionIndex"), Is.EqualTo(1));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(Find<Button>("HandInspectionOverlay/ReadingPanel/Close").gameObject));
            ReadingKey(KeyCode.Escape);
            var rebuilt = root.Find("ChoiceSlot1/ReadRules").GetComponent<Button>();
            Assert.That(rebuilt, Is.Not.SameAs(entry));
            Assert.That(EventSystem.current.currentSelectedGameObject, Is.EqualTo(rebuilt.gameObject));
            Assert.That(match.PendingChoice, Is.SameAs(pending));
            Assert.That(Field<int>("_selectedChoiceOptionIndex"), Is.EqualTo(selected));
            Assert.That(match.Revision, Is.EqualTo(revision)); Assert.That(match.Energy, Is.EqualTo(energy));
        }

        [Test]
        public void ChoiceReaderClosesWhenItsExactOptionDisappearsEvenIfSameCardRemains()
        {
            var match = Field<DemoLocalMatch>("_match");
            var pending = new PendingChoiceDto { choiceId = "duplicate-retired", playerId = "local-player", kind = "ARCHAEOLOGY_TOP_3",
                options = new[] { new PendingChoiceOptionDto { optionIndex = 0, cardId = "nt_002", selectable = true },
                    new PendingChoiceOptionDto { optionIndex = 1, cardId = "nt_002", selectable = true } } };
            typeof(DemoLocalMatch).GetProperty("PendingChoice").GetSetMethod(true).Invoke(match, new object[] { pending });
            Invoke("RefreshAll"); Invoke("AdvanceChoiceOverlayEntrance", 1f);
            Invoke("OpenChoiceRulesForOption", "nt_002", 1);
            pending.options = new[] { pending.options[0] }; Invoke("RefreshAll");
            Assert.That(Find<RectTransform>("HandInspectionOverlay").gameObject.activeSelf, Is.False);
            Assert.That(Field<int>("_choiceInspectionOptionIndex"), Is.EqualTo(-1));
            Assert.That(EventSystem.current.currentSelectedGameObject == null || EventSystem.current.currentSelectedGameObject.activeInHierarchy, Is.True);
            Assert.That(match.PendingChoice, Is.SameAs(pending));
        }

        [TestCase(1f)]
        [TestCase(2f / 3f)]
        [TestCase(1024f / 1920f)]
        public void ReadingKeyHintsRemainReadableAndFitAtWindowScales(float scale)
        {
            _root.transform.Find("DemoCanvas").GetComponent<Canvas>().scaleFactor = scale;
            foreach (var typography in _root.GetComponentsInChildren<DemoHudTypography>(true)) typography.ApplyScale(scale);
            foreach (var overlay in new[] { "HandInspectionOverlay", "StatusInspectionOverlay" })
            {
                var hint = Find<Text>(overlay + "/ReadingPanel/ReadOnlyHint");
                Assert.That(hint.fontSize * scale, Is.GreaterThanOrEqualTo(12f));
                // Text.preferredHeight/Width already convert TextGenerator pixels to UI units.
                Assert.That(hint.preferredHeight, Is.LessThanOrEqualTo(hint.rectTransform.rect.height + 0.5f), overlay);
                Assert.That(hint.preferredWidth, Is.LessThanOrEqualTo(hint.rectTransform.rect.width + 0.5f), overlay);
                Assert.That(hint.text, Does.Contain("只读"));
            }
        }

        [TestCase(1f)]
        [TestCase(2f / 3f)]
        [TestCase(1024f / 1920f)]
        public void All74HandReadersKeepFullRulesAndStableDuplicateInstances(float scale)
        {
            _root.transform.Find("DemoCanvas").GetComponent<Canvas>().scaleFactor = scale;
            var registry = Field<CardContentRegistry>("_registry");
            var match = Field<DemoLocalMatch>("_match");
            var document = JsonUtility.FromJson<CardDefinitionRegistryDocument>(Resources.Load<TextAsset>("CardContent/card-definition-registry.v1").text);
            Assert.That(document.entries, Has.Length.EqualTo(74));
            foreach (var definition in document.entries)
            {
                match.ResetHand(new[] { definition.id, definition.id });
                Invoke("RefreshAll");
                var revision = match.Revision;
                var instances = match.HandCards.Select(card => card.handCardInstanceId).ToArray();
                Invoke("OpenHandInspection");
                var card = Field<CardDetailsView>("_handInspectionDetails").CurrentCard;
                var first = card.HandCardInstanceId;
                Invoke("MoveHandInspection", 1);
                card = Field<CardDetailsView>("_handInspectionDetails").CurrentCard;
                Assert.That(card.HandCardInstanceId, Is.Not.EqualTo(first), definition.id);
                registry.TryGetText(definition.id, out var registered);
                var body = Find<Text>("HandInspectionOverlay/ReadingPanel/RulesViewport/FullRules");
                Assert.That(body.text, Is.EqualTo(registered.rulesText), definition.id);
                Assert.That(card.FullRulesText, Is.EqualTo(registered.rulesText));
                Assert.That(body.resizeTextForBestFit, Is.False);
                Assert.That(body.supportRichText, Is.False);
                Assert.That(body.fontSize * scale, Is.GreaterThanOrEqualTo(12f));
                Canvas.ForceUpdateCanvases();
                Assert.That(body.rectTransform.rect.height + 0.5f, Is.GreaterThanOrEqualTo(body.preferredHeight));
                var scroll = Find<ScrollRect>("HandInspectionOverlay/ReadingPanel/RulesViewport");
                Assert.That(scroll.movementType, Is.EqualTo(ScrollRect.MovementType.Clamped));
                Assert.That(scroll.verticalScrollbarVisibility, Is.EqualTo(ScrollRect.ScrollbarVisibility.AutoHide));
                Assert.That(Field<CanvasGroup>("_handCanvasGroup").interactable, Is.False);
                Invoke("CloseHandInspection");
                Assert.That(match.Revision, Is.EqualTo(revision));
                Assert.That(match.HandCards.Select(value => value.handCardInstanceId), Is.EqualTo(instances));
            }
        }

        [Test]
        public void ArchaeologyReadingFixturePaysItsRealRegisteredCostBeforeShowingChoices()
        {
            var routine = (System.Collections.IEnumerator)typeof(DemoSceneController)
                .GetMethod("SetupArchaeologyPreview", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_controller, null);
            Assert.That(routine.MoveNext(), Is.True);
            var match = Field<DemoLocalMatch>("_match");
            Assert.That(match.Energy, Is.Zero, "The fixture must pay the real cost, not bypass payment.");
            Assert.That(match.PendingChoice?.kind, Is.EqualTo("ARCHAEOLOGY_TOP_3"));
            Assert.That(match.PendingChoice.options, Has.Length.EqualTo(3));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0)?.CardId, Is.EqualTo("db_003"));
        }

        [Test]
        public void PendingCardReaderDoesNotSelectOrConfirmAndClosesWhenItsChoiceChanges()
        {
            var match = Field<DemoLocalMatch>("_match");
            var pending = new PendingChoiceDto { choiceId = "reader-choice", playerId = "local-player", kind = "TOP_CARD_SCRY",
                options = new[] { new PendingChoiceOptionDto { optionIndex = 0, cardId = "nt_002", selectable = true } } };
            typeof(DemoLocalMatch).GetProperty("PendingChoice").GetSetMethod(true).Invoke(match, new object[] { pending });
            Invoke("RefreshAll");
            Invoke("AdvanceChoiceOverlayEntrance", 1f);
            var revision = match.Revision;
            var selected = Field<int>("_selectedChoiceOptionIndex");
            Invoke("OpenChoiceRules", "nt_002");
            Assert.That(Find<RectTransform>("HandInspectionOverlay").gameObject.activeSelf, Is.True);
            var registry = Field<CardContentRegistry>("_registry");
            registry.TryGetText("nt_002", out var cardText);
            Assert.That(Find<Text>("HandInspectionOverlay/ReadingPanel/RulesViewport/FullRules").text, Is.EqualTo(cardText.rulesText));
            Assert.That(Find<Button>("HandInspectionOverlay/ReadingPanel/Next").gameObject.activeSelf, Is.False);
            Assert.That(Field<CanvasGroup>("_choiceOverlayCanvasGroup").interactable, Is.False);
            Invoke("SelectChoiceOption", 0);
            Invoke("ConfirmChoice");
            Assert.That(Field<int>("_selectedChoiceOptionIndex"), Is.EqualTo(selected));
            Assert.That(match.PendingChoice, Is.SameAs(pending));
            Assert.That(match.Revision, Is.EqualTo(revision));
            Invoke("CloseHandInspection");
            Assert.That(Field<CanvasGroup>("_choiceOverlayCanvasGroup").interactable, Is.True);
            Invoke("OpenChoiceRules", "nt_002");
            pending.choiceId = "replacement-choice";
            Invoke("RefreshAll");
            Assert.That(Find<RectTransform>("HandInspectionOverlay").gameObject.activeSelf, Is.False);
        }

        [TestCase("local-player", "db_001")]
        public void PendingReaderRejectsUnknownOptions(string owner, string requested)
        {
            var match = Field<DemoLocalMatch>("_match");
            var pending = new PendingChoiceDto { choiceId = "private-choice", playerId = owner, kind = "TOP_CARD_SCRY",
                options = new[] { new PendingChoiceOptionDto { optionIndex = 0, cardId = "nt_002", selectable = true } } };
            typeof(DemoLocalMatch).GetProperty("PendingChoice").GetSetMethod(true).Invoke(match, new object[] { pending });
            Invoke("RefreshAll");
            Invoke("OpenChoiceRules", requested);
            Assert.That(Find<RectTransform>("HandInspectionOverlay").gameObject.activeSelf, Is.False);
        }

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
            Assert.That(Find<Text>("StatusInspectionOverlay/ReadingPanel/Title").color,
                Is.EqualTo(Find<Text>("StatusPlate/Status").color), "Severity must update even when text is identical.");
            Assert.That(body.color, Is.Not.EqualTo(Find<Text>("StatusPlate/Status").color), "Long-body ink must stay neutral.");
        }

        [Test]
        public void ShortReadingUsesAutoHideAndQuietNeutralBody()
        {
            Invoke("ShowStatus", "红石不足，请选择其他卡牌。", true);
            Invoke("OpenStatusInspection");
            Canvas.ForceUpdateCanvases();
            var scroll = Find<ScrollRect>("StatusInspectionOverlay/ReadingPanel/Viewport");
            scroll.Rebuild(CanvasUpdate.PostLayout);
            Assert.That(scroll.verticalScrollbarVisibility, Is.EqualTo(ScrollRect.ScrollbarVisibility.AutoHide));
            // UGUI vScrollingNeeded deliberately returns true outside play mode.
            // The live Player capture asserts actual visible/hidden state for short and long bodies.
            Assert.That(scroll.content.rect.height, Is.LessThan(scroll.viewport.rect.height));
            Assert.That(Find<Text>("StatusInspectionOverlay/ReadingPanel/Viewport/FullMessage").color,
                Is.Not.EqualTo(Find<Text>("StatusPlate/Status").color));
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
        public void CompatibilityFullReadingKeepsRemedyWhileItsThreeLinePreviewSurvivesResize(float scale)
        {
            _root.transform.Find("DemoCanvas").GetComponent<Canvas>().scaleFactor = scale;
            var failure = ServerCompatibilityFailure.Create(new NakamaConnectionSettings { host = "localhost", port = 17350, serverKey = "private-key" },
                new MatchmakingPreferences(FactionIds.PlainsForest, 42, 48), 39, "prototype-0.64", 41, 47);
            Invoke("HandleOnlineConnectionState", new MatchConnectionStatus(MatchConnectionPhase.Failed,
                "Bearer private-token", compatibilityFailure: failure));
            var summary = Field<DemoReadableSummary>("_statusSummary");
            summary.Refresh();
            Assert.That(Find<Text>("StatusPlate/Status").text, Is.EqualTo(failure.UserSummary));
            Assert.That(summary.FullText, Is.EqualTo(failure.UserDetails));
            Invoke("OpenStatusInspection");
            Assert.That(Find<Text>("StatusInspectionOverlay/ReadingPanel/Viewport/FullMessage").text,
                Is.EqualTo(failure.UserDetails).And.Not.Contain("private-token").And.Not.Contain("private-key"));
            Invoke("CloseStatusInspection");
            Invoke("ShowStatus", "后续对局提示", false);
            Assert.That(summary.FullText, Is.EqualTo("后续对局提示"));
            Assert.That(Find<Text>("StatusPlate/Status").text, Is.EqualTo("后续对局提示"));
        }

        [TestCase(MatchCommandOutcome.Accepted)]
        [TestCase(MatchCommandOutcome.Rejected)]
        [TestCase(MatchCommandOutcome.TimedOut)]
        [TestCase(MatchCommandOutcome.TransportFailed)]
        public void CompletedCommandReadingUsesSafePlayerTextWithoutChangingGameState(MatchCommandOutcome outcome)
        {
            var match = Field<DemoLocalMatch>("_match");
            var beforeRevision = match.Revision;
            var beforeHand = match.HandCards.Count;
            var result = new MatchCommandDispatchResult("private-command", outcome, "INVALID_TARGET", "Bearer private-token", 100000);
            Invoke("HandleOnlineCommandCompleted", result);
            var expected = DemoOnlineFeedback.FormatCommand(result);
            Assert.That(Field<DemoReadableSummary>("_statusSummary").FullText, Is.EqualTo(expected));
            Invoke("OpenStatusInspection");
            Assert.That(Find<Text>("StatusInspectionOverlay/ReadingPanel/Viewport/FullMessage").text,
                Is.EqualTo(expected).And.Not.Contain("private-token").And.Not.Contain("100000"));
            Assert.That(Find<CanvasGroup>("HandPlate/HandCards").interactable, Is.False);
            Invoke("CloseStatusInspection");
            Assert.That(match.Revision, Is.EqualTo(beforeRevision));
            Assert.That(match.HandCards.Count, Is.EqualTo(beforeHand));
        }

        [Test]
        public void ExternalTextReplacementClearsCustomPreviewAndOldFullMessage()
        {
            var summary = Field<DemoReadableSummary>("_statusSummary");
            summary.SetFullText("原完整版本说明", "原三行摘要");
            Find<Text>("StatusPlate/Status").text = "外部新提示";
            Assert.That(summary.FullText, Is.EqualTo("外部新提示"));
            summary.Refresh();
            Assert.That(Find<Text>("StatusPlate/Status").text, Is.EqualTo("外部新提示"));
        }

        [TestCase(MatchConnectionPhase.Ready)]
        [TestCase(MatchConnectionPhase.Authenticating)]
        [TestCase(MatchConnectionPhase.Offline)]
        [TestCase(MatchConnectionPhase.Reconnecting)]
        public void NewConnectionPhaseDoesNotKeepAnOldFailureInTheReadingPanel(MatchConnectionPhase phase)
        {
            Invoke("HandleOnlineConnectionState", new MatchConnectionStatus(MatchConnectionPhase.Failed, "Bearer private-token"));
            Invoke("OpenStatusInspection");
            var status = new MatchConnectionStatus(phase, "Bearer another-private-token", phase == MatchConnectionPhase.Ready ? "private-match" : "");
            Invoke("HandleOnlineConnectionState", status);
            Assert.That(Find<Text>("StatusInspectionOverlay/ReadingPanel/Viewport/FullMessage").text,
                Is.EqualTo(DemoOnlineFeedback.FormatConnectionPhase(status)).And.Not.Contain("private-token"));
        }

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
