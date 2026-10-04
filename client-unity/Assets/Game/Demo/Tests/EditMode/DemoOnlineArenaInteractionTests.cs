using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using BiomeRivals.Core;
using BiomeRivals.Networking;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.UI;

namespace BiomeRivals.Demo.Tests
{
    // These are Scene/input-to-command integration tests, not Nakama acceptance tests.
    // The gateway deliberately rejects captured commands without changing the store.
    public sealed class DemoOnlineArenaInteractionTests
    {
        [TestCase("alice", "nt_002", true)]
        [TestCase("alice", "db_001", false)]
        [TestCase("bob", "nt_002", false)]
        public void AuthoritativePendingReaderRequiresOwnedVisibleCard(string owner, string requested, bool opens)
        {
            using (var scene = new OnlineScene("standard_meadow", "ed_002"))
            {
                var source = scene.AddObject(owner == "alice", "cd_001", DemoSlotKind.Unit, 0);
                scene.Snapshot.activePlayerIndex = owner == "alice" ? 0 : 1;
                scene.Snapshot.pendingChoice = new PendingChoiceDto { choiceId = "online-reader-choice", playerId = owner,
                    sourceCardId = "cd_001", sourceInstanceId = source.instanceId, effectId = "effect.cd_001.01",
                    kind = "TOP_CARD_SCRY", options = new[] { new PendingChoiceOptionDto { optionIndex = 0,
                        cardId = owner == "alice" ? "nt_002" : string.Empty, selectable = owner == "alice" } } };
                scene.Refresh();
                Invoke(scene.Controller, "OpenChoiceRules", requested);
                Assert.That(scene.Controller.transform.Find("DemoCanvas/HandInspectionOverlay").gameObject.activeSelf, Is.EqualTo(opens));
                Assert.That(scene.Gateway.Commands, Is.Empty);
                if (opens)
                {
                    Invoke(scene.Controller, "SelectChoiceOption", 0);
                    Invoke(scene.Controller, "ConfirmChoice");
                    Assert.That(scene.Gateway.Commands, Is.Empty, "Reading must not resolve the server choice.");
                    Invoke(scene.Controller, "CloseHandInspection");
                    Assert.That(scene.Snapshot.pendingChoice.choiceId, Is.EqualTo("online-reader-choice"));
                }
            }
        }

        [TestCase("deep_caverns", "beyond-edge", false)]
        [TestCase("deep_caverns", "not-adjacent", false)]
        [TestCase("deep_caverns", "occupied-landing", false)]
        [TestCase("nether_lava_sea", "beyond-edge", false)]
        [TestCase("nether_lava_sea", "not-adjacent", false)]
        [TestCase("nether_lava_sea", "occupied-landing", false)]
        [TestCase("deep_caverns", "beyond-edge", true)]
        [TestCase("deep_caverns", "not-adjacent", true)]
        [TestCase("deep_caverns", "occupied-landing", true)]
        [TestCase("nether_lava_sea", "beyond-edge", true)]
        [TestCase("nether_lava_sea", "not-adjacent", true)]
        [TestCase("nether_lava_sea", "occupied-landing", true)]
        public void ForbiddenGoatPreviewCannotIssueAnOnlineDeploy(string arenaId, string obstruction, bool drag)
        {
            using (var scene = new OnlineScene(arenaId, "si_004"))
            {
                var count = scene.Snapshot.players[0].unitSlots.Length;
                var targetIndex = obstruction == "beyond-edge" ? count - 2 : 0;
                var anchor = obstruction == "occupied-landing" ? 1 : count - 1;
                var target = scene.AddObject(true, "pf_002", DemoSlotKind.Unit, targetIndex);
                if (obstruction == "occupied-landing") scene.AddObject(true, "pf_001", DemoSlotKind.Unit, 2);
                scene.Refresh();
                scene.SelectHand();
                SetField(scene.Controller, "_selectedDeploymentTargetInstanceId", target.instanceId);
                var preview = (DemoDeploymentPreview)Invoke(scene.Controller, "EvaluateSelectedDeployment", DemoSlotKind.Unit, anchor);
                Assert.That(preview.IsLegal, Is.False, obstruction);
                var stateBefore = JsonUtility.ToJson(scene.Store.Current);
                var selectedHand = GetField(scene.Controller, "_selectedHandCardInstanceId");

                scene.PointAt(DemoSlotKind.Unit, anchor, drag);

                Assert.That(scene.Gateway.Commands, Is.Empty,
                    "a forbidden world-space hover/drop must not reach the online dispatcher");
                Assert.That(JsonUtility.ToJson(scene.Store.Current), Is.EqualTo(stateBefore),
                    "rejected input must not spend energy, remove the hand instance, or alter board/revision");
                Assert.That(GetField(scene.Controller, "_selectedHandCardInstanceId"), Is.EqualTo(selectedHand));
                Assert.That(GetField(scene.Controller, "_selectedDeploymentTargetInstanceId"), Is.EqualTo(target.instanceId));
                Assert.That(((Text)GetField(scene.Controller, "_statusText")).text, Does.Contain("山羊必须部署在目标旁"));
                Assert.That(scene.Session.HasPendingCommand, Is.False);
            }
        }

