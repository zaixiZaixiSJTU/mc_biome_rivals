using System.Reflection;
using System.Linq;
using BiomeRivals.Content;
using BiomeRivals.Core;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoHoverScaleTests
    {
        [Test]
        public void NewCardChoiceFadesInOnceAndBlocksInputUntilSettled()
        {
            var root = new GameObject("CardChoiceEntranceTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                var match = ReadPrivateField<DemoLocalMatch>(controller, "_match");
                SetPendingChoice(match, new PendingChoiceDto
                {
                    choiceId = "choice-entrance-1",
                    playerId = "local-player",
                    kind = "TOP_CARD_SCRY",
                    options = new[]
                    {
                        new PendingChoiceOptionDto { optionIndex = 0, cardId = "cd_005", selectable = true }
                    }
                });

                InvokePrivate(controller, "RefreshPendingChoice");
                var overlay = root.transform.Find("DemoCanvas/ChoiceOverlay");
                var canvasGroup = overlay.GetComponent<CanvasGroup>();
                Assert.That(overlay.gameObject.activeSelf, Is.True);
                Assert.That(canvasGroup.alpha, Is.EqualTo(0f));
                Assert.That(canvasGroup.interactable, Is.False,
                    "Choice options and confirmation stay disabled during the reveal.");
                Assert.That(canvasGroup.blocksRaycasts, Is.True,
                    "The overlay must cover the board while its contents are still appearing.");

                InvokePrivate(controller, "RefreshPendingChoice");
                Assert.That(canvasGroup.alpha, Is.EqualTo(0f),
                    "A refresh of the same pending choice must not restart or skip its entrance.");
                AdvanceChoiceEntrance(controller, 0.05f);
                Assert.That(canvasGroup.alpha, Is.InRange(0.01f, 0.99f));
                Assert.That(canvasGroup.interactable, Is.False);
                Assert.That(canvasGroup.blocksRaycasts, Is.True);

                AdvanceChoiceEntrance(controller, 0.2f);
                Assert.That(canvasGroup.alpha, Is.EqualTo(1f));
                Assert.That(canvasGroup.interactable, Is.True);
                Assert.That(canvasGroup.blocksRaycasts, Is.True);
                InvokePrivate(controller, "RefreshPendingChoice");
                Assert.That(canvasGroup.alpha, Is.EqualTo(1f),
                    "Selecting or refreshing options within the same choice must not replay the reveal.");

                SetPendingChoice(match, null);
                InvokePrivate(controller, "RefreshPendingChoice");
                Assert.That(overlay.gameObject.activeSelf, Is.False);
                Assert.That(canvasGroup.alpha, Is.EqualTo(1f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SingleChoiceUsesFocusedCardLayoutAndThreeCardChoiceUsesComparisonLayout()
        {
            var root = new GameObject("CardChoiceResponsiveLayoutTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                var match = ReadPrivateField<DemoLocalMatch>(controller, "_match");
                var panel = root.transform.Find("DemoCanvas/ChoiceOverlay/ChoicePanel");

                SetPendingChoice(match, new PendingChoiceDto
                {
                    choiceId = "single-card-choice",
                    playerId = "local-player",
                    kind = "TOP_CARD_SCRY",
                    options = new[]
                    {
                        new PendingChoiceOptionDto { optionIndex = 0, cardId = "cd_005", selectable = true }
                    }
                });
                InvokePrivate(controller, "RefreshPendingChoice");

                Assert.That(panel.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(860, 790)));
                Assert.That(panel.Find("FrameSlice").GetComponent<RectTransform>().sizeDelta,
                    Is.EqualTo(new Vector2(860, 790)), "The pixel frame must resize with its panel.");
                Assert.That(panel.Find("MaterialFill").GetComponent<RectTransform>().sizeDelta,
                    Is.EqualTo(new Vector2(850, 780)), "The tiled stone fill must remain inset inside the frame.");
                var singleSlot = panel.Find("InspectedCards/ChoiceSlot0");
                Assert.That(singleSlot.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(286, 452)));
                Assert.That(singleSlot.GetComponentInChildren<CardUI>().RectTransform.sizeDelta,
                    Is.EqualTo(new Vector2(238, 342)));
                Assert.That(singleSlot.GetComponentInChildren<Button>().interactable, Is.True,
                    "Enlarging the card must preserve its direct selection action.");

                SetPendingChoice(match, new PendingChoiceDto
                {
                    choiceId = "three-card-choice",
                    playerId = "local-player",
                    kind = "ARCHAEOLOGY_TOP_3",
                    options = new[]
                    {
                        new PendingChoiceOptionDto { optionIndex = 0, cardId = "db_004", selectable = false },
                        new PendingChoiceOptionDto { optionIndex = 1, cardId = "tk_006", selectable = true },
                        new PendingChoiceOptionDto { optionIndex = 2, cardId = "db_001", selectable = false }
                    }
                });
                InvokePrivate(controller, "RefreshPendingChoice");

                Assert.That(panel.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(1080, 790)));
                Assert.That(panel.Find("MaterialFill").GetComponent<RectTransform>().sizeDelta,
                    Is.EqualTo(new Vector2(1070, 780)), "The stone fill must remain inset inside the taller comparison panel.");
                Assert.That(panel.Find("InspectedCards").GetComponent<RectTransform>().sizeDelta,
                    Is.EqualTo(new Vector2(780, 480)));
                Assert.That(panel.Find("InspectedCards/ChoiceSlot0").GetComponent<RectTransform>().sizeDelta,
                    Is.EqualTo(new Vector2(254, 452)));
                var archaeologyCards = panel.GetComponentsInChildren<CardUI>(true);
                Assert.That(archaeologyCards, Has.Length.EqualTo(3),
                    "The wider comparison layout must retain all three simultaneously visible options.");
                var registry = CardContentLoader.Load();
                foreach (var archaeologyCard in archaeologyCards)
                {
                    Assert.That(archaeologyCard.IsCompact, Is.False,
                        "Archaeology choices keep the detail card material layout.");
                    Assert.That(registry.TryGetText(archaeologyCard.CardId, out var cardText), Is.True);
                    Assert.That(archaeologyCard.FullRulesText, Is.EqualTo(cardText.rulesText));
                    Assert.That(archaeologyCard.transform.parent.Find("ReadRules").GetComponent<Button>(), Is.Not.Null,
                        "Every visible pending card must have a full-rules entry, including non-selectable options.");
                }
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ArrivalAnimationFadesNewCardAndRestoresItsOriginalInputState()
        {
            var root = new GameObject("CardArrivalAnimationTest", typeof(RectTransform), typeof(CanvasGroup));
            try
            {
                var canvasGroup = root.GetComponent<CanvasGroup>();
                canvasGroup.alpha = 0.85f;
                canvasGroup.interactable = true;
                canvasGroup.blocksRaycasts = true;
                var card = root.AddComponent<CardUI>();

                card.PlayArrivalAnimation(0.24f);
                Assert.That(card.IsArrivalAnimating, Is.True);
                Assert.That(canvasGroup.alpha, Is.EqualTo(0f));
                Assert.That(canvasGroup.interactable, Is.False);
                Assert.That(canvasGroup.blocksRaycasts, Is.False);

                AdvanceArrival(card, 0.08f);
                Assert.That(canvasGroup.alpha, Is.InRange(0.01f, 0.84f));
                Assert.That(canvasGroup.interactable, Is.False);
                Assert.That(canvasGroup.blocksRaycasts, Is.False);

                AdvanceArrival(card, 0.17f);
                Assert.That(card.IsArrivalAnimating, Is.False);
                Assert.That(canvasGroup.alpha, Is.EqualTo(0.85f).Within(0.001f));
                Assert.That(canvasGroup.interactable, Is.True);
                Assert.That(canvasGroup.blocksRaycasts, Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void HoveredHandCardRendersInFrontAndRestoresItsFanOrderOnExit()
        {
            var root = new GameObject("HandCardHoverOrderTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();

                var handRoot = root.transform.Find("DemoCanvas/HandPlate/HandCards");
                var hoveredCard = handRoot.GetComponentsInChildren<CardUI>(true)
                    .First(card => card.transform.GetSiblingIndex() < handRoot.childCount - 1);
                var originalSiblingIndex = hoveredCard.transform.GetSiblingIndex();
                var hover = hoveredCard.GetComponent<DemoHoverScale>();
                Assert.That(hover, Is.Not.Null);
                var restingPosition = hoveredCard.transform.localPosition;
                Assert.That(ReadPrivateField<float>(hover, "hoverScale"), Is.EqualTo(1.22f).Within(0.001f));
                Assert.That(ReadPrivateField<float>(hover, "hoverLift"), Is.EqualTo(32f).Within(0.001f));

                hover.OnPointerEnter(null);
                Assert.That(hoveredCard.transform.GetSiblingIndex(), Is.EqualTo(handRoot.childCount - 1),
                    "Hovered cards render above adjacent cards so enlarged rules text remains visible.");
                Assert.That(ReadPrivateField<Vector3>(hover, "_target"), Is.EqualTo(Vector3.one * 1.22f));
                AssertTargetPosition(hover, restingPosition + Vector3.up * 32f);
                AdvanceMotion(hover, 0.05f);
                var liftedPosition = hoveredCard.transform.localPosition;
                Assert.That(liftedPosition.y, Is.GreaterThan(restingPosition.y),
                    "Hover feedback must physically raise the card instead of only moving its outline or scale.");

                hover.OnPointerExit(null);
                Assert.That(hoveredCard.transform.GetSiblingIndex(), Is.EqualTo(originalSiblingIndex),
                    "Leaving a card restores the fan order rather than permanently reordering the hand.");
                AssertTargetPosition(hover, restingPosition);
                AdvanceMotion(hover, 0.05f);
                Assert.That(hoveredCard.transform.localPosition.y, Is.LessThan(liftedPosition.y),
                    "The lift should smoothly settle back when the pointer leaves the card.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StyledButtonPressFeedbackMovesAndScalesOnlyWhileInteractable()
        {
            var root = new GameObject("StyledButtonFeedbackTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();

                var button = root.transform.Find("DemoCanvas/FactionRail/Faction_desert_badlands")
                    .GetComponent<Button>();
                var feedback = button.GetComponent<DemoHoverScale>();
                Assert.That(feedback, Is.Not.Null,
                    "secondary stone buttons should reuse the same animated feedback component as cards and primary actions");
                Assert.That(button.IsInteractable(), Is.True);
                var restingPosition = button.transform.localPosition;

                button.interactable = false;
                feedback.OnPointerEnter(null);
                feedback.OnPointerDown(null);
                Assert.That(feedback.TargetScale, Is.EqualTo(1f),
                    "disabled buttons must not react to stale or synthetic pointer events");
                AssertTargetPosition(feedback, restingPosition);

                button.interactable = true;
                feedback.OnPointerEnter(null);
                Assert.That(feedback.TargetScale, Is.EqualTo(1.035f).Within(0.001f));
                feedback.OnPointerDown(null);
                Assert.That(feedback.TargetScale, Is.EqualTo(0.97f).Within(0.001f));
                AssertTargetPosition(feedback, restingPosition + Vector3.down * 1.5f);
                AdvanceMotion(feedback, 0.05f);
                Assert.That(button.transform.localScale.x, Is.LessThan(1f),
                    "pressing a button should animate a small physical compression");

                feedback.OnPointerUp(null);
                Assert.That(feedback.TargetScale, Is.EqualTo(1.035f).Within(0.001f),
                    "releasing inside the button restores its hover emphasis");
                feedback.OnPointerExit(null);
                Assert.That(feedback.TargetScale, Is.EqualTo(1f));
                AssertTargetPosition(feedback, restingPosition);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeterministicHoverPreviewKeepsSyntheticPointerStateActive()
        {
            var root = new GameObject("HandCardHoverPreviewTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();

                var eventSystem = root.GetComponentInChildren<EventSystem>(true);
                Assert.That(eventSystem, Is.Not.Null);
                Assert.That(eventSystem.enabled, Is.True);
                var handRoot = root.transform.Find("DemoCanvas/HandPlate/HandCards");
                var selectFaction = typeof(DemoSceneController).GetMethod(
                    "SelectFaction", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(selectFaction, Is.Not.Null);
                selectFaction.Invoke(controller, new object[] { "end" });
                var expectedCard = handRoot.GetComponentsInChildren<CardUI>(true)
                    .First(card => card.CardId.StartsWith("ed_", System.StringComparison.Ordinal));
                var hover = expectedCard.GetComponent<DemoHoverScale>();
                var restingPosition = hover.transform.localPosition;
                var preview = typeof(DemoSceneController).GetMethod(
                    "PreviewHandCardHover", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(preview, Is.Not.Null);
                preview.Invoke(controller, null);

                Assert.That(eventSystem.enabled, Is.True, "The screenshot preview must not disable normal UI input globally.");
                Assert.That(hover.PreviewHoverPinned, Is.True);
                Assert.That(ReadPrivateField<Vector3>(hover, "_target"), Is.EqualTo(Vector3.one * 1.22f));
                AssertTargetPosition(hover, restingPosition + Vector3.up * 32f);
                Assert.That(hover.transform.localScale.x, Is.EqualTo(1.22f).Within(0.001f));
                hover.OnPointerExit(null);
                Assert.That(ReadPrivateField<Vector3>(hover, "_target"), Is.EqualTo(Vector3.one * 1.22f),
                    "A simulated pointer exit cannot cancel a deterministic screenshot preview.");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DeployableCardDragTemporarilyPassesRaycastsAndRestoresItsHandPose()
        {
            var root = new GameObject("HandCardDragLifecycleTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();

                var registryField = typeof(DemoSceneController).GetField("_registry", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(registryField, Is.Not.Null);
                var registry = (BiomeRivals.Content.CardContentRegistry)registryField.GetValue(controller);
                var handRoot = root.transform.Find("DemoCanvas/HandPlate/HandCards");
                var card = handRoot.GetComponentsInChildren<CardUI>(true).First(value => value.CardId == "pf_001");
                var font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Arial" }, 16);
                var beginCount = 0;
                var updateCount = 0;
                var endPosition = Vector2.zero;
                card.Bind(registry, "pf_001", card.RectTransform.sizeDelta, true, font, () => { }, null,
                    card.HandCardInstanceId,
                    () => beginCount++, position => { updateCount++; endPosition = position; },
                    position => endPosition = position);
                card.RectTransform.anchoredPosition = new Vector2(137f, -24f);
                card.RectTransform.localRotation = Quaternion.Euler(0f, 0f, -4f);
                var hover = card.GetComponent<DemoHoverScale>();
                hover.Configure(1.22f, 16f, raiseToFrontOnHover: true, lift: 32f);
                var originalSiblingIndex = card.transform.GetSiblingIndex();
                hover.OnPointerEnter(null);
                var originalLocalPosition = hover.transform.localPosition;
                var originalLocalRotation = hover.transform.localRotation;
                AdvanceMotion(hover, 0.05f);

                var eventSystem = root.GetComponentInChildren<EventSystem>(true);
                Assert.That(eventSystem, Is.Not.Null);
                var pointer = new PointerEventData(eventSystem)
                {
                    position = RectTransformUtility.WorldToScreenPoint(null, card.RectTransform.position),
                    button = PointerEventData.InputButton.Left
                };
                card.OnBeginDrag(pointer);

                var canvasGroup = card.GetComponent<CanvasGroup>();
                Assert.That(beginCount, Is.EqualTo(1));
                Assert.That(canvasGroup, Is.Not.Null);
                Assert.That(canvasGroup.blocksRaycasts, Is.False,
                    "The dragged card must stop blocking the battlefield's 3D pointer raycast.");
                Assert.That(card.transform.localPosition, Is.EqualTo(originalLocalPosition));
                Assert.That(card.transform.localScale, Is.EqualTo(Vector3.one));
                var dragOriginWorldPosition = card.transform.position;

                var draggedPosition = pointer.position + new Vector2(96f, 72f);
                pointer.position = draggedPosition;
                card.OnDrag(pointer);
                Assert.That(updateCount, Is.EqualTo(1));
                Assert.That(endPosition, Is.EqualTo(draggedPosition));
                Assert.That(card.transform.position, Is.Not.EqualTo(dragOriginWorldPosition));

                var dropPosition = pointer.position + new Vector2(11f, -9f);
                pointer.position = dropPosition;
                card.OnEndDrag(pointer);

                Assert.That(endPosition, Is.EqualTo(dropPosition));
                Assert.That(canvasGroup.blocksRaycasts, Is.True);
                Assert.That(card.transform.localPosition, Is.EqualTo(originalLocalPosition));
                Assert.That(card.transform.localRotation, Is.EqualTo(originalLocalRotation));
                Assert.That(card.transform.localScale, Is.EqualTo(Vector3.one));
                Assert.That(card.transform.GetSiblingIndex(), Is.EqualTo(originalSiblingIndex));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static void AssertTargetPosition(DemoHoverScale hover, Vector3 expected)
        {
            var targetField = typeof(DemoHoverScale).GetField("_targetLocalPosition",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(targetField, Is.Not.Null);
            Assert.That((Vector3)targetField.GetValue(hover), Is.EqualTo(expected));
        }

        private static T ReadPrivateField<T>(DemoHoverScale hover, string name)
        {
            var field = typeof(DemoHoverScale).GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(hover);
        }

        private static T ReadPrivateField<T>(object owner, string name)
        {
            var field = owner.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null);
            return (T)field.GetValue(owner);
        }

        private static void SetPendingChoice(DemoLocalMatch match, PendingChoiceDto choice)
        {
            var property = typeof(DemoLocalMatch).GetProperty("PendingChoice");
            Assert.That(property, Is.Not.Null);
            var setter = property.GetSetMethod(true);
            Assert.That(setter, Is.Not.Null);
            setter.Invoke(match, new object[] { choice });
        }

        private static void InvokePrivate(object owner, string name, params object[] arguments)
        {
            var method = owner.GetType().GetMethod(name, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(method, Is.Not.Null);
            method.Invoke(owner, arguments);
        }

        private static void AdvanceChoiceEntrance(DemoSceneController controller, float deltaTime) =>
            InvokePrivate(controller, "AdvanceChoiceOverlayEntrance", deltaTime);

        private static void AdvanceMotion(DemoHoverScale hover, float deltaTime)
        {
            var advanceMethod = typeof(DemoHoverScale).GetMethod("AdvanceMotion",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(advanceMethod, Is.Not.Null);
            advanceMethod.Invoke(hover, new object[] { deltaTime });
        }

        private static void AdvanceArrival(CardUI card, float deltaTime)
        {
            var advanceMethod = typeof(CardUI).GetMethod("AdvanceArrivalAnimation",
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(advanceMethod, Is.Not.Null);
            advanceMethod.Invoke(card, new object[] { deltaTime });
        }
    }
}
