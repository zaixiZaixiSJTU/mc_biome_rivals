using System.Reflection;
using BiomeRivals.Bootstrap;
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
            Assert.That(Find<Text>("StatusInspectionOverlay/ReadingPanel/Title").text, Is.EqualTo("操作说明 · 完整内容"));
            Assert.That(Find<Text>("StatusInspectionOverlay/ReadingPanel/Viewport/FullMessage").text, Is.Not.Empty);
            Assert.That(Find<CanvasGroup>("HandPlate/HandCards").interactable, Is.False);
            Assert.That(_root.GetComponent<DemoBattlefieldPointerController>().InputEnabled, Is.False);
            Invoke("CloseStatusInspection");
            Invoke("OpenStatusInspection");
            Assert.That(Find<Text>("StatusInspectionOverlay/ReadingPanel/Viewport/FullMessage").text, Is.EqualTo("当前对局提示"));
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