        [TestCase("deep_caverns", false)]
        [TestCase("nether_lava_sea", false)]
        [TestCase("deep_caverns", true)]
        [TestCase("nether_lava_sea", true)]
        public void LegalGoatPathToFinalSlotKeepsTheExactHandAndTargetInTheCommand(string arenaId, bool drag)
        {
            using (var scene = new OnlineScene(arenaId, "si_004"))
            {
                var count = scene.Snapshot.players[0].unitSlots.Length;
                var target = scene.AddObject(true, "pf_002", DemoSlotKind.Unit, count - 3);
                scene.Refresh();
                scene.SelectHand();
                SetField(scene.Controller, "_selectedDeploymentTargetInstanceId", target.instanceId);
                var anchor = count - 2;
                var preview = (DemoDeploymentPreview)Invoke(scene.Controller, "EvaluateSelectedDeployment", DemoSlotKind.Unit, anchor);
                Assert.That(preview.IsLegal, Is.True, preview.Message);

                scene.PointAt(DemoSlotKind.Unit, anchor, drag);

                Assert.That(scene.Gateway.Commands.Count, Is.EqualTo(1));
                var command = scene.Gateway.Commands.Single();
                Assert.That(command.type, Is.EqualTo(MatchCommandTypes.DeployCard));
                Assert.That(command.payload.cardId, Is.EqualTo("si_004"));
                Assert.That(command.payload.handCardInstanceId, Is.EqualTo("hand-1"));
                Assert.That(command.payload.targetType, Is.EqualTo("UNIT"));
                Assert.That(command.payload.targetInstanceId, Is.EqualTo(target.instanceId));
                Assert.That(command.payload.paymentMethod, Is.EqualTo(MatchPaymentMethods.Redstone));
                Assert.That(command.payload.slotIndex, Is.EqualTo(anchor));
                Assert.That(command.expectedRevision, Is.EqualTo(scene.Snapshot.revision));
                Assert.That(scene.Session.HasPendingCommand, Is.False);
            }
        }

        [TestCase("deep_caverns")]
        [TestCase("nether_lava_sea")]
        public void GoatTargetSelectionUsesInspectorActionAndWorldInputWithoutSendingACommand(string arenaId)
        {
            using (var scene = new OnlineScene(arenaId, "si_004"))
            {
                var count = scene.Snapshot.players[0].unitSlots.Length;
                var target = scene.AddObject(true, "si_002", DemoSlotKind.Unit, count - 3);
                scene.Refresh();
                scene.SelectHand();
                var stateBefore = JsonUtility.ToJson(scene.Store.Current);
                Assert.That(GetField(scene.Controller, "_selectedCardId"), Is.EqualTo("si_004"));
                var action = (Button)Invoke(scene.Controller, "FindSelectedCardActionButton");
                Assert.That(action, Is.Not.Null, "the selected goat must create its named battlecry button");
                Assert.That(action.name, Is.EqualTo("BattlecryTarget"), "unit battlecry must not resolve the spell-only Cast button");
                Assert.That(action.interactable, Is.True, "a valid lane must enable the action");
                Assert.That(action.isActiveAndEnabled, Is.True);
                Debug.Log($"EditMode inspector graphic depth: {action.GetComponent<Image>().depth}; Player must verify Overlay raycasts.");
                // Exercise the actual listener here; the separate graphical Player probe
                // requires a real Overlay GraphicRaycaster hit before invoking this action.
                action.onClick.Invoke();
                Assert.That(GetField(scene.Controller, "_pendingTargetCardId"), Is.EqualTo("si_004"));
                scene.PointAt(DemoSlotKind.Unit, count - 3, false);
                Assert.That(GetField(scene.Controller, "_selectedDeploymentTargetInstanceId"), Is.EqualTo(target.instanceId));
                Assert.That(GetField(scene.Controller, "_pendingTargetCardId"), Is.Null);
                Assert.That(scene.Gateway.Commands, Is.Empty);
                Assert.That(JsonUtility.ToJson(scene.Store.Current), Is.EqualTo(stateBefore));
                scene.PointAt(DemoSlotKind.Unit, count - 2, false);
                Assert.That(scene.Gateway.Commands.Single().payload.targetInstanceId, Is.EqualTo(target.instanceId));
            }
        }

