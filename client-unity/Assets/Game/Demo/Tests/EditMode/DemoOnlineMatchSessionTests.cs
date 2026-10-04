using System;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BiomeRivals.Bootstrap;
using BiomeRivals.Content;
using BiomeRivals.Core;
using BiomeRivals.Networking;
using BiomeRivals.Presentation;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.UI;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoOnlineMatchSessionTests
    {
        [Test]
        public void AuthoritativeViewAlwaysOrientsViewerOnPlayerSide()
        {
            var store = CreateStore(viewerIndex: 1);
            var view = new DemoAuthoritativeMatchView(store);

            Assert.That(view.IsAuthoritative, Is.True);
            Assert.That(view.ViewerIndex, Is.EqualTo(1));
            Assert.That(view.Hand, Is.EqualTo(new[] { "nt_001" }));
            Assert.That(view.PlayerLife, Is.EqualTo(27));
            Assert.That(view.OpponentLife, Is.EqualTo(30));
            Assert.That(view.PlayerArmor, Is.EqualTo(3));
            Assert.That(view.OpponentArmor, Is.EqualTo(2));
            Assert.That(view.Energy, Is.EqualTo(2));
            Assert.That(view.IsPlayerTurn, Is.True);
            Assert.That(view.PlayerFactionId, Is.EqualTo(FactionIds.End));
            Assert.That(view.OpponentFactionId, Is.EqualTo(FactionIds.OceanRiver));
        }

        [Test]
        public void OpponentTopCardChoiceRendersOnlyHiddenBacksAndWaitState()
        {
            var root = new GameObject("OpponentPrivateChoiceUiTest");
            DemoOnlineMatchSession session = null;
            DemoSceneController controller = null;
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                controller = root.AddComponent<DemoSceneController>();
                var compositionRootInstance = typeof(GameCompositionRoot).GetProperty("Instance");
                var compositionRootSetter = compositionRootInstance.GetSetMethod(true);
                var previousCompositionRoot = compositionRootInstance.GetValue(null);
                compositionRootSetter.Invoke(null, new object[] { null });
                try
                {
                    controller.BuildNow();
                }
                finally
                {
                    compositionRootSetter.Invoke(null, new[] { previousCompositionRoot });
                }

                var store = new MatchStateStore();
                store.Replace(new MatchStateDto
                {
                    matchId = "private-choice-ui-match",
                    viewerPlayerId = "bob",
                    protocolVersion = GameVersions.Protocol,
                    rulesetVersion = GameVersions.Ruleset,
                    status = "ACTIVE",
                    phase = "MAIN",
                    turn = 1,
                    activePlayerIndex = 0,
                    players = new[]
                    {
                        new PlayerStateDto
                        {
                            playerId = "alice",
                            factionId = FactionIds.CaveDarkForest,
                            unitSlots = new[] { "object-bat", null, null, null },
                            buildingSlots = new string[3],
                            battlefield = new[]
                            {
                                new BattlefieldObjectStateDto
                                {
                                    instanceId = "object-bat", ownerPlayerId = "alice", cardId = "cd_001",
                                    cardType = "UNIT", slotKind = "UNIT", slotIndex = 0, occupiedSlots = 1,
                                    attack = 1, health = 2, maxHealth = 2
                                }
                            }
                        },
                        new PlayerStateDto
                        {
                            playerId = "bob",
                            factionId = FactionIds.PlainsForest,
                            unitSlots = new string[4],
                            buildingSlots = new string[3]
                        }
                    },
                    pendingChoice = new PendingChoiceDto
                    {
                        choiceId = "private-top-card-choice",
                        playerId = "alice",
                        sourceCardId = "cd_001",
                        sourceInstanceId = "object-bat",
                        effectId = "effect.cd_001.01",
                        kind = "TOP_CARD_SCRY",
                        options = new[]
                        {
                            new PendingChoiceOptionDto
                            {
                                optionIndex = 0,
                                cardId = string.Empty,
                                slotIndex = -1,
                                selectable = false
                            }
                        }
                    }
                });

                session = new DemoOnlineMatchSession(new FakeGateway(), store);
                typeof(DemoSceneController).GetField("_onlineSession", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, session);
                typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);

                var overlay = root.transform.Find("DemoCanvas/ChoiceOverlay");
                var choicePanel = overlay.Find("ChoicePanel");
                var cardsRoot = choicePanel.Find("InspectedCards");
                var hiddenSlot = cardsRoot.Find("HiddenChoiceSlot0");
                Assert.That(session.View.IsChoiceOwner, Is.False);
                Assert.That(overlay.gameObject.activeSelf, Is.True);
                Assert.That(choicePanel.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(860, 790)),
                    "A single hidden option uses the same focused panel dimensions as the owner's single-card choice.");
                Assert.That(hiddenSlot, Is.Not.Null);
                Assert.That(hiddenSlot.GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(286, 452)));
                Assert.That(cardsRoot.GetComponentsInChildren<CardUI>(true), Is.Empty,
                    "The non-owner must never instantiate a card face from a private choice option.");
                Assert.That(hiddenSlot.Find("HiddenLabel").GetComponent<Text>().text, Is.EqualTo("牌库信息保密"));
                Assert.That(overlay.GetComponentsInChildren<Text>(true)
                    .Any(value => value.text.Contains("林地卫道士") || value.text.Contains("cd_005")), Is.False);
                Assert.That(overlay.GetComponentsInChildren<Button>(true)
                    .Any(value => value.isActiveAndEnabled && value.interactable), Is.False,
                    "The observer sees a wait state and receives no choice action.");
                Assert.That(overlay.GetComponent<CanvasGroup>().blocksRaycasts, Is.True,
                    "The pending private choice must keep gameplay input behind the modal blocked.");
            }
            finally
            {
                if (controller != null)
                    typeof(DemoSceneController).GetField("_onlineSession", BindingFlags.Instance | BindingFlags.NonPublic)
                        .SetValue(controller, null);
                session?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AuthoritativeViewExposesTemporaryEnergyWithoutChangingBaseCapacity()
        {
            var store = CreateStore(viewerIndex: 1);
            store.Current.players[1].temporaryRedstone = 2;
            store.Current.players[1].totalRedstone = 4;
            var view = new DemoAuthoritativeMatchView(store);

            Assert.That(view.Energy, Is.EqualTo(4));
            Assert.That(view.TemporaryEnergy, Is.EqualTo(2));
            Assert.That(view.MaxEnergy, Is.EqualTo(2));
        }

        [Test]
        public void AuthoritativeViewDerivesRaiderCostFromSnapshotTurnMarker()
        {
            var store = CreateStore(viewerIndex: 0);
            var view = new DemoAuthoritativeMatchView(store);
            Assert.That(CardContentLoader.Load().TryGetDefinition("db_005", out var raider), Is.True);

            Assert.That(view.GetEffectiveCost(raider), Is.EqualTo(3));
            store.Current.players[0].excavatedThisTurn = true;

            Assert.That(view.ExcavatedThisTurn, Is.True);
            Assert.That(view.GetEffectiveCost(raider), Is.EqualTo(2));
        }

        [Test]
        public void AuthoritativeViewUsesStableHandInstanceForDuplicateFees()
        {
            var store = CreateStore(viewerIndex: 0);
            var player = store.Current.players[0];
            player.hand = new[] { "pf_001", "pf_001" };
            player.handCards = new[]
            {
                new HandCardStateDto { handCardInstanceId = "hand-11", cardId = "pf_001", costModifier = -1,
                    expiresAtEndOfTurnPlayerId = "alice" },
                new HandCardStateDto { handCardInstanceId = "hand-12", cardId = "pf_001", costModifier = 0 }
            };
            var view = new DemoAuthoritativeMatchView(store);
            Assert.That(CardContentLoader.Load().TryGetDefinition("pf_001", out var definition), Is.True);

            Assert.That(view.GetEffectiveCost(definition, "hand-11"), Is.EqualTo(Math.Max(0, definition.cost - 1)));
            Assert.That(view.GetEffectiveCost(definition, "hand-12"), Is.EqualTo(definition.cost));
            Assert.That(view.GetEffectiveCost(definition, "hand-99"), Is.EqualTo(definition.cost));
        }

        [Test]
        public void EventHandProjectionReplacesExactInstancesWithoutRevealingOpponentCards()
        {
            var store = CreateStore(viewerIndex: 0);
            store.Apply(new MatchEventBatchDto
            {
                protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset,
                revision = 1,
                events = System.Array.Empty<MatchEventDto>(),
                handProjection = new HandProjectionDto
                {
                    ownPlayerId = "alice", ownHand = new[] { "pf_001" },
                    ownHandCards = new[] { new HandCardStateDto { handCardInstanceId = "hand-17", cardId = "pf_001" } },
                    opponentPlayerId = "bob", opponentHandCount = 2
                }
            });

            Assert.That(store.Current.players[0].handCards[0].handCardInstanceId, Is.EqualTo("hand-17"));
            Assert.That(store.Current.players[1].hand.Length, Is.EqualTo(2));
            Assert.That(store.Current.players[1].hand[0], Is.Null);
            Assert.That(store.Current.players[1].handCards[0], Is.Null);
        }

        [Test]
        public void AuthoritativeViewProjectsWoolHealthAfterRecoverySnapshotReplacement()
        {
            var store = CreateStore(viewerIndex: 0);
            var recovered = new MatchStateDto
            {
                matchId = "match-1", viewerPlayerId = "alice", protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset, revision = 2, lastEventId = 5,
                status = "ACTIVE", turn = 1, phase = "MAIN", activePlayerIndex = 0,
                players = new[]
                {
                    new PlayerStateDto
                    {
                        playerId = "alice", factionId = FactionIds.OceanRiver,
                        unitSlots = new[] { "object-1", null, null, null }, buildingSlots = new string[3],
                        battlefield = new[]
                        {
                            new BattlefieldObjectStateDto
                            {
                                instanceId = "object-1", ownerPlayerId = "alice", cardId = "pf_002", cardType = "UNIT",
                                slotKind = "UNIT", slotIndex = 0, occupiedSlots = 1,
                                attack = 1, health = 4, maxHealth = 4, summonedTurn = 1,
                                temporaryHealthModifier = 1, temporaryHealthModifierExpiresOnTurn = 1
                            }
                        }
                    },
                    new PlayerStateDto { playerId = "bob", factionId = FactionIds.End,
                        unitSlots = new string[4], buildingSlots = new string[3] }
                }
            };

            store.Replace(recovered);
            var view = new DemoAuthoritativeMatchView(store);
            var sheep = view.GetObject(true, DemoSlotKind.Unit, 0);
            Assert.That(view.Revision, Is.EqualTo(2));
            Assert.That(sheep, Is.Not.Null);
            Assert.That(sheep.Health, Is.EqualTo(4));
            Assert.That(sheep.MaxHealth, Is.EqualTo(4));
            Assert.That(sheep.TemporaryHealthModifier, Is.EqualTo(1));
            Assert.That(sheep.TemporaryHealthModifierExpiresOnRound, Is.EqualTo(1));
        }

        [Test]
        public async Task OnlineSessionWaitsForAuthoritativeDeployAcknowledgement()
        {
            var store = CreateStore(viewerIndex: 0);
            var gateway = new FakeGateway();
            using (var session = new DemoOnlineMatchSession(gateway, store))
            {
                var pending = session.DeployAsync("pf_001", DemoSlotKind.Unit, 2, handCardInstanceId: "hand-1");

                Assert.That(session.HasPendingCommand, Is.True);
                Assert.That(gateway.LastCommand, Is.Not.Null);
                Assert.That(gateway.LastCommand.expectedRevision, Is.Zero);
                Assert.That(gateway.LastCommand.payload.slotIndex, Is.EqualTo(2));
                Assert.That(gateway.LastCommand.payload.handCardInstanceId, Is.EqualTo("hand-1"));

                var batch = new MatchEventBatchDto
                {
                    protocolVersion = GameVersions.Protocol,
                    rulesetVersion = GameVersions.Ruleset,
                    revision = 1,
                    acknowledgedCommandId = gateway.LastCommand.commandId,
                    events = new[]
                    {
                        new MatchEventDto
                        {
                            eventId = 1,
                            type = MatchEventTypes.CardDeployed,
                            payload = new MatchEventPayloadDto
                            {
                                playerId = "alice", instanceId = "object-1", cardId = "pf_001", cardType = "UNIT",
                                slotKind = "UNIT", slotIndex = 2, occupiedSlots = 1,
                                paymentMethod = MatchPaymentMethods.Redstone, redstone = 0,
                                attack = 1, health = 2, maxHealth = 2, summonedTurn = 1,
                                keywords = Array.Empty<string>(), nextInstanceId = 2
                            }
                        }
                    }
                };
                store.Apply(batch);
                gateway.Emit(batch);

                var result = await pending;
                Assert.That(result.Outcome, Is.EqualTo(MatchCommandOutcome.Accepted));
                Assert.That(session.View.UnitSlots[2], Is.EqualTo("pf_001"));
                Assert.That(session.View.GetObject(true, DemoSlotKind.Unit, 2).InstanceId, Is.EqualTo("object-1"));
                Assert.That(session.View.Hand, Is.Empty);
                Assert.That(session.HasPendingCommand, Is.False);
            }
        }

        [Test]
        public async Task OnlineSessionPlayCommandCarriesSelectedHandAndTargetInstances()
        {
            var store = CreateStore(viewerIndex: 0);
            var gateway = new FakeGateway();
            using (var session = new DemoOnlineMatchSession(gateway, store))
            {
                var pending = session.PlayCardAsync("tk_016", "hand-1", "UNIT", "object-9");

                Assert.That(gateway.LastCommand, Is.Not.Null);
                Assert.That(gateway.LastCommand.type, Is.EqualTo(MatchCommandTypes.PlayCard));
                Assert.That(gateway.LastCommand.payload.handCardInstanceId, Is.EqualTo("hand-1"));
                Assert.That(gateway.LastCommand.payload.cardId, Is.EqualTo("tk_016"));
                Assert.That(gateway.LastCommand.payload.targetType, Is.EqualTo("UNIT"));
                Assert.That(gateway.LastCommand.payload.targetInstanceId, Is.EqualTo("object-9"));

                var batch = new MatchEventBatchDto
                {
                    protocolVersion = GameVersions.Protocol,
                    rulesetVersion = GameVersions.Ruleset,
                    revision = 1,
                    acknowledgedCommandId = gateway.LastCommand.commandId,
                    events = Array.Empty<MatchEventDto>(),
                    handProjection = new HandProjectionDto
                    {
                        ownPlayerId = "alice", ownHand = Array.Empty<string>(),
                        ownHandCards = Array.Empty<HandCardStateDto>(),
                        opponentPlayerId = "bob", opponentHandCount = 1
                    }
                };
                store.Apply(batch);
                gateway.Emit(batch);

                var result = await pending;
                Assert.That(result.Outcome, Is.EqualTo(MatchCommandOutcome.Accepted));
                Assert.That(session.View.Hand, Is.Empty);
                Assert.That(session.HasPendingCommand, Is.False);
            }
        }

        [Test]
        public async Task OnlineSessionSendsOpeningHandSelectionAtCurrentRevision()
        {
            var store = CreateStore(viewerIndex: 0);
            store.Current.status = "MULLIGAN";
            var gateway = new FakeGateway();
            using (var session = new DemoOnlineMatchSession(gateway, store))
            {
                _ = session.MulliganAsync(new[] { 1, 2 });

                Assert.That(gateway.LastCommand.type, Is.EqualTo(MatchCommandTypes.Mulligan));
                Assert.That(gateway.LastCommand.expectedRevision, Is.Zero);
                Assert.That(gateway.LastCommand.payload.cardIndices, Is.EqualTo(new[] { 1, 2 }));
                await Task.Yield();
            }
        }

        [Test]
        public async Task OnlineSessionSendsConcedeAtCurrentRevision()
        {
            var store = CreateStore(viewerIndex: 0);
            store.Current.revision = 9;
            var gateway = new FakeGateway();
            using (var session = new DemoOnlineMatchSession(gateway, store))
            {
                _ = session.ConcedeAsync();

                Assert.That(gateway.LastCommand.type, Is.EqualTo(MatchCommandTypes.Concede));
                Assert.That(gateway.LastCommand.expectedRevision, Is.EqualTo(9));
                Assert.That(gateway.LastCommand.payload, Is.Not.Null);
                await Task.Yield();
            }
        }

        [Test]
        public void OnlineSessionRejectsSecondCommandWhileFirstIsPending()
        {
            var store = CreateStore(viewerIndex: 0);
            var gateway = new FakeGateway();
            using (var session = new DemoOnlineMatchSession(gateway, store))
            {
                _ = session.EnterCombatAsync();

                Assert.That(session.CanIssueCommand, Is.False);
                Assert.ThrowsAsync<InvalidOperationException>(async () => await session.EndTurnAsync());
            }
        }

        [UnityTest]
        public System.Collections.IEnumerator PendingOnlineCommandDisablesHeroAndHandInputsUntilRejection()
        {
            var root = new GameObject("PendingOnlineCommandInputTest");
            DemoSceneController controller = null;
            DemoOnlineMatchSession session = null;
            var onlineSessionField = typeof(DemoSceneController).GetField("_onlineSession", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                controller = root.AddComponent<DemoSceneController>();

                var compositionRootInstance = typeof(GameCompositionRoot).GetProperty("Instance");
                var compositionRootSetter = compositionRootInstance.GetSetMethod(true);
                var previousCompositionRoot = compositionRootInstance.GetValue(null);
                compositionRootSetter.Invoke(null, new object[] { null });
                try
                {
                    controller.BuildNow();
                }
                finally
                {
                    compositionRootSetter.Invoke(null, new[] { previousCompositionRoot });
                }

                var gateway = new FakeGateway();
                var onlineStore = CreateStore(viewerIndex: 0);
                onlineStore.Current.players[0].hand[0] = "ed_005";
                onlineStore.Current.players[0].handCards[0].cardId = "ed_005";
                session = new DemoOnlineMatchSession(gateway, onlineStore);
                onlineSessionField.SetValue(controller, session);
                var pointer = root.GetComponent<DemoBattlefieldPointerController>();
                Physics.SyncTransforms();
                var slotScreenPosition = battlefield.BoardCamera.WorldToScreenPoint(
                    battlefield.GetSlotInteractionWorldPosition(true, DemoSlotKind.Unit, 0));
                Assert.That(pointer.ProcessPointerFrame(slotScreenPosition, false, false, false), Is.Not.Null,
                    "The battlefield pointer should be active before an online command is sent.");
                pointer.ProcessPointerFrame(slotScreenPosition, false, true, false);
                var pointerHoveredField = typeof(DemoBattlefieldPointerController).GetField("_hovered", BindingFlags.Instance | BindingFlags.NonPublic);
                var pointerPressedField = typeof(DemoBattlefieldPointerController).GetField("_pressed", BindingFlags.Instance | BindingFlags.NonPublic);
                var selectedCardField = typeof(DemoSceneController).GetField("_selectedCardId", BindingFlags.Instance | BindingFlags.NonPublic);
                selectedCardField.SetValue(controller, "ed_005");
                typeof(DemoSceneController).GetField("_selectedHandCardInstanceId", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, "hand-1");
                var handlePending = typeof(DemoSceneController).GetMethod("HandleOnlineCommandPending", BindingFlags.Instance | BindingFlags.NonPublic);
                var handleCompleted = typeof(DemoSceneController).GetMethod("HandleOnlineCommandCompleted", BindingFlags.Instance | BindingFlags.NonPublic);
                session.CommandPending += commandId => handlePending.Invoke(controller, new object[] { commandId });
                session.CommandCompleted += result => handleCompleted.Invoke(controller, new object[] { result });
                typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);

                var heroButton = (Button)typeof(DemoSceneController).GetField("_playerHeroButton", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(controller);
                var opponentHeroButton = (Button)typeof(DemoSceneController).GetField("_opponentHeroTargetButton", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(controller);
                var endTurnButton = (Button)typeof(DemoSceneController).GetField("_endTurnButton", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(controller);
                var handCanvasGroup = (CanvasGroup)typeof(DemoSceneController).GetField("_handCanvasGroup", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(controller);
                Assert.That(heroButton.interactable, Is.False,
                    "Hero attacks are not available during the main phase.");
                Assert.That(opponentHeroButton.interactable, Is.False,
                    "The enemy hero is not a valid target until combat and an attacker are selected.");
                Assert.That(endTurnButton.interactable, Is.True);
                Assert.That(handCanvasGroup.interactable, Is.True);

                var pending = session.EnterCombatAsync();
                Assert.That(session.HasPendingCommand, Is.True);
                Assert.That(pointer.InputEnabled, Is.False,
                    "The 3D battlefield pointer must lock at the same time as the HUD and hand.");
                Assert.That(pointerHoveredField.GetValue(pointer), Is.Null,
                    "Entering pending state clears stale in-world hover feedback.");
                Assert.That(pointerPressedField.GetValue(pointer), Is.Null,
                    "Entering pending state clears a pressed slot so it cannot click after the response.");
                Assert.That(pointer.ProcessPointerFrame(slotScreenPosition, false, false, false), Is.Null,
                    "The pointer must not raycast or reintroduce hover feedback while an online command is unresolved.");
                typeof(DemoSceneController).GetMethod("OnSlotHovered", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { true, DemoSlotKind.Unit, 0, true });
                typeof(DemoSceneController).GetMethod("OnSlotPressed", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { true, DemoSlotKind.Unit, 0, true });
                Assert.That(pointerHoveredField.GetValue(pointer), Is.Null);
                Assert.That(pointerPressedField.GetValue(pointer), Is.Null);
                Assert.That(heroButton.interactable, Is.False,
                    "The hero HUD is a gameplay input and must lock with all other actions while the server decides the command.");
                Assert.That(opponentHeroButton.interactable, Is.False,
                    "The opponent hero target must not appear actionable while an online command is unresolved.");
                Assert.That(endTurnButton.interactable, Is.False);
                Assert.That(handCanvasGroup.interactable, Is.False);
                Assert.That(handCanvasGroup.blocksRaycasts, Is.False);
                Assert.That(handCanvasGroup.alpha, Is.EqualTo(1f).Within(0.001f),
                    "server-pending input lock must preserve card information opacity");
                Assert.That(endTurnButton.GetComponentInChildren<Text>(true).text, Is.EqualTo("等待服务器"));
                typeof(DemoSceneController).GetMethod("OpenHandInspection", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                Assert.That(controller.transform.Find("DemoCanvas/HandInspectionOverlay").gameObject.activeSelf, Is.True);
                Assert.That(session.HasPendingCommand, Is.True);
                Assert.That(session.CanIssueCommand, Is.False);
                Assert.That(pointer.InputEnabled, Is.False);
                typeof(DemoSceneController).GetMethod("CloseHandInspection", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                Assert.That(handCanvasGroup.interactable, Is.False);
                Assert.That(handCanvasGroup.blocksRaycasts, Is.False);
                Assert.That(pointer.InputEnabled, Is.False, "closing read-only UI must not release a pending server lock");
                var pendingTargetField = typeof(DemoSceneController).GetField("_pendingTargetCardId", BindingFlags.Instance | BindingFlags.NonPublic);
                var cancelTarget = typeof(DemoSceneController).GetMethod("CancelTargetSelection", BindingFlags.Instance | BindingFlags.NonPublic);
                var cancelCurrent = typeof(DemoSceneController).GetMethod("CancelCurrentInteraction", BindingFlags.Instance | BindingFlags.NonPublic);
                pendingTargetField.SetValue(controller, "ed_005");
                typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                var inspectorRoot = (RectTransform)typeof(DemoSceneController).GetField("_inspectorRoot", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(controller);
                var targetedCastButton = inspectorRoot.Find("Cast").GetComponent<Button>();
                Assert.That(targetedCastButton.interactable, Is.False,
                    "A targeted spell's cancel button must be visibly locked while an online command is pending.");
                Assert.That(cancelCurrent.Invoke(controller, null), Is.False,
                    "Esc/right-click cancellation must not mutate a staged action while a server command is unresolved.");
                cancelTarget.Invoke(controller, null);
                Assert.That(pendingTargetField.GetValue(controller), Is.EqualTo("ed_005"),
                    "Direct UI cancellation callbacks must obey the same pending-command lock as keyboard cancellation.");

                var selectedHandCardField = typeof(DemoSceneController).GetField("_selectedHandCardInstanceId", BindingFlags.Instance | BindingFlags.NonPublic);
                var paymentMethodField = typeof(DemoSceneController).GetField("_selectedPaymentMethod", BindingFlags.Instance | BindingFlags.NonPublic);
                var pendingCommand = gateway.LastCommand;
                typeof(DemoSceneController).GetMethod("SelectHandCardInternal", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { "nt_001", "hand-other", false });
                typeof(DemoSceneController).GetMethod("SelectPaymentMethod", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { MatchPaymentMethods.Crafting });
                typeof(DemoSceneController).GetMethod("OnSlotClicked", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { true, DemoSlotKind.Unit, 0 });
                typeof(DemoSceneController).GetMethod("OnEndTurn", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                Assert.That(selectedCardField.GetValue(controller), Is.EqualTo("ed_005"),
                    "A queued hand-card callback must not replace the selection while a command is unresolved.");
                Assert.That(selectedHandCardField.GetValue(controller), Is.EqualTo("hand-1"));
                Assert.That(paymentMethodField.GetValue(controller), Is.EqualTo(MatchPaymentMethods.Redstone),
                    "Payment controls are also immutable while the command is pending.");
                Assert.That(pendingTargetField.GetValue(controller), Is.EqualTo("ed_005"),
                    "Queued slot and end-turn callbacks must preserve the staged target until the command settles.");
                Assert.That(gateway.LastCommand, Is.SameAs(pendingCommand),
                    "A stale end-turn callback must not submit another command.");

                gateway.Reject(new CommandRejectionDto
                {
                    commandId = gateway.LastCommand.commandId,
                    code = "TEST_REJECTED",
                    message = "test rejection",
                    revision = 0
                });
                while (!pending.IsCompleted) yield return null;
                var result = pending.GetAwaiter().GetResult();

                Assert.That(result.Outcome, Is.EqualTo(MatchCommandOutcome.Rejected));
                Assert.That(session.HasPendingCommand, Is.False);
                Assert.That(pointer.InputEnabled, Is.True,
                    "The battlefield pointer should become available again when the command settles.");
                Assert.That(pointer.ProcessPointerFrame(slotScreenPosition, false, false, false), Is.Not.Null,
                    "The same pointer position should resume in-world hover feedback after rejection.");
                Assert.That(heroButton.interactable, Is.False,
                    "Rejecting a command restores the current main-phase legality, not an unavailable hero attack.");
                Assert.That(opponentHeroButton.interactable, Is.False);
                Assert.That(endTurnButton.interactable, Is.True);
                Assert.That(handCanvasGroup.interactable, Is.True);
                Assert.That(cancelCurrent.Invoke(controller, null), Is.True,
                    "Once the server rejects and settles the command, staged input can be canceled normally.");
                Assert.That(pendingTargetField.GetValue(controller), Is.Null);
            }
            finally
            {
                if (controller != null) onlineSessionField.SetValue(controller, null);
                session?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [UnityTest]
        public System.Collections.IEnumerator HeroAttackControlsFollowCombatLegalityAndPendingState()
        {
            var root = new GameObject("HeroAttackControlStateTest");
            DemoSceneController controller = null;
            DemoOnlineMatchSession session = null;
            var onlineSessionField = typeof(DemoSceneController).GetField("_onlineSession", BindingFlags.Instance | BindingFlags.NonPublic);
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                controller = root.AddComponent<DemoSceneController>();

                var compositionRootInstance = typeof(GameCompositionRoot).GetProperty("Instance");
                var compositionRootSetter = compositionRootInstance.GetSetMethod(true);
                var previousCompositionRoot = compositionRootInstance.GetValue(null);
                compositionRootSetter.Invoke(null, new object[] { null });
                try
                {
                    controller.BuildNow();
                }
                finally
                {
                    compositionRootSetter.Invoke(null, new[] { previousCompositionRoot });
                }

                var store = CreateStore(viewerIndex: 0);
                store.Current.players[0].equipment = new EquipmentStateDto
                {
                    instanceId = "weapon-1", cardId = "or_006", attack = 2, durability = 2, maxDurability = 2
                };
                var gateway = new FakeGateway();
                session = new DemoOnlineMatchSession(gateway, store);
                onlineSessionField.SetValue(controller, session);
                var handlePending = typeof(DemoSceneController).GetMethod("HandleOnlineCommandPending", BindingFlags.Instance | BindingFlags.NonPublic);
                var handleCompleted = typeof(DemoSceneController).GetMethod("HandleOnlineCommandCompleted", BindingFlags.Instance | BindingFlags.NonPublic);
                session.CommandPending += commandId => handlePending.Invoke(controller, new object[] { commandId });
                session.CommandCompleted += result => handleCompleted.Invoke(controller, new object[] { result });
                typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);

                var heroButton = (Button)typeof(DemoSceneController).GetField("_playerHeroButton", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(controller);
                var opponentHeroButton = (Button)typeof(DemoSceneController).GetField("_opponentHeroTargetButton", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(controller);
                var playerHudAlpha = (CanvasGroup)typeof(DemoSceneController).GetField("_playerHeroControlAlpha", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(controller);
                var opponentHudAlpha = (CanvasGroup)typeof(DemoSceneController).GetField("_opponentHeroControlAlpha", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(controller);
                Assert.That(heroButton.interactable, Is.False,
                    "Hero attacks are unavailable during the main phase.");
                Assert.That(opponentHeroButton.interactable, Is.False,
                    "The enemy hero cannot be targeted before combat and attacker selection.");
                Assert.That(playerHudAlpha.alpha, Is.EqualTo(0.78f).Within(0.001f));
                Assert.That(opponentHudAlpha.alpha, Is.EqualTo(0.78f).Within(0.001f));

                store.Current.phase = "COMBAT";
                store.Current.turn = 2;
                typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                Assert.That(heroButton.interactable, Is.True,
                    "An equipped, unused hero can be selected during its controller's combat phase.");
                Assert.That(playerHudAlpha.alpha, Is.EqualTo(1f).Within(0.001f));
                Assert.That(opponentHeroButton.interactable, Is.False,
                    "The enemy hero target stays disabled until an attacker is selected.");
                Assert.That(opponentHudAlpha.alpha, Is.EqualTo(0.78f).Within(0.001f));

                typeof(DemoSceneController).GetField("_selectedAttackerInstanceId", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, MatchAttackerIds.Hero);
                typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                Assert.That(opponentHeroButton.interactable, Is.True,
                    "The selected legal hero can target the enemy hero when no taunt blocks it.");
                Assert.That(opponentHudAlpha.alpha, Is.EqualTo(1f).Within(0.001f));

                var pending = session.EnterCombatAsync();
                Assert.That(heroButton.interactable, Is.False);
                Assert.That(opponentHeroButton.interactable, Is.False);
                Assert.That(playerHudAlpha.alpha, Is.EqualTo(0.78f).Within(0.001f));
                Assert.That(opponentHudAlpha.alpha, Is.EqualTo(0.78f).Within(0.001f));
                gateway.Reject(new CommandRejectionDto
                {
                    commandId = gateway.LastCommand.commandId,
                    code = "TEST_REJECTED",
                    message = "test rejection",
                    revision = store.Current.revision
                });
                while (!pending.IsCompleted) yield return null;
                Assert.That(pending.GetAwaiter().GetResult().Outcome, Is.EqualTo(MatchCommandOutcome.Rejected));
                Assert.That(heroButton.interactable, Is.True,
                    "A rejected command releases the lock and restores legal combat input.");
                Assert.That(opponentHeroButton.interactable, Is.True);
                Assert.That(playerHudAlpha.alpha, Is.EqualTo(1f).Within(0.001f));
                Assert.That(opponentHudAlpha.alpha, Is.EqualTo(1f).Within(0.001f));
            }
            finally
            {
                if (controller != null) onlineSessionField.SetValue(controller, null);
                session?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AuthoritativeViewUsesSnapshotKeywordsForChargeAndTauntLegality()
        {
            var store = CreateStore(viewerIndex: 0);
            store.Current.turn = 2;
            store.Current.phase = "COMBAT";
            store.Current.players[0].unitSlots[0] = "object-1";
            store.Current.players[0].battlefield = new[]
            {
                new BattlefieldObjectStateDto
                {
                    instanceId = "object-1", cardId = "pf_001", cardType = "UNIT", slotKind = "UNIT", slotIndex = 0,
                    occupiedSlots = 1, attack = 1, health = 2, maxHealth = 2, summonedTurn = 2, keywords = new[] { "CHARGE" }
                }
            };
            store.Current.players[1].unitSlots[0] = "object-2";
            store.Current.players[1].battlefield = new[]
            {
                new BattlefieldObjectStateDto
                {
                    instanceId = "object-2", cardId = "pf_008", cardType = "UNIT", slotKind = "UNIT", slotIndex = 0,
                    occupiedSlots = 1, attack = 5, health = 7, maxHealth = 7, summonedTurn = 1, keywords = new[] { "TAUNT" }
                }
            };
            var view = new DemoAuthoritativeMatchView(store);
            var attacker = view.GetObject(true, DemoSlotKind.Unit, 0);
            var taunt = view.GetObject(false, DemoSlotKind.Unit, 0);

            Assert.That(view.CanAttackWith(attacker, out _), Is.True);
            Assert.That(view.CanAttackTarget(null, "HERO", out var message), Is.False);
            Assert.That(message, Does.Contain("嘲讽"));
            Assert.That(view.CanAttackTarget(taunt, "UNIT", out _), Is.True);
        }

        [Test]
        public void FinishedAuthoritativeMatchRejectsCombatDeploymentAndNetworkCommands()
        {
            var store = CreateStore(viewerIndex: 0);
            store.Current.status = "FINISHED";
            store.Current.phase = "COMBAT";
            store.Current.winnerPlayerId = "alice";
            var view = new DemoAuthoritativeMatchView(store);
            Assert.That(view.HasWinner, Is.True);
            Assert.That(view.IsPlayerWinner, Is.True);
            Assert.That(CardContentLoader.Load().TryGetDefinition("pf_001", out var bee), Is.True);

            Assert.That(view.CanAttackWith(null, out var attackerMessage), Is.False);
            Assert.That(attackerMessage, Does.Contain("对局已经结束"));
            Assert.That(view.CanAttackWithHero(out var heroMessage), Is.False);
            Assert.That(heroMessage, Does.Contain("对局已经结束"));
            Assert.That(view.CanAttackTarget(null, "HERO", out var targetMessage), Is.False);
            Assert.That(targetMessage, Does.Contain("对局已经结束"));
            var preview = DemoDeploymentRules.Evaluate(view, bee, DemoSlotKind.Unit, 0);
            Assert.That(preview.IsLegal, Is.False);
            Assert.That(preview.Message, Does.Contain("对局已经结束"));

            var gateway = new FakeGateway();
            using (var session = new DemoOnlineMatchSession(gateway, store))
            {
                Assert.That(session.CanIssueCommand, Is.False);
                Assert.ThrowsAsync<InvalidOperationException>(async () => await session.EndTurnAsync());
                Assert.That(gateway.LastCommand, Is.Null);
            }

            store.Current.winnerPlayerId = "bob";
            var defeatedView = new DemoAuthoritativeMatchView(store);
            Assert.That(defeatedView.HasWinner, Is.True);
            Assert.That(defeatedView.IsPlayerWinner, Is.False);

            store.Current.winnerPlayerId = string.Empty;
            var drawnView = new DemoAuthoritativeMatchView(store);
            Assert.That(drawnView.HasWinner, Is.False);
            Assert.That(drawnView.IsPlayerWinner, Is.False);
        }

        [Test]
        public void AuthoritativeDrawEventPresenterShowsGoldDrawBanner()
        {
            var root = new GameObject("OnlineDrawBannerPresentationTest");
            DemoOnlineMatchSession session = null;
            try
            {
                var store = CreateStore(viewerIndex: 0);
                var controller = root.AddComponent<DemoSceneController>();
                var bannerRoot = new GameObject("TurnBanner", typeof(RectTransform));
                bannerRoot.transform.SetParent(root.transform, false);
                typeof(DemoSceneController).GetField("_turnBanner", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, bannerRoot.AddComponent<CanvasGroup>());
                var bannerText = new GameObject("TurnBannerText", typeof(RectTransform)).AddComponent<Text>();
                bannerText.transform.SetParent(bannerRoot.transform, false);
                typeof(DemoSceneController).GetField("_turnBannerText", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, bannerText);

                session = new DemoOnlineMatchSession(new FakeGateway(), store);
                typeof(DemoSceneController).GetField("_onlineSession", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, session);
                var present = typeof(DemoSceneController).GetMethod(
                    "PresentOnlineEvent", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(present, Is.Not.Null);
                var presentation = (IEnumerator)present.Invoke(controller, new object[]
                {
                    new MatchEventDto
                    {
                        type = MatchEventTypes.MatchEnded,
                        payload = new MatchEventPayloadDto { winnerPlayerId = null, reason = "SIMULTANEOUS_DEFEAT" }
                    }
                });

                Assert.That(presentation.MoveNext(), Is.True);
                var bannerAnimation = presentation.Current as IEnumerator;
                Assert.That(bannerAnimation, Is.Not.Null);
                Assert.That(bannerAnimation.MoveNext(), Is.True);
                Assert.That(bannerText.text, Is.EqualTo("平局"));
                Assert.That((Color32)bannerText.color, Is.EqualTo(new Color32(228, 185, 95, 255)));
            }
            finally
            {
                session?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TurnBannerStartsOnSnappedOffsetAndNewBannerReplacesTheOldOne()
        {
            var root = new GameObject("TurnBannerMotionTestRoot");
            try
            {
                var controller = root.AddComponent<DemoSceneController>();
                var bannerRoot = new GameObject("TurnBanner", typeof(RectTransform));
                bannerRoot.transform.SetParent(root.transform, false);
                var bannerRect = bannerRoot.GetComponent<RectTransform>();
                bannerRect.anchoredPosition = new Vector2(0f, 8f);
                var banner = bannerRoot.AddComponent<CanvasGroup>();
                typeof(DemoSceneController).GetField("_turnBanner", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, banner);
                var bannerText = new GameObject("TurnBannerText", typeof(RectTransform)).AddComponent<Text>();
                bannerText.transform.SetParent(bannerRoot.transform, false);
                typeof(DemoSceneController).GetField("_turnBannerText", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, bannerText);
                var showBanner = typeof(DemoSceneController).GetMethod("ShowTurnBanner", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(showBanner, Is.Not.Null);

                var oldAnimation = (IEnumerator)showBanner.Invoke(controller, new object[] { "旧回合", Color.yellow });
                Assert.That(oldAnimation.MoveNext(), Is.True);
                Assert.That(banner.alpha, Is.EqualTo(0f));
                Assert.That(bannerRect.anchoredPosition.y, Is.EqualTo(-6f), "the initial slide offset must be an integer design pixel");

                var newAnimation = (IEnumerator)showBanner.Invoke(controller, new object[] { "新回合", Color.cyan });
                Assert.That(newAnimation.MoveNext(), Is.True);
                Assert.That(bannerText.text, Is.EqualTo("新回合"));
                Assert.That(bannerRect.anchoredPosition.y, Is.EqualTo(-6f),
                    "a replacement banner must restart from the stable resting position rather than accumulating old motion");
                Assert.That(oldAnimation.MoveNext(), Is.False, "a newer turn banner must cancel the stale animation");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PresentationQueueCanRebaseForANewOrRejoinedMatch()
        {
            var root = new GameObject("PresentationQueueTest");
            try
            {
                var queue = root.AddComponent<PresentationQueue>();
                queue.Reset(37);

                Assert.That(queue.LastQueuedEventId, Is.EqualTo(37));
                Assert.That(queue.PendingCount, Is.Zero);
                Assert.That(queue.IsPlaying, Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OnlineBeeHealingUsesAuthoritativeHealingEventInsteadOfDeploymentPayload()
        {
            var root = new GameObject("OnlineBeeHealingPresentationTest");
            DemoOnlineMatchSession session = null;
            try
            {
                var compositionRoot = root.AddComponent<GameCompositionRoot>();
                typeof(GameCompositionRoot).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(compositionRoot, null);
                var transport = new FakeTransport();
                compositionRoot.RegisterOnlineTransport(transport);
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));
                compositionRoot.MatchStateStore.Replace(CreateStore(viewerIndex: 0).Current);

                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                battlefield.BuildNow();
                var controller = root.AddComponent<DemoSceneController>();
                typeof(DemoSceneController).GetField("_battlefield", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, battlefield);
                typeof(DemoSceneController).GetField("_registry", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, CardContentLoader.Load());
                var statusText = new GameObject("OnlineBeeHealingStatus").AddComponent<Text>();
                statusText.transform.SetParent(root.transform, false);
                typeof(DemoSceneController).GetField("_statusText", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, statusText);
                typeof(DemoSceneController).GetMethod("RegisterOnlineEventPresenters", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                session = new DemoOnlineMatchSession(compositionRoot.MatchGateway, compositionRoot.MatchStateStore);
                typeof(DemoSceneController).GetField("_onlineSession", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, session);

                Assert.That(compositionRoot.PresentationQueue.Registry.TryResolve(
                    MatchEventTypes.CardDeployed, out var deployPresenter), Is.True);
                Assert.That(compositionRoot.PresentationQueue.Registry.TryResolve(
                    MatchEventTypes.HeroHealed, out var healedPresenter), Is.True);

                var deployed = deployPresenter.Play(new MatchEventDto
                {
                    type = MatchEventTypes.CardDeployed,
                    payload = new MatchEventPayloadDto { playerId = "alice", cardId = "pf_001" }
                });
                Assert.That(deployed.MoveNext(), Is.True);
                Assert.That(statusText.text, Does.Not.Contain("恢复 0 点"),
                    "CARD_DEPLOYED does not carry battlecry healing and must not fabricate a zero amount.");

                var healed = healedPresenter.Play(new MatchEventDto
                {
                    type = MatchEventTypes.HeroHealed,
                    payload = new MatchEventPayloadDto
                    {
                        playerId = "alice", sourceCardId = "pf_001", effectId = "effect.pf_001.01",
                        healing = 1, life = 29
                    }
                });
                Assert.That(healed.MoveNext(), Is.True);
                Assert.That(statusText.text, Is.EqualTo("蜜蜂：己方英雄恢复 1 点生命，当前 29 点。"));

                var capped = healedPresenter.Play(new MatchEventDto
                {
                    type = MatchEventTypes.HeroHealed,
                    payload = new MatchEventPayloadDto
                    {
                        playerId = "alice", sourceCardId = "pf_001", effectId = "effect.pf_001.01",
                        healing = 0, life = 30
                    }
                });
                Assert.That(capped.MoveNext(), Is.True);
                Assert.That(statusText.text, Is.EqualTo("蜜蜂：己方英雄生命已满，本次没有恢复。"));
            }
            finally
            {
                session?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OnlineLethalAttackDefersDamagePopupUntilDeathEventProvidesTheOriginalSlot()
        {
            var root = new GameObject("OnlineLethalAttackPresentationTest");
            DemoOnlineMatchSession session = null;
            try
            {
                var compositionRoot = root.AddComponent<GameCompositionRoot>();
                typeof(GameCompositionRoot).GetMethod("Awake", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(compositionRoot, null);
                var transport = new FakeTransport();
                compositionRoot.RegisterOnlineTransport(transport);
                transport.EmitStatus(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1"));
                compositionRoot.MatchStateStore.Replace(CreateStore(viewerIndex: 0).Current);

                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                battlefield.BuildNow();
                var controller = root.AddComponent<DemoSceneController>();
                typeof(DemoSceneController).GetField("_battlefield", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, battlefield);
                typeof(DemoSceneController).GetField("_registry", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, CardContentLoader.Load());
                var statusText = new GameObject("OnlineEventStatus").AddComponent<Text>();
                statusText.transform.SetParent(root.transform, false);
                typeof(DemoSceneController).GetField("_statusText", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, statusText);
                typeof(DemoSceneController).GetMethod("RegisterOnlineEventPresenters", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                session = new DemoOnlineMatchSession(compositionRoot.MatchGateway, compositionRoot.MatchStateStore);
                typeof(DemoSceneController).GetField("_onlineSession", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, session);

                Assert.That(compositionRoot.PresentationQueue.Registry.TryResolve(
                    MatchEventTypes.AttackResolved, out var attackPresenter), Is.True);
                Assert.That(compositionRoot.PresentationQueue.Registry.TryResolve(
                    MatchEventTypes.ObjectDied, out var deathPresenter), Is.True);
                Assert.That(compositionRoot.PresentationQueue.Registry.TryResolve(
                    MatchEventTypes.ObjectReturned, out var returnPresenter), Is.True);
                Assert.That(compositionRoot.PresentationQueue.Registry.TryResolve(
                    MatchEventTypes.HandCardCostModifierExpired, out var expiryPresenter), Is.True);
                var attack = new MatchEventDto
                {
                    type = MatchEventTypes.AttackResolved,
                    payload = new MatchEventPayloadDto
                    {
                        attackerPlayerId = "alice", attackerInstanceId = MatchAttackerIds.Hero,
                        targetPlayerId = "bob", targetType = "UNIT", targetInstanceId = "dead-target",
                        damageToTarget = 5, targetHealth = 0
                    }
                };

                var attackPresentation = attackPresenter.Play(attack);
                Assert.That(attackPresentation.MoveNext(), Is.True);
                Assert.That(root.transform.Find("CombatFeedback").childCount, Is.Zero,
                    "a lethal popup must wait for the death event's authoritative slot coordinates");

                var death = new MatchEventDto
                {
                    type = MatchEventTypes.ObjectDied,
                    payload = new MatchEventPayloadDto
                    {
                        playerId = "bob", instanceId = "dead-target", cardId = "pf_001",
                        slotKind = "UNIT", slotIndex = 2, occupiedSlots = 1
                    }
                };
                var deathPresentation = deathPresenter.Play(death);
                Assert.That(deathPresentation.MoveNext(), Is.True);

                var popup = root.transform.Find("CombatFeedback/CombatDamage_Opponent");
                Assert.That(popup, Is.Not.Null);
                Assert.That(popup.Find("Label").GetComponent<TextMesh>().text, Is.EqualTo("-5"));
                var expectedPosition = battlefield.GetSlotWorldPosition(false, DemoSlotKind.Unit, 2) + Vector3.up * 2.1f;
                Assert.That(popup.localPosition.x, Is.EqualTo(expectedPosition.x).Within(0.001f));
                Assert.That(popup.localPosition.y, Is.EqualTo(expectedPosition.y).Within(0.001f));
                Assert.That(popup.localPosition.z, Is.EqualTo(expectedPosition.z).Within(0.001f));
                Assert.That(root.transform.Find("CombatFeedback/CombatDamage_Player"), Is.Null);

                var returned = returnPresenter.Play(new MatchEventDto
                {
                    type = MatchEventTypes.ObjectReturned,
                    payload = new MatchEventPayloadDto
                    {
                        cardId = "pf_001", sourceCardId = "ed_005", ownerPlayerId = "alice",
                        controllerPlayerId = "alice", destination = "HAND", costModifier = -1,
                        expiresAtEndOfTurnPlayerId = "alice", fromSlotKind = "UNIT", fromSlotIndex = 1
                    }
                });
                Assert.That(returned.MoveNext(), Is.True);
                Assert.That(statusText.text, Does.Contain("蜜蜂从战场返回你的手牌"));
                Assert.That(statusText.text, Does.Contain("费用减少 1"));

                var expired = expiryPresenter.Play(new MatchEventDto
                {
                    type = MatchEventTypes.HandCardCostModifierExpired,
                    payload = new MatchEventPayloadDto
                    {
                        playerId = "alice", cardId = "pf_001", expiredCostModifier = -1, effectiveCost = 2
                    }
                });
                Assert.That(expired.MoveNext(), Is.True);
                Assert.That(statusText.text, Does.Contain("回手减费（-1）已到期"));
                Assert.That(statusText.text, Does.Contain("费用恢复为 2"));
            }
            finally
            {
                session?.Dispose();
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void ObjectReturnedStatusShowsDiscountOnlyToTheCardOwner()
        {
            var payload = new MatchEventPayloadDto
            {
                cardId = "pf_001",
                sourceCardId = "ed_005",
                ownerPlayerId = "alice",
                controllerPlayerId = "bob",
                destination = "HAND",
                costModifier = -2,
                expiresAtEndOfTurnPlayerId = "bob"
            };
            var format = typeof(DemoSceneController).GetMethod(
                "FormatObjectReturnedStatus", BindingFlags.Static | BindingFlags.NonPublic);

            var ownerMessage = (string)format.Invoke(null,
                new object[] { payload, "alice", "蜜蜂", "末影珍珠" });
            var opponentProjection = new MatchEventPayloadDto
            {
                cardId = payload.cardId,
                sourceCardId = payload.sourceCardId,
                ownerPlayerId = payload.ownerPlayerId,
                controllerPlayerId = payload.controllerPlayerId,
                destination = payload.destination,
                costModifier = payload.costModifier,
                expiresAtEndOfTurnPlayerId = payload.expiresAtEndOfTurnPlayerId
            };
            var opponentMessage = (string)format.Invoke(null,
                new object[] { opponentProjection, "bob", "蜜蜂", "末影珍珠" });
            var missingViewerMessage = (string)format.Invoke(null,
                new object[] { payload, string.Empty, "蜜蜂", "末影珍珠" });

            Assert.That(ownerMessage, Does.Contain("费用减少 2"));
            Assert.That(ownerMessage, Does.Contain("敌方回合结束时恢复"));
            Assert.That(opponentMessage, Does.Contain("返回对手的手牌"));
            Assert.That(opponentMessage, Does.Not.Contain("费用"));
            Assert.That(opponentMessage, Does.Not.Contain("减少"));
            Assert.That(missingViewerMessage, Does.Not.Contain("费用"));
        }

        [Test]
        public void ObjectReturnedStatusExplainsFullHandDiscardWithoutAHiddenDiscount()
        {
            var payload = new MatchEventPayloadDto
            {
                cardId = "pf_001",
                sourceCardId = "ed_002",
                ownerPlayerId = "alice",
                controllerPlayerId = "alice",
                destination = "DISCARD",
                costModifier = 0
            };
            var format = typeof(DemoSceneController).GetMethod(
                "FormatObjectReturnedStatus", BindingFlags.Static | BindingFlags.NonPublic);

            var message = (string)format.Invoke(null,
                new object[] { payload, "alice", "蜜蜂", "紫颂果" });

            Assert.That(message, Does.Contain("因手牌已满进入你的弃牌堆"));
            Assert.That(message, Does.Not.Contain("费用"));
        }

        [Test]
        public void CostModifierExpiryStatusIsOnlyShownToItsHandOwner()
        {
            var payload = new MatchEventPayloadDto
            {
                playerId = "alice",
                cardId = "pf_001",
                expiredCostModifier = -1,
                effectiveCost = 2
            };
            var format = typeof(DemoSceneController).GetMethod(
                "FormatHandCardCostModifierExpiredStatus", BindingFlags.Static | BindingFlags.NonPublic);

            var ownerMessage = (string)format.Invoke(null, new object[] { payload, "alice", "蜜蜂" });
            var opponentProjection = new MatchEventPayloadDto { playerId = "alice" };
            var opponentMessage = (string)format.Invoke(null, new object[] { opponentProjection, "bob", string.Empty });
            var missingViewerMessage = (string)format.Invoke(null, new object[] { payload, string.Empty, "蜜蜂" });

            Assert.That(ownerMessage, Does.Contain("回手减费（-1）已到期"));
            Assert.That(ownerMessage, Does.Contain("费用恢复为 2"));
            Assert.That(opponentMessage, Is.Empty);
            Assert.That(missingViewerMessage, Is.Empty);
        }

        private static MatchStateStore CreateStore(int viewerIndex)
        {
            var players = new[]
            {
                new PlayerStateDto
                {
                    playerId = "alice", factionId = FactionIds.OceanRiver, life = 30, armor = 2,
                    redstone = 1, totalRedstone = 1, redstoneCapacity = 1,
                    hand = new[] { "pf_001" },
                    handCards = viewerIndex == 0
                        ? new[] { new HandCardStateDto { handCardInstanceId = "hand-1", cardId = "pf_001" } }
                        : new HandCardStateDto[] { null },
                    unitSlots = new string[4], buildingSlots = new string[3]
                },
                new PlayerStateDto
                {
                    playerId = "bob", factionId = FactionIds.End, life = 27, armor = 3,
                    redstone = 2, totalRedstone = 2, redstoneCapacity = 2,
                    hand = new[] { "nt_001" },
                    handCards = viewerIndex == 1
                        ? new[] { new HandCardStateDto { handCardInstanceId = "hand-2", cardId = "nt_001" } }
                        : new HandCardStateDto[] { null },
                    unitSlots = new string[4], buildingSlots = new string[3]
                }
            };
            var store = new MatchStateStore();
            store.Replace(new MatchStateDto
            {
                matchId = "match-1",
                viewerPlayerId = players[viewerIndex].playerId,
                protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset,
                revision = 0,
                lastEventId = 0,
                status = "ACTIVE",
                turn = 1,
                phase = "MAIN",
                activePlayerIndex = viewerIndex,
                players = players
            });
            return store;
        }

        private sealed class FakeTransport : IMatchTransport
        {
            public event Action<int, string> MessageReceived;
            public event Action<Exception> Faulted;
            public event Action<MatchConnectionStatus> ConnectionStateChanged;

            public MatchConnectionStatus CurrentStatus { get; private set; } =
                new MatchConnectionStatus(MatchConnectionPhase.Offline);

            public Task ConnectAsync() => Task.CompletedTask;
            public Task DisconnectAsync() => Task.CompletedTask;
            public Task SendAsync(int opcode, string json) => Task.CompletedTask;
            public void Dispose() { }

            public void EmitStatus(MatchConnectionStatus status)
            {
                CurrentStatus = status;
                ConnectionStateChanged?.Invoke(status);
            }
        }

        private sealed class FakeGateway : IMatchGateway
        {
            public event Action<MatchEventBatchDto> EventBatchReceived;
            public event Action<MatchStateDto> SnapshotReceived;
            public event Action<CommandRejectionDto> CommandRejected;
            public event Action<Exception> Faulted;
            public event Action<MatchConnectionStatus> ConnectionStateChanged;

            public MatchConnectionStatus CurrentStatus { get; private set; } =
                new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", "match-1");
            public MatchCommandDto LastCommand { get; private set; }

            public Task ConnectAsync() => Task.CompletedTask;
            public Task DisconnectAsync() => Task.CompletedTask;
            public Task SendCommandAsync(MatchCommandDto command)
            {
                LastCommand = command;
                return Task.CompletedTask;
            }
            public void Emit(MatchEventBatchDto batch) => EventBatchReceived?.Invoke(batch);
            public void Reject(CommandRejectionDto rejection) => CommandRejected?.Invoke(rejection);
            public void Dispose() { }
        }
    }
}
