using System.Linq;
using System.Reflection;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoUiNavigationTests
    {
        private GameObject _root;
        private EventSystem _system;
        private EventSystem _previousSystem;
        private Canvas _canvas;

        [SetUp]
        public void SetUp()
        {
            _previousSystem = EventSystem.current;
            _root = new GameObject("NavigationTest", typeof(RectTransform), typeof(Canvas));
            _canvas = _root.GetComponent<Canvas>();
            _canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            var events = new GameObject("Events", typeof(EventSystem));
            events.transform.SetParent(_root.transform);
            _system = events.GetComponent<EventSystem>();
            typeof(EventSystem).GetMethod("OnEnable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_system, null);
            EventSystem.current = _system;
        }

        [TearDown]
        public void TearDown()
        {
            _system.SetSelectedGameObject(null);
            typeof(EventSystem).GetMethod("OnDisable", BindingFlags.Instance | BindingFlags.NonPublic).Invoke(_system, null);
            Object.DestroyImmediate(_root);
            if (_previousSystem != null) EventSystem.current = _previousSystem;
        }

        private Button Button(string name)
        {
            var child = new GameObject(name, typeof(RectTransform), typeof(Image), typeof(Button));
            child.transform.SetParent(_root.transform, false);
            child.GetComponent<RectTransform>().sizeDelta = new Vector2(120, 40);
            return child.GetComponent<Button>();
        }

        [Test]
        public void TabCycleSkipsDisabledHiddenAndGroupLockedControlsWithoutClicking()
        {
            var first = Button("First");
            var last = Button("Last");
            var disabled = Button("Disabled"); disabled.interactable = false;
            var hidden = Button("Hidden"); hidden.gameObject.SetActive(false);
            var locked = Button("Locked"); locked.gameObject.AddComponent<CanvasGroup>().interactable = false;
            var clicks = 0;
            first.onClick.AddListener(() => clicks++); last.onClick.AddListener(() => clicks++);
            var controls = new Selectable[] { first, disabled, hidden, locked, first, last, null };
            Assert.That(DemoUiNavigation.FocusNext(_system, controls, false), Is.True);
            Assert.That(_system.currentSelectedGameObject, Is.EqualTo(first.gameObject));
            DemoUiNavigation.FocusNext(_system, controls, false);
            Assert.That(_system.currentSelectedGameObject, Is.EqualTo(last.gameObject));
            DemoUiNavigation.FocusNext(_system, controls, false);
            Assert.That(_system.currentSelectedGameObject, Is.EqualTo(first.gameObject));
            DemoUiNavigation.FocusNext(_system, controls, true);
            Assert.That(_system.currentSelectedGameObject, Is.EqualTo(last.gameObject));
            Assert.That(clicks, Is.Zero);
        }

        [TestCase(false)]
        [TestCase(true)]
        public void ForeignFocusEntersOnlyTheRequestedScope(bool reverse)
        {
            var outside = Button("Outside"); var first = Button("First"); var last = Button("Last");
            _system.SetSelectedGameObject(outside.gameObject);
            DemoUiNavigation.FocusNext(_system, new Selectable[] { first, last }, reverse);
            Assert.That(_system.currentSelectedGameObject, Is.EqualTo((reverse ? last : first).gameObject));
        }

        [Test]
        public void NoEligibleControlsClearsSelectionAndMissingEventSystemIsSafe()
        {
            var button = Button("Disabled"); _system.SetSelectedGameObject(button.gameObject); button.interactable = false;
            Assert.That(DemoUiNavigation.FocusNext(_system, new Selectable[] { button }, false), Is.False);
            Assert.That(_system.currentSelectedGameObject, Is.Null);
            Assert.That(DemoUiNavigation.FocusNext(null, new Selectable[] { button }, false), Is.False);
        }

        [Test]
        public void RestoreFocusUsesOnlyActiveInteractableOriginalOrFallback()
        {
            var original = Button("Original"); var fallback = Button("Fallback");
            DemoUiNavigation.RestoreFocus(_system, original.gameObject, fallback);
            Assert.That(_system.currentSelectedGameObject, Is.EqualTo(original.gameObject));
            original.interactable = false;
            DemoUiNavigation.RestoreFocus(_system, original.gameObject, original, fallback);
            Assert.That(_system.currentSelectedGameObject, Is.EqualTo(fallback.gameObject));
            fallback.gameObject.SetActive(false);
            DemoUiNavigation.RestoreFocus(_system, original.gameObject, fallback);
            Assert.That(_system.currentSelectedGameObject, Is.Null);
        }

        [TestCase(1f)]
        [TestCase(2f / 3f)]
        [TestCase(1024f / 1920f)]
        public void FocusGraphicUsesScreenSizedNonRaycastingCornersOnlyOnEligibleSelection(float scale)
        {
            _canvas.scaleFactor = scale;
            var button = Button("Focused");
            button.GetComponent<RectTransform>().sizeDelta = new Vector2(160, 80);
            var marker = DemoUiFocusIndicator.Attach(button);
            Assert.That(DemoUiFocusIndicator.Attach(button), Is.SameAs(marker));
            Assert.That(marker.raycastTarget, Is.False);
            using (var vertices = new VertexHelper())
            {
                var populate = typeof(DemoUiFocusIndicator).GetMethod("OnPopulateMesh", BindingFlags.Instance | BindingFlags.NonPublic,
                    null, new[] { typeof(VertexHelper) }, null);
                populate.Invoke(marker, new object[] { vertices });
                Assert.That(vertices.currentVertCount, Is.Zero);
                _system.SetSelectedGameObject(button.gameObject);
                marker.RefreshVisual(); populate.Invoke(marker, new object[] { vertices });
                Assert.That(marker.HasVisibleFocus, Is.True);
                Assert.That(vertices.currentVertCount, Is.EqualTo(64));
                var left = new UIVertex(); var right = new UIVertex();
                vertices.PopulateUIVertex(ref left, 32); vertices.PopulateUIVertex(ref right, 34);
                Assert.That((right.position.x - left.position.x) * scale, Is.EqualTo(8f).Within(0.01f));
                Assert.That((right.position.y - left.position.y) * scale, Is.EqualTo(2f).Within(0.01f));
                button.interactable = false;
                marker.RefreshVisual(); populate.Invoke(marker, new object[] { vertices });
                Assert.That(marker.HasVisibleFocus, Is.False);
                Assert.That(vertices.currentVertCount, Is.Zero);
                button.interactable = true;
                button.gameObject.AddComponent<CanvasGroup>().interactable = false;
                marker.RefreshVisual(); populate.Invoke(marker, new object[] { vertices });
                Assert.That(vertices.currentVertCount, Is.Zero);
            }
        }

        [Test]
        public void AttachDoesNotReuseADeactivatedPendingDestroyGraphic()
        {
            var button = Button("Rebound");
            var original = DemoUiFocusIndicator.Attach(button); original.gameObject.SetActive(false);
            var fresh = DemoUiFocusIndicator.Attach(button);
            Assert.That(fresh, Is.Not.SameAs(original));
            Assert.That(button.GetComponentsInChildren<DemoUiFocusIndicator>().Single(), Is.SameAs(fresh));
        }

        [TestCase(800f)]
        [TestCase(150f)]
        public void ScrollIsClampedStopsInertiaAndKeepsShortContentAtTheTop(float height)
        {
            var viewport = new GameObject("Viewport", typeof(RectTransform), typeof(ScrollRect));
            viewport.transform.SetParent(_root.transform, false);
            var viewRect = viewport.GetComponent<RectTransform>(); viewRect.sizeDelta = new Vector2(300, 200);
            var content = new GameObject("Content", typeof(RectTransform)); content.transform.SetParent(viewRect, false);
            var contentRect = content.GetComponent<RectTransform>();
            contentRect.anchorMin = contentRect.anchorMax = contentRect.pivot = new Vector2(0, 1);
            contentRect.sizeDelta = new Vector2(300, height);
            var scroll = viewport.GetComponent<ScrollRect>(); scroll.viewport = viewRect; scroll.content = contentRect;
            scroll.horizontal = false;
            DemoUiNavigation.ScrollToEdge(scroll, false);
            scroll.velocity = new Vector2(0, 50);
            DemoUiNavigation.ScrollVertical(scroll, 100);
            Assert.That(scroll.velocity, Is.EqualTo(Vector2.zero));
            if (height > 200)
                Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f - 100f / (height - 200)).Within(0.01f));
            else Assert.That(contentRect.anchoredPosition.y, Is.EqualTo(0f).Within(0.01f));
            DemoUiNavigation.ScrollToEdge(scroll, true);
            if (height > 200) Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(0f).Within(0.01f));
            else Assert.That(contentRect.anchoredPosition.y, Is.EqualTo(0f).Within(0.01f));
            DemoUiNavigation.ScrollVertical(scroll, -10000);
            if (height > 200) Assert.That(scroll.verticalNormalizedPosition, Is.EqualTo(1f).Within(0.01f));
            else Assert.That(contentRect.anchoredPosition.y, Is.EqualTo(0f).Within(0.01f));
        }

        [Test]
        public void DirectionalNavigationCanBeDisabledWithoutDisablingSubmit()
        {
            var button = Button("Submit"); var count = 0; button.onClick.AddListener(() => count++);
            DemoUiNavigation.DisableDirectionalNavigation(button, null);
            Assert.That(button.navigation.mode, Is.EqualTo(Navigation.Mode.None));
            ExecuteEvents.Execute(button.gameObject, new BaseEventData(_system), ExecuteEvents.submitHandler);
            Assert.That(count, Is.EqualTo(1));
        }
    }
}