        [TestCase("deep_caverns", "pf_001", "UNIT", false)]
        [TestCase("nether_lava_sea", "si_007", "BUILDING", true)]
        [TestCase("deep_caverns", "cd_007", "BUILDING", true)]
        [TestCase("nether_lava_sea", "cd_007", "BUILDING", false)]
        public void FinalUnitBuildingAndTwoSlotAnchorRouteThroughWorldInput(string arenaId, string cardId, string kindName, bool drag)
        {
            using (var scene = new OnlineScene(arenaId, cardId))
            {
                var kind = kindName == "UNIT" ? DemoSlotKind.Unit : DemoSlotKind.Building;
                var width = cardId == "cd_007" ? 2 : 1;
                var count = scene.Battlefield.GetSlotCount(kind);
                scene.SelectHand();
                scene.PointAt(kind, count - width, drag);
                Assert.That(scene.Gateway.Commands.Count, Is.EqualTo(1));
                var payload = scene.Gateway.Commands.Single().payload;
                Assert.That(payload.cardId, Is.EqualTo(cardId));
                Assert.That(payload.slotKind, Is.EqualTo(kindName));
                Assert.That(payload.slotIndex, Is.EqualTo(count - width));
                Assert.That(payload.handCardInstanceId, Is.EqualTo("hand-1"));
                Assert.That(payload.paymentMethod, Is.EqualTo(MatchPaymentMethods.Redstone));
                Assert.That(payload.targetInstanceId, Is.Empty);
            }
        }

        [TestCase("deep_caverns", false)]
        [TestCase("nether_lava_sea", true)]
        public void TwoSlotStructureCannotStartAtTheLastBuildingCell(string arenaId, bool drag)
        {
            using (var scene = new OnlineScene(arenaId, "cd_007"))
            {
                scene.SelectHand();
                var count = scene.Battlefield.GetSlotCount(DemoSlotKind.Building);
                var stateBefore = JsonUtility.ToJson(scene.Store.Current);
                scene.PointAt(DemoSlotKind.Building, count - 1, drag);
                Assert.That(scene.Gateway.Commands, Is.Empty);
                Assert.That(JsonUtility.ToJson(scene.Store.Current), Is.EqualTo(stateBefore));
            }
        }

        [TestCase("nether_lava_sea")]
        [TestCase("end_void")]
        public void FourBuildingCellsKeepTheStandardOuterAnchorsToAvoidTheDetailsHud(string arenaId)
        {
            using (var scene = new OnlineScene(arenaId, "cd_004"))
            {
                foreach (var friendly in new[] { true, false })
                {
                    var first = scene.Battlefield.GetSlotWorldPosition(friendly, DemoSlotKind.Building, 0);
                    var last = scene.Battlefield.GetSlotWorldPosition(friendly, DemoSlotKind.Building, 3);
                    var standardOuterAnchor = friendly ? 4.35f : 4.5f;
                    Assert.That(first.x, Is.EqualTo(-standardOuterAnchor).Within(0.001f));
                    Assert.That(last.x, Is.EqualTo(standardOuterAnchor).Within(0.001f));
                    Assert.That(last.x / 1.5f, Is.GreaterThan(2.85f), "the compact pads must still have a physical gap");
                }
                scene.SelectHand();
                scene.PointAt(DemoSlotKind.Building, 3, false);
                Assert.That(scene.Gateway.Commands.Count, Is.EqualTo(1), "compact end cells must remain selectable");
            }
        }

        [TestCase("standard_meadow")]
        [TestCase("deep_caverns")]
        [TestCase("nether_lava_sea")]
        public void RecoverySnapshotRestoresAnInterruptedBannerWithoutChangingGameplay(string arenaId)
        {
            using (var scene = new OnlineScene(arenaId, "cd_001"))
            {
                scene.SelectHand();
                var before = JsonUtility.ToJson(scene.Store.Current);
                var selected = GetField(scene.Controller, "_selectedHandCardInstanceId");
                var banner = (CanvasGroup)GetField(scene.Controller, "_turnBanner");
                var rect = (RectTransform)banner.transform;
                var resting = rect.anchoredPosition;
                var animation = (System.Collections.IEnumerator)Invoke(scene.Controller, "ShowTurnBanner", "你的回合", Color.white);
                Assert.That(animation.MoveNext(), Is.True);
                banner.alpha = 0.75f;
                rect.anchoredPosition = resting + Vector2.up * 8f;
                var sequence = (int)GetField(scene.Controller, "_turnBannerSequence");

                Invoke(scene.Controller, "HandleOnlinePresentationSnapshot", scene.Snapshot);

                Assert.That(banner.alpha, Is.Zero);
                Assert.That(rect.anchoredPosition, Is.EqualTo(resting));
                Assert.That((int)GetField(scene.Controller, "_turnBannerSequence"), Is.EqualTo(sequence + 1));
                Assert.That(animation.MoveNext(), Is.False, "the obsolete iterator must not restore its old banner alpha");
                Assert.That(JsonUtility.ToJson(scene.Store.Current), Is.EqualTo(before));
                Assert.That(GetField(scene.Controller, "_selectedHandCardInstanceId"), Is.EqualTo(selected));
                Assert.That(scene.Session.HasPendingCommand, Is.False);
                Assert.That(scene.Gateway.Commands, Is.Empty);
                Invoke(scene.Controller, "HandleOnlinePresentationSnapshot", scene.Snapshot);
                Assert.That(banner.alpha, Is.Zero, "repeated snapshots must stay at the resting pose");
            }
        }

        [TestCase("standard_meadow")]
        [TestCase("deep_caverns")]
        [TestCase("nether_lava_sea")]
        public void AuditPreparationCombatProtectsBothCriticalUnitsBuildingsAndHeroes(string arenaId)
        {
            using (var scene = new OnlineScene(arenaId, "cd_001"))
            {
                scene.Snapshot.phase = "COMBAT";
                var last = scene.Snapshot.players[0].unitSlots.Length - 1;
                scene.AddObject(true, "cd_002", DemoSlotKind.Unit, last);
                scene.AddObject(false, "cd_002", DemoSlotKind.Unit, last);
                scene.AddObject(true, "cd_004", DemoSlotKind.Building, 0);
                scene.AddObject(false, "cd_004", DemoSlotKind.Building, 0);
                scene.Refresh();
                Assert.That(DemoOnlineDeploymentAudit.TrySelectPreparationCombat(scene.Session.View, out _, out _), Is.False);
                var attacker = scene.AddObject(true, "cd_002", DemoSlotKind.Unit, 0);
                var target = scene.AddObject(false, "cd_002", DemoSlotKind.Unit, 0);
                scene.Refresh();
                Assert.That(DemoOnlineDeploymentAudit.TrySelectPreparationCombat(scene.Session.View, out var selectedAttacker, out var selectedTarget), Is.True);
                Assert.That(selectedAttacker.InstanceId, Is.EqualTo(attacker.instanceId));
                Assert.That(selectedTarget.InstanceId, Is.EqualTo(target.instanceId));
                attacker.cardId = "cd_003";
                scene.Refresh();
                Assert.That(DemoOnlineDeploymentAudit.TrySelectPreparationCombat(scene.Session.View, out _, out _), Is.True,
                    "ordinary combat permits this unit; the audit gates combat until both unit prefixes complete");
                attacker.cardId = "cd_002";
                target.cardId = "cd_003";
                scene.Refresh();
                Assert.That(DemoOnlineDeploymentAudit.TrySelectPreparationCombat(scene.Session.View, out _, out _), Is.True);
                target.cardId = "cd_002";
                attacker.hasAttacked = true;
                scene.Refresh();
                Assert.That(DemoOnlineDeploymentAudit.TrySelectPreparationCombat(scene.Session.View, out _, out _), Is.False,
                    "a spent preparation unit must not fall back to the protected last unit");
                attacker.hasAttacked = false;
                scene.Snapshot.phase = "MAIN";
                scene.Refresh();
                Assert.That(DemoOnlineDeploymentAudit.TrySelectPreparationCombat(scene.Session.View, out _, out _), Is.False);
            }
        }

        [TestCase("standard_meadow")]
        [TestCase("deep_caverns")]
        [TestCase("nether_lava_sea")]
        public void OccupancyAuditUsesStableObjectIdentityNotTheViewsCardLabel(string arenaId)
        {
            using (var scene = new OnlineScene(arenaId, "cd_001"))
            {
                var last = scene.Snapshot.players[0].unitSlots.Length - 1;
                var unit = scene.AddObject(true, "cd_001", DemoSlotKind.Unit, last);
                scene.Refresh();
                Assert.That(scene.Session.View.UnitSlots[last], Is.EqualTo("cd_001"), "view slots are card labels, not instance IDs");
                Assert.That(DemoOnlineDeploymentAudit.CriticalUnitOccupiesLastCell(scene.Session.View, unit.instanceId), Is.True);
                Assert.That(DemoOnlineDeploymentAudit.CriticalUnitOccupiesLastCell(scene.Session.View, "cd_001"), Is.False);
                Assert.That(DemoOnlineDeploymentAudit.CriticalUnitOccupiesLastCell(scene.Session.View, "object-different"), Is.False);
                scene.Snapshot.players[0].battlefield = Array.Empty<BattlefieldObjectStateDto>();
                scene.Snapshot.players[0].unitSlots[last] = null;
                scene.Refresh();
                Assert.That(DemoOnlineDeploymentAudit.CriticalUnitOccupiesLastCell(scene.Session.View, unit.instanceId), Is.False);
            }
        }

        [TestCase("standard_meadow")]
        [TestCase("deep_caverns")]
        [TestCase("nether_lava_sea")]
        public void PreparationMaterialsUseExactOwnHandAndLivingFriendlyTarget(string arenaId)
        {
            using (var scene = new OnlineScene(arenaId, "tk_009"))
            {
                scene.AddObject(false, "cd_001", DemoSlotKind.Unit, 0);
                scene.Refresh();
                Assert.That(DemoOnlineBuildingProbe.TrySelectPreparationMaterial(scene.Session.View, out _, out _), Is.False,
                    "enemy units cannot be used as friendly material targets");
                var unit = scene.AddObject(true, "cd_001", DemoSlotKind.Unit, 0);
                scene.Refresh();
                Assert.That(DemoOnlineBuildingProbe.TrySelectPreparationMaterial(scene.Session.View, out var card, out var target), Is.True);
                Assert.That(card.handCardInstanceId, Is.EqualTo("hand-1"));
                Assert.That(target.InstanceId, Is.EqualTo(unit.instanceId));
                unit.health = 0;
                scene.Refresh();
                Assert.That(DemoOnlineBuildingProbe.TrySelectPreparationMaterial(scene.Session.View, out _, out _), Is.False);
                scene.Snapshot.players[0].hand[0] = "tk_010";
                scene.Snapshot.players[0].handCards[0].cardId = "tk_010";
                var building = scene.AddObject(true, "cd_007", DemoSlotKind.Building, 0, 2);
                scene.Refresh();
                Assert.That(DemoOnlineBuildingProbe.TrySelectPreparationMaterial(scene.Session.View, out card, out target), Is.True);
                Assert.That(card.cardId, Is.EqualTo("tk_010"));
                Assert.That(target.InstanceId, Is.EqualTo(building.instanceId));
                scene.Snapshot.phase = "COMBAT";
                scene.Refresh();
                Assert.That(DemoOnlineBuildingProbe.TrySelectPreparationMaterial(scene.Session.View, out _, out _), Is.False);
                scene.Snapshot.phase = "MAIN";
                scene.Snapshot.activePlayerIndex = 1;
                scene.Refresh();
                Assert.That(DemoOnlineBuildingProbe.TrySelectPreparationMaterial(scene.Session.View, out _, out _), Is.False);
                scene.Snapshot.activePlayerIndex = 0;
                scene.Snapshot.players[0].hand[0] = "cd_008";
                scene.Snapshot.players[0].handCards[0].cardId = "cd_008";
                scene.Refresh();
                Assert.That(DemoOnlineBuildingProbe.TrySelectPreparationMaterial(scene.Session.View, out _, out _), Is.False,
                    "pending cards cannot be consumed as implemented materials");
            }
        }

        [TestCase("active")]
        [TestCase("opponent")]
        [TestCase("combat")]
        [TestCase("win")]
        [TestCase("loss")]
        [TestCase("draw")]
        public void LockedHandPreservesSevenCardsInformationOpacityAndInputGates(string state)
        {
            using (var scene = new OnlineScene("standard_meadow", "ed_002"))
            {
                var ids = new[] { "ed_002", "ed_003", "ed_004", "ed_005", "ed_006", "ed_007", "ed_008" };
                scene.Snapshot.players[0].hand = ids;
                scene.Snapshot.players[0].handCards = ids.Select((id, index) => new HandCardStateDto
                    { cardId = id, handCardInstanceId = "hand-" + (index + 1) }).ToArray();
                scene.Refresh();
                var hand = scene.Controller.transform.Find("DemoCanvas/HandPlate/HandCards");
                foreach (var card in hand.GetComponentsInChildren<CardUI>()) Invoke(card, "AdvanceArrivalAnimation", 1f);
                var baseline = hand.GetComponentsInChildren<Text>().Select(value => value.color).ToArray();
                if (state == "opponent") scene.Snapshot.activePlayerIndex = 1;
                if (state == "combat") scene.Snapshot.phase = "COMBAT";
                if (state == "win" || state == "loss" || state == "draw")
                {
                    scene.Snapshot.status = "FINISHED";
                    scene.Snapshot.winnerPlayerId = state == "win" ? "alice" : state == "loss" ? "bob" : null;
                    scene.Snapshot.players[0].life = state == "win" ? 30 : 0;
                    scene.Snapshot.players[1].life = state == "loss" ? 30 : 0;
                }
                scene.Refresh();
                var group = hand.GetComponent<CanvasGroup>();
                var canUseHand = state == "active";
                Assert.That(group.alpha, Is.EqualTo(1f).Within(0.001f), "input locking must not dim card information");
                Assert.That(group.interactable, Is.EqualTo(canUseHand));
                Assert.That(group.blocksRaycasts, Is.EqualTo(canUseHand));
                Assert.That(hand.GetComponentsInChildren<CardUI>().Length, Is.EqualTo(7));
                foreach (var card in hand.GetComponentsInChildren<CardUI>())
                    Assert.That(card.GetComponent<Button>().colors.disabledColor.a, Is.EqualTo(1f).Within(0.001f),
                        "disabled frame must retain its textured reading surface");
                Assert.That(hand.GetComponentsInChildren<Text>().Select(value => value.color).ToArray(), Is.EqualTo(baseline));
                foreach (var text in hand.GetComponentsInChildren<Text>())
                {
                    var alpha = text.color.a * text.GetComponentsInParent<CanvasGroup>().Aggregate(1f, (value, ancestor) => value * ancestor.alpha);
                    Assert.That(alpha, Is.GreaterThanOrEqualTo(0.799f), text.name + " remains legible when its input is locked");
                }
                Assert.That(scene.Gateway.Commands, Is.Empty, "readability changes must not issue gameplay commands");
            }
        }

        [TestCase("active")]
        [TestCase("opponent")]
        [TestCase("combat")]
        [TestCase("finished")]
        public void ReadOnlyHandBrowserPreservesStableInstancesAndGameplayLocks(string state)
        {
            using (var scene = new OnlineScene("standard_meadow", "ed_002"))
            {
                scene.Snapshot.players[0].hand = new[] { "ed_002", "ed_008" };
                scene.Snapshot.players[0].handCards = new[]
                {
                    new HandCardStateDto { cardId = "ed_002", handCardInstanceId = "hand-1" },
                    new HandCardStateDto { cardId = "ed_008", handCardInstanceId = "hand-2", costModifier = -1, expiresAtEndOfTurnPlayerId = "alice" }
                };
                if (state == "opponent") scene.Snapshot.activePlayerIndex = 1;
                if (state == "combat") scene.Snapshot.phase = "COMBAT";
                if (state == "finished")
                {
                    scene.Snapshot.status = "FINISHED";
                    scene.Snapshot.winnerPlayerId = "bob";
                    scene.Snapshot.players[0].life = 0;
                }
                scene.Refresh();
                var beforeSelected = GetField(scene.Controller, "_selectedHandCardInstanceId");
                Invoke(scene.Controller, "OpenHandInspection");
                Invoke(scene.Controller, "MoveHandInspection", 1);
                var overlay = scene.Controller.transform.Find("DemoCanvas/HandInspectionOverlay");
                Assert.That(overlay.gameObject.activeSelf, Is.True);
                var card = overlay.GetComponentInChildren<CardUI>();
                Assert.That(card.HandCardInstanceId, Is.EqualTo("hand-2"));
                Assert.That(card.CardId, Is.EqualTo("ed_008"));
                Assert.That(card.DisplayedCost, Is.EqualTo(card.BaseCost - 1));
                var registry = (BiomeRivals.Content.CardContentRegistry)GetField(scene.Controller, "_registry");
                Assert.That(registry.TryGetText("ed_008", out var text), Is.True);
                Assert.That(card.FullRulesText, Is.EqualTo(text.rulesText));
                Assert.That(overlay.Find("ReadingPanel/RulesViewport/FullRules").GetComponent<Text>().text, Is.EqualTo(text.rulesText));
                Assert.That(card.transform.Find("Rules").GetComponent<RectTransform>().rect.height,
                    Is.EqualTo(card.RectTransform.rect.height * 0.2f).Within(0.01f));
                var readingButton = card.GetComponent<Button>();
                Assert.That(readingButton == null || !readingButton.IsInteractable(), Is.True);
                Assert.That(GetField(scene.Controller, "_selectedHandCardInstanceId"), Is.EqualTo(beforeSelected));
                Assert.That(scene.Controller.GetComponent<DemoBattlefieldPointerController>().InputEnabled, Is.False);
                Assert.That(scene.Controller.transform.Find("DemoCanvas/HandPlate/HandCards").GetComponent<CanvasGroup>().interactable, Is.False);
                Assert.That(scene.Gateway.Commands, Is.Empty);
                Invoke(scene.Controller, "CloseHandInspection");
                Assert.That(overlay.gameObject.activeSelf, Is.False);
                Assert.That(scene.Controller.GetComponent<DemoBattlefieldPointerController>().InputEnabled,
                    Is.EqualTo(state == "active" || state == "combat"), "inspection must not re-enable opponent or terminal world input");
                Assert.That(scene.Controller.transform.Find("DemoCanvas/HandPlate/HandCards").GetComponent<CanvasGroup>().interactable,
                    Is.EqualTo(state == "active"));
                Assert.That(scene.Gateway.Commands, Is.Empty);
                Invoke(scene.Controller, "OpenHandInspection");
                scene.Snapshot.players[0].hand = Array.Empty<string>();
                scene.Snapshot.players[0].handCards = Array.Empty<HandCardStateDto>();
                scene.Refresh();
                Assert.That(overlay.gameObject.activeSelf, Is.False, "removed instances must not leave stale private information visible");
            }
        }

        [TestCase("opponent")]
        [TestCase("win")]
        [TestCase("loss")]
        public void FullHandStatePreviewUsesLocalCommandsAndRetainsSevenInstances(string state)
        {
            var root = new GameObject("FullHandStatePreviewTest");
            try
            {
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                Invoke(controller, "SetupHandReadabilityStatePreview", state);
                var match = (DemoLocalMatch)GetField(controller, "_match");
                Assert.That(match.HandCards.Count, Is.EqualTo(7));
                Assert.That(match.HandCards.Select(card => card.handCardInstanceId).Distinct().Count(), Is.EqualTo(7));
                Assert.That(match.IsFinished, Is.EqualTo(state != "opponent"));
                if (state == "opponent") Assert.That(match.IsPlayerTurn, Is.False);
                else Assert.That(match.HasWinner && match.IsPlayerWinner == (state == "win"), Is.True);
                if (state == "loss") Assert.That(match.FatigueCount, Is.EqualTo(1));
                var hand = controller.transform.Find("DemoCanvas/HandPlate/HandCards").GetComponent<CanvasGroup>();
                Assert.That(hand.alpha, Is.EqualTo(1f));
                Assert.That(hand.interactable || hand.blocksRaycasts, Is.False);
                Assert.That(controller.GetComponent<DemoBattlefieldPointerController>().InputEnabled, Is.False);
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        private sealed class OnlineScene : IDisposable
        {
            private readonly GameObject _root = new GameObject("OnlineArenaInputTest");
            public readonly DemoSceneController Controller;
            public readonly DemoBattlefield3D Battlefield;
            public readonly MatchStateDto Snapshot;
            public readonly MatchStateStore Store = new MatchStateStore();
            public readonly CaptureGateway Gateway = new CaptureGateway();
            public readonly DemoOnlineMatchSession Session;
            private int _nextObject;

            public OnlineScene(string arenaId, string cardId)
            {
                Assert.That(ArenaLayouts.TryGet(arenaId, out var layout), Is.True);
                Battlefield = _root.AddComponent<DemoBattlefield3D>();
                Battlefield.Configure(Shader.Find("Standard"), Shader.Find("BiomeRivals/Demo/GroundSurface"));
                Controller = _root.AddComponent<DemoSceneController>();
                Controller.BuildNow(); // Exercise default -> authoritative topology transition.
                Snapshot = new MatchStateDto
                {
                    matchId = "arena-input-audit", viewerPlayerId = "alice", arenaId = arenaId,
                    protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                    status = "ACTIVE", phase = "MAIN", turn = 5, revision = 10,
                    players = Enumerable.Range(0, 2).Select(index => new PlayerStateDto
                    {
                        playerId = index == 0 ? "alice" : "bob", factionId = "snow_ice", life = 30,
                        redstone = 10, totalRedstone = 10, redstoneCapacity = 10,
                        hand = index == 0 ? new[] { cardId } : Array.Empty<string>(),
                        handCards = index == 0 ? new[] { new HandCardStateDto { cardId = cardId, handCardInstanceId = "hand-1" } } : Array.Empty<HandCardStateDto>(),
                        unitSlots = new string[layout.UnitSlotCount], buildingSlots = new string[layout.BuildingSlotCount]
                    }).ToArray()
                };
                Store.Replace(Snapshot);
                Session = new DemoOnlineMatchSession(Gateway, Store);
                SetField(Controller, "_onlineGateway", Gateway);
                SetField(Controller, "_onlineSession", Session);
                Refresh();
            }

            public BattlefieldObjectStateDto AddObject(bool friendly, string cardId, DemoSlotKind kind, int index, int width = 1)
            {
                var player = Snapshot.players[friendly ? 0 : 1];
                var value = new BattlefieldObjectStateDto
                {
                    instanceId = "object-" + ++_nextObject, ownerPlayerId = player.playerId,
                    cardId = cardId, cardType = kind == DemoSlotKind.Unit ? "UNIT" : width == 1 ? "BUILDING" : "STRUCTURE",
                    slotKind = kind == DemoSlotKind.Unit ? "UNIT" : "BUILDING", slotIndex = index,
                    occupiedSlots = width, attack = 2, health = 4, maxHealth = 4, summonedTurn = 1
                };
                player.battlefield = player.battlefield.Concat(new[] { value }).ToArray();
                var slots = kind == DemoSlotKind.Unit ? player.unitSlots : player.buildingSlots;
                for (var offset = 0; offset < width; offset++) slots[index + offset] = value.instanceId;
                return value;
            }

            public void Refresh()
            {
                Store.Replace(Snapshot);
                Invoke(Controller, "RefreshAll");
                Physics.SyncTransforms();
            }

            public void SelectHand()
            {
                var card = _root.transform.Find("DemoCanvas/HandPlate/HandCards").GetComponentsInChildren<CardUI>()
                    .Single(value => value.HandCardInstanceId == "hand-1");
                card.GetComponent<Button>().onClick.Invoke();
            }

            public void PointAt(DemoSlotKind kind, int index, bool drag)
            {
                var screen = Battlefield.BoardCamera.WorldToScreenPoint(Battlefield.GetSlotInteractionWorldPosition(true, kind, index));
                var pointer = _root.GetComponent<DemoBattlefieldPointerController>();
                // Explicit UI-block=false isolates the world input routing in EditMode.
                // GraphicRaycaster/real Player acceptance remain separate E2 runtime gates.
                var down = pointer.ProcessPointerFrame(screen, false, true, false);
                Assert.That(down, Is.Not.Null);
                Assert.That(down.Kind, Is.EqualTo(kind)); Assert.That(down.Index, Is.EqualTo(index));
                if (drag) Invoke(Controller, "OnHandCardDragDropped", (Vector2)screen);
                else pointer.ProcessPointerFrame(screen, false, false, true);
            }

            public void Dispose()
            {
                Session.Dispose();
                UnityEngine.Object.DestroyImmediate(_root);
            }
        }

        private static object Invoke(object target, string method, params object[] arguments) =>
            target.GetType().GetMethod(method, BindingFlags.Instance | BindingFlags.NonPublic).Invoke(target, arguments);
        private static object GetField(object target, string field) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).GetValue(target);
        private static void SetField(object target, string field, object value) =>
            target.GetType().GetField(field, BindingFlags.Instance | BindingFlags.NonPublic).SetValue(target, value);

        private sealed class CaptureGateway : IMatchGateway
        {
            public readonly List<MatchCommandDto> Commands = new List<MatchCommandDto>();
            public MatchConnectionStatus CurrentStatus => new MatchConnectionStatus(MatchConnectionPhase.Ready, "audit", "arena-input-audit");
            public event Action<MatchEventBatchDto> EventBatchReceived { add { } remove { } }
            public event Action<MatchStateDto> SnapshotReceived { add { } remove { } }
            public event Action<CommandRejectionDto> CommandRejected;
            public event Action<Exception> Faulted { add { } remove { } }
            public event Action<MatchConnectionStatus> ConnectionStateChanged { add { } remove { } }
            public Task ConnectAsync() => Task.CompletedTask;
            public Task DisconnectAsync() => Task.CompletedTask;
            public Task SendCommandAsync(MatchCommandDto command)
            {
                Commands.Add(command);
                CommandRejected?.Invoke(new CommandRejectionDto
                {
                    commandId = command.commandId, revision = command.expectedRevision,
                    code = "AUDIT_CAPTURE_ONLY", message = "Captured, not a server acceptance."
                });
                return Task.CompletedTask;
            }
            public void Dispose() { }
        }
    }
}
