using BiomeRivals.Content;
using BiomeRivals.Core;
using BiomeRivals.Demo.Editor;
using BiomeRivals.Networking;
using NUnit.Framework;
using System.Collections;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.UI;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoLocalMatchTests
    {
        [TestCase("standard_meadow", 4, 3)]
        [TestCase("plains_sunrise", 4, 3)]
        [TestCase("deep_caverns", 5, 2)]
        [TestCase("nether_lava_sea", 3, 4)]
        [TestCase("end_void", 3, 4)]
        [TestCase("deep_ocean", 4, 3)]
        [TestCase("desert_storm", 4, 3)]
        public void BattlefieldBuildsAndRaycastsEveryTerrainSpecificSlot(string arenaId, int unitSlotCount, int buildingSlotCount)
        {
            var root = new GameObject("TerrainSpecificBattlefieldSlotsTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                typeof(DemoSceneController).GetField("_match", BindingFlags.Instance | BindingFlags.NonPublic)
                    .SetValue(controller, new DemoLocalMatch(arenaId));
                controller.BuildNow();

                Assert.That(battlefield.ArenaId, Is.EqualTo(arenaId));
                Assert.That(battlefield.GetSlotCount(DemoSlotKind.Unit), Is.EqualTo(unitSlotCount));
                Assert.That(battlefield.GetSlotCount(DemoSlotKind.Building), Is.EqualTo(buildingSlotCount));
                foreach (var kind in new[] { DemoSlotKind.Unit, DemoSlotKind.Building })
                {
                    var slotCount = battlefield.GetSlotCount(kind);
                    Assert.That(battlefield.GetSlotWorldPosition(true, kind, 0).x,
                        Is.EqualTo(-battlefield.GetSlotWorldPosition(true, kind, slotCount - 1).x).Within(0.001f),
                        $"{arenaId} {kind} slots should remain centered on the 3D battlefield");
                    Assert.That(root.transform.Find($"BattlefieldGeometry/SlotMarker_Player_{kind}_{slotCount - 1}/InteractiveGround"),
                        Is.Not.Null, $"the last player-side {kind} slot should have a world surface");
                    Assert.That(root.transform.Find($"BattlefieldGeometry/SlotMarker_Opponent_{kind}_{slotCount - 1}/InteractiveGround"),
                        Is.Not.Null, $"the last opponent-side {kind} slot should have a world surface");
                    Assert.That(root.transform.Find($"BattlefieldGeometry/SlotMarker_Player_{kind}_{slotCount}/InteractiveGround"),
                        Is.Null, $"no extra player-side {kind} slot should be created");
                    Assert.That(root.transform.Find($"DemoCanvas/Player{kind}Slot{slotCount - 1}"), Is.Not.Null,
                        $"the final player-side {kind} UI cell should match the world layout");
                    Assert.That(root.transform.Find($"DemoCanvas/Opponent{kind}Slot{slotCount - 1}"), Is.Not.Null,
                        $"the final opponent-side {kind} UI cell should match the world layout");
                    Assert.That(root.transform.Find($"DemoCanvas/Player{kind}Slot{slotCount}"), Is.Null,
                        $"the UI must not leave an extra player-side {kind} cell from the default layout");

                    for (var sideIndex = 0; sideIndex < 2; sideIndex++)
                    {
                        var playerSide = sideIndex == 0;
                        for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
                        {
                            var screenPosition = battlefield.BoardCamera.WorldToScreenPoint(
                                battlefield.GetSlotInteractionWorldPosition(playerSide, kind, slotIndex));
                            Assert.That(battlefield.TryRaycastSlot(screenPosition, out var target), Is.True,
                                $"{arenaId} {(playerSide ? "player" : "opponent")} {kind} slot {slotIndex} should be clickable in world space");
                            Assert.That(target.Player, Is.EqualTo(playerSide));
                            Assert.That(target.Kind, Is.EqualTo(kind));
                            Assert.That(target.Index, Is.EqualTo(slotIndex));
                        }
                    }
                }

                var differentArenaId = arenaId == "standard_meadow" ? "deep_caverns" : "standard_meadow";
                Assert.Throws<System.InvalidOperationException>(() => battlefield.ConfigureArena(differentArenaId),
                    "changing the slot topology after scene geometry is built would desynchronize hit targets and UI");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase("deep_caverns", 5, 2)]
        [TestCase("nether_lava_sea", 3, 4)]
        public void AuthoritativeArenaRebuildsDefaultWorldAndUiAndRestoresLocalOnDisconnect(string arenaId, int units, int buildings)
        {
            var root = new GameObject("AuthoritativeArenaTransitionTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(Shader.Find("Standard"), Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                var oldSurface = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Unit_0");
                var canvas = root.transform.Find("DemoCanvas");
                var originalHudOrder = canvas.Cast<Transform>().Where(child => !child.name.Contains("Slot"))
                    .Select(child => child.name).ToArray();
                battlefield.SetSlotState(true, DemoSlotKind.Unit, 0, true, false);
                var players = Enumerable.Range(0, 2).Select(index => new PlayerStateDto
                {
                    playerId = index == 0 ? "alice" : "bob", factionId = "plains_forest", life = 30,
                    redstone = 1, totalRedstone = 1, redstoneCapacity = 1,
                    hand = System.Array.Empty<string>(), handCards = System.Array.Empty<HandCardStateDto>(),
                    unitSlots = new string[units], buildingSlots = new string[buildings]
                }).ToArray();
                var snapshot = new MatchStateDto
                {
                    matchId = "arena-transition", viewerPlayerId = "alice", arenaId = arenaId,
                    protocolVersion = GameVersions.Protocol, rulesetVersion = GameVersions.Ruleset,
                    status = "ACTIVE", phase = "MAIN", turn = 1, players = players
                };
                var store = new MatchStateStore();
                store.Replace(snapshot);
                var gateway = new FakeMatchGateway(new MatchConnectionStatus(MatchConnectionPhase.Ready, "ready", snapshot.matchId));
                SetControllerField(controller, "_onlineGateway", gateway);
                SetControllerField(controller, "_onlineSession", new DemoOnlineMatchSession(gateway, store));
                var refresh = typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic);
                refresh.Invoke(controller, null);
                Assert.That(battlefield.ArenaId, Is.EqualTo(arenaId));
                Assert.That(canvas.Cast<Transform>().Where(child => !child.name.Contains("Slot")).Select(child => child.name),
                    Is.EqualTo(originalHudOrder), "world topology changes must not raise the hand plate over turn controls");
                Assert.That(oldSurface == null || !oldSurface.gameObject.activeInHierarchy, Is.True,
                    "old topology must not retain live raycast/highlight surfaces");
                Assert.That(battlefield.HasActiveGameplayHighlights, Is.False);
                Assert.That(root.GetComponentsInChildren<DemoBattlefieldSlotTarget>().Length, Is.EqualTo(28), "surface + footprint per logical slot");
                foreach (var side in new[] { true, false })
                foreach (var kind in new[] { DemoSlotKind.Unit, DemoSlotKind.Building })
                {
                    var count = kind == DemoSlotKind.Unit ? units : buildings;
                    Assert.That(battlefield.GetSlotCount(kind), Is.EqualTo(count));
                    for (var index = 0; index < count; index++)
                    {
                        var screen = battlefield.BoardCamera.WorldToScreenPoint(battlefield.GetSlotInteractionWorldPosition(side, kind, index));
                        Assert.That(battlefield.TryRaycastSlot(screen, out var hit), Is.True);
                        Assert.That(hit.Player, Is.EqualTo(side)); Assert.That(hit.Kind, Is.EqualTo(kind));
                        Assert.That(hit.Index, Is.EqualTo(index));
                        Assert.That(root.transform.Find($"DemoCanvas/{(side ? "Player" : "Opponent")}{kind}Slot{index}"), Is.Not.Null);
                    }
                    Assert.That(root.transform.Find($"DemoCanvas/{(side ? "Player" : "Opponent")}{kind}Slot{count}"), Is.Null);
                }
                var newSurface = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Unit_0");
                store.Replace(snapshot); // Same-layout recovery must not destroy/recreate the world.
                refresh.Invoke(controller, null);
                Assert.That(root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Unit_0"), Is.SameAs(newSurface));
                gateway.SetStatus(new MatchConnectionStatus(MatchConnectionPhase.Offline));
                refresh.Invoke(controller, null);
                Assert.That(battlefield.ArenaId, Is.EqualTo(ArenaLayouts.DefaultArenaId));
                Assert.That(battlefield.GetSlotCount(DemoSlotKind.Unit), Is.EqualTo(4));
                Assert.That(battlefield.GetSlotCount(DemoSlotKind.Building), Is.EqualTo(3));
                Assert.That(root.GetComponentsInChildren<DemoBattlefieldSlotTarget>().Length, Is.EqualTo(28));
            }
            finally { UnityEngine.Object.DestroyImmediate(root); }
        }

        [Test]
        public void EmptyUnitSlotSearchUsesFullTerrainCapacityAndOccupiedStructureRanges()
        {
            var battlefield = Enumerable.Range(0, 4).Select(index => new DemoBattlefieldObject
            {
                SlotKind = DemoSlotKind.Unit,
                SlotIndex = index,
                OccupiedSlots = 1,
                Health = 2
            }).ToList();

            Assert.That(DemoDeploymentRules.FindFirstEmptyUnitSlot(battlefield, 5), Is.EqualTo(4),
                "a fifth slot on a deep-caverns arena remains available after the first four fill");
            Assert.That(DemoDeploymentRules.FindFirstEmptyUnitSlot(battlefield, 4), Is.EqualTo(-1),
                "standard four-slot arenas are full when their four legal slots are occupied");

            battlefield.Add(new DemoBattlefieldObject
            {
                SlotKind = DemoSlotKind.Unit,
                SlotIndex = 3,
                OccupiedSlots = 2,
                Health = 8
            });
            Assert.That(DemoDeploymentRules.FindFirstEmptyUnitSlot(battlefield, 5), Is.EqualTo(-1),
                "multi-slot objects reserve every legal slot in their occupied range");
            battlefield.RemoveAt(battlefield.Count - 1);

            battlefield.Add(new DemoBattlefieldObject
            {
                SlotKind = DemoSlotKind.Unit,
                SlotIndex = 4,
                OccupiedSlots = 1,
                Health = 0
            });
            Assert.That(DemoDeploymentRules.FindFirstEmptyUnitSlot(battlefield, 5), Is.EqualTo(4),
                "a defeated object does not block a slot while awaiting view cleanup");
        }

        [Test]
        public void SceneControllerKeepsFifthDeepCavernsSlotAvailableForSummonReadiness()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            var match = CreateScenarioMatch("deep_caverns");
            for (var index = 0; index < 4; index++)
            {
                match.ResetHand(new[] { bee.id });
                var deployed = match.ApplyDeploy(bee,
                    match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, index));
                Assert.That(deployed.Accepted, Is.True, deployed.Message);
            }

            var root = new GameObject("DeepCavernsSummonReadinessTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                SetControllerField(controller, "_match", match);
                controller.BuildNow();
                var hasEmptySlot = typeof(DemoSceneController).GetMethod(
                    "HasEmptyUnitSlot", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(hasEmptySlot, Is.Not.Null);

                Assert.That(hasEmptySlot.Invoke(controller, new object[] { true }), Is.EqualTo(true),
                    "the UI-side effect availability check must expose the fifth legal unit slot");
                match.ResetHand(new[] { bee.id });
                var fifthDeployment = match.ApplyDeploy(bee,
                    match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 4));
                Assert.That(fifthDeployment.Accepted, Is.True, fifthDeployment.Message);
                Assert.That(hasEmptySlot.Invoke(controller, new object[] { true }), Is.EqualTo(false),
                    "once the fifth slot is occupied, the same UI-side check must report a full row");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase("nt_008", true, "ready")]
        [TestCase("nt_008", false, "ready")]
        [TestCase("nt_008", true, "waiting")]
        [TestCase("nt_008", false, "waiting")]
        [TestCase("nt_008", true, "full")]
        [TestCase("nt_008", false, "full")]
        [TestCase("nt_008", true, "no-energy")]
        [TestCase("nt_008", false, "no-energy")]
        [TestCase("nt_008", true, "temporary-energy")]
        [TestCase("nt_008", false, "temporary-energy")]
        [TestCase("nt_008", true, "finished")]
        [TestCase("nt_008", false, "finished")]
        [TestCase("cd_008", true, "ready")]
        [TestCase("cd_008", false, "ready")]
        [TestCase("cd_008", true, "waiting")]
        [TestCase("cd_008", false, "waiting")]
        [TestCase("cd_008", true, "full")]
        [TestCase("cd_008", false, "full")]
        [TestCase("cd_008", true, "finished")]
        [TestCase("cd_008", false, "finished")]
        public void SummonReadinessMatchesPublicSnapshotNameplateAndWorldSurface(string cardId, bool player, string state)
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition(cardId, out var definition), Is.True);
            var ownerIndex = player ? 0 : 1;
            var players = new[]
            {
                new PlayerStateDto { playerId = "alice", life = 30, mulliganCompleted = true, redstoneCapacity = 1 },
                new PlayerStateDto { playerId = "bob", life = 30, mulliganCompleted = true, redstoneCapacity = 1 }
            };
            var owner = players[ownerIndex];
            // Mansion is ready even with no redstone; fortress may pay temporary redstone.
            owner.redstone = cardId == "nt_008" && state != "no-energy" && state != "temporary-energy" ? 1 : 0;
            owner.temporaryRedstone = state == "temporary-energy" ? 1 : 0;
            owner.totalRedstone = owner.redstone + owner.temporaryRedstone;
            var objects = new System.Collections.Generic.List<BattlefieldObjectStateDto>
            {
                new BattlefieldObjectStateDto
                {
                    instanceId = "object-1", ownerPlayerId = owner.playerId, cardId = cardId,
                    cardType = "BUILDING", slotKind = "BUILDING", slotIndex = 0,
                    occupiedSlots = definition.buildingSlots, health = definition.health, maxHealth = definition.health
                }
            };
            for (var index = 0; index < definition.buildingSlots; index++) owner.buildingSlots[index] = "object-1";
            if (state == "full")
            {
                for (var index = 0; index < owner.unitSlots.Length; index++)
                {
                    var instanceId = $"object-{index + 2}";
                    owner.unitSlots[index] = instanceId;
                    objects.Add(new BattlefieldObjectStateDto
                    {
                        instanceId = instanceId, ownerPlayerId = owner.playerId, cardId = "pf_001",
                        cardType = "UNIT", slotKind = "UNIT", slotIndex = index,
                        occupiedSlots = 1, attack = 1, health = 1, maxHealth = 1
                    });
                }
            }
            owner.battlefield = objects.ToArray();
            var store = new MatchStateStore();
            var snapshot = new MatchStateDto
            {
                matchId = "summon-readiness", viewerPlayerId = "alice", protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset, turn = 2, phase = "MAIN",
                status = state == "finished" ? "FINISHED" : "ACTIVE",
                winnerPlayerId = state == "finished" ? owner.playerId : string.Empty,
                activePlayerIndex = state == "waiting" ? 1 - ownerIndex : ownerIndex, players = players
            };
            if (state == "finished") players[1 - ownerIndex].life = 0;
            store.Replace(snapshot);
            var root = new GameObject("SummonReadinessSnapshotTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                var gateway = new FakeMatchGateway(new MatchConnectionStatus(MatchConnectionPhase.Matchmaking));
                SetControllerField(controller, "_onlineGateway", gateway);
                SetControllerField(controller, "_onlineSession", new DemoOnlineMatchSession(gateway, store));
                typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);

                var expectedStatus = state == "waiting" ? player ? "等待己方回合" : "等待敌方回合"
                    : state == "full" ? "单位格已满" : state == "no-energy" ? "红石不足"
                    : state == "finished" ? "对局已结束" : "结束阶段就绪";
                var expectedKind = state == "ready" || state == "temporary-energy"
                    ? cardId == "nt_008" ? DemoEngineReadyKind.NetherFortress : DemoEngineReadyKind.Mansion
                    : DemoEngineReadyKind.None;
                var side = player ? "Player" : "Opponent";
                var label = root.transform.Find($"DemoCanvas/{side}BuildingSlot0/Content/WorldLabelText").GetComponent<Text>();
                Assert.That(label.text, Does.Contain(expectedStatus));
                Assert.That(label.text.Split('\n').Length, Is.EqualTo(2), "name/stats and readiness have separate lines");
                Assert.That(label.resizeTextMinSize, Is.EqualTo(18), "summon status must remain 12px at 1280x720");
                Assert.That(label.rectTransform.sizeDelta.y, Is.EqualTo(48));
                AssertSummonEngineKind(battlefield, player, definition.buildingSlots, expectedKind);

                // Refresh must clear an existing glow, not only render an initially inactive surface.
                if (expectedKind != DemoEngineReadyKind.None)
                {
                    snapshot.activePlayerIndex = 1 - ownerIndex;
                    owner.temporaryRedstone = 0;
                    owner.totalRedstone = owner.redstone;
                    store.Replace(snapshot);
                    typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic)
                        .Invoke(controller, null);
                    AssertSummonEngineKind(battlefield, player, definition.buildingSlots, DemoEngineReadyKind.None);
                }
                Assert.That(owner.battlefield.Length, Is.EqualTo(objects.Count), "presentation cannot summon units");
                Assert.That(owner.redstone, Is.EqualTo(cardId == "nt_008" && state != "no-energy" && state != "temporary-energy" ? 1 : 0),
                    "presentation cannot spend redstone");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase("nt_008", DemoEngineReadyKind.NetherFortress)]
        [TestCase("cd_008", DemoEngineReadyKind.Mansion)]
        public void LocalEnemySummonReadinessFollowsRealTurnAndResolution(string cardId, DemoEngineReadyKind readyKind)
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition(cardId, out var definition), Is.True);
            var match = CreateScenarioMatch();
            var root = new GameObject("LocalEnemySummonReadinessTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                SetControllerField(controller, "_match", match);
                controller.BuildNow();
                var refresh = typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic);
                // BuildNow prepares its default factions; seed this scenario afterwards.
                match.ResetDeckAndHand(new[] { "pf_001", "pf_001" }, new string[0]);
                match.ResetOpponent(new[] { definition });
                refresh.Invoke(controller, null);
                AssertSummonEngineKind(battlefield, false, definition.buildingSlots, DemoEngineReadyKind.None);
                match.EndPlayerTurn();
                refresh.Invoke(controller, null);
                AssertSummonEngineKind(battlefield, false, definition.buildingSlots, readyKind);
                var label = root.transform.Find("DemoCanvas/OpponentBuildingSlot0/Content/WorldLabelText").GetComponent<Text>();
                Assert.That(label.text, Does.Contain("结束阶段就绪"));
                match.BeginNextPlayerTurn();
                refresh.Invoke(controller, null);
                AssertSummonEngineKind(battlefield, false, definition.buildingSlots, DemoEngineReadyKind.None);
                Assert.That(match.OpponentBattlefield.Any(value => value.CardId == (cardId == "nt_008" ? "tk_015" : "tk_011")), Is.True);
                label = root.transform.Find("DemoCanvas/OpponentBuildingSlot0/Content/WorldLabelText").GetComponent<Text>();
                Assert.That(label.text, Does.Contain("等待敌方回合"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        private static void AssertSummonEngineKind(DemoBattlefield3D battlefield, bool player, int occupiedSlots, DemoEngineReadyKind expected)
        {
            var markers = (IDictionary)typeof(DemoBattlefield3D)
                .GetField("_slotMarkers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(battlefield);
            for (var index = 0; index < occupiedSlots; index++)
            {
                var marker = markers[$"{player}:Building:{index}"];
                Assert.That(marker, Is.Not.Null);
                Assert.That(marker.GetType().GetField("EngineReadyKind").GetValue(marker), Is.EqualTo(expected),
                    $"every occupied ground surface for {(player ? "player" : "opponent")} must agree with the nameplate");
            }
        }

        [Test]
        public void SummonReadinessPreviewSettlesThroughLegalMansionDeployAndEnemyTurn()
        {
            var root = new GameObject("SummonReadinessPreviewTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                typeof(DemoSceneController).GetMethod("SetupSummonReadinessPreview", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                var match = (DemoLocalMatch)GetControllerField(controller, "_match");
                Assert.That(match.IsPlayerTurn, Is.False);
                Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0)?.CardId, Is.EqualTo("tk_011"));
                Assert.That(match.OpponentEnergy, Is.EqualTo(1));
                Assert.That((string)GetControllerField(controller, "_selectedHandCardInstanceId"),
                    Is.EqualTo(match.HandCards[0].handCardInstanceId), "details must show the remaining real hand instance");
                AssertSummonEngineKind(battlefield, true, 3, DemoEngineReadyKind.None);
                AssertSummonEngineKind(battlefield, false, 3, DemoEngineReadyKind.NetherFortress);
                Assert.That(root.transform.Find("DemoCanvas/PlayerBuildingSlot0/Content/WorldLabelText")
                    .GetComponent<Text>().text, Does.Contain("等待己方回合"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [TestCase("pf_005", DemoEngineReadyKind.Nursery)]
        [TestCase("or_007", DemoEngineReadyKind.Coral)]
        [TestCase("db_004", DemoEngineReadyKind.Cactus)]
        [TestCase("cd_004", DemoEngineReadyKind.Sculk)]
        [TestCase("db_007", DemoEngineReadyKind.Temple)]
        [TestCase("cd_007", DemoEngineReadyKind.Mine)]
        [TestCase("cd_008", DemoEngineReadyKind.Mansion)]
        [TestCase("si_008", DemoEngineReadyKind.IceSpire)]
        [TestCase("si_007", DemoEngineReadyKind.SnowHut)]
        [TestCase("ed_007", DemoEngineReadyKind.EndCrystal)]
        [TestCase("nt_008", DemoEngineReadyKind.NetherFortress)]
        [TestCase("nt_003", DemoEngineReadyKind.Blaze)]
        [TestCase("or_008", DemoEngineReadyKind.None)]
        public void FinishedSnapshotClearsAllActiveWorldCuesAndSelectionsOnBothSides(string cardId, DemoEngineReadyKind activeKind)
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition(cardId, out var definition), Is.True);
            foreach (var player in new[] { true, false })
            {
                var ownerIndex = player ? 0 : 1;
                var players = new[]
                {
                    new PlayerStateDto { playerId = "alice", life = 30, redstoneCapacity = 1, redstone = 1, totalRedstone = 1, mulliganCompleted = true },
                    new PlayerStateDto { playerId = "bob", life = 30, redstoneCapacity = 1, redstone = 1, totalRedstone = 1, mulliganCompleted = true }
                };
                var owner = players[ownerIndex];
                var other = players[1 - ownerIndex];
                var sourceKind = definition.cardType == "UNIT" ? DemoSlotKind.Unit : DemoSlotKind.Building;
                var source = new BattlefieldObjectStateDto
                {
                    instanceId = "object-1", ownerPlayerId = owner.playerId, cardId = cardId,
                    cardType = definition.cardType, slotKind = sourceKind == DemoSlotKind.Unit ? "UNIT" : "BUILDING",
                    occupiedSlots = sourceKind == DemoSlotKind.Unit ? 1 : definition.buildingSlots,
                    health = definition.health, maxHealth = definition.health, attack = definition.attack, summonedTurn = 1
                };
                var sourceSlots = sourceKind == DemoSlotKind.Unit ? owner.unitSlots : owner.buildingSlots;
                for (var index = 0; index < source.occupiedSlots; index++) sourceSlots[index] = source.instanceId;
                var injured = new BattlefieldObjectStateDto
                {
                    instanceId = "object-2", ownerPlayerId = owner.playerId, cardId = "pf_001", cardType = "UNIT",
                    slotKind = "UNIT", slotIndex = sourceKind == DemoSlotKind.Unit ? 1 : 0,
                    occupiedSlots = 1, health = 1, maxHealth = 2, attack = 1, summonedTurn = 1
                };
                owner.unitSlots[injured.slotIndex] = injured.instanceId;
                owner.battlefield = new[] { source, injured };
                var enemyUnit = new BattlefieldObjectStateDto
                {
                    instanceId = "object-3", ownerPlayerId = other.playerId, cardId = "pf_001", cardType = "UNIT",
                    slotKind = "UNIT", occupiedSlots = 1, health = 2, maxHealth = 2, attack = 1, summonedTurn = 1
                };
                other.unitSlots[0] = enemyUnit.instanceId;
                other.battlefield = new[] { enemyUnit };
                var activeIndex = cardId == "cd_004" ? 1 - ownerIndex : ownerIndex;
                players[activeIndex].cardsPlayedThisTurn = 1;
                var snapshot = new MatchStateDto
                {
                    matchId = "terminal-world-cues", viewerPlayerId = "alice", protocolVersion = GameVersions.Protocol,
                    rulesetVersion = GameVersions.Ruleset, status = "ACTIVE", phase = "COMBAT", turn = 2,
                    activePlayerIndex = activeIndex, players = players
                };
                var store = new MatchStateStore();
                store.Replace(snapshot);
                var root = new GameObject("TerminalWorldCuesTest");
                try
                {
                    var battlefield = root.AddComponent<DemoBattlefield3D>();
                    battlefield.Configure(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                        Shader.Find("BiomeRivals/Demo/GroundSurface"));
                    var controller = root.AddComponent<DemoSceneController>();
                    controller.BuildNow();
                    var gateway = new FakeMatchGateway(new MatchConnectionStatus(MatchConnectionPhase.Matchmaking));
                    SetControllerField(controller, "_onlineGateway", gateway);
                    SetControllerField(controller, "_onlineSession", new DemoOnlineMatchSession(gateway, store));
                    var refresh = typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic);
                    refresh.Invoke(controller, null);
                    var markers = (IDictionary)typeof(DemoBattlefield3D)
                        .GetField("_slotMarkers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(battlefield);
                    var sourceMarker = markers[$"{player}:{sourceKind}:0"];
                    Assert.That(sourceMarker.GetType().GetField("EngineReadyKind").GetValue(sourceMarker), Is.EqualTo(activeKind),
                        "the active fixture must prove a cue exists before terminal cleanup");
                    if (cardId == "or_008")
                    {
                        var threat = markers[$"{!player}:Unit:0"];
                        Assert.That(threat.GetType().GetField("EndPhaseThreat").GetValue(threat), Is.EqualTo(true));
                    }
                    var pointer = root.GetComponent<DemoBattlefieldPointerController>();
                    var screenPoint = battlefield.BoardCamera
                        .WorldToScreenPoint(battlefield.GetSlotInteractionWorldPosition(player, DemoSlotKind.Unit, injured.slotIndex));
                    Physics.SyncTransforms();
                    Assert.That(pointer.InputEnabled, Is.EqualTo(activeIndex == 0), "opponent turns must not accept world input");
                    if (activeIndex != 0)
                    {
                        Assert.That(pointer.ProcessPointerFrame(screenPoint, false, true, false), Is.Null);
                        // Explicitly seed stale pointer state for the terminal-cleanup stress fixture;
                        // do not assume production opponent input is enabled just to establish a dirty precondition.
                        pointer.SetInputEnabled(true);
                    }
                    Assert.That(pointer.ProcessPointerFrame(screenPoint, false, true, false), Is.Not.Null);
                    // Also cover a multi-cell rejected deployment that the pointer's one hit cannot clear alone.
                    battlefield.SetSlotRangeHovered(true, DemoSlotKind.Building, 0, 3, true, true);
                    battlefield.SetSlotRangePressed(true, DemoSlotKind.Building, 0, 3, true, true);
                    SetControllerField(controller, "_selectedAttackerInstanceId", source.instanceId);
                    SetControllerField(controller, "_selectedDeploymentTargetInstanceId", injured.instanceId);
                    SetControllerField(controller, "_pendingTargetCardId", "pf_006");
                    ((System.Collections.Generic.List<string>)GetControllerField(controller, "_selectedCardTargetInstanceIds"))
                        .Add(enemyUnit.instanceId);

                    snapshot.status = "FINISHED";
                    snapshot.winnerPlayerId = owner.playerId;
                    other.life = 0;
                    store.Replace(snapshot);
                    refresh.Invoke(controller, null);

                    Assert.That(pointer.InputEnabled, Is.False);
                    Assert.That(battlefield.HasActiveGameplayHighlights, Is.False);
                    Assert.That(pointer.ProcessPointerFrame(screenPoint, false, false, true), Is.Null);
                    foreach (var field in new[] { "_selectedCardId", "_selectedHandCardInstanceId", "_selectedAttackerInstanceId",
                        "_selectedDeploymentTargetInstanceId", "_pendingTargetCardId" })
                        Assert.That(GetControllerField(controller, field), Is.Null, field);
                    Assert.That((ICollection)GetControllerField(controller, "_selectedCardTargetInstanceIds"), Is.Empty);
                    foreach (DictionaryEntry entry in markers)
                    {
                        foreach (var field in new[] { "ValidTarget", "PriorityTarget", "EndPhaseThreat", "Hovered", "Pressed", "HoverRejected", "PressRejected" })
                            Assert.That(entry.Value.GetType().GetField(field).GetValue(entry.Value), Is.EqualTo(false), $"{entry.Key} {field}");
                        Assert.That(entry.Value.GetType().GetField("EngineReadyKind").GetValue(entry.Value), Is.EqualTo(DemoEngineReadyKind.None));
                    }
                    var texts = root.GetComponentsInChildren<Text>().Where(value => value.name == "WorldLabelText").Select(value => value.text).ToArray();
                    foreach (var staleCue in new[] { "待触发", "等待出土", "监听敌方", "结束阶段就绪", "神殿锁定" })
                        Assert.That(texts.Any(value => value.Contains(staleCue)), Is.False, staleCue);
                    Assert.That(texts.Any(value => value.Contains(registry.TryGetText(cardId, out var cardText) ? cardText.name : cardId)), Is.True);
                    Assert.That(owner.battlefield.Length, Is.EqualTo(2));
                    Assert.That(other.battlefield.Length, Is.EqualTo(1));
                    Assert.That(owner.totalRedstone, Is.EqualTo(1), "terminal presentation must not spend resources");
                    Assert.That(root.transform.Find("DemoCanvas/EndTurnButton").GetComponent<Button>().interactable, Is.False);
                    Assert.That(root.transform.Find("DemoCanvas/HandPlate/HandCards").GetComponent<CanvasGroup>().interactable, Is.False);
                }
                finally
                {
                    UnityEngine.Object.DestroyImmediate(root);
                }
            }
        }

        [Test]
        public void ClearingSlotInteractionsPreservesPassiveStateAndEventPulse()
        {
            var root = new GameObject("ClearInteractionsPreservesFinalStateTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                battlefield.SetSlotState(true, DemoSlotKind.Unit, 0, true, true, true);
                battlefield.SetSlotAura(true, DemoSlotKind.Unit, 0, 2);
                battlefield.SetSlotPoisoned(true, DemoSlotKind.Unit, 0, true);
                battlefield.SetSlotBurning(true, DemoSlotKind.Unit, 0, true);
                battlefield.SetSlotWithered(true, DemoSlotKind.Unit, 0, true);
                battlefield.PulseSlotRange(true, DemoSlotKind.Unit, 0, 1, Color.yellow);
                battlefield.SetSlotRangeHovered(true, DemoSlotKind.Unit, 0, 2, true, true);
                battlefield.SetSlotRangePressed(true, DemoSlotKind.Unit, 0, 2, true, true);
                battlefield.ClearSlotInteractions();
                var markers = (IDictionary)typeof(DemoBattlefield3D)
                    .GetField("_slotMarkers", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(battlefield);
                var marker = markers["True:Unit:0"];
                foreach (var field in new[] { "Occupied", "Burning", "Withered", "Poisoned" })
                    Assert.That(marker.GetType().GetField(field).GetValue(marker), Is.EqualTo(true));
                Assert.That(marker.GetType().GetField("AuraLayers").GetValue(marker), Is.EqualTo(2));
                Assert.That((float)marker.GetType().GetField("PresentationPulseDuration").GetValue(marker), Is.GreaterThan(0));
                foreach (DictionaryEntry entry in markers)
                    foreach (var field in new[] { "ValidTarget", "PriorityTarget", "Hovered", "Pressed", "HoverRejected", "PressRejected" })
                        Assert.That(entry.Value.GetType().GetField(field).GetValue(entry.Value), Is.EqualTo(false));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void TerminalWorldPreviewFinishesThroughLethalHeroAttackAndKeepsReactiveBuildings()
        {
            var root = new GameObject("TerminalWorldPreviewTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                typeof(DemoSceneController).GetMethod("SetupTerminalWorldPreview", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                var match = (DemoLocalMatch)GetControllerField(controller, "_match");
                Assert.That(match.IsFinished && match.HasWinner && match.IsPlayerWinner, Is.True);
                Assert.That(match.PlayerLife, Is.EqualTo(30));
                Assert.That(match.OpponentLife, Is.Zero);
                Assert.That(match.GetObject(true, DemoSlotKind.Building, 0)?.CardId, Is.EqualTo("or_007"));
                Assert.That(match.GetObject(false, DemoSlotKind.Building, 0)?.CardId, Is.EqualTo("si_008"));
                Assert.That(battlefield.HasActiveGameplayHighlights, Is.False);
                Assert.That(root.GetComponent<DemoBattlefieldPointerController>().InputEnabled, Is.False);
                Assert.That((bool)GetControllerField(controller, "_matchOutcomePreviewCompleted"), Is.True);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OpponentResourceHudTracksCurrentAndUpcomingPublicEnergy()
        {
            var root = new GameObject("OpponentEnergyUiTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();

                var matchField = typeof(DemoSceneController).GetField("_match", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(matchField, Is.Not.Null);
                var match = (DemoLocalMatch)matchField.GetValue(controller);
                var resourceText = root.transform.Find("DemoCanvas/OpponentEnergyPlate/Resource").GetComponent<Text>();
                var refresh = typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(refresh, Is.Not.Null);

                Assert.That(resourceText.text, Is.EqualTo("敌方红石 · 下回合 1/1"));
                Assert.That(resourceText.raycastTarget, Is.False, "the informational plate must not intercept battlefield input");
                Assert.That(resourceText.transform.parent.GetComponent<Image>().raycastTarget, Is.False,
                    "the stone plate must not intercept battlefield input either");

                match.EndPlayerTurn();
                Assert.That(match.IsPlayerTurn, Is.False);
                refresh.Invoke(controller, null);
                Assert.That(resourceText.text, Is.EqualTo("敌方红石 ◆ 1/1"));
                Assert.That(match.OpponentEnergy, Is.EqualTo(1));
                Assert.That(match.OpponentMaxEnergy, Is.EqualTo(1));

                match.BeginNextPlayerTurn();
                refresh.Invoke(controller, null);
                Assert.That(resourceText.text, Is.EqualTo("敌方红石 · 下回合 2/2"));

                var store = new MatchStateStore();
                store.Replace(new MatchStateDto
                {
                    matchId = "opponent-energy-view",
                    viewerPlayerId = "alice",
                    protocolVersion = GameVersions.Protocol,
                    rulesetVersion = GameVersions.Ruleset,
                    status = "ACTIVE",
                    turn = 6,
                    phase = "MAIN",
                    activePlayerIndex = 1,
                    players = new[]
                    {
                        new PlayerStateDto
                        {
                            playerId = "alice", life = 30, redstone = 6, totalRedstone = 6, redstoneCapacity = 6
                        },
                        new PlayerStateDto
                        {
                            playerId = "bob", life = 30, redstone = 4, temporaryRedstone = 2,
                            totalRedstone = 6, redstoneCapacity = 7
                        }
                    }
                });
                var authoritativeView = new DemoAuthoritativeMatchView(store);
                var format = typeof(DemoSceneController).GetMethod("FormatOpponentEnergy", BindingFlags.Static | BindingFlags.NonPublic);
                Assert.That(format, Is.Not.Null);
                Assert.That(authoritativeView.OpponentEnergy, Is.EqualTo(6));
                Assert.That(authoritativeView.OpponentMaxEnergy, Is.EqualTo(7));
                Assert.That(authoritativeView.OpponentTemporaryEnergy, Is.EqualTo(2));
                Assert.That(format.Invoke(null, new object[] { authoritativeView }),
                    Is.EqualTo("敌方红石 ◆ 6/7 · 临时 +2"));

                store.Replace(new MatchStateDto
                {
                    matchId = "opponent-energy-first-turn",
                    viewerPlayerId = "alice",
                    protocolVersion = GameVersions.Protocol,
                    rulesetVersion = GameVersions.Ruleset,
                    status = "ACTIVE",
                    turn = 1,
                    phase = "MAIN",
                    activePlayerIndex = 0,
                    players = new[]
                    {
                        new PlayerStateDto { playerId = "alice", life = 30, redstone = 1, totalRedstone = 1, redstoneCapacity = 1 },
                        new PlayerStateDto { playerId = "bob", life = 30, redstone = 1, totalRedstone = 1, redstoneCapacity = 1 }
                    }
                });
                authoritativeView = new DemoAuthoritativeMatchView(store);
                Assert.That(format.Invoke(null, new object[] { authoritativeView }),
                    Is.EqualTo("敌方红石 · 下回合 1/1"));

                store.Current.turn = 2;
                Assert.That(format.Invoke(null, new object[] { authoritativeView }),
                    Is.EqualTo("敌方红石 · 下回合 2/2"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void MatchEndedBannerMapsAuthoritativeDrawSeparatelyFromVictoryAndDefeat()
        {
            var resolve = typeof(DemoSceneController).GetMethod(
                "ResolveMatchEndOutcome", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);

            Assert.That(resolve.Invoke(null, new object[] { "alice", "alice" }).ToString(), Is.EqualTo("PlayerWon"));
            Assert.That(resolve.Invoke(null, new object[] { "bob", "alice" }).ToString(), Is.EqualTo("PlayerLost"));
            Assert.That(resolve.Invoke(null, new object[] { null, "alice" }).ToString(), Is.EqualTo("Draw"));
            Assert.That(resolve.Invoke(null, new object[] { string.Empty, "alice" }).ToString(), Is.EqualTo("Draw"));
        }

        [Test]
        public void LocalMatchEndBannerUsesSameThreeOutcomeSemantics()
        {
            var resolve = typeof(DemoSceneController).GetMethod(
                "ResolveLocalMatchEndOutcome", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);

            Assert.That(resolve.Invoke(null, new object[] { true, true }).ToString(), Is.EqualTo("PlayerWon"));
            Assert.That(resolve.Invoke(null, new object[] { true, false }).ToString(), Is.EqualTo("PlayerLost"));
            Assert.That(resolve.Invoke(null, new object[] { false, false }).ToString(), Is.EqualTo("Draw"));
        }

        [Test]
        public void SelectedHandCardDoesNotFallBackToAnotherCopyWhenItsInstanceDisappears()
        {
            var resolve = typeof(DemoSceneController).GetMethod(
                "ResolveHandCardInstanceId", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(resolve, Is.Not.Null);
            var hand = new[]
            {
                new HandCardStateDto { cardId = "tk_016", handCardInstanceId = "hand-first" },
                new HandCardStateDto { cardId = "tk_016", handCardInstanceId = "hand-discounted" }
            };

            Assert.That(resolve.Invoke(null, new object[] { hand, "tk_016", "hand-discounted" }), Is.EqualTo("hand-discounted"));
            Assert.That(resolve.Invoke(null, new object[] { hand, "tk_016", "hand-no-longer-in-hand" }), Is.EqualTo(string.Empty));
            Assert.That(resolve.Invoke(null, new object[] { hand, "tk_016", string.Empty }), Is.EqualTo("hand-first"));
        }

        [Test]
        public void LocalHandCardInstancesKeepTheirIdentityAndExactTemporaryCostThroughRemoval()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("tk_016", out var shell), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "tk_016", "tk_016" });
            var originalInstances = match.HandCards.ToArray();
            var firstInstanceId = originalInstances[0].handCardInstanceId;
            var discountedInstanceId = originalInstances[1].handCardInstanceId;

            Assert.That(firstInstanceId, Is.Not.EqualTo(discountedInstanceId));
            Assert.That(match.TrySetHandCardCostModifier(discountedInstanceId, -1, "local-player"), Is.True);
            Assert.That(match.GetEffectiveCost(shell, firstInstanceId), Is.EqualTo(shell.cost));
            Assert.That(match.GetEffectiveCost(shell, discountedInstanceId), Is.EqualTo(System.Math.Max(0, shell.cost - 1)));
            var energyBeforeInvalidPlay = match.Energy;
            var invalidInstance = match.ApplyPlayCard(shell,
                match.CreatePlayCardCommand("tk_016", handCardInstanceId: "local-hand-not-in-hand"));
            Assert.That(invalidInstance.Code, Is.EqualTo(DemoCommandRejectionCode.CardNotInHand));
            Assert.That(match.Energy, Is.EqualTo(energyBeforeInvalidPlay));

            Assert.That(match.TryCast(shell, out _, firstInstanceId), Is.True);
            Assert.That(match.HandCards, Has.Length.EqualTo(1));
            Assert.That(match.HandCards[0].handCardInstanceId, Is.EqualTo(discountedInstanceId));
            Assert.That(match.GetEffectiveCost(shell, discountedInstanceId), Is.EqualTo(System.Math.Max(0, shell.cost - 1)));

            match.EndPlayerTurn();
            Assert.That(match.HandCards[0].costModifier, Is.Zero);
            Assert.That(match.HandCards[0].expiresAtEndOfTurnPlayerId, Is.Empty);
            Assert.That(match.GetEffectiveCost(shell, discountedInstanceId), Is.EqualTo(shell.cost));
        }

        [Test]
        public void LocalDeploymentRejectionClassifiesCostUsingTheSelectedDuplicate()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_001", out var spider), Is.True);
            Assert.That(registry.TryGetDefinition("pf_008", out var golem), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { spider.id, golem.id, golem.id });
            SetPrivateProperty(match, nameof(DemoLocalMatch.Energy), 6);
            var firstGolem = match.HandCards[1];
            var secondGolem = match.HandCards[2];
            Assert.That(match.TrySetHandCardCostModifier(firstGolem.handCardInstanceId, -1, "local-player"), Is.True);
            Assert.That(match.ApplyDeploy(spider, match.CreateDeployCommand(spider.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            Assert.That(match.Energy, Is.EqualTo(5));

            var result = match.ApplyDeploy(golem,
                match.CreateDeployCommand(golem.id, DemoSlotKind.Unit, 1, handCardInstanceId: secondGolem.handCardInstanceId));

            Assert.That(match.GetEffectiveCost(golem, firstGolem.handCardInstanceId), Is.EqualTo(5));
            Assert.That(match.GetEffectiveCost(golem, secondGolem.handCardInstanceId), Is.EqualTo(6));
            Assert.That(result.Code, Is.EqualTo(DemoCommandRejectionCode.InsufficientRedstone));
            Assert.That(match.Energy, Is.EqualTo(5));
            Assert.That(match.HandCards.Select(value => value.handCardInstanceId),
                Does.Contain(secondGolem.handCardInstanceId));
        }

        [Test]
        public void LocalDeploymentPreviewRejectsAmbiguousDuplicateWithoutSelectedInstance()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_001", out var spider), Is.True);
            Assert.That(registry.TryGetDefinition("pf_008", out var golem), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { spider.id, golem.id, golem.id });
            SetPrivateProperty(match, nameof(DemoLocalMatch.Energy), 6);
            var discountedGolem = match.HandCards[1];
            var fullCostGolem = match.HandCards[2];
            Assert.That(match.TrySetHandCardCostModifier(discountedGolem.handCardInstanceId, -1, "local-player"), Is.True);
            Assert.That(match.TryDeploy(spider, DemoSlotKind.Unit, 0, out _), Is.True);

            var ambiguous = DemoDeploymentRules.Evaluate(match, golem, DemoSlotKind.Unit, 1);
            var discounted = DemoDeploymentRules.Evaluate(match, golem, DemoSlotKind.Unit, 1,
                handCardInstanceId: discountedGolem.handCardInstanceId);
            var fullCost = DemoDeploymentRules.Evaluate(match, golem, DemoSlotKind.Unit, 1,
                handCardInstanceId: fullCostGolem.handCardInstanceId);
            Assert.That(ambiguous.IsLegal, Is.False, "duplicate copies require an exact selection");
            Assert.That(discounted.IsLegal, Is.True, "the selected discounted copy is payable at five");
            Assert.That(fullCost.IsLegal, Is.False, "the undiscounted copy costs six after one energy was spent");
            Assert.That(() => match.CreateDeployCommand(golem.id, DemoSlotKind.Unit, 1),
                Throws.ArgumentException, "ambiguous command creation must require an exact hand instance");
        }

        [Test]
        public void LocalDemoSupportsDeployCastAndTurnLoop()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "pf_001", "pf_005", "tk_016" });

            Assert.That(match.Energy, Is.EqualTo(1), "each player starts with one redstone energy");
            Assert.That(match.MaxEnergy, Is.EqualTo(1));

            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(match.TryDeploy(bee, DemoSlotKind.Unit, 0, out _), Is.True);
            Assert.That(match.UnitSlots[0], Is.EqualTo("pf_001"));
            Assert.That(match.Energy, Is.Zero);

            match.EndPlayerTurn();
            Assert.That(match.IsPlayerTurn, Is.False);
            Assert.That(match.OpponentEnergy, Is.EqualTo(1));
            Assert.That(match.OpponentMaxEnergy, Is.EqualTo(1));
            match.BeginNextPlayerTurn();
            Assert.That(match.IsPlayerTurn, Is.True);
            Assert.That(match.Round, Is.EqualTo(2));
            Assert.That(match.Energy, Is.EqualTo(2));
            Assert.That(match.MaxEnergy, Is.EqualTo(2));

            Assert.That(registry.TryGetDefinition("pf_005", out var nursery), Is.True);
            Assert.That(match.TryDeploy(nursery, DemoSlotKind.Building, 0, out _), Is.True);
            Assert.That(match.BuildingSlots[0], Is.EqualTo("pf_005"));
            Assert.That(match.Energy, Is.Zero);

            match.EndPlayerTurn();
            Assert.That(match.OpponentEnergy, Is.EqualTo(2));
            match.BeginNextPlayerTurn();
            Assert.That(match.Round, Is.EqualTo(3));
            Assert.That(match.Energy, Is.EqualTo(3));

            Assert.That(registry.TryGetDefinition("tk_016", out var shell), Is.True);
            Assert.That(match.TryCast(shell, out var castMessage), Is.True);
            Assert.That(castMessage, Does.Contain("2 点护甲"));
            Assert.That(match.Energy, Is.EqualTo(2));
            Assert.That(match.PlayerArmor, Is.EqualTo(2));
            Assert.That(match.DiscardPile, Does.Contain("tk_016"));
        }

        [TestCase("standard_meadow", 4, 3)]
        [TestCase("plains_sunrise", 4, 3)]
        [TestCase("deep_caverns", 5, 2)]
        [TestCase("nether_lava_sea", 3, 4)]
        [TestCase("end_void", 3, 4)]
        [TestCase("deep_ocean", 4, 3)]
        [TestCase("desert_storm", 4, 3)]
        public void LocalDemoAllocatesTheRegisteredSymmetricArenaLayout(
            string arenaId,
            int expectedUnitSlots,
            int expectedBuildingSlots)
        {
            var match = new DemoLocalMatch(arenaId);

            Assert.That(match.ArenaId, Is.EqualTo(arenaId));
            Assert.That(match.UnitSlots, Has.Length.EqualTo(expectedUnitSlots));
            Assert.That(match.OpponentUnitSlots, Has.Length.EqualTo(expectedUnitSlots));
            Assert.That(match.BuildingSlots, Has.Length.EqualTo(expectedBuildingSlots));
            Assert.That(match.OpponentBuildingSlots, Has.Length.EqualTo(expectedBuildingSlots));
            Assert.That(match.UnitSlots, Is.Not.SameAs(match.OpponentUnitSlots));
            Assert.That(match.BuildingSlots, Is.Not.SameAs(match.OpponentBuildingSlots));
        }

        [Test]
        public void LocalDemoRejectsAnUnregisteredArenaBeforeCreatingBoardState()
        {
            Assert.Throws<System.ArgumentOutOfRangeException>(() => new DemoLocalMatch("unknown_arena"));
        }

        [Test]
        public void DeepCavernsCanDeployIntoItsFifthUnitSlotAndRejectsTheNextIndex()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch("deep_caverns");
            match.ResetHand(new[] { "pf_001" });
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);

            var lastSlot = match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 4));
            Assert.That(lastSlot.Accepted, Is.True, lastSlot.Message);
            Assert.That(match.UnitSlots[4], Is.EqualTo("pf_001"));

            match.ResetHand(new[] { "pf_001" });
            var outsidePreview = DemoDeploymentRules.Evaluate(
                match, bee, DemoSlotKind.Unit, 5, handCardInstanceId: match.HandCards[0].handCardInstanceId);
            Assert.That(outsidePreview.IsLegal, Is.False);
            Assert.That(match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 5)).Accepted, Is.False);
            Assert.That(match.PlayerBattlefield, Has.Count.EqualTo(1));
        }

        [Test]
        public void DeepCavernsTreatsTheFifthUnitSlotAsTheEdgeForMovementAndAdjacency()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch("deep_caverns");
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("si_004", out var goat), Is.True);
            match.ResetHand(new[] { sheep.id });
            Assert.That(match.ApplyDeploy(sheep,
                match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 2)).Accepted, Is.True);
            var target = match.GetObject(true, DemoSlotKind.Unit, 2);
            match.ResetHand(new[] { goat.id });

            Assert.That(DemoCardTargeting.TryGetRule(goat, out var rule), Is.True);
            Assert.That(DemoCardTargeting.HasLegalTarget(match, rule), Is.True);
            Assert.That(DemoCardTargeting.IsLegalTarget(match, rule, true, DemoSlotKind.Unit, target), Is.True);
            var result = match.ApplyDeploy(goat, match.CreateDeployCommand(
                goat.id, DemoSlotKind.Unit, 3, MatchPaymentMethods.Redstone, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.UnitSlots[2], Is.Null.Or.Empty);
            Assert.That(match.UnitSlots[3], Is.EqualTo(goat.id));
            Assert.That(match.UnitSlots[4], Is.EqualTo(sheep.id));
            Assert.That(target.SlotIndex, Is.EqualTo(4));
        }

        [Test]
        public void DeepCavernsRejectsDeploymentWhenAllFiveUnitSlotsAreOccupied()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch("deep_caverns");
            match.ResetHand(new[] { "pf_001" });
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            for (var index = 0; index < match.UnitSlots.Length; index++)
                match.UnitSlots[index] = "occupied-" + index;
            var energyBefore = match.Energy;
            var handCardInstanceId = match.HandCards[0].handCardInstanceId;

            var preview = DemoDeploymentRules.Evaluate(
                match, bee, DemoSlotKind.Unit, 4, handCardInstanceId: handCardInstanceId);
            var result = match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 4));

            Assert.That(preview.IsLegal, Is.False);
            Assert.That(result.Accepted, Is.False);
            Assert.That(match.Energy, Is.EqualTo(energyBefore));
            Assert.That(match.HandCards.Single().handCardInstanceId, Is.EqualTo(handCardInstanceId));
        }

        [Test]
        public void WoodlandRallySummonsIntoTheFifthDeepCavernsSlotBeforeDrawingAtCapacity()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_007", out var rally), Is.True);
            var match = CreateScenarioMatch("deep_caverns");
            match.ResetDeckAndHand(new[] { rally.id }, new[] { "pf_001" });
            for (var index = 0; index < 4; index++)
                match.UnitSlots[index] = "occupied-" + index;

            var result = match.ApplyPlayCard(rally, match.CreatePlayCardCommand(rally.id));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.UnitSlots[4], Is.EqualTo("tk_004"));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 4).CardId, Is.EqualTo("tk_004"));
            Assert.That(match.Hand, Is.EqualTo(new[] { "pf_001" }));
            Assert.That(result.Message, Does.Contain("召唤 1 个林地伙伴"));
            Assert.That(result.Message, Does.Contain("抽取 1 张牌"));
        }

        [Test]
        public void NetherLavaSeaAllowsAContinuousStructureAtItsRightEdge()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch("nether_lava_sea");
            match.ResetHand(new[] { "db_007" });
            Assert.That(registry.TryGetDefinition("db_007", out var temple), Is.True);

            var preview = DemoDeploymentRules.Evaluate(match, temple, DemoSlotKind.Building, 2);
            Assert.That(preview.IsLegal, Is.True, preview.Message);
            var deployed = match.ApplyDeploy(temple,
                match.CreateDeployCommand(temple.id, DemoSlotKind.Building, 2));

            Assert.That(deployed.Accepted, Is.True, deployed.Message);
            Assert.That(match.BuildingSlots, Is.EqualTo(new string[] { null, null, "db_007", "db_007" }));
            match.ResetHand(new[] { "db_007" });
            var outsidePreview = DemoDeploymentRules.Evaluate(match, temple, DemoSlotKind.Building, 3,
                handCardInstanceId: match.HandCards[0].handCardInstanceId);
            Assert.That(outsidePreview.IsLegal, Is.False);
            Assert.That(outsidePreview.Message, Does.Contain("连续 2"));
        }

        [Test]
        public void LocalTurnStartDrawsBurnsAndAppliesEscalatingFatigue()
        {
            var drawing = CreateScenarioMatch();
            drawing.ResetDeckAndHand(new[] { "pf_001" }, new[] { "pf_002", "pf_003" });
            var originalHandInstanceId = drawing.HandCards.Single().handCardInstanceId;
            drawing.EndPlayerTurn();
            var draw = drawing.BeginNextPlayerTurn();
            Assert.That(draw.Outcome, Is.EqualTo(DemoDrawOutcome.Drawn));
            Assert.That(draw.CardId, Is.EqualTo("pf_003"));
            Assert.That(drawing.Hand, Does.Contain("pf_003"));
            Assert.That(drawing.HandCards.Last().cardId, Is.EqualTo("pf_003"));
            Assert.That(drawing.HandCards.Last().handCardInstanceId, Is.Not.EqualTo(originalHandInstanceId));
            Assert.That(drawing.Deck, Has.Count.EqualTo(1));

            var burning = CreateScenarioMatch();
            burning.ResetDeckAndHand(
                new[] { "pf_001", "pf_002", "pf_003", "pf_004", "pf_005", "pf_006", "pf_007" },
                new[] { "pf_008" });
            burning.EndPlayerTurn();
            var burn = burning.BeginNextPlayerTurn();
            Assert.That(burn.Outcome, Is.EqualTo(DemoDrawOutcome.Burned));
            Assert.That(burning.Hand, Has.Count.EqualTo(7));
            Assert.That(burning.DiscardPile, Is.EqualTo(new[] { "pf_008" }));

            var fatiguing = CreateScenarioMatch();
            fatiguing.ResetDeckAndHand(new string[0], new string[0]);
            fatiguing.EndPlayerTurn();
            var firstFatigue = fatiguing.BeginNextPlayerTurn();
            fatiguing.EndPlayerTurn();
            var secondFatigue = fatiguing.BeginNextPlayerTurn();
            Assert.That(firstFatigue.FatigueDamage, Is.EqualTo(1));
            Assert.That(secondFatigue.FatigueDamage, Is.EqualTo(2));
            Assert.That(fatiguing.PlayerLife, Is.EqualTo(27));
            Assert.That(fatiguing.FatigueCount, Is.EqualTo(2));
        }

        [Test]
        public void LocalImplementedEffectsResolveAndPendingEffectsRemainUnspent()
        {
            var registry = CardContentLoader.Load();

            var sacrificeMatch = CreateScenarioMatch();
            sacrificeMatch.ResetDeckAndHand(new[] { "nt_006" }, new[] { "nt_001" });
            Assert.That(registry.TryGetDefinition("nt_006", out var sacrifice), Is.True);
            Assert.That(sacrifice.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            Assert.That(sacrificeMatch.TryCast(sacrifice, out var sacrificeMessage), Is.True);
            Assert.That(sacrificeMatch.PlayerLife, Is.EqualTo(28));
            Assert.That(sacrificeMatch.Hand, Is.EqualTo(new[] { "nt_001" }));
            Assert.That(sacrificeMessage, Does.Contain("真实伤害"));

            var fleshMatch = CreateScenarioMatch();
            fleshMatch.ResetDeckAndHand(new[] { "tk_005" }, new string[0]);
            Assert.That(registry.TryGetDefinition("tk_005", out var flesh), Is.True);
            Assert.That(fleshMatch.TryCast(flesh, out _), Is.True);
            Assert.That(fleshMatch.PlayerLife, Is.EqualTo(29));

            var pendingMatch = CreateScenarioMatch();
            pendingMatch.ResetDeckAndHand(new[] { "ed_005" }, new string[0]);
            Assert.That(registry.TryGetDefinition("ed_005", out var pending), Is.True);
            Assert.That(pendingMatch.TryCast(pending, out var pendingMessage), Is.False);
            Assert.That(pendingMatch.Hand, Does.Contain("ed_005"));
            Assert.That(pendingMatch.Energy, Is.EqualTo(6));
            Assert.That(pendingMessage, Does.Contain("回手"));
        }

        [Test]
        public void LocalChorusFruitReturnsOnlyItsSelectedFriendlyInstanceWithAnExpiringDiscount()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { "pf_001" }, new string[0]);
            Assert.That(registry.TryGetDefinition("pf_001", out var sheep), Is.True);
            Assert.That(match.TryDeploy(sheep, DemoSlotKind.Unit, 1, out _), Is.True);
            var target = match.PlayerBattlefield.Single();
            match.ResetHand(new[] { "ed_002" });
            Assert.That(registry.TryGetDefinition("ed_002", out var chorusFruit), Is.True);
            Assert.That(chorusFruit.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            var command = match.CreatePlayCardCommand("ed_002", "UNIT", target.InstanceId);
            var result = match.ApplyPlayCard(chorusFruit, command);

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.PlayerBattlefield, Is.Empty);
            Assert.That(match.UnitSlots[1], Is.Empty);
            var returned = match.HandCards.Single(value => value.cardId == "pf_001");
            Assert.That(returned.handCardInstanceId, Does.StartWith("local-hand-"));
            Assert.That(returned.costModifier, Is.EqualTo(-1));
            Assert.That(returned.expiresAtEndOfTurnPlayerId, Is.EqualTo("local-player"));
            Assert.That(match.DiscardPile, Does.Contain("ed_002"));

            match.EndPlayerTurn();
            Assert.That(match.HandCards.Single(value => value.handCardInstanceId == returned.handCardInstanceId).costModifier, Is.Zero);
        }

        [Test]
        public void LocalEnderPearlReturnsItsFriendlyTargetWithAnExactTwoPointDiscountThatExpiresAtTurnEnd()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { "pf_004" }, new string[0]);
            Assert.That(registry.TryGetDefinition("pf_004", out var villager), Is.True);
            Assert.That(match.TryDeploy(villager, DemoSlotKind.Unit, 1, out _), Is.True);
            var target = match.PlayerBattlefield.Single();
            match.ResetHand(new[] { "ed_005" });
            Assert.That(registry.TryGetDefinition("ed_005", out var enderPearl), Is.True);

            var result = match.ApplyPlayCard(enderPearl,
                match.CreatePlayCardCommand("ed_005", "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.PlayerBattlefield, Is.Empty);
            Assert.That(match.UnitSlots[1], Is.Empty);
            var returned = match.HandCards.Single(value => value.cardId == "pf_004");
            Assert.That(returned.costModifier, Is.EqualTo(-2));
            Assert.That(returned.expiresAtEndOfTurnPlayerId, Is.EqualTo("local-player"));
            Assert.That(match.GetEffectiveCost(villager, returned.handCardInstanceId), Is.EqualTo(1));
            Assert.That(match.DiscardPile, Does.Contain("ed_005"));

            match.EndPlayerTurn();

            var expired = match.HandCards.Single(value => value.handCardInstanceId == returned.handCardInstanceId);
            Assert.That(expired.costModifier, Is.Zero);
            Assert.That(expired.expiresAtEndOfTurnPlayerId, Is.Empty);
            Assert.That(match.GetEffectiveCost(villager, expired.handCardInstanceId), Is.EqualTo(3));
        }

        [Test]
        public void LocalChorusFruitSettlesLethalAuraLossAndTriggersTheAdjacentUnitDeathrattle()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { "or_005", "nt_001", "ed_002" }, new string[0]);
            Assert.That(registry.TryGetDefinition("or_005", out var turtle), Is.True);
            Assert.That(registry.TryGetDefinition("nt_001", out var zombie), Is.True);
            Assert.That(registry.TryGetDefinition("ed_002", out var chorusFruit), Is.True);
            Assert.That(match.TryDeploy(turtle, DemoSlotKind.Unit, 1, out _), Is.True);
            Assert.That(match.TryDeploy(zombie, DemoSlotKind.Unit, 2, out _), Is.True);
            var auraSource = match.GetObject(true, DemoSlotKind.Unit, 1);
            var auraDependent = match.GetObject(true, DemoSlotKind.Unit, 2);
            Assert.That(auraSource, Is.Not.Null);
            Assert.That(auraDependent.AdjacencyHealthModifier, Is.EqualTo(1));
            auraDependent.Health = 1;

            var command = match.CreatePlayCardCommand("ed_002", "UNIT", auraSource.InstanceId);
            var result = match.ApplyPlayCard(chorusFruit, command);

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.PlayerBattlefield.Any(value => value.InstanceId == auraDependent.InstanceId), Is.False);
            Assert.That(match.DiscardPile, Does.Contain("nt_001"));
            Assert.That(match.PlayerBattlefield.Any(value => value.CardId == "tk_014" && value.SlotIndex == 2), Is.True);
            Assert.That(result.Message, Does.Contain("亡语"));
        }

        [Test]
        public void LocalReturnTargetingAllowsMaterialOnDragonAvatarButBlocksItsControllersSpell()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("ed_002", out var chorusFruit), Is.True);
            Assert.That(registry.TryGetDefinition("ed_005", out var enderPearl), Is.True);
            Assert.That(DemoCardTargeting.TryGetRule(chorusFruit, out var materialRule), Is.True);
            Assert.That(DemoCardTargeting.TryGetRule(enderPearl, out var spellRule), Is.True);
            var match = CreateScenarioMatch();
            var dragonAvatar = new DemoBattlefieldObject
            {
                InstanceId = "object-test-dragon", CardId = "tk_017", Player = true,
                SlotKind = DemoSlotKind.Unit, SlotIndex = 0, Health = 8
            };
            var enemyUnit = new DemoBattlefieldObject
            {
                InstanceId = "object-test-enemy", CardId = "pf_004", Player = false,
                SlotKind = DemoSlotKind.Unit, SlotIndex = 0, Health = 4
            };

            Assert.That(DemoCardTargeting.IsLegalTarget(match, materialRule, true, DemoSlotKind.Unit, dragonAvatar), Is.True);
            Assert.That(DemoCardTargeting.IsLegalTarget(match, spellRule, true, DemoSlotKind.Unit, dragonAvatar), Is.False);
            Assert.That(DemoCardTargeting.IsLegalTarget(match, materialRule, false, DemoSlotKind.Unit, enemyUnit), Is.False);
            Assert.That(DemoCardTargeting.IsLegalTarget(match, spellRule, false, DemoSlotKind.Unit, enemyUnit), Is.False);
        }

        [Test]
        public void LocalSuspiciousSandBurialExcavatesBeforeTheNormalDraw()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { "db_002" }, new[] { "pf_001" });
            Assert.That(registry.TryGetDefinition("db_002", out var suspiciousSand), Is.True);
            Assert.That(suspiciousSand.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            Assert.That(match.TryCast(suspiciousSand, out var message), Is.True);
            Assert.That(message, Does.Contain("陶片"));
            Assert.That(match.BuriedCount, Is.EqualTo(1));
            Assert.That(match.Deck, Does.Contain("tk_006"));
            Assert.That(match.PlayerArmor, Is.EqualTo(1));

            match.EndPlayerTurn();
            var firstDraw = match.BeginNextPlayerTurn();
            var excavated = firstDraw.ExcavatedCardIds;
            if (excavated.Length == 0)
            {
                match.EndPlayerTurn();
                excavated = match.BeginNextPlayerTurn().ExcavatedCardIds;
            }

            Assert.That(excavated, Is.EqualTo(new[] { "tk_006" }));
            Assert.That(match.BuriedCount, Is.Zero);
            Assert.That(match.Hand, Does.Contain("tk_006"));
            Assert.That(match.HandCards.Single(value => value.cardId == "tk_006").handCardInstanceId,
                Does.StartWith("local-hand-"));
            Assert.That(match.PlayerArmor, Is.EqualTo(2));
            Assert.That(match.ExcavatedThisTurn, Is.True);
        }

        [Test]
        public void LocalBadlandsRaiderCostsOneLessOnlyDuringAnExcavationTurn()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("db_005", out var raider), Is.True);
            Assert.That(registry.TryGetDefinition("db_004", out var camel), Is.True);
            var match = new DemoLocalMatch();
            match.ResetDeckAndHand(new[] { raider.id }, new[] { "db_001", "tk_006" }, new[] { "tk_006" });

            Assert.That(match.GetEffectiveCost(raider), Is.EqualTo(3));
            Assert.That(match.GetEffectiveCost(camel), Is.EqualTo(camel.cost));

            match.EndPlayerTurn();
            var draw = match.BeginNextPlayerTurn();

            Assert.That(draw.ExcavatedCardIds, Is.EqualTo(new[] { "tk_006" }));
            Assert.That(match.ExcavatedThisTurn, Is.True);
            Assert.That(match.GetEffectiveCost(raider), Is.EqualTo(2));
            Assert.That(match.GetEffectiveCost(camel), Is.EqualTo(camel.cost));
            Assert.That(match.TryDeploy(raider, DemoSlotKind.Unit, 0, out _), Is.True);
            Assert.That(match.Energy, Is.Zero, "Turn two starts at two energy and the discounted raider spends both.");

            match.EndPlayerTurn();
            Assert.That(match.ExcavatedThisTurn, Is.False);
            Assert.That(match.GetEffectiveCost(raider), Is.EqualTo(3));
        }

        [Test]
        public void LocalBadlandsRaiderStacksExcavationAndHandInstanceDiscountBeforeClampingAtZero()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("db_005", out var raider), Is.True);
            var match = new DemoLocalMatch();
            match.ResetDeckAndHand(new[] { raider.id }, new[] { "db_001", "tk_006" }, new[] { "tk_006" });
            match.EndPlayerTurn();
            var draw = match.BeginNextPlayerTurn();
            Assert.That(draw.ExcavatedCardIds, Is.EqualTo(new[] { "tk_006" }));
            Assert.That(match.ExcavatedThisTurn, Is.True);

            var raiderHandCard = match.HandCards.Single(value => value.cardId == raider.id);
            Assert.That(match.TrySetHandCardCostModifier(raiderHandCard.handCardInstanceId, -3, "local-player"), Is.True);
            Assert.That(match.GetEffectiveCost(raider, raiderHandCard.handCardInstanceId), Is.Zero);
            SetPrivateProperty(match, "Energy", 0);

            Assert.That(match.TryDeploy(raider, DemoSlotKind.Unit, 0, out _, handCardInstanceId: raiderHandCard.handCardInstanceId), Is.True);
            Assert.That(match.Energy, Is.Zero);
            Assert.That(match.HandCards.Any(value => value.handCardInstanceId == raiderHandCard.handCardInstanceId), Is.False);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0)?.CardId, Is.EqualTo(raider.id));
        }

        [Test]
        public void LocalCaveBatMayMoveTheRevealedTopCardToTheDeckBottom()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { "cd_001" }, new[] { "cd_002", "cd_005" });
            Assert.That(registry.TryGetDefinition("cd_001", out var caveBat), Is.True);
            Assert.That(caveBat.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            var deployed = match.ApplyDeploy(caveBat, match.CreateDeployCommand(caveBat.id, DemoSlotKind.Unit, 0));

            Assert.That(deployed.Accepted, Is.True);
            Assert.That(match.PendingChoice, Is.Not.Null);
            Assert.That(match.PendingChoice.kind, Is.EqualTo("TOP_CARD_SCRY"));
            Assert.That(match.PendingChoice.options.Single().cardId, Is.EqualTo("cd_005"));
            var resolved = match.ApplyResolveChoice(
                match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, 0));

            Assert.That(resolved.Accepted, Is.True);
            Assert.That(match.PendingChoice, Is.Null);
            Assert.That(match.Deck, Is.EqualTo(new[] { "cd_005", "cd_002" }));
        }

        [Test]
        public void LocalCaveBatMayKeepTheTopCardAndSkipsAnEmptyDeck()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("cd_001", out var caveBat), Is.True);
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { caveBat.id }, new[] { "cd_002", "cd_005" });
            Assert.That(match.TryDeploy(caveBat, DemoSlotKind.Unit, 0, out _), Is.True);

            var kept = match.ApplyResolveChoice(
                match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, -1));

            Assert.That(kept.Accepted, Is.True);
            Assert.That(match.Deck, Is.EqualTo(new[] { "cd_002", "cd_005" }));

            var emptyMatch = CreateScenarioMatch();
            emptyMatch.ResetDeckAndHand(new[] { caveBat.id }, System.Array.Empty<string>());
            Assert.That(emptyMatch.TryDeploy(caveBat, DemoSlotKind.Unit, 0, out _), Is.True);
            Assert.That(emptyMatch.PendingChoice, Is.Null);
        }

        [Test]
        public void LocalCaveSpiderPoisonsAfterAttackingAndTicksAtTheTargetsEndPhase()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("cd_002", out var caveSpider), Is.True);
            Assert.That(registry.TryGetDefinition("or_005", out var turtle), Is.True);
            Assert.That(caveSpider.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { caveSpider.id }, new[] { "cd_001", "cd_003", "cd_005" });
            match.ResetOpponent(new[] { turtle });
            Assert.That(match.ApplyDeploy(caveSpider,
                match.CreateDeployCommand(caveSpider.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);

            var attacked = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(attacked.Accepted, Is.True, attacked.Message);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0), Is.Null, "The Spider still applies poison after simultaneous retaliation kills it.");
            Assert.That(target.Health, Is.EqualTo(4));
            Assert.That(target.Statuses.Single().statusId, Is.EqualTo("POISON"));
            Assert.That(target.Statuses.Single().remainingDuration, Is.EqualTo(3));
            Assert.That(target.Statuses.Single().sourcePlayerId, Is.EqualTo("local-player"));
            Assert.That(attacked.Message, Does.Contain("中毒"));

            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(target.Health, Is.EqualTo(3));
            Assert.That(target.Statuses.Single().remainingDuration, Is.EqualTo(2));
        }

        [Test]
        public void LocalCaveSpiderRetaliationPoisonsTheAttackerBeforeItsEndPhase()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("or_005", out var turtle), Is.True);
            Assert.That(registry.TryGetDefinition("cd_002", out var caveSpider), Is.True);
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { turtle.id }, new[] { "or_001", "or_002" });
            match.ResetOpponent(new[] { caveSpider });
            Assert.That(match.ApplyDeploy(turtle,
                match.CreateDeployCommand(turtle.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var defender = match.GetObject(false, DemoSlotKind.Unit, 0);

            var attacked = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", defender.InstanceId));

            Assert.That(attacked.Accepted, Is.True, attacked.Message);
            Assert.That(attacker.Health, Is.EqualTo(4));
            Assert.That(attacker.Statuses.Single().statusId, Is.EqualTo("POISON"));
            Assert.That(attacker.Statuses.Single().sourcePlayerId, Is.EqualTo("opponent"));
            var ended = match.ApplyEndTurn(match.CreateEndTurnCommand());
            Assert.That(ended.Accepted, Is.True, ended.Message);
            Assert.That(attacker.Health, Is.EqualTo(3));
            Assert.That(attacker.Statuses.Single().remainingDuration, Is.EqualTo(2));
            Assert.That(ended.Message, Does.Contain("中毒造成 1 点伤害"));
        }

        [Test]
        public void LocalBlazeAppliesFireBeforeRetaliationDeathAndDropsABlazeRod()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("nt_003", out var blaze), Is.True);
            Assert.That(registry.TryGetDefinition("or_005", out var turtle), Is.True);
            Assert.That(blaze.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { blaze.id }, new[] { "nt_001", "nt_006" });
            match.ResetOpponent(new[] { turtle });
            Assert.That(match.ApplyDeploy(blaze,
                match.CreateDeployCommand(blaze.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var opponentHandBefore = match.OpponentHandCount;

            var attacked = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(attacked.Accepted, Is.True, attacked.Message);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0), Is.Null);
            Assert.That(target.Health, Is.EqualTo(3));
            Assert.That(target.Statuses.Single().statusId, Is.EqualTo("FIRE"));
            Assert.That(target.Statuses.Single().remainingDuration, Is.EqualTo(2));
            Assert.That(target.Statuses.Single().sourceCardId, Is.EqualTo("nt_003"));
            Assert.That(match.OpponentHandCount, Is.EqualTo(opponentHandBefore + 1));
            Assert.That(attacked.Message, Does.Contain("着火"));
            Assert.That(attacked.Message, Does.Contain("烈焰棒"));

            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(target.Health, Is.EqualTo(2));
            Assert.That(target.Statuses.Single().remainingDuration, Is.EqualTo(1));
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(target.Health, Is.EqualTo(1));
            Assert.That(target.Statuses, Is.Empty);
        }

        [Test]
        public void LocalBlazeRetaliationBurnsTheAttackerAndAwardsItsDropToTheKiller()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            Assert.That(registry.TryGetDefinition("nt_003", out var blaze), Is.True);
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { ironGolem.id }, new[] { "pf_001" });
            match.ResetOpponent(new[] { blaze });
            Assert.That(match.ApplyDeploy(ironGolem,
                match.CreateDeployCommand(ironGolem.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var defender = match.GetObject(false, DemoSlotKind.Unit, 0);

            var attacked = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", defender.InstanceId));

            Assert.That(attacked.Accepted, Is.True, attacked.Message);
            Assert.That(attacker.Health, Is.EqualTo(4));
            Assert.That(attacker.Statuses.Single().statusId, Is.EqualTo("FIRE"));
            Assert.That(attacker.Statuses.Single().sourcePlayerId, Is.EqualTo("opponent"));
            Assert.That(match.GetObject(false, DemoSlotKind.Unit, 0), Is.Null);
            Assert.That(match.Hand, Does.Contain("tk_013"));
        }

        [Test]
        public void LocalBlazeRodDamagesThenBurnsOnlyASurvivingEnemyUnit()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("tk_013", out var blazeRod), Is.True);
            Assert.That(registry.TryGetDefinition("or_005", out var turtle), Is.True);
            Assert.That(blazeRod.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { blazeRod.id });
            match.ResetOpponent(new[] { turtle });
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);

            var result = match.ApplyPlayCard(blazeRod,
                match.CreatePlayCardCommand(blazeRod.id, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(target.Health, Is.EqualTo(5));
            Assert.That(target.Statuses.Single().statusId, Is.EqualTo("FIRE"));
            Assert.That(target.Statuses.Single().remainingDuration, Is.EqualTo(2));
            Assert.That(target.Statuses.Single().sourceCardId, Is.EqualTo("tk_013"));
            Assert.That(match.Hand, Is.Empty);
            Assert.That(match.DiscardPile, Does.Contain("tk_013"));

            var lethal = CreateScenarioMatch();
            lethal.ResetHand(new[] { blazeRod.id });
            Assert.That(registry.TryGetDefinition("cd_002", out var caveSpider), Is.True);
            lethal.ResetOpponent(new[] { caveSpider });
            var lethalTarget = lethal.GetObject(false, DemoSlotKind.Unit, 0);
            lethalTarget.Health = 1;
            var lethalResult = lethal.ApplyPlayCard(blazeRod,
                lethal.CreatePlayCardCommand(blazeRod.id, "UNIT", lethalTarget.InstanceId));
            Assert.That(lethalResult.Accepted, Is.True, lethalResult.Message);
            Assert.That(lethal.GetObject(false, DemoSlotKind.Unit, 0), Is.Null);
        }

        [Test]
        public void LocalStriderCanDeployWithoutATargetWhenNoFriendlyUnitIsBurning()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("nt_004", out var strider), Is.True);
            Assert.That(strider.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            Assert.That(DemoCardTargeting.TryGetRule(strider, out var targetRule), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { strider.id });

            Assert.That(DemoCardTargeting.HasLegalTarget(match, targetRule), Is.False);
            Assert.That(DemoCardTargeting.RequiresTargetNow(match, targetRule), Is.False);
            var result = match.ApplyDeploy(strider,
                match.CreateDeployCommand(strider.id, DemoSlotKind.Unit, 0));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0).CardId, Is.EqualTo("nt_004"));
            Assert.That(result.Message, Does.Contain("战吼无目标"));
        }

        [TestCase(29, 30, 1, "蜜蜂战吼恢复 1 点生命")]
        [TestCase(30, 30, 0, "蜜蜂战吼未恢复生命，英雄生命已满")]
        public void LocalBeeBattlecryHealsOneAndNeverExceedsHeroCap(
            int startingLife, int expectedLife, int expectedHealing, string expectedFeedback)
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(bee.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { bee.id });
            match.ResetPlayerLife(startingLife);

            var result = match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.PlayerLife, Is.EqualTo(expectedLife));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0).CardId, Is.EqualTo(bee.id));
            Assert.That(match.Energy, Is.EqualTo(5), "the implemented battlecry follows a normally paid deployment");
            Assert.That(result.Message, Does.Contain(expectedFeedback));
        }

        [Test]
        public void LocalStriderRequiresABurningFriendlyTargetThenRemovesFireAndHeals()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(registry.TryGetDefinition("nt_004", out var strider), Is.True);
            Assert.That(DemoCardTargeting.TryGetRule(strider, out var targetRule), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { bee.id, strider.id });
            Assert.That(match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            var target = match.GetObject(true, DemoSlotKind.Unit, 0);
            target.Health = target.MaxHealth - 1;
            target.Statuses = new[]
            {
                new BattlefieldStatusStateDto
                {
                    statusId = "FIRE", remainingDuration = 2, sourcePlayerId = "opponent",
                    sourceCardId = "nt_003", sourceInstanceId = "object-99", effectId = "effect.nt_003.01"
                }
            };
            Assert.That(DemoCardTargeting.HasLegalTarget(match, targetRule), Is.True);
            Assert.That(DemoCardTargeting.RequiresTargetNow(match, targetRule), Is.True);
            Assert.That(DemoCardTargeting.IsLegalTarget(match, targetRule, true, DemoSlotKind.Unit, target), Is.True);

            var energyBefore = match.Energy;
            var missing = match.ApplyDeploy(strider,
                match.CreateDeployCommand(strider.id, DemoSlotKind.Unit, 1));
            Assert.That(missing.Accepted, Is.False);
            Assert.That(missing.Code, Is.EqualTo(DemoCommandRejectionCode.InvalidTarget));
            Assert.That(match.Energy, Is.EqualTo(energyBefore));
            Assert.That(match.Hand, Does.Contain(strider.id));

            var result = match.ApplyDeploy(strider,
                match.CreateDeployCommand(strider.id, DemoSlotKind.Unit, 1, MatchPaymentMethods.Redstone,
                    "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(target.Statuses, Is.Empty);
            Assert.That(target.Health, Is.EqualTo(target.MaxHealth));
            Assert.That(result.Message, Does.Contain("移除"));
            Assert.That(result.Message, Does.Contain("恢复 1 点生命"));
        }

        [Test]
        public void LocalArchaeologistRequiresAChoiceAndExcavatesTheSelectedBuriedCard()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(
                new[] { "db_003" },
                new[] { "db_004", "tk_006", "db_001" },
                new[] { "tk_006" });
            Assert.That(registry.TryGetDefinition("db_003", out var archaeologist), Is.True);
            Assert.That(archaeologist.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            var deployed = match.ApplyDeploy(archaeologist, match.CreateDeployCommand("db_003", DemoSlotKind.Unit, 0));

            Assert.That(deployed.Accepted, Is.True);
            Assert.That(match.PendingChoice, Is.Not.Null);
            Assert.That(match.PendingChoice.options.Select(option => option.cardId),
                Is.EqualTo(new[] { "db_001", "tk_006", "db_004" }));
            Assert.That(match.PendingChoice.options.Single(option => option.selectable).optionIndex, Is.EqualTo(1));
            var blocked = match.ApplyEnterCombat(match.CreateEnterCombatCommand());
            Assert.That(blocked.Accepted, Is.False);
            Assert.That(blocked.Code, Is.EqualTo(DemoCommandRejectionCode.ChoiceRequired));

            var revisionBeforeInvalidChoice = match.Revision;
            var invalid = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, 0));
            Assert.That(invalid.Accepted, Is.False);
            Assert.That(invalid.Code, Is.EqualTo(DemoCommandRejectionCode.InvalidChoice));
            Assert.That(match.Revision, Is.EqualTo(revisionBeforeInvalidChoice));
            Assert.That(match.PendingChoice, Is.Not.Null);

            var resolved = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, 1));
            Assert.That(resolved.Accepted, Is.True);
            Assert.That(match.PendingChoice, Is.Null);
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_006", "db_001" }));
            Assert.That(match.Deck, Is.EqualTo(new[] { "db_004" }));
            Assert.That(match.BuriedCount, Is.Zero);
            Assert.That(match.PlayerArmor, Is.EqualTo(1));
        }

        [Test]
        public void LocalArchaeologistExplicitlyConfirmsWhenInspectionFindsNoBuriedCard()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { "db_003" }, new[] { "db_004", "db_001" });
            Assert.That(registry.TryGetDefinition("db_003", out var archaeologist), Is.True);
            Assert.That(match.TryDeploy(archaeologist, DemoSlotKind.Unit, 0, out _), Is.True);

            var choiceId = match.PendingChoice.choiceId;
            Assert.That(match.PendingChoice.options.All(option => !option.selectable), Is.True);
            var invalid = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(choiceId, 0));
            Assert.That(invalid.Accepted, Is.False);
            var resolved = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(choiceId, -1));

            Assert.That(resolved.Accepted, Is.True);
            Assert.That(match.PendingChoice, Is.Null);
            Assert.That(match.Hand, Is.Empty);
            Assert.That(match.Deck, Is.EqualTo(new[] { "db_004", "db_001" }));
        }

        [Test]
        public void TargetedSnowballUsesStableInstanceAndExpiresAtEndOfTurn()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { "si_001" }, new string[0]);
            Assert.That(registry.TryGetDefinition("si_001", out var snowball), Is.True);
            Assert.That(registry.TryGetDefinition("nt_003", out var blaze), Is.True);
            match.ResetOpponent(new[] { blaze });
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            Assert.That(target.InstanceId, Does.Match("^object-[0-9]+$"));

            var missingTarget = match.ApplyPlayCard(snowball, match.CreatePlayCardCommand("si_001"));
            Assert.That(missingTarget.Accepted, Is.False);
            Assert.That(missingTarget.Code, Is.EqualTo(DemoCommandRejectionCode.InvalidTarget));
            Assert.That(match.Hand, Does.Contain("si_001"));

            var played = match.ApplyPlayCard(snowball, match.CreatePlayCardCommand("si_001", "UNIT", target.InstanceId));
            Assert.That(played.Accepted, Is.True);
            Assert.That(target.Attack, Is.EqualTo(2));
            Assert.That(target.TemporaryAttackModifier, Is.EqualTo(-1));
            Assert.That(match.DiscardPile, Does.Contain("si_001"));

            match.EndPlayerTurn();
            Assert.That(target.Attack, Is.EqualTo(3));
            Assert.That(target.TemporaryAttackModifier, Is.Zero);
        }

        [Test]
        public void ExpandedImplementedEffectsResolveInLocalParityMode()
        {
            var registry = CardContentLoader.Load();

            var beeMatch = CreateScenarioMatch();
            beeMatch.ResetDeckAndHand(new[] { "nt_006", "pf_001" }, new[] { "nt_001" });
            Assert.That(registry.TryGetDefinition("nt_006", out var sacrifice), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(beeMatch.TryCast(sacrifice, out _), Is.True);
            Assert.That(beeMatch.PlayerLife, Is.EqualTo(28));
            Assert.That(beeMatch.TryDeploy(bee, DemoSlotKind.Unit, 0, out var beeMessage), Is.True);
            Assert.That(beeMatch.PlayerLife, Is.EqualTo(29));
            Assert.That(beeMessage, Does.Contain("战吼"));

            var boneMatch = CreateScenarioMatch();
            boneMatch.ResetHand(new[] { "pf_001", "tk_009" });
            Assert.That(registry.TryGetDefinition("tk_009", out var bone), Is.True);
            Assert.That(boneMatch.TryDeploy(bee, DemoSlotKind.Unit, 0, out _), Is.True);
            var friendlyUnit = boneMatch.GetObject(true, DemoSlotKind.Unit, 0);
            var boneResult = boneMatch.ApplyPlayCard(bone,
                boneMatch.CreatePlayCardCommand("tk_009", "UNIT", friendlyUnit.InstanceId));
            Assert.That(boneResult.Accepted, Is.True);
            Assert.That(friendlyUnit.Attack, Is.EqualTo(2));
            Assert.That(friendlyUnit.TemporaryAttackModifier, Is.EqualTo(1));
            boneMatch.EndPlayerTurn();
            Assert.That(friendlyUnit.Attack, Is.EqualTo(1));

            var cobbleMatch = CreateScenarioMatch();
            cobbleMatch.ResetHand(new[] { "db_004", "tk_010" });
            Assert.That(registry.TryGetDefinition("db_004", out var fence), Is.True);
            Assert.That(registry.TryGetDefinition("tk_010", out var cobblestone), Is.True);
            Assert.That(cobbleMatch.TryDeploy(fence, DemoSlotKind.Building, 0, out _), Is.True);
            var friendlyBuilding = cobbleMatch.GetObject(true, DemoSlotKind.Building, 0);
            friendlyBuilding.Health = 1;
            var cobbleResult = cobbleMatch.ApplyPlayCard(cobblestone,
                cobbleMatch.CreatePlayCardCommand("tk_010", "BUILDING", friendlyBuilding.InstanceId));
            Assert.That(cobbleResult.Accepted, Is.True);
            Assert.That(friendlyBuilding.Health, Is.EqualTo(3));

            var stormMatch = CreateScenarioMatch();
            stormMatch.ResetHand(new[] { "pf_001", "db_006" });
            Assert.That(registry.TryGetDefinition("db_006", out var sandstorm), Is.True);
            Assert.That(registry.TryGetDefinition("nt_003", out var blaze), Is.True);
            Assert.That(stormMatch.TryDeploy(bee, DemoSlotKind.Unit, 0, out _), Is.True);
            stormMatch.ResetOpponent(new[] { blaze });
            Assert.That(stormMatch.TryCast(sandstorm, out var stormMessage), Is.True);
            Assert.That(stormMatch.GetObject(true, DemoSlotKind.Unit, 0), Is.Null);
            Assert.That(stormMatch.GetObject(false, DemoSlotKind.Unit, 0).Health, Is.EqualTo(1));
            Assert.That(stormMessage, Does.Contain("沙尘暴"));
        }

        [Test]
        public void TargetRegistrySeparatesFriendlyEnemyAndBuildingTargets()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("si_001", out var snowball), Is.True);
            Assert.That(registry.TryGetDefinition("tk_001", out var wool), Is.True);
            Assert.That(registry.TryGetDefinition("tk_002", out var wheat), Is.True);
            Assert.That(registry.TryGetDefinition("tk_009", out var bone), Is.True);
            Assert.That(registry.TryGetDefinition("tk_010", out var cobblestone), Is.True);
            Assert.That(registry.TryGetDefinition("tk_013", out var blazeRod), Is.True);
            Assert.That(registry.TryGetDefinition("pf_006", out var breeding), Is.True);

            Assert.That(DemoCardTargeting.TryGetRule(snowball, out var snowballRule), Is.True);
            Assert.That(snowballRule.Owner, Is.EqualTo(DemoTargetOwner.Enemy));
            Assert.That(snowballRule.SlotKind, Is.EqualTo(DemoSlotKind.Unit));
            Assert.That(DemoCardTargeting.TryGetRule(wool, out var woolRule), Is.True);
            Assert.That(woolRule.Owner, Is.EqualTo(DemoTargetOwner.Friendly));
            Assert.That(woolRule.SlotKind, Is.EqualTo(DemoSlotKind.Unit));
            Assert.That(woolRule.TargetType, Is.EqualTo("UNIT"));
            Assert.That(DemoCardTargeting.TryGetRule(wheat, out var wheatRule), Is.True);
            Assert.That(wheatRule.Owner, Is.EqualTo(DemoTargetOwner.Friendly));
            Assert.That(wheatRule.TargetType, Is.EqualTo("UNIT"));
            Assert.That(DemoCardTargeting.TryGetRule(bone, out var boneRule), Is.True);
            Assert.That(boneRule.Owner, Is.EqualTo(DemoTargetOwner.Friendly));
            Assert.That(boneRule.TargetType, Is.EqualTo("UNIT"));
            Assert.That(DemoCardTargeting.TryGetRule(cobblestone, out var cobbleRule), Is.True);
            Assert.That(cobbleRule.Owner, Is.EqualTo(DemoTargetOwner.Friendly));
            Assert.That(cobbleRule.SlotKind, Is.EqualTo(DemoSlotKind.Building));
            Assert.That(cobbleRule.TargetType, Is.EqualTo("BUILDING"));
            Assert.That(DemoCardTargeting.TryGetRule(blazeRod, out var blazeRodRule), Is.True);
            Assert.That(blazeRodRule.Owner, Is.EqualTo(DemoTargetOwner.Enemy));
            Assert.That(blazeRodRule.SlotKind, Is.EqualTo(DemoSlotKind.Unit));
            Assert.That(DemoCardTargeting.TryGetRule(breeding, out var breedingRule), Is.True);
            Assert.That(breedingRule.Owner, Is.EqualTo(DemoTargetOwner.Friendly));
            Assert.That(breedingRule.RequiredTargetCount, Is.EqualTo(2));
        }

        [Test]
        public void StructureRequiresConsecutiveBuildingSlots()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "db_007" });
            Assert.That(registry.TryGetDefinition("db_007", out var temple), Is.True);
            Assert.That(temple.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            Assert.That(match.TryDeploy(temple, DemoSlotKind.Building, 2, out var error), Is.False);
            Assert.That(error, Does.Contain("连续 2"));
            Assert.That(match.TryDeploy(temple, DemoSlotKind.Building, 1, out var message), Is.True);
            Assert.That(match.BuildingSlots[1], Is.EqualTo("db_007"));
            Assert.That(match.BuildingSlots[2], Is.EqualTo("db_007"));
            Assert.That(match.BuriedCount, Is.EqualTo(2));
            Assert.That(match.Deck, Does.Contain("tk_007"));
            Assert.That(match.Deck, Does.Contain("tk_008"));
            Assert.That(message, Does.Contain("藏宝图与炸药机关"));
        }

        [Test]
        public void ExcavationTokensAreImplementedButCannotBePlayedManually()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();

            foreach (var tokenId in new[] { "tk_006", "tk_007", "tk_008" })
            {
                Assert.That(registry.TryGetDefinition(tokenId, out var token), Is.True);
                Assert.That(token.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
                Assert.That(token.manualPlayAllowed, Is.False);
                match.ResetHand(new[] { tokenId });
                var result = match.ApplyPlayCard(token, match.CreatePlayCardCommand(tokenId));
                Assert.That(result.Accepted, Is.False, tokenId);
                Assert.That(result.Code, Is.EqualTo(DemoCommandRejectionCode.CardNotPlayable), tokenId);
            }
        }

        [Test]
        public void StructureDeploymentPreviewValidatesTheWholeProspectiveRange()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "db_007" });
            Assert.That(registry.TryGetDefinition("db_007", out var temple), Is.True);

            var legal = DemoDeploymentRules.Evaluate(match, temple, DemoSlotKind.Building, 0);
            var outside = DemoDeploymentRules.Evaluate(match, temple, DemoSlotKind.Building, 2);

            Assert.That(legal.IsLegal, Is.True);
            Assert.That(legal.OccupiedSlots, Is.EqualTo(2));
            Assert.That(legal.Message, Does.Contain("1—2"));
            Assert.That(outside.IsLegal, Is.False);
            Assert.That(outside.Message, Does.Contain("连续 2"));

            match.BuildingSlots[1] = "occupied-instance";
            var overlap = DemoDeploymentRules.Evaluate(match, temple, DemoSlotKind.Building, 0);
            Assert.That(overlap.IsLegal, Is.False);
            Assert.That(overlap.Message, Does.Contain("并非全部空闲"));
        }

        [Test]
        public void CraftingPreviewAndLocalDeploymentConsumeMaterialsInsteadOfRedstone()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "db_002", "db_007", "tk_006", "db_002" });
            Assert.That(registry.TryGetDefinition("db_007", out var temple), Is.True);

            var preview = DemoDeploymentRules.Evaluate(
                match, temple, DemoSlotKind.Building, 1, MatchPaymentMethods.Crafting);
            Assert.That(preview.IsLegal, Is.True);
            var energyBefore = match.Energy;
            var command = match.CreateDeployCommand(
                temple.id, DemoSlotKind.Building, 1, MatchPaymentMethods.Crafting);
            var result = match.ApplyDeploy(temple, command);

            Assert.That(result.Accepted, Is.True);
            Assert.That(match.Energy, Is.EqualTo(energyBefore));
            Assert.That(match.Hand, Is.EqualTo(new[] { "db_002" }));
            Assert.That(match.DiscardPile, Is.EqualTo(new[] { "db_002", "tk_006" }));
            Assert.That(match.PlayerBattlefield.Single().Health, Is.EqualTo(10));
            Assert.That(match.PlayerBattlefield.Single().MaxHealth, Is.EqualTo(10));
            Assert.That(match.BuildingSlots[1], Is.EqualTo("db_007"));
            Assert.That(match.BuildingSlots[2], Is.EqualTo("db_007"));
            Assert.That(match.BuriedCount, Is.EqualTo(2));
            Assert.That(match.Deck, Does.Contain("tk_007"));
            Assert.That(match.Deck, Does.Contain("tk_008"));
        }

        [Test]
        public void HandAffordabilityUsesAvailableRedstoneTotalIncludingTemporaryEnergy()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "pf_002" });
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(sheep.cost, Is.EqualTo(2));
            SetPrivateProperty(match, nameof(DemoLocalMatch.Energy), 1);
            SetPrivateField(match, "_temporaryEnergy", 1);

            Assert.That(DemoDeploymentRules.CanPayWithRedstoneOrCrafting(
                match, sheep, match.HandCards[0].handCardInstanceId), Is.False,
                "Energy is already the authoritative available total, not the base pool to which temporary energy is re-added");

            SetPrivateProperty(match, nameof(DemoLocalMatch.Energy), 2);
            Assert.That(DemoDeploymentRules.CanPayWithRedstoneOrCrafting(
                match, sheep, match.HandCards[0].handCardInstanceId), Is.True,
                "the hand view must allow a card when total redstone, including temporary energy, covers its cost");
        }

        [Test]
        public void HandAffordabilityRecognizesACompleteCraftingFallbackWithoutRedstone()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "db_002", "db_007", "tk_006" });
            Assert.That(registry.TryGetDefinition("db_007", out var temple), Is.True);
            var templeInstanceId = match.HandCards.Single(value => value.cardId == temple.id).handCardInstanceId;
            SetPrivateProperty(match, nameof(DemoLocalMatch.Energy), 0);

            Assert.That(DemoDeploymentRules.CanPayWithRedstoneOrCrafting(
                match, temple, templeInstanceId), Is.True,
                "complete registered materials provide an affordable deployment path even with zero redstone");

            match.ResetHand(new[] { "db_002", "db_007" });
            var productWithoutFullRecipe = match.HandCards.Single(value => value.cardId == temple.id).handCardInstanceId;
            Assert.That(DemoDeploymentRules.CanPayWithRedstoneOrCrafting(
                match, temple, productWithoutFullRecipe), Is.False,
                "a recipe card with missing material and insufficient redstone must be visually unaffordable");
        }

        [Test]
        public void CraftingDeploymentConsumesTheSelectedProductInstanceNotAnotherCopy()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "db_007", "db_002", "tk_006", "db_007" });
            Assert.That(registry.TryGetDefinition("db_007", out var temple), Is.True);
            var firstProduct = match.HandCards[0].handCardInstanceId;
            var selectedProduct = match.HandCards[3].handCardInstanceId;

            Assert.That(DemoDeploymentRules.CanPayWithCrafting(
                match, temple, out _, selectedProduct), Is.True);
            Assert.That(DemoDeploymentRules.CanPayWithCrafting(
                match, temple, out _, "local-hand-no-longer-present"), Is.False,
                "a stale selected ID must not silently resolve to the other copy");

            var command = match.CreateDeployCommand(
                temple.id, DemoSlotKind.Building, 0, MatchPaymentMethods.Crafting,
                handCardInstanceId: selectedProduct);
            var result = match.ApplyDeploy(temple, command);

            Assert.That(result.Accepted, Is.True);
            Assert.That(match.HandCards.Select(value => value.handCardInstanceId),
                Is.EqualTo(new[] { firstProduct }));
            Assert.That(match.Hand, Is.EqualTo(new[] { "db_007" }));
            Assert.That(match.DiscardPile, Is.EqualTo(new[] { "db_002", "tk_006" }));
        }

        [Test]
        public void TreasureMapGeneratesEmeraldThenRepairsItsDesertTempleBeforeTheNormalDraw()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "db_007" });
            Assert.That(registry.TryGetDefinition("db_007", out var temple), Is.True);
            Assert.That(match.TryDeploy(temple, DemoSlotKind.Building, 0, out _), Is.True);
            var templeObject = match.GetObject(true, DemoSlotKind.Building, 0);
            templeObject.Health = 4;
            match.ResetDeckAndHand(new string[0], new[] { "pf_001", "tk_007" }, new[] { "tk_007" });

            match.EndPlayerTurn();
            var draw = match.BeginNextPlayerTurn();

            Assert.That(draw.Outcome, Is.EqualTo(DemoDrawOutcome.Drawn));
            Assert.That(draw.CardId, Is.EqualTo("pf_001"));
            Assert.That(draw.ExcavatedCardIds, Is.EqualTo(new[] { "tk_007" }));
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_007", "tk_018", "pf_001" }));
            Assert.That(match.BuriedCount, Is.Zero);
            Assert.That(match.ExcavatedThisTurn, Is.True);
            Assert.That(templeObject.Health, Is.EqualTo(6));
        }

        [Test]
        public void TntTrapDealsNormalAndTrueDamageRepairsTempleAndContinuesTheNormalDraw()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "db_007", "tk_016" });
            Assert.That(registry.TryGetDefinition("db_007", out var temple), Is.True);
            Assert.That(registry.TryGetDefinition("tk_016", out var shulkerShell), Is.True);
            Assert.That(match.TryDeploy(temple, DemoSlotKind.Building, 0, out _), Is.True);
            Assert.That(match.TryCast(shulkerShell, out _), Is.True);
            var templeObject = match.GetObject(true, DemoSlotKind.Building, 0);
            templeObject.Health = 4;
            match.ResetDeckAndHand(new string[0], new[] { "pf_001", "tk_008" }, new[] { "tk_008" });

            match.EndPlayerTurn();
            var draw = match.BeginNextPlayerTurn();

            Assert.That(draw.Outcome, Is.EqualTo(DemoDrawOutcome.Drawn));
            Assert.That(draw.CardId, Is.EqualTo("pf_001"));
            Assert.That(draw.ExcavatedCardIds, Is.EqualTo(new[] { "tk_008" }));
            Assert.That(match.PlayerLife, Is.EqualTo(29));
            Assert.That(match.PlayerArmor, Is.EqualTo(2), "TNT self-damage is true damage and must bypass armor.");
            Assert.That(match.OpponentLife, Is.EqualTo(27));
            Assert.That(match.OpponentArmor, Is.Zero);
            Assert.That(templeObject.Health, Is.EqualTo(6));
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_008", "pf_001" }));
            Assert.That(match.IsFinished, Is.False);
        }

        [Test]
        public void LethalTntTrapReturnsMatchEndedStopsTheNormalDrawAndLocksDeployment()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            SetPrivateProperty(match, nameof(DemoLocalMatch.PlayerLife), 1);
            SetPrivateProperty(match, nameof(DemoLocalMatch.OpponentLife), 3);
            match.ResetDeckAndHand(new string[0], new[] { "pf_001", "tk_008" }, new[] { "tk_008" });

            match.EndPlayerTurn();
            var draw = match.BeginNextPlayerTurn();

            Assert.That(draw.Outcome, Is.EqualTo(DemoDrawOutcome.MatchEnded));
            Assert.That(draw.ExcavatedCardIds, Is.EqualTo(new[] { "tk_008" }));
            Assert.That(match.PlayerLife, Is.Zero);
            Assert.That(match.OpponentLife, Is.Zero);
            Assert.That(match.IsFinished, Is.True);
            Assert.That(match.HasWinner, Is.True, "the excavated DB-007 card's owner wins the simultaneous lethal special case");
            Assert.That(match.IsPlayerWinner, Is.True);
            Assert.That(match.Deck, Is.EqualTo(new[] { "pf_001" }), "normal draw must not continue after lethal excavation");

            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            match.ResetHand(new[] { bee.id });
            var preview = DemoDeploymentRules.Evaluate(match, bee, DemoSlotKind.Unit, 0);
            Assert.That(preview.IsLegal, Is.False);
            Assert.That(preview.Message, Does.Contain("对局已经结束"));
        }

        [Test]
        public void ArchaeologyChoiceLethalTntClearsChoiceAndDoesNotDrawAgain()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            Assert.That(registry.TryGetDefinition("db_003", out var archaeologist), Is.True);
            SetPrivateProperty(match, nameof(DemoLocalMatch.PlayerLife), 1);
            SetPrivateProperty(match, nameof(DemoLocalMatch.OpponentLife), 3);
            match.ResetDeckAndHand(new[] { archaeologist.id }, new[] { "pf_001", "tk_008" }, new[] { "tk_008" });
            Assert.That(match.TryDeploy(archaeologist, DemoSlotKind.Unit, 0, out _), Is.True);
            Assert.That(match.PendingChoice, Is.Not.Null);

            var result = match.ApplyResolveChoice(
                match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, selectedOptionIndex: 0));

            Assert.That(result.Accepted, Is.True);
            Assert.That(match.PendingChoice, Is.Null);
            Assert.That(match.IsFinished, Is.True);
            Assert.That(match.PlayerLife, Is.Zero);
            Assert.That(match.OpponentLife, Is.Zero);
            Assert.That(match.Deck, Is.EqualTo(new[] { "pf_001" }));
            Assert.That(result.Message, Does.Contain("结束对局"));
        }

        [Test]
        public void LocalCraftingFailureDoesNotConsumeCardsOrOccupySlots()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "db_007", "tk_006" });
            Assert.That(registry.TryGetDefinition("db_007", out var temple), Is.True);

            var preview = DemoDeploymentRules.Evaluate(
                match, temple, DemoSlotKind.Building, 0, MatchPaymentMethods.Crafting);
            Assert.That(preview.IsLegal, Is.False);
            Assert.That(preview.Message, Does.Contain("db_002×1"));
            var result = match.ApplyDeploy(
                temple,
                match.CreateDeployCommand(temple.id, DemoSlotKind.Building, 0, MatchPaymentMethods.Crafting));

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Code, Is.EqualTo(DemoCommandRejectionCode.MissingMaterials));
            Assert.That(match.Hand, Is.EqualTo(new[] { "db_007", "tk_006" }));
            Assert.That(match.DiscardPile, Is.Empty);
            Assert.That(match.BuildingSlots.All(string.IsNullOrEmpty), Is.True);
        }

        private static void SetPrivateProperty(DemoLocalMatch match, string propertyName, int value)
        {
            var property = typeof(DemoLocalMatch).GetProperty(propertyName,
                BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
            Assert.That(property, Is.Not.Null, propertyName);
            property.SetValue(match, value);
        }

        private static void SetPrivateField(DemoLocalMatch match, string fieldName, int value)
        {
            var field = typeof(DemoLocalMatch).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(match, value);
        }

        private static int GetPrivateIntField(DemoLocalMatch match, string fieldName)
        {
            var field = typeof(DemoLocalMatch).GetField(fieldName,
                BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return (int)field.GetValue(match);
        }

        [Test]
        public void LocalCombatReleasesEverySlotOfAThreeSlotStructure()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch("nether_lava_sea");
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            Assert.That(registry.TryGetDefinition("ed_008", out var portalFrame), Is.True);
            match.ResetHand(new[] { ironGolem.id });
            match.ResetOpponent(new[] { portalFrame });
            Assert.That(match.OpponentBuildingSlots,
                Is.EqualTo(new[] { portalFrame.id, portalFrame.id, portalFrame.id, null }));
            Assert.That(match.TryDeploy(ironGolem, DemoSlotKind.Unit, 0, out _), Is.True);

            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var structure = match.GetObject(false, DemoSlotKind.Building, 2);
            Assert.That(structure.OccupiedSlots, Is.EqualTo(3));
            structure.Health = 1;

            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "BUILDING", structure.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(attacker.Health, Is.EqualTo(7), "structures do not retaliate");
            Assert.That(match.GetObject(false, DemoSlotKind.Building, 0), Is.Null);
            Assert.That(match.OpponentBuildingSlots.All(string.IsNullOrEmpty), Is.True);
        }

        [Test]
        public void DeployCommandUsesSharedFieldsAndRevisionGuard()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "pf_001", "pf_002" });
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);

            var command = match.CreateDeployCommand("pf_001", DemoSlotKind.Unit, 2);
            Assert.That(command.protocolVersion, Is.EqualTo(GameVersions.Protocol));
            Assert.That(command.rulesetVersion, Is.EqualTo(GameVersions.Ruleset));
            Assert.That(command.type, Is.EqualTo("DEPLOY_CARD"));
            Assert.That(command.payload.cardId, Is.EqualTo("pf_001"));
            Assert.That(command.payload.slotKind, Is.EqualTo("UNIT"));
            Assert.That(command.payload.slotIndex, Is.EqualTo(2));

            var accepted = match.ApplyDeploy(bee, command);
            Assert.That(accepted.Accepted, Is.True);
            Assert.That(accepted.Revision, Is.EqualTo(1));
            Assert.That(match.Revision, Is.EqualTo(1));

            command.expectedRevision = match.Revision;
            var duplicate = match.ApplyDeploy(bee, command);
            Assert.That(duplicate.Accepted, Is.False);
            Assert.That(duplicate.Code, Is.EqualTo(DemoCommandRejectionCode.DuplicateCommand));
            Assert.That(match.Energy, Is.EqualTo(5));

            var stale = match.CreateDeployCommand("pf_002", DemoSlotKind.Unit, 3);
            stale.expectedRevision = 0;
            Assert.That(registry.TryGetDefinition("pf_002", out var cow), Is.True);
            var rejected = match.ApplyDeploy(cow, stale);
            Assert.That(rejected.Accepted, Is.False);
            Assert.That(rejected.Code, Is.EqualTo(DemoCommandRejectionCode.RevisionMismatch));
            Assert.That(match.UnitSlots[3], Is.Null);
            Assert.That(match.Hand, Does.Contain("pf_002"));
        }

        [Test]
        public void LocalCombatResolvesRetaliationDeathAndHeroDamage()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "pf_003" });
            Assert.That(registry.TryGetDefinition("pf_003", out var attackerDefinition), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var targetDefinition), Is.True);
            match.ResetOpponent(new[] { targetDefinition });

            Assert.That(match.TryDeploy(attackerDefinition, DemoSlotKind.Unit, 0, out _), Is.True);
            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            Assert.That(match.CanAttackWith(attacker, out var summoningMessage), Is.False);
            Assert.That(summoningMessage, Does.Contain("刚被召唤"));

            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            Assert.That(match.CanAttackWith(attacker, out _), Is.True);

            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var attack = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));
            Assert.That(attack.Accepted, Is.True);
            Assert.That(attacker.Health, Is.EqualTo(1));
            Assert.That(attacker.HasAttacked, Is.True);
            Assert.That(match.GetObject(false, DemoSlotKind.Unit, 0), Is.Null);
            Assert.That(match.OpponentUnitSlots[0], Is.Empty);
        }

        [Test]
        public void LocalHuskDropsRottenFleshToItsEnemyKiller()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            Assert.That(registry.TryGetDefinition("db_001", out var husk), Is.True);
            Assert.That(husk.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            match.ResetDeckAndHand(new[] { ironGolem.id }, new string[0]);
            match.ResetOpponent(new[] { husk });
            Assert.That(match.TryDeploy(ironGolem, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Message, Does.Contain("尸壳掉落"));
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_005" }));
            Assert.That(match.HandCards.Single().cardId, Is.EqualTo("tk_005"));
            Assert.That(match.HandCards.Single().handCardInstanceId, Does.StartWith("local-hand-"));
            Assert.That(match.GetObject(false, DemoSlotKind.Unit, 0), Is.Null);
        }

        [Test]
        public void LocalRetaliationCreditsHuskLootToTheOpponent()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            Assert.That(registry.TryGetDefinition("db_001", out var husk), Is.True);
            Assert.That(registry.TryGetDefinition("nt_003", out var blaze), Is.True);
            match.ResetDeckAndHand(new[] { husk.id }, new string[0]);
            match.ResetOpponent(new[] { blaze });
            Assert.That(match.TryDeploy(husk, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Message, Does.Contain("对手获得一张腐肉"));
            Assert.That(match.OpponentHandCount, Is.EqualTo(6));
            Assert.That(match.DiscardPile, Is.EqualTo(new[] { "db_001" }));
        }

        [Test]
        public void LocalSandstormDropsOnlyTheEnemyHuskLoot()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            Assert.That(registry.TryGetDefinition("db_001", out var husk), Is.True);
            Assert.That(registry.TryGetDefinition("db_006", out var sandstorm), Is.True);
            match.ResetHand(new[] { husk.id, sandstorm.id });
            match.ResetOpponent(new[] { husk });
            Assert.That(match.TryDeploy(husk, DemoSlotKind.Unit, 0, out _), Is.True);

            var result = match.ApplyPlayCard(sandstorm, match.CreatePlayCardCommand(sandstorm.id));

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Message, Does.Contain("尸壳掉落"));
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_005" }));
            Assert.That(match.DiscardPile, Is.EqualTo(new[] { "db_006", "db_001" }));
            Assert.That(match.OpponentHandCount, Is.EqualTo(5));
        }

        [Test]
        public void LocalDungeonSkeletonDeathrattleDamagesBeforeDroppingBone()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            Assert.That(registry.TryGetDefinition("cd_003", out var dungeonSkeleton), Is.True);
            Assert.That(dungeonSkeleton.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            match.ResetDeckAndHand(new[] { ironGolem.id }, new string[0]);
            match.ResetOpponent(new[] { dungeonSkeleton });
            Assert.That(match.TryDeploy(ironGolem, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(attacker.Health, Is.EqualTo(3));
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_009" }));
            var deathrattleIndex = result.Message.IndexOf("地牢骷髅亡语", System.StringComparison.Ordinal);
            var dropIndex = result.Message.IndexOf("地牢骷髅掉落", System.StringComparison.Ordinal);
            Assert.That(deathrattleIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(dropIndex, Is.GreaterThan(deathrattleIndex));
        }

        [Test]
        public void LocalDungeonSkeletonSkipsDeathrattleWithoutALegalEnemyUnit()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            Assert.That(registry.TryGetDefinition("pf_003", out var wolf), Is.True);
            Assert.That(registry.TryGetDefinition("cd_003", out var dungeonSkeleton), Is.True);
            match.ResetDeckAndHand(new[] { wolf.id }, new string[0]);
            match.ResetOpponent(new[] { dungeonSkeleton });
            Assert.That(match.TryDeploy(wolf, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(match.PlayerBattlefield, Is.Empty);
            Assert.That(match.OpponentBattlefield, Is.Empty);
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_009" }));
            Assert.That(result.Message, Does.Not.Contain("地牢骷髅亡语"));
            Assert.That(result.Message, Does.Contain("地牢骷髅掉落"));
        }

        [Test]
        public void LocalDungeonSkeletonPropagatesAChainedHuskKillCredit()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            Assert.That(registry.TryGetDefinition("pf_003", out var wolf), Is.True);
            Assert.That(registry.TryGetDefinition("db_001", out var husk), Is.True);
            Assert.That(registry.TryGetDefinition("cd_003", out var dungeonSkeleton), Is.True);
            match.ResetDeckAndHand(new[] { wolf.id, husk.id }, new string[0]);
            match.ResetOpponent(new[] { dungeonSkeleton });
            Assert.That(match.TryDeploy(wolf, DemoSlotKind.Unit, 0, out _), Is.True);
            Assert.That(match.TryDeploy(husk, DemoSlotKind.Unit, 1, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(match.PlayerBattlefield, Is.Empty);
            Assert.That(match.OpponentBattlefield, Is.Empty);
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_009" }));
            Assert.That(match.OpponentHandCount, Is.EqualTo(6));
            Assert.That(result.Message, Does.Contain("对手获得一张腐肉"));
        }

        [Test]
        public void LocalGrazingSheepDropsWoolToItsEnemyKiller()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(sheep.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            match.ResetDeckAndHand(new[] { ironGolem.id }, new string[0]);
            match.ResetOpponent(new[] { sheep });
            Assert.That(match.TryDeploy(ironGolem, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_001" }));
            Assert.That(result.Message, Does.Contain("放牧绵羊掉落"));
        }

        [Test]
        public void LocalCombatAllowsChargeOnTheSummonedRound()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "pf_001" });
            Assert.That(registry.TryGetDefinition("pf_001", out var definition), Is.True);
            Assert.That(match.TryDeploy(definition, DemoSlotKind.Unit, 0, out _), Is.True);
            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            attacker.Keywords = new[] { "CHARGE" };
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            Assert.That(match.CanAttackWith(attacker, out _), Is.True);

            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "HERO"));

            Assert.That(result.Accepted, Is.True);
            Assert.That(match.OpponentLife, Is.EqualTo(29));
        }

        [Test]
        public void LocalCombatHighlightsAndEnforcesTauntTargets()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "pf_003" });
            Assert.That(registry.TryGetDefinition("pf_003", out var attackerDefinition), Is.True);
            Assert.That(registry.TryGetDefinition("pf_008", out var tauntDefinition), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var normalDefinition), Is.True);
            match.ResetOpponent(new[] { tauntDefinition, normalDefinition });
            Assert.That(match.TryDeploy(attackerDefinition, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var taunt = match.GetObject(false, DemoSlotKind.Unit, 0);
            var normal = match.GetObject(false, DemoSlotKind.Unit, 2);
            Assert.That(taunt.HasKeyword("TAUNT"), Is.True);
            Assert.That(match.CanAttackTarget(null, "HERO", out var heroMessage), Is.False);
            Assert.That(heroMessage, Does.Contain("嘲讽"));
            Assert.That(match.CanAttackTarget(normal, "UNIT", out _), Is.False);
            Assert.That(match.CanAttackTarget(taunt, "UNIT", out _), Is.True);

            var bypass = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "HERO"));
            Assert.That(bypass.Accepted, Is.False);
            Assert.That(bypass.Code, Is.EqualTo(DemoCommandRejectionCode.TauntTargetRequired));
            Assert.That(attacker.HasAttacked, Is.False);

            var legal = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", taunt.InstanceId));
            Assert.That(legal.Accepted, Is.True);
        }

        [Test]
        public void LocalShulkerDeathrattleGeneratesShulkerShell()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            Assert.That(registry.TryGetDefinition("ed_004", out var shulker), Is.True);
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            match.ResetHand(new[] { shulker.id });
            match.ResetOpponent(new[] { ironGolem });
            Assert.That(match.TryDeploy(shulker, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Message, Does.Contain("潜影贝亡语"));
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_016" }));
            Assert.That(match.DiscardPile, Is.EqualTo(new[] { "ed_004" }));
        }

        [Test]
        public void LocalShulkerDeathrattleDiscardsShellAtFullHand()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            Assert.That(registry.TryGetDefinition("ed_004", out var shulker), Is.True);
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            match.ResetDeckAndHand(
                new[] { "ed_004", "ed_001", "ed_001", "ed_001", "ed_001", "ed_001", "ed_001" },
                new[] { "ed_003" });
            match.ResetOpponent(new[] { ironGolem });
            Assert.That(match.TryDeploy(shulker, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.Hand.Count, Is.EqualTo(7));
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Message, Does.Contain("手牌已满"));
            Assert.That(match.Hand.Count, Is.EqualTo(7));
            Assert.That(match.DiscardPile, Is.EqualTo(new[] { "ed_004", "tk_016" }));
        }

        [Test]
        public void LocalMagmaCubeDeathrattleSummonsTokenIntoReleasedSlot()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            Assert.That(registry.TryGetDefinition("nt_001", out var magmaCube), Is.True);
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            match.ResetHand(new[] { magmaCube.id });
            match.ResetOpponent(new[] { ironGolem });
            Assert.That(match.TryDeploy(magmaCube, DemoSlotKind.Unit, 2, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var attacker = match.GetObject(true, DemoSlotKind.Unit, 2);
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True);
            Assert.That(result.Message, Does.Contain("岩浆怪亡语"));
            Assert.That(result.Message, Does.Contain("单位格 3"));
            Assert.That(match.UnitSlots[2], Is.EqualTo("tk_014"));
            var token = match.GetObject(true, DemoSlotKind.Unit, 2);
            Assert.That(token.CardId, Is.EqualTo("tk_014"));
            Assert.That(token.Attack, Is.EqualTo(1));
            Assert.That(token.Health, Is.EqualTo(1));
            Assert.That(token.SummonedRound, Is.EqualTo(match.Round));
            Assert.That(match.CanAttackWith(token, out var message), Is.False);
            Assert.That(message, Does.Contain("冲锋"));
        }

        [Test]
        public void LocalSimultaneousMagmaDeathsResolveBothSummonsAfterTheDeathBatch()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            Assert.That(registry.TryGetDefinition("nt_001", out var magmaCube), Is.True);
            Assert.That(registry.TryGetDefinition("db_006", out var sandstorm), Is.True);
            match.ResetHand(new[] { magmaCube.id, sandstorm.id });
            match.ResetOpponent(new[] { magmaCube });
            Assert.That(match.TryDeploy(magmaCube, DemoSlotKind.Unit, 1, out _), Is.True);

            var result = match.ApplyPlayCard(sandstorm, match.CreatePlayCardCommand(sandstorm.id));

            Assert.That(result.Accepted, Is.True);
            Assert.That(match.UnitSlots[1], Is.EqualTo("tk_014"));
            Assert.That(match.OpponentUnitSlots[0], Is.EqualTo("tk_014"));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 1).CardId, Is.EqualTo("tk_014"));
            Assert.That(match.GetObject(false, DemoSlotKind.Unit, 0).CardId, Is.EqualTo("tk_014"));
            var ownMessageIndex = result.Message.IndexOf("岩浆怪亡语", System.StringComparison.Ordinal);
            var enemyMessageIndex = result.Message.IndexOf("敌方岩浆怪亡语", System.StringComparison.Ordinal);
            Assert.That(ownMessageIndex, Is.GreaterThanOrEqualTo(0));
            Assert.That(enemyMessageIndex, Is.GreaterThan(ownMessageIndex));
        }

        [Test]
        public void LocalCombatCanDefeatOpponentHero()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "pf_003" });
            Assert.That(registry.TryGetDefinition("pf_003", out var attackerDefinition), Is.True);
            Assert.That(match.TryDeploy(attackerDefinition, DemoSlotKind.Unit, 0, out _), Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);

            DemoCommandResult result = null;
            while (!match.IsFinished)
            {
                attacker.HasAttacked = false;
                result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "HERO"));
                Assert.That(result.Accepted, Is.True);
            }

            Assert.That(match.OpponentLife, Is.Zero);
            Assert.That(result.Message, Does.Contain("胜利"));
        }

        [Test]
        public void CombatDamagePopupIsWorldSpaceAndCenteredAcrossOccupiedBuildingSlots()
        {
            var root = new GameObject("CombatDamagePopupWorldSpaceTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));

                battlefield.ShowCombatDamageNumber(true, DemoSlotKind.Building, 0, 2, 0);
                battlefield.ShowCombatDamageNumber(true, DemoSlotKind.Building, 0, 2, 5);

                var feedbackRoot = root.transform.Find("CombatFeedback");
                var popup = feedbackRoot.Find("CombatDamage_Player");
                var label = popup.Find("Label").GetComponent<TextMesh>();
                var expectedPosition = (battlefield.GetSlotWorldPosition(true, DemoSlotKind.Building, 0) +
                    battlefield.GetSlotWorldPosition(true, DemoSlotKind.Building, 1)) * 0.5f + Vector3.up * 2.1f;

                Assert.That(feedbackRoot, Is.Not.Null);
                Assert.That(feedbackRoot.childCount, Is.EqualTo(1), "zero damage should not create a popup");
                Assert.That(popup.parent, Is.EqualTo(feedbackRoot), "damage text must live in the 3D battlefield, not the overlay Canvas");
                Assert.That(label.text, Is.EqualTo("-5"));
                Assert.That(popup.localPosition.x, Is.EqualTo(expectedPosition.x).Within(0.001f));
                Assert.That(popup.localPosition.y, Is.EqualTo(expectedPosition.y).Within(0.001f));
                Assert.That(popup.localPosition.z, Is.EqualTo(expectedPosition.z).Within(0.001f));
                Assert.That(label.font, Is.Not.Null);
                Assert.That(popup.Find("Shadow"), Is.Not.Null, "the pixel label should keep its contrast shadow");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SceneHandAndDetailsKeepDuplicateCardCostsBoundToTheClickedInstance()
        {
            var root = new GameObject("DuplicateHandUiTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();

                var matchField = typeof(DemoSceneController).GetField("_match", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(matchField, Is.Not.Null);
                var match = (DemoLocalMatch)matchField.GetValue(controller);
                match.ResetHand(new[] { "db_005", "db_005" });
                var originalHand = match.HandCards;
                var firstInstanceId = originalHand[0].handCardInstanceId;
                var discountedInstanceId = originalHand[1].handCardInstanceId;
                Assert.That(match.TrySetHandCardCostModifier(discountedInstanceId, -2, "local-player"), Is.True);

                var selectHandCard = typeof(DemoSceneController).GetMethod("SelectHandCard", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(selectHandCard, Is.Not.Null);
                selectHandCard.Invoke(controller, new object[] { "db_005", discountedInstanceId });

                var handRoot = root.transform.Find("DemoCanvas/HandPlate/HandCards");
                var firstCard = handRoot.GetComponentsInChildren<CardUI>(true)
                    .Single(card => card.HandCardInstanceId == firstInstanceId);
                var discountedCard = handRoot.GetComponentsInChildren<CardUI>(true)
                    .Single(card => card.HandCardInstanceId == discountedInstanceId);
                var detailsRoot = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent");
                var details = detailsRoot.GetComponent<CardDetailsView>().CurrentCard;

                Assert.That(firstCard.CardId, Is.EqualTo("db_005"));
                Assert.That(firstCard.DisplayedCost, Is.EqualTo(firstCard.BaseCost));
                Assert.That(discountedCard.CardId, Is.EqualTo("db_005"));
                Assert.That(discountedCard.DisplayedCost, Is.EqualTo(1));
                Assert.That(firstCard.gameObject.name, Is.Not.EqualTo(discountedCard.gameObject.name));
                Assert.That(discountedCard.RectTransform.anchoredPosition.y, Is.GreaterThan(firstCard.RectTransform.anchoredPosition.y));
                Assert.That(details.HandCardInstanceId, Is.EqualTo(discountedInstanceId));
                Assert.That(details.DisplayedCost, Is.EqualTo(1));

                firstCard.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                details = detailsRoot.GetComponent<CardDetailsView>().CurrentCard;
                Assert.That(details.HandCardInstanceId, Is.EqualTo(firstInstanceId));
                Assert.That(details.DisplayedCost, Is.EqualTo(details.BaseCost));
                Assert.That(handRoot.GetComponentsInChildren<CardUI>(true)
                    .Single(card => card.HandCardInstanceId == discountedInstanceId).DisplayedCost, Is.EqualTo(1));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CancelCurrentInteractionClearsOnlyTheMostSpecificStagedAction()
        {
            var root = new GameObject("CancelCurrentInteractionTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();

                var firstCard = FindHandCard(root, "pf_001");
                SetControllerField(controller, "_selectedCardId", firstCard.CardId);
                SetControllerField(controller, "_selectedHandCardInstanceId", firstCard.HandCardInstanceId);
                SetControllerField(controller, "_pendingTargetCardId", "pf_003");
                SetControllerField(controller, "_selectedDeploymentTargetInstanceId", "object-7");
                SetControllerField(controller, "_selectedAttackerInstanceId", MatchAttackerIds.Hero);

                var cancel = typeof(DemoSceneController).GetMethod("CancelCurrentInteraction",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(cancel, Is.Not.Null);

                Assert.That(cancel.Invoke(controller, null), Is.True);
                Assert.That(GetControllerField(controller, "_pendingTargetCardId"), Is.Null);
                Assert.That(GetControllerField(controller, "_selectedDeploymentTargetInstanceId"), Is.EqualTo("object-7"));
                Assert.That(GetControllerField(controller, "_selectedAttackerInstanceId"), Is.EqualTo(MatchAttackerIds.Hero));
                Assert.That(GetControllerField(controller, "_selectedCardId"), Is.EqualTo("pf_001"));
                Assert.That(((UnityEngine.UI.Text)GetControllerField(controller, "_statusText")).text, Is.EqualTo("已取消目标选择。"));

                Assert.That(cancel.Invoke(controller, null), Is.True);
                Assert.That(GetControllerField(controller, "_selectedDeploymentTargetInstanceId"), Is.Null);
                Assert.That(GetControllerField(controller, "_selectedAttackerInstanceId"), Is.EqualTo(MatchAttackerIds.Hero));
                Assert.That(GetControllerField(controller, "_selectedCardId"), Is.EqualTo("pf_001"));
                Assert.That(((UnityEngine.UI.Text)GetControllerField(controller, "_statusText")).text, Is.EqualTo("已取消战吼目标选择。"));

                Assert.That(cancel.Invoke(controller, null), Is.True);
                Assert.That(GetControllerField(controller, "_selectedAttackerInstanceId"), Is.Null);
                Assert.That(GetControllerField(controller, "_selectedCardId"), Is.EqualTo("pf_001"));
                Assert.That(((UnityEngine.UI.Text)GetControllerField(controller, "_statusText")).text, Is.EqualTo("已取消攻击者选择。"));

                Assert.That(cancel.Invoke(controller, null), Is.True);
                Assert.That(GetControllerField(controller, "_selectedCardId"), Is.Null);
                Assert.That(GetControllerField(controller, "_selectedHandCardInstanceId"), Is.Null);
                Assert.That(((UnityEngine.UI.Text)GetControllerField(controller, "_statusText")).text, Is.EqualTo("已取消手牌选择。"));
                Assert.That(cancel.Invoke(controller, null), Is.False);
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CraftingScenePreviewBindsTheResetHandCardInstance()
        {
            var root = new GameObject("CraftingPreviewHandBindingTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                typeof(DemoSceneController).GetMethod("SetupCraftingPreview", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { true });

                var match = (DemoLocalMatch)typeof(DemoSceneController)
                    .GetField("_match", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                var selectedInstanceId = typeof(DemoSceneController)
                    .GetMethod("GetSelectedHandCardInstanceId", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null) as string;
                var inspector = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent");
                var details = inspector.GetComponent<CardDetailsView>().CurrentCard;

                Assert.That(match.HandCards.Any(card => card != null && card.handCardInstanceId == selectedInstanceId &&
                    card.cardId == "db_007"), Is.True);
                Assert.That(details.HandCardInstanceId, Is.EqualTo(selectedInstanceId));
                Assert.That(inspector.Find("StaleHandSelection"), Is.Null);
                SetControllerField(controller, "_selectedPaymentMethod", MatchPaymentMethods.Redstone);
                typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                var craftingButton = inspector.Find("PayCrafting").GetComponent<UnityEngine.UI.Button>();
                Assert.That(craftingButton.interactable, Is.True);
                craftingButton.onClick.Invoke();
                Assert.That(GetControllerField(controller, "_selectedPaymentMethod"), Is.EqualTo(MatchPaymentMethods.Crafting));
                var dragBeginSelection = typeof(DemoSceneController).GetMethod(
                    "SelectHandCardInternal", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(dragBeginSelection, Is.Not.Null);
                dragBeginSelection.Invoke(controller, new object[] { "db_007", selectedInstanceId, false });
                Assert.That(GetControllerField(controller, "_selectedPaymentMethod"), Is.EqualTo(MatchPaymentMethods.Crafting),
                    "drag-begin reselecting the exact product instance must preserve its chosen crafting payment");
                details = inspector.GetComponent<CardDetailsView>().CurrentCard;
                var detailRect = details.RectTransform;
                var paymentRect = inspector.Find("PayCrafting").GetComponent<RectTransform>();
                Assert.That(detailRect.anchoredPosition.y - detailRect.sizeDelta.y * 0.5f,
                    Is.GreaterThan(paymentRect.anchoredPosition.y + paymentRect.sizeDelta.y * 0.5f),
                    "the enlarged detail card must stay clear of crafting payment controls");
                var paymentHintRect = inspector.Find("DeployHint").GetComponent<RectTransform>();
                Assert.That(paymentRect.anchoredPosition.y - paymentRect.sizeDelta.y * 0.5f,
                    Is.GreaterThan(paymentHintRect.anchoredPosition.y + paymentHintRect.sizeDelta.y * 0.5f),
                    "payment controls must stay clear of deployment instructions");
                var otherMaterial = match.HandCards.Single(card => card != null && card.cardId == "db_002");
                dragBeginSelection.Invoke(controller, new object[] { otherMaterial.cardId, otherMaterial.handCardInstanceId, false });
                Assert.That(GetControllerField(controller, "_selectedPaymentMethod"), Is.EqualTo(MatchPaymentMethods.Redstone),
                    "choosing a different hand instance must return payment to the default redstone option");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FinishedMatchInspectorKeepsOutcomeAndFinalHeroLifeVisible()
        {
            var root = new GameObject("FinishedMatchInspectorTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();

                var match = (DemoLocalMatch)typeof(DemoSceneController)
                    .GetField("_match", BindingFlags.Instance | BindingFlags.NonPublic).GetValue(controller);
                var finishedProperty = typeof(DemoLocalMatch).GetProperty(nameof(DemoLocalMatch.IsFinished),
                    BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic);
                Assert.That(finishedProperty, Is.Not.Null);
                var refresh = typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(refresh, Is.Not.Null);
                var inspector = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent");
                var roundText = root.transform.Find("DemoCanvas/RoundPlate/Round").GetComponent<Text>();
                var selectFaction = typeof(DemoSceneController).GetMethod(
                    "SelectFaction", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(selectFaction, Is.Not.Null);
                selectFaction.Invoke(controller, new object[] { "desert_badlands" });

                SetPrivateProperty(match, nameof(DemoLocalMatch.PlayerLife), 29);
                SetPrivateProperty(match, nameof(DemoLocalMatch.OpponentLife), 0);
                finishedProperty.SetValue(match, true);
                refresh.Invoke(controller, null);
                Assert.That(inspector.Find("MatchOutcome").GetComponent<Text>().text, Is.EqualTo("胜利"));
                Assert.That(roundText.text, Is.EqualTo("对局结束 · 胜利"));
                Assert.That((Color32)roundText.color, Is.EqualTo(new Color32(228, 185, 95, 255)),
                    "a victory should keep its semantic gold even when the active faction has a different accent");
                Assert.That(inspector.Find("Header").GetComponent<Text>().text, Is.EqualTo("对局结果"));
                Assert.That(root.transform.Find("DemoCanvas/EndTurnButton/Label").GetComponent<Text>().text, Is.EqualTo("本局已结束"));
                Assert.That(root.transform.Find("DemoCanvas/EndTurnButton").GetComponent<Button>().interactable, Is.False);
                Assert.That(root.transform.Find("DemoCanvas/StatusPlate/Status").GetComponent<Text>().text, Is.EqualTo("本局胜利 · 所有操作已锁定。"));
                var summary = inspector.Find("MatchSummary").GetComponent<Text>().text;
                Assert.That(summary, Does.Contain("己方  29"));
                Assert.That(summary, Does.Contain("对手  0"));
                Assert.That(summary, Does.Contain("所有操作已锁定"));

                SetPrivateProperty(match, nameof(DemoLocalMatch.PlayerLife), 0);
                SetPrivateProperty(match, nameof(DemoLocalMatch.OpponentLife), 17);
                refresh.Invoke(controller, null);
                Assert.That(inspector.Find("MatchOutcome").GetComponent<Text>().text, Is.EqualTo("战败"));
                Assert.That(roundText.text, Is.EqualTo("对局结束 · 战败"));
                Assert.That((Color32)roundText.color, Is.EqualTo(new Color32(224, 90, 71, 255)),
                    "a defeat should keep its semantic danger color instead of the faction accent");
                Assert.That(root.transform.Find("DemoCanvas/StatusPlate/Status").GetComponent<Text>().text, Is.EqualTo("本局战败 · 所有操作已锁定。"));
                Assert.That(inspector.Find("MatchSummary").GetComponent<Text>().text, Does.Contain("对手  17"));

                SetPrivateProperty(match, nameof(DemoLocalMatch.OpponentLife), 0);
                refresh.Invoke(controller, null);
                Assert.That(match.HasWinner, Is.False);
                Assert.That(inspector.Find("MatchOutcome").GetComponent<Text>().text, Is.EqualTo("平局"));
                Assert.That(roundText.text, Is.EqualTo("对局结束 · 平局"));
                Assert.That((Color32)roundText.color, Is.EqualTo(new Color32(228, 185, 95, 255)),
                    "a draw should keep its semantic gold rather than the faction accent");
                Assert.That(root.transform.Find("DemoCanvas/StatusPlate/Status").GetComponent<Text>().text, Is.EqualTo("本局平局 · 所有操作已锁定。"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StaleSelectedCardInstanceDisablesInspectorActionsDespiteAnotherSameCard()
        {
            var root = new GameObject("StaleDuplicateHandUiTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();

                var matchField = typeof(DemoSceneController).GetField("_match", BindingFlags.Instance | BindingFlags.NonPublic);
                var match = (DemoLocalMatch)matchField.GetValue(controller);
                match.ResetHand(new[] { "db_005", "db_005" });
                var staleInstanceId = match.HandCards[1].handCardInstanceId;
                Assert.That(match.TrySetHandCardCostModifier(staleInstanceId, -2, "local-player"), Is.True);
                typeof(DemoSceneController).GetMethod("SelectHandCard", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { "db_005", staleInstanceId });

                match.ResetHand(new[] { "db_005" });
                typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);

                var inspector = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent");
                Assert.That(inspector.Find("StaleHandSelection"), Is.Not.Null);
                Assert.That(inspector.Find("Cast"), Is.Null,
                    "a remaining same-card copy must not make the stale selection actionable");
                Assert.That(inspector.GetComponent<CardDetailsView>().CurrentCard.HandCardInstanceId,
                    Is.Empty, "the details card must not masquerade as the remaining duplicate");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PendingDeployableCardExplainsMissingEffectWithoutDisablingBaseDeployment()
        {
            var root = new GameObject("PendingDeployableDisclosureTestRoot");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                root.transform.Find("DemoCanvas/FactionRail/Faction_end")
                    .GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

                var inspector = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent");
                var disclosure = inspector.Find("PendingEffectNotice").GetComponent<UnityEngine.UI.Text>();
                Assert.That(disclosure.text, Does.Contain("仅基础属性可用"));
                Assert.That(disclosure.text, Does.Contain("效果尚未接入"));
                Assert.That(inspector.Find("DeployHint").GetComponent<UnityEngine.UI.Text>().text,
                    Does.Contain("选择发光的单位格"), "the normal deployment affordance stays available");

                var canPreview = typeof(DemoSceneController).GetMethod("IsPreviewingDeployment", BindingFlags.Instance | BindingFlags.NonPublic);
                var previewArgs = new object[] { true, 0 };
                Assert.That(canPreview.Invoke(controller, previewArgs), Is.True,
                    "pending battlecry implementation must not block legal base deployment");
                Assert.That((int)previewArgs[1], Is.EqualTo(1));

                var match = (DemoLocalMatch)GetControllerField(controller, "_match");
                Assert.That(CardContentLoader.Current.TryGetDefinition("ed_001", out var definition), Is.True);
                var energyBefore = match.Energy;
                var result = match.ApplyDeploy(definition,
                    match.CreateDeployCommand(definition.id, DemoSlotKind.Unit, 0));
                Assert.That(result.Accepted, Is.True, result.Message);
                var deployed = match.PlayerBattlefield.Single();
                Assert.That(deployed.CardId, Is.EqualTo("ed_001"));
                Assert.That(deployed.Attack, Is.EqualTo(definition.attack));
                Assert.That(deployed.Health, Is.EqualTo(definition.health));
                Assert.That(match.Energy, Is.EqualTo(energyBefore - definition.cost));

                var selectFaction = typeof(DemoSceneController).GetMethod("SelectFaction", BindingFlags.Instance | BindingFlags.NonPublic);
                var refreshAll = typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic);
                foreach (var pendingCardId in new[] { "ed_001", "ed_003", "db_008", "tk_017" })
                {
                    Assert.That(CardContentLoader.Current.TryGetDefinition(pendingCardId, out var pendingDefinition), Is.True);
                    Assert.That(pendingDefinition.effectImplementationStatus, Is.EqualTo("PENDING"), pendingCardId);
                    Assert.That(pendingDefinition.cardType, Is.EqualTo("UNIT"), pendingCardId);

                    var factionId = pendingDefinition.factionId == "neutral" ? "end" : pendingDefinition.factionId;
                    selectFaction.Invoke(controller, new object[] { factionId });
                    match.ResetHand(new[] { pendingCardId });
                    refreshAll.Invoke(controller, null);
                    FindHandCard(root, pendingCardId).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();

                    Assert.That(inspector.Find("PendingEffectNotice"), Is.Not.Null,
                        $"registered pending deployable {pendingCardId} should disclose its unavailable effect");
                    Assert.That(inspector.Find("DeployHint"), Is.Not.Null,
                        $"registered pending deployable {pendingCardId} should retain the normal deployment affordance");
                }

                selectFaction.Invoke(controller, new object[] { "end" });
                FindHandCard(root, "ed_004").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(inspector.Find("PendingEffectNotice"), Is.Null,
                    "implemented cards do not inherit the pending notice from a prior selection");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RuleDiagnosticsAreHiddenByDefaultAndAvailableOnDemand()
        {
            var root = new GameObject("RuleDiagnosticsVisibilityTestRoot");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();

                var inspector = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent");
                Assert.That(inspector.Find("Implementation"), Is.Null,
                    "player-facing card details must not expose internal effect IDs by default");

                SetControllerField(controller, "_showRuleDiagnostics", true);
                typeof(DemoSceneController).GetMethod("RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                var diagnostics = inspector.Find("Implementation").GetComponent<UnityEngine.UI.Text>();
                Assert.That(diagnostics.text, Does.Contain("effect.pf_001.01"),
                    "the explicit development diagnostic mode should retain effect IDs for debugging");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SelectedDeploymentHintUsesItsRegisteredBiomeAccent()
        {
            var root = new GameObject("ThemedDeploymentHintTestRoot");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                var matchField = typeof(DemoSceneController).GetField("_match", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(matchField, Is.Not.Null);
                ((DemoLocalMatch)matchField.GetValue(controller)).ResetPlayerRedstoneForScenario(10, 10);

                var inspector = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent");
                var factionCards = new[]
                {
                    new[] { "plains_forest", "pf_001" },
                    new[] { "desert_badlands", "db_001" },
                    new[] { "snow_ice", "si_002" },
                    new[] { "cave_dark_forest", "cd_001" },
                    new[] { "ocean_river", "or_001" },
                    new[] { "nether", "nt_001" },
                    new[] { "end", "ed_004" }
                };
                foreach (var factionCard in factionCards)
                {
                    var factionId = factionCard[0];
                    root.transform.Find("DemoCanvas/FactionRail/Faction_" + factionId)
                        .GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    FindHandCard(root, factionCard[1]).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    var selectedCardId = (string)GetControllerField(controller, "_selectedCardId");
                    Assert.That(CardContentLoader.Current.TryGetDefinition(selectedCardId, out var definition), Is.True, factionId);
                    Assert.That(CardContentLoader.Current.TryGetTheme(definition.themeId, out var theme), Is.True, factionId);
                    var hint = inspector.Find("DeployHint").GetComponent<UnityEngine.UI.Text>();
                    Assert.That(hint.color, Is.EqualTo(theme.Accent),
                        $"{factionId} action guidance should follow the selected card's registered biome palette");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CombatInstructionTitleUsesTheActivePlayerBiomeAccent()
        {
            var root = new GameObject("ThemedCombatInstructionTestRoot");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                var match = (DemoLocalMatch)GetControllerField(controller, "_match");
                Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

                var selectFaction = typeof(DemoSceneController).GetMethod(
                    "SelectFaction", BindingFlags.Instance | BindingFlags.NonPublic);
                var refreshAll = typeof(DemoSceneController).GetMethod(
                    "RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(selectFaction, Is.Not.Null);
                Assert.That(refreshAll, Is.Not.Null);
                var inspector = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent");
                var factionIds = new[]
                {
                    "plains_forest", "desert_badlands", "snow_ice", "cave_dark_forest",
                    "ocean_river", "nether", "end"
                };

                foreach (var factionId in factionIds)
                {
                    selectFaction.Invoke(controller, new object[] { factionId });
                    refreshAll.Invoke(controller, null);
                    Assert.That(CardContentLoader.Current.TryGetTheme(factionId, out var theme), Is.True, factionId);
                    var title = inspector.Find("CombatTitle").GetComponent<UnityEngine.UI.Text>();
                    Assert.That(title.color, Is.EqualTo(theme.Accent),
                        $"combat instructions should follow the active player's {factionId} biome palette");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RoundIndicatorFollowsTheFactionWhoseTurnIsActive()
        {
            var root = new GameObject("ThemedRoundIndicatorTestRoot");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                var match = (DemoLocalMatch)GetControllerField(controller, "_match");
                var selectFaction = typeof(DemoSceneController).GetMethod(
                    "SelectFaction", BindingFlags.Instance | BindingFlags.NonPublic);
                var selectOpponentFaction = typeof(DemoSceneController).GetMethod(
                    "SelectOpponentFaction", BindingFlags.Instance | BindingFlags.NonPublic);
                var refreshAll = typeof(DemoSceneController).GetMethod(
                    "RefreshAll", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(selectFaction, Is.Not.Null);
                Assert.That(selectOpponentFaction, Is.Not.Null);
                Assert.That(refreshAll, Is.Not.Null);

                var roundText = root.transform.Find("DemoCanvas/RoundPlate/Round").GetComponent<UnityEngine.UI.Text>();
                var factionIds = new[]
                {
                    "plains_forest", "desert_badlands", "snow_ice", "cave_dark_forest",
                    "ocean_river", "nether", "end"
                };
                for (var index = 0; index < factionIds.Length; index++)
                {
                    var playerFaction = factionIds[index];
                    var opponentFaction = factionIds[(index + 1) % factionIds.Length];
                    selectFaction.Invoke(controller, new object[] { playerFaction });
                    selectOpponentFaction.Invoke(controller, new object[] { opponentFaction });
                    refreshAll.Invoke(controller, null);

                    Assert.That(CardContentLoader.Current.TryGetTheme(playerFaction, out var playerTheme), Is.True, playerFaction);
                    Assert.That(roundText.color, Is.EqualTo(playerTheme.Accent),
                        $"player-turn indicator should use {playerFaction}'s registered accent");

                    var endTurn = match.ApplyEndTurn(match.CreateEndTurnCommand());
                    Assert.That(endTurn.Accepted, Is.True, endTurn.Message);
                    refreshAll.Invoke(controller, null);
                    Assert.That(CardContentLoader.Current.TryGetTheme(opponentFaction, out var opponentTheme), Is.True, opponentFaction);
                    Assert.That(roundText.color, Is.EqualTo(opponentTheme.Accent),
                        $"opponent-turn indicator should use {opponentFaction}'s registered accent");

                    match.BeginNextPlayerTurn();
                    refreshAll.Invoke(controller, null);
                    Assert.That(roundText.color, Is.EqualTo(playerTheme.Accent),
                        "returning the turn restores the player's biome accent");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void FactionAvatarUsesItsRegisteredMinecraftPortraitAndRetainsFallback()
        {
            var root = new GameObject("FactionAvatarTestRoot");
            try
            {
                var configuredBattlefield = root.AddComponent<DemoBattlefield3D>();
                configuredBattlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();

                var playerPortrait = root.transform.Find("DemoCanvas/PlayerHUD/AvatarIcon").GetComponent<UnityEngine.UI.Image>();
                var playerFallback = root.transform.Find("DemoCanvas/PlayerHUD/AvatarGlyph").GetComponent<UnityEngine.UI.Text>();
                var opponentPortrait = root.transform.Find("DemoCanvas/OpponentHUD/AvatarIcon").GetComponent<UnityEngine.UI.Image>();
                var opponentFallback = root.transform.Find("DemoCanvas/OpponentHUD/AvatarGlyph").GetComponent<UnityEngine.UI.Text>();
                var selectPlayerFaction = typeof(DemoSceneController).GetMethod("SelectFaction", BindingFlags.Instance | BindingFlags.NonPublic);
                var selectOpponentFaction = typeof(DemoSceneController).GetMethod("SelectOpponentFaction", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(selectPlayerFaction, Is.Not.Null);
                Assert.That(selectOpponentFaction, Is.Not.Null);

                var portraits = new[]
                {
                    new[] { "plains_forest", "pf_008" },
                    new[] { "desert_badlands", "db_003" },
                    new[] { "snow_ice", "si_005" },
                    new[] { "cave_dark_forest", "cd_005" },
                    new[] { "ocean_river", "or_004" },
                    new[] { "nether", "nt_003" },
                    new[] { "end", "ed_003" }
                };

                foreach (var portrait in portraits)
                {
                    selectPlayerFaction.Invoke(controller, new object[] { portrait[0] });
                    selectOpponentFaction.Invoke(controller, new object[] { portrait[0] });
                    Assert.That(playerPortrait.sprite, Is.Not.Null, portrait[0] + " player portrait sprite");
                    Assert.That(opponentPortrait.sprite, Is.Not.Null, portrait[0] + " opponent portrait sprite");
                    Assert.That(playerPortrait.sprite.name, Is.EqualTo("DemoArt_" + portrait[1]));
                    Assert.That(opponentPortrait.sprite.name, Is.EqualTo("DemoArt_" + portrait[1]));
                    Assert.That(playerPortrait.gameObject.activeSelf, Is.True);
                    Assert.That(opponentPortrait.gameObject.activeSelf, Is.True);
                    Assert.That(playerFallback.gameObject.activeSelf, Is.False);
                    Assert.That(opponentFallback.gameObject.activeSelf, Is.False);
                    Assert.That(playerPortrait.preserveAspect, Is.True);
                    Assert.That(opponentPortrait.preserveAspect, Is.True);
                    Assert.That(playerPortrait.raycastTarget, Is.False);
                    Assert.That(opponentPortrait.raycastTarget, Is.False);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void OpponentFactionControlsDisableWheneverFactionSelectionIsLocked()
        {
            var root = new GameObject("FactionSelectionLockUiTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();

                var previous = root.transform.Find("DemoCanvas/OpponentFactionSelector/PreviousOpponentFaction")
                    .GetComponent<Button>();
                var next = root.transform.Find("DemoCanvas/OpponentFactionSelector/NextOpponentFaction")
                    .GetComponent<Button>();
                var playerFaction = root.transform.Find("DemoCanvas/FactionRail/Faction_plains_forest")
                    .GetComponent<Button>();
                var previousLabel = previous.GetComponentInChildren<Text>();
                var nextLabel = next.GetComponentInChildren<Text>();
                var factionLabel = playerFaction.GetComponentInChildren<Text>();
                var previousColor = previousLabel.color;
                var nextColor = nextLabel.color;
                var factionColor = factionLabel.color;
                Assert.That(previous.interactable, Is.True, "local previews allow selecting the opponent biome");
                Assert.That(next.interactable, Is.True, "local previews allow selecting the opponent biome");
                Assert.That(playerFaction.interactable, Is.True, "local previews allow selecting the player biome");

                var gateway = new FakeMatchGateway(new MatchConnectionStatus(MatchConnectionPhase.Matchmaking));
                SetControllerField(controller, "_onlineGateway", gateway);
                typeof(DemoSceneController).GetMethod("RefreshFactionButtons", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);

                Assert.That(previous.interactable, Is.False,
                    "the opponent selector must not advertise a local-only change during matchmaking");
                Assert.That(next.interactable, Is.False,
                    "the opponent selector must not advertise a local-only change during matchmaking");
                Assert.That(playerFaction.interactable, Is.False,
                    "both faction selectors follow the same lock state");
                Assert.That(previousLabel.color, Is.Not.EqualTo(previousColor),
                    "the opponent arrow glyph must visibly read as disabled, not merely lose its button callback");
                Assert.That(nextLabel.color, Is.Not.EqualTo(nextColor));
                Assert.That(factionLabel.color, Is.Not.EqualTo(factionColor));

                var playerFactionBeforeAttempt = battlefield.PlayerFactionId;
                var opponentFactionBeforeAttempt = battlefield.OpponentFactionId;
                typeof(DemoSceneController).GetMethod("SelectFaction", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { "end" });
                typeof(DemoSceneController).GetMethod("SelectOpponentFaction", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, new object[] { "end" });
                Assert.That(battlefield.PlayerFactionId, Is.EqualTo(playerFactionBeforeAttempt),
                    "a queued player-faction callback must not mutate the scene after locking");
                Assert.That(battlefield.OpponentFactionId, Is.EqualTo(opponentFactionBeforeAttempt),
                    "a queued opponent-faction callback must not mutate the authoritative scene theme");

                gateway.SetStatus(new MatchConnectionStatus(MatchConnectionPhase.Offline));
                typeof(DemoSceneController).GetMethod("RefreshFactionButtons", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                Assert.That(previous.interactable, Is.True, "offline preview controls restore after returning to local mode");
                Assert.That(next.interactable, Is.True, "offline preview controls restore after returning to local mode");
                Assert.That(previousLabel.color, Is.EqualTo(previousColor));
                Assert.That(nextLabel.color, Is.EqualTo(nextColor));
                Assert.That(factionLabel.color, Is.EqualTo(factionColor));

                SetControllerField(controller, "_previewOnlineStatus", true);
                typeof(DemoSceneController).GetMethod("RefreshFactionButtons", BindingFlags.Instance | BindingFlags.NonPublic)
                    .Invoke(controller, null);
                Assert.That(previous.interactable, Is.False, "the reconnect-state visual preview must use the same input lock");
                Assert.That(next.interactable, Is.False);
                Assert.That(previousLabel.color, Is.Not.EqualTo(previousColor));
                Assert.That(nextLabel.color, Is.Not.EqualTo(nextColor));
                Assert.That(factionLabel.color, Is.Not.EqualTo(factionColor));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void GeneratedSceneAndRuntimeHierarchyExist()
        {
            Assert.That(AssetDatabase.LoadAssetAtPath<SceneAsset>(DemoSceneBuilder.ScenePath), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(DemoUiPrefabBuilder.PrefabFolder + "/BasePanel.prefab").GetComponent<BasePanel>(), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(DemoUiPrefabBuilder.PrefabFolder + "/SecondaryButton.prefab").GetComponent<SecondaryButton>(), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(DemoUiPrefabBuilder.PrefabFolder + "/PrimaryActionButton.prefab").GetComponent<PrimaryActionButton>(), Is.Not.Null);
            Assert.That(AssetDatabase.LoadAssetAtPath<GameObject>(DemoUiPrefabBuilder.PrefabFolder + "/CardUI.prefab").GetComponent<CardUI>(), Is.Not.Null);

            var registry = CardContentLoader.Load();
            var root = new GameObject("DemoTestRoot");
            try
            {
                var configuredBattlefield = root.AddComponent<DemoBattlefield3D>();
                configuredBattlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                var worldLabelTexts = root.GetComponentsInChildren<Text>(true)
                    .Where(value => value.name == "WorldLabelText")
                    .ToArray();
                Assert.That(worldLabelTexts, Is.Not.Empty, "the generated match should render unit nameplates");
                Assert.That(worldLabelTexts.All(value => value.fontSize == 15 && value.resizeTextForBestFit &&
                    value.resizeTextMinSize == 12 && value.resizeTextMaxSize == 15), Is.True,
                    "unit nameplates should use the larger readable type scale while shrinking long status strings to fit");
                Assert.That(worldLabelTexts.All(value => value.rectTransform.sizeDelta.y >= 28f), Is.True,
                    "unit nameplate text needs enough vertical room for a readable line");
                Assert.That(registry.TryGetDefinition("si_005", out var polarBearDefinition), Is.True);
                var multilineLabelHost = new GameObject("MultilineWorldLabelHost", typeof(RectTransform)).GetComponent<RectTransform>();
                multilineLabelHost.SetParent(root.transform.Find("DemoCanvas"), false);
                multilineLabelHost.sizeDelta = new Vector2(150, 118);
                var createWorldPieceLabel = typeof(DemoSceneController).GetMethod(
                    "CreateWorldPieceLabel", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(createWorldPieceLabel, Is.Not.Null);
                var modifiedPolarBear = new DemoBattlefieldObject
                {
                    CardId = "si_005",
                    Attack = polarBearDefinition.attack + 2,
                    Health = polarBearDefinition.health + 1,
                    MaxHealth = polarBearDefinition.health + 1,
                    TemporaryHealthModifier = 1
                };
                createWorldPieceLabel.Invoke(controller, new object[]
                {
                    multilineLabelHost, "si_005", multilineLabelHost.sizeDelta, false, modifiedPolarBear
                });
                var multilineLabelText = multilineLabelHost.GetComponentInChildren<Text>(true);
                Assert.That(multilineLabelText.text.Split('\n').Length, Is.EqualTo(3),
                    "permanent attack and temporary health modifiers should remain separately legible");
                Assert.That(multilineLabelText.resizeTextForBestFit, Is.True);
                Assert.That(multilineLabelText.resizeTextMinSize, Is.EqualTo(12));
                Assert.That(multilineLabelText.rectTransform.sizeDelta.y, Is.EqualTo(54f));
                var multilinePlate = multilineLabelHost.Cast<Transform>()
                    .Single(value => value.name == "WorldLabel")
                    .GetComponent<Image>();
                Assert.That(multilinePlate.rectTransform.sizeDelta.y, Is.EqualTo(64f));
                var battlefield = root.GetComponent<DemoBattlefield3D>();
                Assert.That(battlefield, Is.Not.Null);
                Assert.That(battlefield.BoardCamera, Is.Not.Null);
                Assert.That(battlefield.LetterboxCamera, Is.Not.Null,
                    "a full-screen backdrop camera fills the bars outside the 16:9 battlefield viewport");
                Assert.That(battlefield.LetterboxCamera.rect, Is.EqualTo(new Rect(0f, 0f, 1f, 1f)));
                Assert.That(battlefield.LetterboxCamera.cullingMask, Is.Zero,
                    "the backdrop camera clears the whole display without drawing world geometry twice");
                Assert.That(battlefield.LetterboxCamera.depth, Is.LessThan(battlefield.BoardCamera.depth));
                Assert.That(battlefield.BoardCamera.orthographic, Is.False);
                Assert.That(battlefield.BoardCamera.fieldOfView, Is.EqualTo(42.5f).Within(0.01f));
                var boardCamera = battlefield.BoardCamera;
                var viewportTarget = new RenderTexture(1280, 960, 24, RenderTextureFormat.ARGB32);
                viewportTarget.Create();
                try
                {
                    boardCamera.targetTexture = viewportTarget;
                    boardCamera.rect = DemoBattlefield3D.CalculateAspectViewport(4f / 3f);
                    Assert.That(boardCamera.aspect,
                        Is.EqualTo((float)boardCamera.pixelWidth / boardCamera.pixelHeight).Within(0.01f),
                        "the camera projection should match its letterboxed viewport pixel ratio");
                    Assert.That(boardCamera.aspect, Is.EqualTo(16f / 9f).Within(0.01f));
                    Assert.That(boardCamera.rect.x, Is.Zero.Within(0.001f));
                    Assert.That(boardCamera.rect.width, Is.EqualTo(1f).Within(0.001f));
                    Assert.That(boardCamera.rect.y, Is.EqualTo(0.125f).Within(0.001f));
                    Assert.That(boardCamera.rect.height, Is.EqualTo(0.75f).Within(0.001f));
                    var ultrawide = DemoBattlefield3D.CalculateAspectViewport(21f / 9f);
                    Assert.That(ultrawide.x, Is.EqualTo(5f / 42f).Within(0.001f));
                    Assert.That(ultrawide.width, Is.EqualTo(16f / 21f).Within(0.001f));
                    Assert.That(ultrawide.height, Is.EqualTo(1f).Within(0.001f));
                }
                finally
                {
                    boardCamera.targetTexture = null;
                    viewportTarget.Release();
                    UnityEngine.Object.DestroyImmediate(viewportTarget);
                }
                Assert.That(root.transform.Find("BattlefieldGeometry"), Is.Not.Null);
                Assert.That(root.transform.Find("BattlefieldPieces"), Is.Not.Null);
                var unitMarker = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Unit_0/InteractiveGround");
                var opponentUnitMarker = root.transform.Find("BattlefieldGeometry/SlotMarker_Opponent_Unit_0/InteractiveGround");
                var buildingMarker = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Building_0/InteractiveGround");
                var buildingInteractionFootprint = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Building_0/InteractionFootprint");
                var unitRiser = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Unit_0/GroundRiser");
                Assert.That(unitMarker, Is.Not.Null);
                Assert.That(buildingMarker, Is.Not.Null);
                Assert.That(buildingInteractionFootprint, Is.Not.Null);
                Assert.That(buildingInteractionFootprint.GetComponent<BoxCollider>().size.x, Is.EqualTo(2.85f).Within(0.01f));
                Assert.That(buildingInteractionFootprint.GetComponent<BoxCollider>().size.z, Is.EqualTo(1.20f).Within(0.01f));
                Assert.That(unitRiser, Is.Not.Null);
                Assert.That(unitMarker.IsChildOf(root.transform.Find("DemoCanvas")), Is.False);
                Assert.That(root.GetComponent<DemoBattlefieldPointerController>(), Is.Not.Null);
                Assert.That(unitMarker.GetComponent<MeshCollider>(), Is.Not.Null);
                var unitTarget = unitMarker.GetComponent<DemoBattlefieldSlotTarget>();
                Assert.That(unitTarget, Is.Not.Null);
                Assert.That(unitTarget.Player, Is.True);
                Assert.That(unitTarget.Kind, Is.EqualTo(DemoSlotKind.Unit));
                Assert.That(unitTarget.Index, Is.Zero);
                Assert.That(unitMarker.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.EqualTo(60));
                Assert.That(unitMarker.GetComponent<MeshRenderer>().enabled, Is.True);
                Assert.That(buildingMarker.GetComponent<MeshRenderer>().enabled, Is.True);
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.shader.name, Is.EqualTo("BiomeRivals/Demo/GroundSurface"));
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_UseScreenProjection"), Is.EqualTo(0f));
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name, Is.EqualTo("grass_block_top"));
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name, Is.EqualTo("netherrack"));
                Assert.That(root.transform.Find("BattlefieldGeometry/Ground_Player_0_-3"), Is.Not.Null, "voxel terrain is built for the player half");
                Assert.That(root.transform.Find("BattlefieldGeometry/Ground_Opponent_0_3"), Is.Not.Null, "voxel terrain is built for the opponent half");
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.GreaterThan(0f));
                Assert.That(buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.Zero);
                battlefield.SetSlotState(true, DemoSlotKind.Unit, 0, true, false);
                var friendlyTargetColor = unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_HighlightColor");
                Assert.That(friendlyTargetColor.g, Is.GreaterThan(friendlyTargetColor.r),
                    "friendly action cells should use a Minecraft grass/lime accent, not the old cyan overlay-like color");
                battlefield.SetSlotState(false, DemoSlotKind.Unit, 0, true, false);
                var enemyTargetColor = opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_HighlightColor");
                Assert.That(enemyTargetColor.r, Is.GreaterThan(enemyTargetColor.g));
                Assert.That(enemyTargetColor.g, Is.GreaterThan(enemyTargetColor.b),
                    "hostile action cells should read as warm block/torch amber and remain distinct from friendly cells");
                battlefield.SetSlotState(true, DemoSlotKind.Unit, 0, false, false);
                battlefield.SetSlotState(false, DemoSlotKind.Unit, 0, false, false);
                battlefield.SetSlotEngineReady(true, DemoSlotKind.Building, 0, DemoEngineReadyKind.Nursery);
                Assert.That(buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.GreaterThan(0f));
                var nurseryHighlight = buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_HighlightColor");
                battlefield.SetSlotEngineReady(true, DemoSlotKind.Building, 0, DemoEngineReadyKind.Coral);
                var coralHighlight = buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_HighlightColor");
                Assert.That(nurseryHighlight, Is.Not.EqualTo(coralHighlight));
                battlefield.SetSlotEngineReady(true, DemoSlotKind.Building, 0, DemoEngineReadyKind.Cactus);
                var cactusHighlight = buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_HighlightColor");
                Assert.That(cactusHighlight, Is.Not.EqualTo(nurseryHighlight));
                Assert.That(cactusHighlight, Is.Not.EqualTo(coralHighlight));
                battlefield.SetSlotEngineReady(true, DemoSlotKind.Building, 0, DemoEngineReadyKind.Temple);
                var templeHighlight = buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_HighlightColor");
                Assert.That(templeHighlight, Is.Not.EqualTo(cactusHighlight));
                Assert.That(templeHighlight, Is.Not.EqualTo(coralHighlight));
                battlefield.SetSlotEngineReady(true, DemoSlotKind.Building, 0, DemoEngineReadyKind.IceSpire);
                var iceSpireHighlight = buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_HighlightColor");
                Assert.That(iceSpireHighlight, Is.Not.EqualTo(templeHighlight));
                battlefield.SetSlotEngineReady(true, DemoSlotKind.Building, 0, DemoEngineReadyKind.SnowHut);
                var snowHutHighlight = buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_HighlightColor");
                Assert.That(snowHutHighlight, Is.Not.EqualTo(iceSpireHighlight));
                battlefield.SetSlotEngineReady(true, DemoSlotKind.Building, 0, DemoEngineReadyKind.EndCrystal);
                var endCrystalHighlight = buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_HighlightColor");
                Assert.That(endCrystalHighlight, Is.Not.EqualTo(snowHutHighlight));
                battlefield.SetSlotEngineReady(true, DemoSlotKind.Building, 0, DemoEngineReadyKind.NetherFortress);
                var fortressHighlight = buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_HighlightColor");
                Assert.That(fortressHighlight, Is.Not.EqualTo(endCrystalHighlight));
                var synchronizedMarker = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Building_1/InteractiveGround");
                battlefield.SetSlotEngineReady(true, DemoSlotKind.Building, 0, DemoEngineReadyKind.Temple, "object-temple");
                battlefield.SetSlotEngineReady(true, DemoSlotKind.Building, 1, DemoEngineReadyKind.Temple, "object-temple");
                var synchronizedColorA = buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_HighlightColor");
                var synchronizedColorB = synchronizedMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_HighlightColor");
                Assert.That(Vector4.Distance(synchronizedColorA, synchronizedColorB), Is.LessThan(0.001f));
                battlefield.SetSlotEngineReady(true, DemoSlotKind.Building, 0, DemoEngineReadyKind.None);
                battlefield.SetSlotEngineReady(true, DemoSlotKind.Building, 1, DemoEngineReadyKind.None);
                Assert.That(buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.Zero);
                battlefield.SetSlotEndPhaseThreat(false, DemoSlotKind.Unit, 0, true);
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.GreaterThan(0f));
                battlefield.SetSlotEndPhaseThreat(false, DemoSlotKind.Unit, 0, false);
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.Zero);
                battlefield.SetSlotPoisoned(false, DemoSlotKind.Unit, 0, true);
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.GreaterThan(0f));
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_HighlightColor").g,
                    Is.GreaterThan(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_HighlightColor").r));
                battlefield.SetSlotPoisoned(false, DemoSlotKind.Unit, 0, false);
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.Zero);
                battlefield.SetSlotBurning(false, DemoSlotKind.Unit, 0, true);
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.GreaterThan(0f));
                var fireHighlight = opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_HighlightColor");
                Assert.That(fireHighlight.r, Is.GreaterThan(fireHighlight.b));
                battlefield.SetSlotBurning(false, DemoSlotKind.Unit, 0, false);
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.Zero);
                battlefield.SetSlotWithered(false, DemoSlotKind.Unit, 0, true);
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.GreaterThan(0f));
                var witherHighlight = opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_HighlightColor");
                Assert.That(witherHighlight.b, Is.GreaterThan(witherHighlight.g));
                battlefield.SetSlotWithered(false, DemoSlotKind.Unit, 0, false);
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.Zero);
                battlefield.SetSlotState(false, DemoSlotKind.Unit, 0, true, false, true);
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.GreaterThanOrEqualTo(0.32f));
                battlefield.SetSlotState(false, DemoSlotKind.Unit, 0, false, false);
                battlefield.SyncPieces(System.Array.Empty<DemoBattlefieldObject>(), new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-withered", CardId = "or_005", Player = false,
                        SlotKind = DemoSlotKind.Unit, SlotIndex = 0, OccupiedSlots = 1, Health = 2, MaxHealth = 6,
                        Statuses = new[]
                        {
                            new BattlefieldStatusStateDto
                            {
                                statusId = "WITHER", remainingDuration = 1, sourcePlayerId = "player",
                                sourceCardId = "nt_005", sourceInstanceId = "object-source", effectId = "effect.nt_005.01"
                            }
                        }
                    }
                }, registry);
                Assert.That(root.transform.Find("BattlefieldPieces/Piece_object-withered_or_005/WitherStatusFx"), Is.Not.Null);
                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-nursery", CardId = "pf_005", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 0, OccupiedSlots = 1, Health = 4, MaxHealth = 4
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                Assert.That(root.transform.Find("BattlefieldPieces/Piece_object-nursery_pf_005/NurserySoil"), Is.Not.Null);
                Assert.That(root.transform.Find("BattlefieldPieces/Piece_object-nursery_pf_005/SaplingCenterLeaves"), Is.Not.Null);
                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-reef", CardId = "or_007", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 0, OccupiedSlots = 1, Health = 5, MaxHealth = 5
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                Assert.That(root.transform.Find("BattlefieldPieces/Piece_object-reef_or_007/CoralCore"), Is.Not.Null);
                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-monument", CardId = "or_008", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 0, OccupiedSlots = 3, Health = 11, MaxHealth = 11
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                Assert.That(root.transform.Find("BattlefieldPieces/Piece_object-monument_or_008/MonumentCore"), Is.Not.Null);
                Assert.That(root.transform.Find("BattlefieldPieces/Piece_object-monument_or_008/MonumentLeftTower"), Is.Not.Null);
                Assert.That(root.transform.Find("BattlefieldPieces/Piece_object-monument_or_008/MonumentRightTower"), Is.Not.Null);
                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-fortress", CardId = "nt_008", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 0, OccupiedSlots = 3, Health = 10, MaxHealth = 10
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                var fortressPiece = root.transform.Find("BattlefieldPieces/Piece_object-fortress_nt_008");
                Assert.That(fortressPiece.Find("FortressBridge"), Is.Not.Null);
                Assert.That(fortressPiece.Find("FortressLeftTower"), Is.Not.Null);
                Assert.That(fortressPiece.Find("FortressRightTower"), Is.Not.Null);
                Assert.That(fortressPiece.Find("FortressLeftMagma").GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name,
                    Is.EqualTo("magma"));
                Physics.SyncTransforms();
                for (var sideIndex = 0; sideIndex < 2; sideIndex++)
                {
                    var playerSide = sideIndex == 0;
                    foreach (var slotKind in new[] { DemoSlotKind.Unit, DemoSlotKind.Building })
                    {
                        var slotCount = slotKind == DemoSlotKind.Unit ? 4 : 3;
                        for (var slotIndex = 0; slotIndex < slotCount; slotIndex++)
                        {
                            var screenPosition = battlefield.BoardCamera.WorldToScreenPoint(
                                battlefield.GetSlotInteractionWorldPosition(playerSide, slotKind, slotIndex));
                            Assert.That(battlefield.TryRaycastSlot(screenPosition, out var target), Is.True,
                                $"{(playerSide ? "player" : "opponent")} {slotKind} slot {slotIndex} should cover its full pixel-grid surface");
                            Assert.That(target.Player, Is.EqualTo(playerSide));
                            Assert.That(target.Kind, Is.EqualTo(slotKind));
                Assert.That(target.Index, Is.EqualTo(slotIndex));
                        }
                    }
                }
                var buildingCenter = battlefield.BoardCamera.WorldToScreenPoint(
                    battlefield.GetSlotInteractionWorldPosition(true, DemoSlotKind.Building, 1));
                var buildingCenterMarker = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Building_1/InteractiveGround");
                battlefield.SetSlotState(true, DemoSlotKind.Building, 1, true, false);
                var pointerController = root.GetComponent<DemoBattlefieldPointerController>();
                var hoveredCenterTarget = pointerController.ProcessPointerFrame(buildingCenter, false, false, false);
                Assert.That(hoveredCenterTarget.Kind, Is.EqualTo(DemoSlotKind.Building));
                Assert.That(hoveredCenterTarget.Index, Is.EqualTo(1));
                Assert.That(buildingCenterMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"),
                    Is.GreaterThanOrEqualTo(0.78f), "hovering the visual seam should still drive the in-world ground highlight");
                pointerController.enabled = false;
                pointerController.enabled = true;
                battlefield.SetSlotState(true, DemoSlotKind.Building, 1, false, false);
                var unitMeshVertices = unitMarker.GetComponent<MeshFilter>().sharedMesh.vertices;
                var nearZ = unitMeshVertices.Min(vertex => vertex.z);
                var farZ = unitMeshVertices.Max(vertex => vertex.z);
                var nearVertices = unitMeshVertices.Where(vertex => Mathf.Abs(vertex.z - nearZ) < 0.001f).ToArray();
                var farVertices = unitMeshVertices.Where(vertex => Mathf.Abs(vertex.z - farZ) < 0.001f).ToArray();
                var nearWidth = ProjectedWidth(battlefield.BoardCamera, unitMarker, nearVertices);
                var farWidth = ProjectedWidth(battlefield.BoardCamera, unitMarker, farVertices);
                Assert.That(nearWidth, Is.GreaterThan(farWidth));
                Assert.That(root.transform.Find("DemoCanvas"), Is.Not.Null);
                var canvas = root.transform.Find("DemoCanvas").GetComponent<UnityEngine.Canvas>();
                var scaler = root.transform.Find("DemoCanvas").GetComponent<UnityEngine.UI.CanvasScaler>();
                Assert.That(canvas.pixelPerfect, Is.True);
                Assert.That(scaler.screenMatchMode, Is.EqualTo(CanvasScaler.ScreenMatchMode.Expand),
                    "the complete UI must fit the same centered reference aspect as the battlefield");
                Assert.That(scaler.referencePixelsPerUnit, Is.EqualTo(DemoUiMetrics.PixelsPerUnit));
                var statusText = root.transform.Find("DemoCanvas/StatusPlate/Status").GetComponent<UnityEngine.UI.Text>();
                Assert.That(statusText.resizeTextForBestFit, Is.False);
                Assert.That(statusText.GetComponent<DemoReadableSummary>(), Is.Not.Null);
                Assert.That(GameObject.Find("EndTurnButton"), Is.Not.Null);
                Assert.That(GameObject.Find("Faction_plains_forest"), Is.Not.Null);
                Assert.That(root.transform.Find("DemoCanvas/OnlineStatusPanel/Status").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("本地模式"));
                var onlineStatusPanel = root.transform.Find("DemoCanvas/OnlineStatusPanel").GetComponent<RectTransform>();
                Assert.That(onlineStatusPanel.anchoredPosition, Is.EqualTo(new Vector2(444, 456)));
                Assert.That(onlineStatusPanel.sizeDelta, Is.EqualTo(new Vector2(358, 84)));
                Assert.That(onlineStatusPanel.anchoredPosition.x - onlineStatusPanel.sizeDelta.x * 0.5f,
                    Is.GreaterThan(255f), "online status must not overlap the centered title plate");
                Assert.That(onlineStatusPanel.anchoredPosition.x + onlineStatusPanel.sizeDelta.x * 0.5f,
                    Is.LessThan(635f), "online status must leave space before the round plate");
                var onlineStatus = root.transform.Find("DemoCanvas/OnlineStatusPanel/Status").GetComponent<UnityEngine.UI.Text>();
                Assert.That(onlineStatus.fontSize, Is.EqualTo(15));
                Assert.That(onlineStatus.resizeTextForBestFit, Is.False,
                    "connection status uses deliberate semantic lines at the full readable type size");
                Assert.That(onlineStatus.horizontalOverflow, Is.EqualTo(HorizontalWrapMode.Wrap));
                Assert.That(onlineStatus.verticalOverflow, Is.EqualTo(VerticalWrapMode.Truncate));
                var onlineStatusMessages = new[]
                {
                    "寻找对手中\n沙漠",
                    "正在重连\n第 999 次",
                    "版本不兼容\n检查两端",
                    "联机底座\n未启动"
                };
                var onlineStatusBounds = onlineStatus.rectTransform.rect;
                foreach (var message in onlineStatusMessages)
                {
                    var settings = onlineStatus.GetGenerationSettings(onlineStatusBounds.size);
                    settings.resizeTextForBestFit = false;
                    settings.fontSize = onlineStatus.fontSize;
                    settings.horizontalOverflow = HorizontalWrapMode.Wrap;
                    settings.verticalOverflow = VerticalWrapMode.Overflow;
                    var requiredHeight = new TextGenerator().GetPreferredHeight(message, settings);
                    Assert.That(requiredHeight, Is.LessThanOrEqualTo(onlineStatusBounds.height + 0.5f),
                        $"online status message must fit without vertical truncation: {message}");
                }
                var accountStatus = root.transform.Find("DemoCanvas/OnlineStatusPanel/Account").GetComponent<UnityEngine.UI.Text>();
                Assert.That(accountStatus.fontSize, Is.EqualTo(15));
                Assert.That(accountStatus.text,
                    Is.EqualTo("游客 · 未登录"));
                var deckStatus = root.transform.Find("DemoCanvas/OnlineStatusPanel/Deck").GetComponent<UnityEngine.UI.Text>();
                Assert.That(deckStatus.fontSize, Is.EqualTo(14));
                Assert.That(deckStatus.text,
                    Is.EqualTo("卡组 · 平原"));
                Assert.That(root.transform.Find("DemoCanvas/OnlineStatusPanel/OnlineAction").GetComponent<SecondaryButton>(), Is.Not.Null);
                var onlineActionLabel = root.transform.Find("DemoCanvas/OnlineStatusPanel/OnlineAction").GetComponentInChildren<UnityEngine.UI.Text>();
                Assert.That(onlineActionLabel.fontSize, Is.EqualTo(14));
                Assert.That(onlineActionLabel.text,
                    Is.EqualTo("匹配"));
                var playerSlotHitArea = root.transform.Find("DemoCanvas/PlayerUnitSlot0");
                Assert.That(playerSlotHitArea.GetComponent<UnityEngine.UI.Graphic>(), Is.Null);
                Assert.That(playerSlotHitArea.GetComponent<UnityEngine.UI.Button>(), Is.Null);
                Assert.That(root.transform.Find("DemoCanvas/OpponentUnitSlot0").GetComponent<UnityEngine.UI.Graphic>(), Is.Null);
                Assert.That(root.transform.Find("DemoCanvas/OpponentHUD").GetComponent<UnityEngine.UI.Button>(), Is.Not.Null);
                Assert.That(root.transform.Find("DemoCanvas/OpponentHUD/Health").GetComponent<UnityEngine.UI.Text>().text, Does.Contain("30"));
                var opponentHud = root.transform.Find("DemoCanvas/OpponentHUD");
                Assert.That(opponentHud, Is.Not.Null);
                Assert.That(opponentHud.GetComponent<BasePanel>(), Is.Not.Null);
                Assert.That(opponentHud.GetComponent<UnityEngine.UI.Outline>(), Is.Null);
                var materialFill = opponentHud.Find("MaterialFill")?.GetComponent<UnityEngine.UI.Image>();
                var frameSlice = opponentHud.Find("FrameSlice")?.GetComponent<UnityEngine.UI.Image>();
                Assert.That(materialFill, Is.Not.Null);
                Assert.That(materialFill.type, Is.EqualTo(UnityEngine.UI.Image.Type.Tiled));
                Assert.That(materialFill.sprite.pixelsPerUnit, Is.EqualTo(DemoUiMetrics.PixelsPerUnit));
                Assert.That(frameSlice, Is.Not.Null);
                Assert.That(frameSlice.type, Is.EqualTo(UnityEngine.UI.Image.Type.Sliced));
                Assert.That(frameSlice.fillCenter, Is.False, "Border must not stretch a second stone texture behind the reading surface.");
                Assert.That(materialFill.color.a, Is.EqualTo(1f), "World and frame textures must not bleed into the reading surface.");
                Assert.That(frameSlice.pixelsPerUnitMultiplier, Is.EqualTo(1f));
                Assert.That(frameSlice.sprite.pixelsPerUnit, Is.EqualTo(DemoUiMetrics.PixelsPerUnit));
                Assert.That(frameSlice.sprite.border, Is.EqualTo(Vector4.one * DemoUiMetrics.FrameBorderPixels));
                Assert.That(opponentHud.Find("FrameTop"), Is.Null);
                Assert.That(opponentHud.Find("FrameCornerNW"), Is.Null);
                Assert.That(opponentHud.Find("RivetNW")?.GetComponent<UnityEngine.UI.Image>(), Is.Not.Null);
                Assert.That(frameSlice.sprite.texture.name, Does.Contain("stone_bricks"));
                Assert.That(root.transform.Find("DemoCanvas/PlayerHUD/FrameSlice").GetComponent<UnityEngine.UI.Image>().sprite.texture.name, Does.Contain("stone_bricks"));
                Assert.That(root.transform.Find("DemoCanvas/PlayerHUD/EffectFlash").GetComponent<CanvasGroup>().alpha, Is.Zero);
                Assert.That(root.transform.Find("DemoCanvas/CardDetailsPanel/FrameSlice").GetComponent<UnityEngine.UI.Image>().sprite.texture.name, Does.Contain("stone_bricks"));
                Assert.That(root.transform.Find("DemoCanvas/HandPlate/FrameSlice").GetComponent<UnityEngine.UI.Image>().sprite.texture.name, Does.Contain("stone_bricks"));
                Assert.That(root.transform.Find("DemoCanvas/HandLabel").GetComponent<UnityEngine.UI.Text>().text, Does.Contain("牌库 25"));
                Assert.That(root.transform.Find("DemoCanvas/EndTurnButton/FrameSlice").GetComponent<UnityEngine.UI.Image>().sprite.texture.name, Does.Contain("stone_bricks"));
                var endTurn = root.transform.Find("DemoCanvas/EndTurnButton");
                Assert.That(endTurn.GetComponent<PrimaryActionButton>(), Is.Not.Null);
                Assert.That(endTurn.GetComponent<SecondaryButton>(), Is.Null);
                var endTurnButton = endTurn.GetComponent<UnityEngine.UI.Button>();
                var endTurnFrame = endTurn.Find("FrameSlice").GetComponent<UnityEngine.UI.Image>();
                Assert.That(endTurnButton.targetGraphic, Is.SameAs(endTurnFrame));
                Assert.That(endTurnButton.colors.normalColor, Is.EqualTo(DemoUiStyleCatalog.GetFrameTint(DemoUiStyleClass.PrimaryActionButton)));
                Assert.That(endTurnButton.colors.highlightedColor, Is.EqualTo(Color.Lerp(
                    DemoUiStyleCatalog.GetFrameTint(DemoUiStyleClass.PrimaryActionButton),
                    DemoUiStyleCatalog.GetInteractionTint(DemoUiStyleClass.PrimaryActionButton), 0.38f)));
                Assert.That(endTurnButton.colors.pressedColor, Is.Not.EqualTo(endTurnButton.colors.normalColor));
                Assert.That(root.GetComponentsInChildren<PrimaryActionButton>(true), Has.Length.EqualTo(3));
                var mulliganOverlay = root.transform.Find("DemoCanvas/MulliganOverlay");
                Assert.That(mulliganOverlay, Is.Not.Null);
                Assert.That(mulliganOverlay.gameObject.activeSelf, Is.False, "Opening-hand UI stays hidden in the offline sandbox.");
                Assert.That(mulliganOverlay.Find("MulliganPanel/ConfirmMulligan").GetComponent<PrimaryActionButton>(), Is.Not.Null);
                var choiceOverlay = root.transform.Find("DemoCanvas/ChoiceOverlay");
                Assert.That(choiceOverlay, Is.Not.Null);
                Assert.That(choiceOverlay.gameObject.activeSelf, Is.False, "Card choices stay hidden until an effect offers one.");
                Assert.That(choiceOverlay.Find("ChoicePanel/ConfirmChoice").GetComponent<PrimaryActionButton>(), Is.Not.Null);
                var factionButtons = root.GetComponentsInChildren<SecondaryButton>(true)
                    .Where(style => style.name.StartsWith("Faction_", System.StringComparison.Ordinal))
                    .ToArray();
                Assert.That(factionButtons, Has.Length.EqualTo(7));
                var neutralButtonColor = DemoUiStyleCatalog.GetRootFill(DemoUiStyleClass.SecondaryButton);
                foreach (var style in factionButtons)
                {
                    Assert.That(style.GetComponent<UnityEngine.UI.Image>().color, Is.EqualTo(neutralButtonColor));
                    Assert.That(style.transform.Find("FrameSlice").GetComponent<UnityEngine.UI.Image>().sprite.texture.name, Does.Contain("stone_bricks"));
                }
                Assert.That(root.transform.Find("DemoCanvas/FactionRail/Faction_plains_forest/SelectionAccent").gameObject.activeSelf, Is.True);
                Assert.That(root.transform.Find("DemoCanvas/FactionRail/Faction_desert_badlands/SelectionAccent").gameObject.activeSelf, Is.False);
                Assert.That(root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent").GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(272f, 701f)));
                var themedCard = root.GetComponentsInChildren<CardUI>(true)
                    .First(card => card.CardId == "pf_001").gameObject;
                Assert.That(themedCard, Is.Not.Null);
                var handCard = root.transform.Find("DemoCanvas/HandPlate/HandCards").GetComponentsInChildren<CardUI>(true)
                    .First(card => card.CardId == "pf_001");
                var detailCard = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent").GetComponentsInChildren<CardUI>(true)
                    .First(card => card.CardId == "pf_001");
                var detailsView = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent").GetComponent<CardDetailsView>();
                Assert.That(handCard, Is.Not.Null);
                Assert.That(detailCard, Is.Not.Null);
                Assert.That(handCard.CardId, Is.EqualTo(detailCard.CardId));
                Assert.That(handCard.IsCompact, Is.True);
                Assert.That(detailCard.IsCompact, Is.False);
                Assert.That(detailsView.CurrentCard, Is.SameAs(detailCard));
                Assert.That(detailCard.RectTransform.sizeDelta, Is.EqualTo(new Vector2(250f, 430f)));
                Assert.That(detailCard.RectTransform.anchoredPosition, Is.EqualTo(new Vector2(0f, 120f)));
                var inspectorHeader = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/Header").GetComponent<UnityEngine.UI.Text>();
                var headerRect = inspectorHeader.rectTransform;
                var cardRect = detailCard.RectTransform;
                Assert.That(headerRect.anchoredPosition.y - headerRect.sizeDelta.y * 0.5f,
                    Is.GreaterThan(cardRect.anchoredPosition.y + cardRect.sizeDelta.y * 0.5f),
                    "the larger detail card must remain visually separated from its header");
                var detailActionRect = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/DeployHint").GetComponent<RectTransform>();
                Assert.That(cardRect.anchoredPosition.y - cardRect.sizeDelta.y * 0.5f,
                    Is.GreaterThan(detailActionRect.anchoredPosition.y + detailActionRect.sizeDelta.y * 0.5f),
                    "the enlarged rules card must stay clear of its deployment instructions");
                Assert.That(themedCard.GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardFrame_plains_forest"));
                Assert.That(themedCard.transform.Find("MaterialFill"), Is.Null);
                Assert.That(themedCard.transform.Find("TitleBand"), Is.Null);
                Assert.That(themedCard.transform.Find("CostSocket"), Is.Null);
                Assert.That(themedCard.transform.Find("CostSocketFrame").GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardCostSocket_plains_forest"));
                Assert.That(themedCard.transform.Find("AttackSocketFrame").GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardAttackSocket_plains_forest"));
                Assert.That(themedCard.transform.Find("HealthSocketFrame").GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardHealthSocket_plains_forest"));
                var artSurface = themedCard.transform.Find("ArtSurface").GetComponent<UnityEngine.UI.Image>();
                Assert.That(artSurface.type, Is.EqualTo(UnityEngine.UI.Image.Type.Tiled));
                Assert.That(artSurface.sprite.name, Is.EqualTo("CardArtSurface_polished_blackstone_bricks"));
                var themedRules = themedCard.transform.Find("Rules").GetComponent<UnityEngine.UI.Text>();
                Assert.That(themedRules.alignment, Is.EqualTo(UnityEngine.TextAnchor.MiddleCenter));
                Assert.That(themedRules.alignByGeometry, Is.True);
                Assert.That(themedRules.resizeTextForBestFit, Is.True);
                var themedCanvasScale = themedRules.canvas.rootCanvas.scaleFactor;
                Assert.That(themedRules.resizeTextMinSize * themedCanvasScale, Is.GreaterThanOrEqualTo(12f));
                Assert.That(themedRules.resizeTextMinSize * themedCanvasScale, Is.LessThan(12.7f));
                Assert.That(themedRules.resizeTextMaxSize * themedCanvasScale, Is.GreaterThanOrEqualTo(15f));
                Assert.That(themedRules.resizeTextMaxSize * themedCanvasScale, Is.LessThan(15.7f));
                Assert.That(themedRules.rectTransform.sizeDelta.x,
                    Is.EqualTo(themedCard.GetComponent<RectTransform>().sizeDelta.x * 158f / 218f).Within(0.01f));
                Assert.That(themedRules.rectTransform.anchoredPosition.x, Is.Zero.Within(0.001f));
                Assert.That(themedRules.rectTransform.anchoredPosition.y,
                    Is.EqualTo(themedCard.GetComponent<RectTransform>().sizeDelta.y * (145f / 520f - 0.5f)).Within(0.01f));
                var themedArtRect = themedCard.transform.Find("ArtSurface").GetComponent<RectTransform>();
                var themedTypeRect = themedCard.transform.Find("Type").GetComponent<RectTransform>();
                Assert.That(themedRules.rectTransform.anchoredPosition.y + themedRules.rectTransform.sizeDelta.y * 0.5f,
                    Is.LessThan(themedArtRect.anchoredPosition.y - themedArtRect.sizeDelta.y * 0.5f),
                    "rules text must stay below the art surface");
                Assert.That(themedRules.rectTransform.anchoredPosition.y - themedRules.rectTransform.sizeDelta.y * 0.5f,
                    Is.GreaterThan(themedTypeRect.anchoredPosition.y + themedTypeRect.sizeDelta.y * 0.5f),
                    "rules text must not collide with the type label");
                var detailRules = detailCard.transform.Find("Rules").GetComponent<UnityEngine.UI.Text>();
                Assert.That(detailRules.alignment, Is.EqualTo(UnityEngine.TextAnchor.MiddleCenter));
                Assert.That(detailRules.resizeTextForBestFit, Is.True);
                Assert.That(detailRules.resizeTextMinSize, Is.EqualTo(12));
                var frameMappings = new[,]
                {
                    { "plains_forest", "pf" },
                    { "desert_badlands", "db" },
                    { "snow_ice", "si" },
                    { "cave_dark_forest", "cd" },
                    { "ocean_river", "or" },
                    { "nether", "nt" },
                    { "end", "ed" }
                };
                var decorLandmarks = new[,]
                {
                    { "plains_forest", "ForestTree_Player_0Trunk" },
                    { "desert_badlands", "Cactus_Player_0" },
                    { "snow_ice", "IceSpike_Player_0" },
                    { "cave_dark_forest", "Boulder_Player_0" },
                    { "ocean_river", "CoralStack_Player_0_0" },
                    { "nether", "BasaltPillar_Player_0" },
                    { "end", "ObsidianPillar_Player_0" }
                };
                var factionGroundTextures = new[,]
                {
                    { "plains_forest", "grass_block_top" },
                    { "desert_badlands", "red_sandstone" },
                    { "snow_ice", "snow_block" },
                    { "cave_dark_forest", "mossy_stone_bricks" },
                    { "ocean_river", "prismarine_bricks" },
                    { "nether", "netherrack" },
                    { "end", "purpur_block" }
                };
                for (var mapping = 0; mapping < frameMappings.GetLength(0); mapping++)
                {
                    var themeId = frameMappings[mapping, 0];
                    var prefix = frameMappings[mapping, 1];
                    GameObject.Find("Faction_" + themeId).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Assert.That(battlefield.PlayerFactionId, Is.EqualTo(themeId));
                    var theme = DemoBattlefieldThemeCatalog.Get(themeId);
                    Assert.That(battlefield.BoardCamera.backgroundColor,
                        Is.EqualTo(DemoBattlefieldThemeCatalog.GetSkyColor(themeId, battlefield.OpponentFactionId)),
                        "the 3D sky color follows both current battlefield themes");
                    Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name, Is.EqualTo(factionGroundTextures[mapping, 1]));
                    Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetColor("_Color"),
                        Is.EqualTo(theme.GroundColor), "deploy pads follow the faction ground tint");
                    var playerGround = root.transform.Find("BattlefieldGeometry/Ground_Player_0_-3").GetComponent<MeshRenderer>().sharedMaterial;
                    Assert.That(playerGround.mainTexture.name, Is.EqualTo(theme.PrimaryTextureKey),
                        "voxel terrain ground texture follows the player faction");
                    var playerFoundation = root.transform.Find("BattlefieldGeometry/PlayerFoundation").GetComponent<MeshRenderer>().sharedMaterial;
                    Assert.That(playerFoundation.mainTexture.name, Is.EqualTo(theme.FoundationTextureKey),
                        "voxel terrain foundation follows the player faction");
                    var opponentFoundation = root.transform.Find("BattlefieldGeometry/OpponentFoundation").GetComponent<MeshRenderer>().sharedMaterial;
                    Assert.That(opponentFoundation.mainTexture.name, Is.EqualTo(DemoBattlefieldThemeCatalog.Get(battlefield.OpponentFactionId).FoundationTextureKey),
                        "voxel terrain foundation follows the opponent faction");
                    var playerLight = root.transform.Find("PlayerEnvironmentLight").GetComponent<Light>();
                    Assert.That(playerLight.color, Is.EqualTo(theme.EnvironmentLight),
                        "player environment light follows the player faction");
                    var opponentLight = root.transform.Find("OpponentEnvironmentLight").GetComponent<Light>();
                    Assert.That(opponentLight.color, Is.EqualTo(DemoBattlefieldThemeCatalog.Get(battlefield.OpponentFactionId).EnvironmentLight),
                        "opponent environment light follows the opponent faction");
                    var decorRoot = root.transform.Find("BattlefieldDecor");
                    Assert.That(decorRoot, Is.Not.Null);
                    Assert.That(decorRoot.childCount, Is.GreaterThanOrEqualTo(6), themeId);
                    Assert.That(decorRoot.Find("DecorLamp_Player_L"), Is.Not.Null, themeId);
                    Assert.That(decorRoot.Find("DecorLamp_Opponent_R"), Is.Not.Null, themeId);
                    Assert.That(decorRoot.Find(decorLandmarks[mapping, 1]), Is.Not.Null,
                        $"{themeId} decorations rebuild with the selected factions");
                    var mappedCardId = prefix + "_001";
                    var card = root.GetComponentsInChildren<CardUI>(true)
                        .FirstOrDefault(view => view.CardId == mappedCardId)?.gameObject;
                    Assert.That(card, Is.Not.Null, themeId);
                    Assert.That(card.GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardFrame_" + themeId));
                    Assert.That(card.transform.Find("CostSocketFrame").GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardCostSocket_" + themeId));
                    Assert.That(registry.TryGetDefinition(mappedCardId, out var mappedDefinition), Is.True);
                    if (mappedDefinition.hasAttack)
                        Assert.That(card.transform.Find("AttackSocketFrame").GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardAttackSocket_" + themeId));
                    else
                        Assert.That(card.transform.Find("AttackSocketFrame"), Is.Null);
                    if (mappedDefinition.hasHealth)
                        Assert.That(card.transform.Find("HealthSocketFrame").GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardHealthSocket_" + themeId));
                    else
                        Assert.That(card.transform.Find("HealthSocketFrame"), Is.Null);
                    if (mappedDefinition.hasDurability)
                        Assert.That(card.transform.Find("DurabilitySocketFrame").GetComponent<UnityEngine.UI.Image>().sprite.name, Is.EqualTo("CardHealthSocket_" + themeId));
                }

                var nextOpponent = root.transform.Find("DemoCanvas/OpponentFactionSelector/NextOpponentFaction").GetComponent<UnityEngine.UI.Button>();
                var skyBeforeOpponentChange = battlefield.BoardCamera.backgroundColor;
                var letterboxSkyBeforeOpponentChange = battlefield.LetterboxCamera.backgroundColor;
                nextOpponent.onClick.Invoke();
                Assert.That(battlefield.OpponentFactionId, Is.EqualTo("end"));
                Assert.That(battlefield.BoardCamera.backgroundColor,
                    Is.EqualTo(DemoBattlefieldThemeCatalog.GetSkyColor(battlefield.PlayerFactionId, "end")));
                Assert.That(battlefield.BoardCamera.backgroundColor, Is.Not.EqualTo(skyBeforeOpponentChange),
                    "changing only the opponent faction also refreshes the shared world sky");
                Assert.That(battlefield.LetterboxCamera.backgroundColor,
                    Is.EqualTo(battlefield.BoardCamera.backgroundColor),
                    "letterbox bars stay continuous with the shared sky when the opponent faction changes");
                Assert.That(battlefield.LetterboxCamera.backgroundColor, Is.Not.EqualTo(letterboxSkyBeforeOpponentChange));
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name, Is.EqualTo("purpur_block"));
                Assert.That(root.transform.Find("DemoCanvas/OpponentFactionSelector/FactionLabel").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("敌方 · 末地"));
                Assert.That(root.transform.Find("DemoCanvas/OpponentHUD/Name").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("虚空行者"));

                GameObject.Find("Faction_nether").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(root.transform.Find("DemoCanvas/PlayerHUD/Name").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("熔岩统御者"));
                FindHandCard(root, "nt_006").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                var implementedCast = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/Cast").GetComponent<UnityEngine.UI.Button>();
                Assert.That(implementedCast.interactable, Is.True);
                Assert.That(implementedCast.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("释放卡牌"));
                Assert.That(root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/Implementation"), Is.Null,
                    "internal effect status belongs in explicit diagnostic mode, not the player inspector");

                GameObject.Find("Faction_snow_ice").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                FindHandCard(root, "si_001").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                var targetCast = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/Cast").GetComponent<UnityEngine.UI.Button>();
                Assert.That(targetCast.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("选择敌方目标"));
                targetCast.onClick.Invoke();
                Assert.That(root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/Cast").GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("取消目标选择"));
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.GreaterThan(0f));

                var playerUnit = battlefield.GetSlotReferencePosition(true, DemoSlotKind.Unit, 0);
                var opponentUnit = battlefield.GetSlotReferencePosition(false, DemoSlotKind.Unit, 0);
                Assert.That(playerUnit.y, Is.LessThan(opponentUnit.y));
                Assert.That(playerUnit, Is.Not.EqualTo(opponentUnit));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("pf_001", out var beeTexture), Is.True);
                Assert.That(beeTexture, Is.EqualTo("entity_bee"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("nt_003", out var blazeTexture), Is.True);
                Assert.That(blazeTexture, Is.EqualTo("entity_blaze"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("si_003", out var strayTexture), Is.True);
                Assert.That(strayTexture, Is.EqualTo("entity_stray"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("tk_014", out var smallMagmaTexture), Is.True);
                Assert.That(smallMagmaTexture, Is.EqualTo("entity_magma_cube"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("tk_003", out var juvenileTexture), Is.True);
                Assert.That(juvenileTexture, Is.EqualTo("entity_sheep_baby"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("tk_004", out var companionTexture), Is.True);
                Assert.That(companionTexture, Is.EqualTo("entity_wolf"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("pf_008", out var golemTexture), Is.True);
                Assert.That(golemTexture, Is.EqualTo("entity_iron_golem"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("cd_005", out var vindicatorTexture), Is.True);
                Assert.That(vindicatorTexture, Is.EqualTo("entity_vindicator"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("tk_011", out var recruitTexture), Is.True);
                Assert.That(recruitTexture, Is.EqualTo("entity_vindicator"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("cd_001", out var batTexture), Is.True);
                Assert.That(batTexture, Is.EqualTo("entity_bat"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("cd_002", out var caveSpiderTexture), Is.True);
                Assert.That(caveSpiderTexture, Is.EqualTo("entity_cave_spider"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("si_002", out var snowGolemTexture), Is.True);
                Assert.That(snowGolemTexture, Is.EqualTo("entity_snow_golem"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("si_004", out var goatTexture), Is.True);
                Assert.That(goatTexture, Is.EqualTo("entity_goat"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("or_001", out var salmonTexture), Is.True);
                Assert.That(salmonTexture, Is.EqualTo("entity_salmon"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("or_002", out var dolphinTexture), Is.True);
                Assert.That(dolphinTexture, Is.EqualTo("entity_dolphin"));
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("or_003", out var drownedTexture), Is.True);
                Assert.That(drownedTexture, Is.EqualTo("entity_drowned"));

                var piecesRoot = root.transform.Find("BattlefieldPieces");
                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-render-1", CardId = "pf_005", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 0, OccupiedSlots = 1, Health = 4, MaxHealth = 4
                    },
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-render-2", CardId = "pf_005", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 1, OccupiedSlots = 1, Health = 4, MaxHealth = 4
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                Assert.That(piecesRoot.childCount, Is.EqualTo(2), "adjacent copies of one building card remain separate stable objects");
                Assert.That(piecesRoot.Find("Piece_object-render-1_pf_005"), Is.Not.Null);
                Assert.That(piecesRoot.Find("Piece_object-render-2_pf_005"), Is.Not.Null);

                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-render-3", CardId = "db_007", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 0, OccupiedSlots = 2, Health = 8, MaxHealth = 8
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                Assert.That(piecesRoot.childCount, Is.EqualTo(1), "one multi-slot structure produces one world object");
                var structurePiece = piecesRoot.Find("Piece_object-render-3_db_007");
                var expectedStructureCenter = (battlefield.GetSlotWorldPosition(true, DemoSlotKind.Building, 0) +
                                               battlefield.GetSlotWorldPosition(true, DemoSlotKind.Building, 1)) * 0.5f;
                Assert.That(structurePiece, Is.Not.Null);
                Assert.That(structurePiece.localPosition.x, Is.EqualTo(expectedStructureCenter.x).Within(0.001f));
                Assert.That(structurePiece.Find("TempleFoundation"), Is.Not.Null);
                Assert.That(structurePiece.Find("TempleLeftTower"), Is.Not.Null);
                Assert.That(structurePiece.Find("TempleRightTower"), Is.Not.Null);
                Assert.That(structurePiece.Find("TempleCentralShrine"), Is.Not.Null);
                Assert.That(structurePiece.Find("TempleEntrance"), Is.Not.Null);
                Assert.That(structurePiece.Find("TempleFoundation").GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name,
                    Is.EqualTo("cut_sandstone"));

                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-render-4", CardId = "db_004", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 0, OccupiedSlots = 1, Health = 5, MaxHealth = 5
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                var cactusPiece = piecesRoot.Find("Piece_object-render-4_db_004");
                Assert.That(cactusPiece, Is.Not.Null);
                Assert.That(cactusPiece.Find("CactusPost_0"), Is.Not.Null);
                Assert.That(cactusPiece.Find("CactusPost_1"), Is.Not.Null);
                Assert.That(cactusPiece.Find("CactusPost_2"), Is.Not.Null);
                Assert.That(cactusPiece.Find("CactusFenceFoundation"), Is.Not.Null);

                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-render-5", CardId = "cd_004", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 0, OccupiedSlots = 1, Health = 4, MaxHealth = 4
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                var sensorPiece = piecesRoot.Find("Piece_object-render-5_cd_004");
                Assert.That(sensorPiece, Is.Not.Null);
                Assert.That(sensorPiece.Find("SensorBody"), Is.Not.Null);
                Assert.That(sensorPiece.Find("SensorTop"), Is.Not.Null);
                Assert.That(sensorPiece.Find("FrontLeftTendril/Tip"), Is.Not.Null);
                Assert.That(sensorPiece.Find("SensorTop").GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name,
                    Is.EqualTo("sculk_sensor_top"));

                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-render-6", CardId = "cd_007", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 0, OccupiedSlots = 2, Health = 7, MaxHealth = 7
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                var minePiece = piecesRoot.Find("Piece_object-render-6_cd_007");
                Assert.That(minePiece, Is.Not.Null);
                Assert.That(minePiece.Find("MineEntrance"), Is.Not.Null);
                Assert.That(minePiece.Find("MineLeftSupport"), Is.Not.Null);
                Assert.That(minePiece.Find("MineCart"), Is.Not.Null);
                Assert.That(minePiece.Find("MineFoundation").GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name,
                    Is.EqualTo("cobblestone"));

                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-render-7", CardId = "cd_008", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 0, OccupiedSlots = 3, Health = 11, MaxHealth = 11
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                var mansionPiece = piecesRoot.Find("Piece_object-render-7_cd_008");
                Assert.That(mansionPiece, Is.Not.Null);
                Assert.That(mansionPiece.Find("MansionCentralHall"), Is.Not.Null);
                Assert.That(mansionPiece.Find("MansionLeftWing"), Is.Not.Null);
                Assert.That(mansionPiece.Find("MansionEntrance"), Is.Not.Null);
                Assert.That(mansionPiece.Find("MansionCentralHall").GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name,
                    Is.EqualTo("dark_oak_planks"));

                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-render-8", CardId = "si_008", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 0, OccupiedSlots = 2, Health = 10, MaxHealth = 10
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                var iceSpirePiece = piecesRoot.Find("Piece_object-render-8_si_008");
                Assert.That(iceSpirePiece, Is.Not.Null);
                Assert.That(iceSpirePiece.Find("IceSpireFoundation"), Is.Not.Null);
                Assert.That(iceSpirePiece.Find("IceSpireCenter"), Is.Not.Null);
                Assert.That(iceSpirePiece.Find("IceSpireCenterTip"), Is.Not.Null);
                Assert.That(iceSpirePiece.Find("IceSpireCenter").GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name,
                    Is.EqualTo("packed_ice"));

                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-render-hut", CardId = "si_007", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 0, OccupiedSlots = 1, Health = 5, MaxHealth = 5
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                var snowHutPiece = piecesRoot.Find("Piece_object-render-hut_si_007");
                Assert.That(snowHutPiece, Is.Not.Null);
                Assert.That(snowHutPiece.Find("SnowHutEntrance"), Is.Not.Null);
                Assert.That(snowHutPiece.Find("SnowHutCrown"), Is.Not.Null);
                Assert.That(snowHutPiece.Find("SnowHutWarmCore"), Is.Not.Null);
                Assert.That(snowHutPiece.Find("SnowHutLower").GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name,
                    Is.EqualTo("snow_block"));

                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-render-crystal", CardId = "ed_007", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 0, OccupiedSlots = 1, Health = 4, MaxHealth = 4
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                var endCrystalPiece = piecesRoot.Find("Piece_object-render-crystal_ed_007");
                Assert.That(endCrystalPiece, Is.Not.Null);
                Assert.That(endCrystalPiece.Find("EndCrystalFoundation"), Is.Not.Null);
                Assert.That(endCrystalPiece.Find("EndCrystalFloatingAssembly/EndCrystalCore"), Is.Not.Null);
                Assert.That(endCrystalPiece.Find("EndCrystalFloatingAssembly/EndCrystalWireCage/EndCrystalCageX_0"), Is.Not.Null);
                Assert.That(endCrystalPiece.Find("EndCrystalFoundation").GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name,
                    Is.EqualTo("obsidian"));

                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-render-anchor", CardId = "nt_007", Player = true,
                        SlotKind = DemoSlotKind.Building, SlotIndex = 0, OccupiedSlots = 1, Health = 5, MaxHealth = 5
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                var anchorPiece = piecesRoot.Find("Piece_object-render-anchor_nt_007");
                Assert.That(anchorPiece, Is.Not.Null);
                Assert.That(anchorPiece.Find("RespawnAnchorBody"), Is.Not.Null);
                Assert.That(anchorPiece.Find("RespawnAnchorTop"), Is.Not.Null);
                Assert.That(anchorPiece.Find("RespawnAnchorCore"), Is.Not.Null);
                Assert.That(anchorPiece.Find("RespawnAnchorTop").GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name,
                    Is.EqualTo("respawn_anchor_top"));

                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-burning-blaze", CardId = "nt_003", Player = true,
                        SlotKind = DemoSlotKind.Unit, SlotIndex = 1, OccupiedSlots = 1, Attack = 3, Health = 3, MaxHealth = 3,
                        Statuses = new[]
                        {
                            new BattlefieldStatusStateDto
                            {
                                statusId = "FIRE", remainingDuration = 2, sourcePlayerId = "opponent",
                                sourceCardId = "tk_013", sourceInstanceId = "effect-1", effectId = "effect.tk_013.01"
                            }
                        }
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                var burningPiece = piecesRoot.Find("Piece_object-burning-blaze_nt_003");
                Assert.That(burningPiece, Is.Not.Null);
                Assert.That(burningPiece.Find("FireStatusFx/FlameL"), Is.Not.Null);
                Assert.That(burningPiece.Find("FireStatusFx/FlameHigh"), Is.Not.Null);

                battlefield.SyncPieces(new[]
                {
                    new DemoBattlefieldObject
                    {
                        InstanceId = "object-render-9", CardId = "si_004", Player = true,
                        SlotKind = DemoSlotKind.Unit, SlotIndex = 1, OccupiedSlots = 1, Attack = 3, Health = 2, MaxHealth = 2
                    }
                }, System.Array.Empty<DemoBattlefieldObject>(), registry);
                var goatPiece = piecesRoot.Find("Piece_object-render-9_si_004");
                Assert.That(goatPiece, Is.Not.Null);
                var goatModel = goatPiece.Find("Model");
                if (goatModel != null)
                {
                    var goatBones = goatModel.GetComponentsInChildren<Transform>(true).Select(transform => transform.name).ToHashSet();
                    Assert.That(goatBones, Does.Contain("Bone_body"), "goat geometry bones build under the model root");
                    Assert.That(goatBones, Does.Contain("Bone_left_horn"));
                    Assert.That(goatPiece.GetComponentInChildren<MeshRenderer>().sharedMaterial.mainTexture.name,
                        Is.EqualTo("entity_goat"));
                }
                else
                {
                    Assert.That(goatPiece.Find("Body"), Is.Not.Null, "goat falls back to the generic block creature when geometry is not extracted");
                }

                var buildingMarker1 = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Building_1/InteractiveGround");
                var buildingMarker2 = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Building_2/InteractiveGround");
                battlefield.SetSlotState(true, DemoSlotKind.Building, 0, true, false);
                battlefield.SetSlotState(true, DemoSlotKind.Building, 1, true, false);
                battlefield.SetSlotRangeHovered(true, DemoSlotKind.Building, 0, 2, true, false);
                Assert.That(buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.EqualTo(0.78f).Within(0.001f));
                Assert.That(buildingMarker1.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.EqualTo(0.78f).Within(0.001f));
                battlefield.SetSlotRangeHovered(true, DemoSlotKind.Building, 0, 2, false, false);
                battlefield.SetSlotRangeHovered(true, DemoSlotKind.Building, 2, 2, true, true);
                Assert.That(buildingMarker2.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.GreaterThanOrEqualTo(0.84f));
                battlefield.SetSlotRangeHovered(true, DemoSlotKind.Building, 2, 2, false, true);

                battlefield.SetSlotState(true, DemoSlotKind.Unit, 0, true, false);
                battlefield.SetSlotHovered(true, DemoSlotKind.Unit, 0, true);
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.EqualTo(0.78f).Within(0.001f));
                Assert.That(unitRiser.GetComponent<MeshRenderer>().enabled, Is.True);
                battlefield.SetSlotPressed(true, DemoSlotKind.Unit, 0, true);
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.EqualTo(0.92f).Within(0.001f));
                Assert.That(unitRiser.GetComponent<MeshRenderer>().enabled, Is.False);
                battlefield.SetSlotPressed(true, DemoSlotKind.Unit, 0, false);
                battlefield.SetSlotState(true, DemoSlotKind.Unit, 0, false, true);
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.Zero);
                Assert.That(unitRiser.GetComponent<MeshRenderer>().enabled, Is.False);
                battlefield.SetSlotState(false, DemoSlotKind.Unit, 0, true, true);
                battlefield.SetSlotHovered(false, DemoSlotKind.Unit, 0, true);
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.EqualTo(0.78f).Within(0.001f));
                endTurn.GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(endTurn.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("结束回合"));
                Assert.That(root.transform.Find("DemoCanvas/RoundPlate/Round").GetComponent<UnityEngine.UI.Text>().text, Does.Contain("战斗"));
                Assert.That(root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/CombatHint"), Is.Not.Null);
                Assert.That(root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/Header").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("战斗指令"));
                var combatHand = root.transform.Find("DemoCanvas/HandPlate/HandCards").GetComponent<CanvasGroup>();
                Assert.That(combatHand.alpha, Is.EqualTo(1f).Within(0.001f));
                Assert.That(combatHand.interactable, Is.False);
                Assert.That(combatHand.blocksRaycasts, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PolarBearWoolPreviewConsumesOneExactInstanceAndRendersTheThreeLineModifierNameplate()
        {
            var root = new GameObject("PolarBearWoolPreviewTest");
            try
            {
                var configuredBattlefield = root.AddComponent<DemoBattlefield3D>();
                configuredBattlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                var setupPreview = typeof(DemoSceneController).GetMethod(
                    "SetupPolarBearWoolPreview", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(setupPreview, Is.Not.Null);
                setupPreview.Invoke(controller, null);

                var matchField = typeof(DemoSceneController).GetField("_match", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(matchField, Is.Not.Null);
                var match = (DemoLocalMatch)matchField.GetValue(controller);
                var bear = match.GetObject(true, DemoSlotKind.Unit, 1);
                Assert.That(bear, Is.Not.Null);
                Assert.That(bear.Attack, Is.EqualTo(4));
                Assert.That(bear.MaxHealth, Is.EqualTo(7));
                Assert.That(bear.TemporaryHealthModifier, Is.EqualTo(1));
                Assert.That(bear.HasKeyword("TAUNT"), Is.True);
                Assert.That(match.HandCards.Count(value => value.cardId == "tk_001"), Is.EqualTo(1),
                    "one of the duplicate Wool instances should remain after the selected copy is cast");
                var remainingWool = match.HandCards.Single(value => value.cardId == "tk_001");
                var selectedHandInstanceField = typeof(DemoSceneController).GetField(
                    "_selectedHandCardInstanceId", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(selectedHandInstanceField, Is.Not.Null);
                Assert.That(selectedHandInstanceField.GetValue(controller), Is.EqualTo(remainingWool.handCardInstanceId),
                    "the preview should select the remaining duplicate instead of retaining the consumed instance");
                Assert.That(root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/StaleHandSelection"), Is.Null,
                    "the Inspector must not display a stale-instance warning when a matching copy remains");

                var content = root.transform.Find("DemoCanvas/PlayerUnitSlot1/Content");
                Assert.That(content, Is.Not.Null);
                var label = content.GetComponentsInChildren<Text>(true)
                    .Single(value => value.name == "WorldLabelText");
                Assert.That(label.text.Split('\n').Length, Is.EqualTo(3));
                Assert.That(label.resizeTextForBestFit, Is.True);
                Assert.That(label.resizeTextMinSize, Is.EqualTo(12));
                Assert.That(label.rectTransform.sizeDelta.y, Is.EqualTo(54f));
                var plate = content.Cast<Transform>().Single(value => value.name == "WorldLabel").GetComponent<Image>();
                Assert.That(plate.rectTransform.sizeDelta.y, Is.EqualTo(64f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void EndReturnInteractionPreviewUsesScenarioEnergyWithoutChangingDefaultOpeningEnergy()
        {
            var root = new GameObject("EndReturnInteractionPreviewTest");
            try
            {
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();

                var registry = CardContentLoader.Load();
                Assert.That(registry.TryGetDefinition("ed_003", out var endermanDefinition), Is.True);
                Assert.That(registry.TryGetDefinition("ed_002", out var chorusFruitDefinition), Is.True);
                var matchField = typeof(DemoSceneController).GetField("_match", BindingFlags.Instance | BindingFlags.NonPublic);
                var configurePreview = typeof(DemoSceneController).GetMethod(
                    "ConfigureEndReturnInteractionPreview", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(matchField, Is.Not.Null);
                Assert.That(configurePreview, Is.Not.Null);
                var match = (DemoLocalMatch)matchField.GetValue(controller);
                Assert.That(match.Energy, Is.EqualTo(1), "production offline matches must retain the 1/1 opening rule");
                Assert.That(match.MaxEnergy, Is.EqualTo(1));

                configurePreview.Invoke(controller, new object[] { endermanDefinition, chorusFruitDefinition });
                Assert.That(match.MaxEnergy, Is.EqualTo(4), "only this demonstration fixture may override the live opening economy");
                Assert.That(match.Energy, Is.EqualTo(4));
                Assert.That(DemoDeploymentRules.Evaluate(match, endermanDefinition, DemoSlotKind.Unit, 1).IsLegal, Is.True);
                var enderman = match.HandCards.Single(value => value.cardId == "ed_003");
                var deploy = match.ApplyDeploy(endermanDefinition,
                    match.CreateDeployCommand("ed_003", DemoSlotKind.Unit, 1, handCardInstanceId: enderman.handCardInstanceId));
                Assert.That(deploy.Accepted, Is.True, deploy.Message);
                Assert.That(match.Energy, Is.EqualTo(1), "the fixture should pay 3 for ED-003");
                var deployedEnderman = match.GetObject(true, DemoSlotKind.Unit, 1);
                Assert.That(deployedEnderman, Is.Not.Null);
                var play = match.ApplyPlayCard(chorusFruitDefinition,
                    match.CreatePlayCardCommand("ed_002", "UNIT", deployedEnderman.InstanceId));
                Assert.That(play.Accepted, Is.True, play.Message);

                var returnedEnderman = match.HandCards.Single(value => value.cardId == "ed_003");
                Assert.That(match.Energy, Is.EqualTo(0), "the fixture should then pay 1 for ED-002");
                Assert.That(returnedEnderman.costModifier, Is.EqualTo(-1));
                Assert.That(match.GetEffectiveCost(endermanDefinition, returnedEnderman.handCardInstanceId), Is.EqualTo(2));
                Assert.That(match.GetObject(true, DemoSlotKind.Unit, 1), Is.Null,
                    "the real preview interaction must clear the deployed unit's slot after return");
                Assert.That(match.DiscardPile.Contains("ed_002"), Is.True);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void DarknessPreviewUsesTwoExactSandInstancesBeforeSelectingSnowballTarget()
        {
            var root = new GameObject("DarknessPreviewTest");
            try
            {
                var configuredBattlefield = root.AddComponent<DemoBattlefield3D>();
                configuredBattlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                var setupPreview = typeof(DemoSceneController).GetMethod(
                    "SetupDarknessPreview", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(setupPreview, Is.Not.Null);
                setupPreview.Invoke(controller, null);

                var matchField = typeof(DemoSceneController).GetField("_match", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(matchField, Is.Not.Null);
                var match = (DemoLocalMatch)matchField.GetValue(controller);
                Assert.That(match.HasPlayerStatus(true, "DARK"), Is.True,
                    "playing the second unique Suspicious Sand instance should trigger the Sculk Sensor");
                var snowball = match.HandCards.Single(value => value.cardId == "si_001");
                var selectedHandInstanceField = typeof(DemoSceneController).GetField(
                    "_selectedHandCardInstanceId", BindingFlags.Instance | BindingFlags.NonPublic);
                var pendingTargetField = typeof(DemoSceneController).GetField(
                    "_pendingTargetCardId", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(selectedHandInstanceField, Is.Not.Null);
                Assert.That(selectedHandInstanceField.GetValue(controller), Is.EqualTo(snowball.handCardInstanceId));
                Assert.That(pendingTargetField, Is.Not.Null);
                Assert.That(pendingTargetField.GetValue(controller), Is.EqualTo("si_001"),
                    "Snowball should remain selected in target mode so the UI can demonstrate the Darkness edge restriction");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void PresentationPulseTintsTheGroundSurfaceWithoutPretendingTheSlotWasPressed()
        {
            var root = new GameObject("GroundPresentationPulseTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                battlefield.BuildNow();
                battlefield.SetSlotState(true, DemoSlotKind.Unit, 0, false, false);

                var marker = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Unit_0/InteractiveGround");
                var riser = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Unit_0/GroundRiser");
                var material = marker.GetComponent<MeshRenderer>().sharedMaterial;
                battlefield.PulseSlotRange(true, DemoSlotKind.Unit, 0, 1, new Color(1f, 0.89f, 0.48f, 1f));

                var highlight = material.GetColor("_HighlightColor");
                Assert.That(material.GetFloat("_HighlightStrength"), Is.GreaterThan(0.5f));
                Assert.That(highlight.r, Is.GreaterThan(highlight.g));
                Assert.That(highlight.g, Is.GreaterThan(highlight.b));
                Assert.That(riser.GetComponent<MeshRenderer>().enabled, Is.False,
                    "an event pulse must stay on the textured ground and must not masquerade as hover lift");

                var markers = (IDictionary)typeof(DemoBattlefield3D)
                    .GetField("_slotMarkers", BindingFlags.Instance | BindingFlags.NonPublic)
                    .GetValue(battlefield);
                var slotKey = typeof(DemoBattlefield3D).GetMethod(
                    "SlotKey", BindingFlags.Static | BindingFlags.NonPublic)
                    .Invoke(null, new object[] { true, DemoSlotKind.Unit, 0 });
                var markerState = markers[slotKey];
                var markerStateType = markerState.GetType();
                var markerRoot = (Transform)markerStateType
                    .GetField("Root", BindingFlags.Instance | BindingFlags.Public)
                    .GetValue(markerState);
                var initialPosition = markerRoot.localPosition;
                var initialScale = markerRoot.localScale;
                var pulseStart = (float)markerStateType
                    .GetField("PresentationPulseStartedAt", BindingFlags.Instance | BindingFlags.Public)
                    .GetValue(markerState);
                var pulseDuration = (float)markerStateType
                    .GetField("PresentationPulseDuration", BindingFlags.Instance | BindingFlags.Public)
                    .GetValue(markerState);
                Assert.That(pulseDuration, Is.EqualTo(0.95f).Within(0.001f),
                    "ground event feedback must remain readable through its short presentation window");
                var updateMarker = typeof(DemoBattlefield3D).GetMethod(
                    "UpdateSlotMarker", BindingFlags.Static | BindingFlags.NonPublic);
                updateMarker.Invoke(null, new object[] { markerState, pulseStart + 0.15f, 0.05f });

                Assert.That(markerRoot.localPosition.y, Is.GreaterThan(initialPosition.y));
                Assert.That(markerRoot.localScale.x, Is.GreaterThan(initialScale.x));
                Assert.That((bool)markerStateType
                    .GetField("Pressed", BindingFlags.Instance | BindingFlags.Public)
                    .GetValue(markerState), Is.False);
                updateMarker.Invoke(null, new object[] { markerState, pulseStart + pulseDuration + 0.1f, 1f });
                Assert.That(markerRoot.localPosition.y, Is.EqualTo(initialPosition.y).Within(0.001f));
                Assert.That(markerRoot.localScale.x, Is.EqualTo(initialScale.x).Within(0.001f));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase(2, "-1")]
        [TestCase(1, "-2")]
        [TestCase(0, "-3")]
        public void CardUiShowsTheFullEffectiveDiscountWithoutChangingBaseCost(int effectiveCost, string expectedDiscount)
        {
            var root = new GameObject("DiscountedCardTest", typeof(RectTransform), typeof(UnityEngine.UI.Image), typeof(CardUI));
            try
            {
                var card = root.GetComponent<CardUI>();
                card.Bind(CardContentLoader.Load(), "db_005", new Vector2(166, 216), true, null, null, effectiveCost);

                Assert.That(card.BaseCost, Is.EqualTo(3));
                Assert.That(card.DisplayedCost, Is.EqualTo(effectiveCost));
                Assert.That(root.transform.Find("Cost").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo(effectiveCost.ToString()));
                Assert.That(root.transform.Find("CostModifierBadge").GetComponent<UnityEngine.UI.Image>().type, Is.EqualTo(UnityEngine.UI.Image.Type.Tiled));
                Assert.That(root.transform.Find("CostModifier").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo(expectedDiscount));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void UnaffordableHandCardIsDimmedButRemainsSelectableAndRaycastable()
        {
            var root = new GameObject("UnaffordableCardTest", typeof(RectTransform), typeof(Image), typeof(CardUI), typeof(Button));
            try
            {
                var card = root.GetComponent<CardUI>();
                card.Bind(CardContentLoader.Load(), "db_005", new Vector2(166, 216), true, null, () => { });

                card.SetResourceAffordable(false);

                var canvasGroup = root.GetComponent<CanvasGroup>();
                Assert.That(card.IsResourceAffordable, Is.False);
                Assert.That(canvasGroup.alpha, Is.EqualTo(0.8f).Within(0.001f),
                    "Affordability should be apparent without washing out card art or rules text.");
                Assert.That(canvasGroup.interactable, Is.True,
                    "dimmed cards must remain interactive so the player can inspect their details");
                Assert.That(canvasGroup.blocksRaycasts, Is.True,
                    "dimmed cards must remain selectable and draggable");
                Assert.That(root.GetComponent<Button>().interactable, Is.True);

                card.SetResourceAffordable(true);
                Assert.That(card.IsResourceAffordable, Is.True);
                Assert.That(canvasGroup.alpha, Is.EqualTo(1f).Within(0.001f));

                card.SetResourceAffordable(false);
                card.Bind(CardContentLoader.Load(), "db_005", new Vector2(166, 216), true, null, () => { });
                Assert.That(root.GetComponent<CanvasGroup>().alpha, Is.EqualTo(1f).Within(0.001f),
                    "reusing a card for another view must clear stale affordability dimming");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void RegisteredCardTitlesAvoidCostSocketsAndFitCompactAndDetailFrames()
        {
            var registry = CardContentLoader.Load();
            var definitionAsset = Resources.Load<TextAsset>("CardContent/card-definition-registry.v1");
            Assert.That(definitionAsset, Is.Not.Null);
            var definitions = JsonUtility.FromJson<CardDefinitionRegistryDocument>(definitionAsset.text);
            Assert.That(definitions?.entries, Is.Not.Null);
            Assert.That(definitions.entries, Has.Length.EqualTo(74));

            var font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 20);
            var compactRoot = new GameObject("CompactCardNameAudit", typeof(RectTransform), typeof(Image), typeof(CardUI));
            var detailRoot = new GameObject("DetailCardNameAudit", typeof(RectTransform), typeof(Image), typeof(CardUI));
            var canvasRoot = new GameObject("1280x720CardNameCanvas", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.scaleFactor = 2f / 3f;
            compactRoot.transform.SetParent(canvasRoot.transform, false);
            detailRoot.transform.SetParent(canvasRoot.transform, false);
            try
            {
                foreach (var definition in definitions.entries)
                {
                    Assert.That(registry.TryGetText(definition.id, out var cardText), Is.True);
                    AssertTitleFits(compactRoot, registry, definition.id, new Vector2(166f, 216f), true, font, cardText.name);
                    AssertTitleFits(detailRoot, registry, definition.id, new Vector2(250f, 430f), false, font, cardText.name);
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(compactRoot);
                UnityEngine.Object.DestroyImmediate(detailRoot);
                UnityEngine.Object.DestroyImmediate(canvasRoot);
                if (font != null) UnityEngine.Object.DestroyImmediate(font);
            }
        }

        private static void AssertTitleFits(GameObject root, CardContentRegistry registry, string cardId,
            Vector2 cardSize, bool compact, Font font, string expectedName)
        {
            root.GetComponent<CardUI>().Bind(registry, cardId, cardSize, compact, font, null);
            var title = root.transform.Find("Name").GetComponent<Text>();
            var titleRect = title.rectTransform;
            var socketRect = root.transform.Find("CostSocketFrame").GetComponent<RectTransform>();
            var titleMinX = titleRect.anchoredPosition.x + titleRect.rect.xMin;
            var socketMaxX = socketRect.anchoredPosition.x + socketRect.rect.xMax;
            Assert.That(titleMinX, Is.GreaterThanOrEqualTo(socketMaxX + 4f),
                $"{cardId} title must have a visible gap after the cost socket in {(compact ? "hand" : "detail")} view");
            Assert.That(title.text, Is.EqualTo(expectedName), $"{cardId} must retain its complete registered name");

            var settings = title.GetGenerationSettings(titleRect.rect.size);
            settings.resizeTextForBestFit = false;
            settings.fontSize = title.resizeTextMinSize;
            settings.horizontalOverflow = HorizontalWrapMode.Wrap;
            settings.verticalOverflow = VerticalWrapMode.Overflow;
            Assert.That(new TextGenerator().GetPreferredHeight(title.text, settings),
                Is.LessThanOrEqualTo(titleRect.rect.height + 0.5f),
                $"{cardId} full registered title must fit without vertical truncation in {(compact ? "hand" : "detail")} view");
        }

        [Test]
        public void EveryRegisteredDetailCardRuleFitsAt1280ReadableMinimum()
        {
            var registry = CardContentLoader.Load();
            var definitionAsset = Resources.Load<TextAsset>("CardContent/card-definition-registry.v1");
            Assert.That(definitionAsset, Is.Not.Null);
            var definitions = JsonUtility.FromJson<CardDefinitionRegistryDocument>(definitionAsset.text);
            Assert.That(definitions?.entries, Is.Not.Null);
            Assert.That(definitions.entries, Has.Length.EqualTo(74));

            var font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 20);
            var cardRoot = new GameObject("DetailCardRulesAudit", typeof(RectTransform), typeof(Image), typeof(CardUI));
            var canvasRoot = new GameObject("1280x720CardCanvas", typeof(RectTransform), typeof(Canvas));
            var canvas = canvasRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.scaleFactor = 2f / 3f;
            cardRoot.transform.SetParent(canvasRoot.transform, false);
            try
            {
                var card = cardRoot.GetComponent<CardUI>();
                foreach (var definition in definitions.entries)
                {
                    card.Bind(registry, definition.id, new Vector2(250, 430), false, font, null);
                    var rules = cardRoot.transform.Find("Rules").GetComponent<Text>();
                    Assert.That(rules.resizeTextMinSize * canvas.scaleFactor, Is.GreaterThanOrEqualTo(12f),
                        $"{definition.id} must retain the detail card's readable screen-space minimum");
                    Assert.That(rules.resizeTextMinSize * canvas.scaleFactor, Is.LessThan(13f),
                        $"{definition.id} must not overshoot the detail card's intended 12px minimum by a full pixel");

                    var bounds = rules.rectTransform.rect;
                    var settings = rules.GetGenerationSettings(bounds.size);
                    settings.resizeTextForBestFit = false;
                    settings.fontSize = rules.resizeTextMinSize;
                    settings.verticalOverflow = VerticalWrapMode.Overflow;
                    settings.horizontalOverflow = HorizontalWrapMode.Wrap;
                    Assert.That(new TextGenerator().GetPreferredHeight(rules.text, settings),
                        Is.LessThanOrEqualTo(bounds.height + 0.5f),
                        $"{definition.id} paper preview must fit without vertical truncation");
                    registry.TryGetText(definition.id, out var registered);
                    Assert.That(card.FullRulesText, Is.EqualTo(registered.rulesText), "Full rules are retained for the reading view.");
                    Assert.That(rules.rectTransform.rect.height, Is.EqualTo(430f * 104f / 520f).Within(0.01f));
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(cardRoot);
                UnityEngine.Object.DestroyImmediate(canvasRoot);
                if (font != null) UnityEngine.Object.DestroyImmediate(font);
            }
        }

        [Test]
        public void SevenCardHandLayoutKeepsEveryCardInsideStoneHandPlate()
        {
            var root = new GameObject("SevenCardHandLayoutTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                var setupPreview = typeof(DemoSceneController).GetMethod(
                    "SetupFullHandPreview", BindingFlags.Instance | BindingFlags.NonPublic);
                var verifyPreview = typeof(DemoSceneController).GetMethod(
                    "IsFullHandLayoutWithinPlate", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(setupPreview, Is.Not.Null);
                Assert.That(verifyPreview, Is.Not.Null);

                root.transform.Find("DemoCanvas/FactionRail/Faction_desert_badlands")
                    .GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(battlefield.PlayerFactionId, Is.EqualTo("desert_badlands"));
                setupPreview.Invoke(controller, null);
                Assert.That(verifyPreview.Invoke(controller, null), Is.EqualTo(true));
                Assert.That(battlefield.PlayerFactionId, Is.EqualTo("desert_badlands"),
                    "the full-hand preview must preserve the explicitly selected battlefield faction");
                Assert.That(root.transform.Find("BattlefieldGeometry/Ground_Player_0_-3")
                        .GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name,
                    Is.EqualTo("red_sandstone"),
                    "the full-hand preview must keep the selected player's terrain theme");

                var handPlate = root.transform.Find("DemoCanvas/HandPlate").GetComponent<RectTransform>();
                var handRoot = root.transform.Find("DemoCanvas/HandPlate/HandCards");
                var cards = handRoot.GetComponentsInChildren<CardUI>(true);
                Assert.That(cards, Has.Length.EqualTo(7));
                Assert.That(cards.All(card => card.CardId.StartsWith("ed_", System.StringComparison.Ordinal)), Is.True,
                    "the preview must continue to use the seven End cards that stress the layout");
                Assert.That(cards.All(card => card.RectTransform.sizeDelta == new Vector2(166f, 216f)), Is.True);
                var sortedCenters = cards.Select(card => card.RectTransform.anchoredPosition.x).OrderBy(value => value).ToArray();
                Assert.That(sortedCenters.Zip(sortedCenters.Skip(1), (left, right) => right - left)
                    .All(spacing => spacing > 180f && spacing < 190f), Is.True);
                Assert.That(cards.Max(card => Mathf.Abs(card.RectTransform.localEulerAngles.z > 180f
                    ? card.RectTransform.localEulerAngles.z - 360f
                    : card.RectTransform.localEulerAngles.z)), Is.LessThanOrEqualTo(1.5f));

                var horizontalBounds = new System.Collections.Generic.List<Vector2>();
                foreach (var card in cards)
                {
                    var worldCorners = new Vector3[4];
                    card.RectTransform.GetWorldCorners(worldCorners);
                    var localCorners = worldCorners.Select(handPlate.InverseTransformPoint).ToArray();
                    Assert.That(localCorners.Min(value => value.x),
                        Is.GreaterThanOrEqualTo(-handPlate.rect.width * 0.5f + 24f - 0.5f));
                    Assert.That(localCorners.Max(value => value.x),
                        Is.LessThanOrEqualTo(handPlate.rect.width * 0.5f - 24f + 0.5f));
                    horizontalBounds.Add(new Vector2(localCorners.Min(value => value.x), localCorners.Max(value => value.x)));
                }
                horizontalBounds.Sort((left, right) => left.x.CompareTo(right.x));
                Assert.That(horizontalBounds.Zip(horizontalBounds.Skip(1), (left, right) => right.x - left.y)
                    .All(gap => gap >= 8f), Is.True,
                    "The projected card bounds must leave a visible gap so adjacent face text is not covered.");

                var energyPlate = root.transform.Find("DemoCanvas/EnergyPlate").GetComponent<RectTransform>();
                var energyCorners = new Vector3[4];
                energyPlate.GetWorldCorners(energyCorners);
                var energyBounds = new Rect(
                    energyCorners.Min(corner => handPlate.InverseTransformPoint(corner).x),
                    energyCorners.Min(corner => handPlate.InverseTransformPoint(corner).y),
                    energyCorners.Max(corner => handPlate.InverseTransformPoint(corner).x) - energyCorners.Min(corner => handPlate.InverseTransformPoint(corner).x),
                    energyCorners.Max(corner => handPlate.InverseTransformPoint(corner).y) - energyCorners.Min(corner => handPlate.InverseTransformPoint(corner).y));
                foreach (var card in cards)
                {
                    var corners = new Vector3[4];
                    card.RectTransform.GetWorldCorners(corners);
                    var cardBounds = new Rect(
                        corners.Min(corner => handPlate.InverseTransformPoint(corner).x),
                        corners.Min(corner => handPlate.InverseTransformPoint(corner).y),
                        corners.Max(corner => handPlate.InverseTransformPoint(corner).x) - corners.Min(corner => handPlate.InverseTransformPoint(corner).x),
                        corners.Max(corner => handPlate.InverseTransformPoint(corner).y) - corners.Min(corner => handPlate.InverseTransformPoint(corner).y));
                    Assert.That(cardBounds.Overlaps(energyBounds), Is.False,
                        "The energy plate must not cover any full-hand card or its stat sockets.");
                }

                var statusPlate = root.transform.Find("DemoCanvas/StatusPlate").GetComponent<RectTransform>();
                var statusCorners = new Vector3[4];
                statusPlate.GetWorldCorners(statusCorners);
                var statusMinX = statusCorners.Min(corner => handPlate.InverseTransformPoint(corner).x);
                Assert.That(statusMinX, Is.GreaterThanOrEqualTo(horizontalBounds.Last().y + 8f),
                    "The lower-right status plate must not cover the last full-hand card.");
                Assert.That(statusPlate.anchoredPosition.x + statusPlate.rect.width * 0.5f,
                    Is.LessThanOrEqualTo(960f - 8f),
                    "The status plate must retain a safe inset from the right edge of the screen.");
                var inspectorPanel = root.transform.Find("DemoCanvas/CardDetailsPanel").GetComponent<RectTransform>();
                var inspectorCorners = new Vector3[4];
                inspectorPanel.GetWorldCorners(inspectorCorners);
                var inspectorMinX = inspectorCorners.Min(corner => handPlate.InverseTransformPoint(corner).x);
                var inspectorMaxX = inspectorCorners.Max(corner => handPlate.InverseTransformPoint(corner).x);
                Assert.That(inspectorMinX, Is.GreaterThanOrEqualTo(horizontalBounds.Last().y + 8f),
                    "The card details panel must leave at least 8 design units beyond the rightmost full-hand card.");
                Assert.That(inspectorMaxX, Is.LessThanOrEqualTo(960f - 8f),
                    "The card details panel must retain an 8-unit inset from the right edge of the design canvas.");
                var statusText = statusPlate.Find("Status").GetComponent<UnityEngine.UI.Text>();
                var statusMessages = new[]
                {
                    "七张末地手牌预览：长卡名避开费用徽章，卡牌完整落在石砖底板内。",
                    "等待服务端确认战场操作；此期间手牌、目标和回合控制均已锁定。",
                    "本次部署失败：相邻建筑格已占用，请选择连续空闲的建筑范围。"
                };
                foreach (var message in statusMessages)
                {
                    var settings = statusText.GetGenerationSettings(statusText.rectTransform.rect.size);
                    settings.resizeTextForBestFit = false;
                    settings.fontSize = statusText.resizeTextMinSize;
                    settings.horizontalOverflow = HorizontalWrapMode.Wrap;
                    settings.verticalOverflow = VerticalWrapMode.Overflow;
                    var requiredHeight = new TextGenerator().GetPreferredHeight(message, settings);
                    Assert.That(requiredHeight, Is.LessThanOrEqualTo(statusText.rectTransform.rect.height + 0.5f),
                        $"Status text must fit without truncation in the card-clear panel: {message}");
                }
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void CompactCardRulesShowEllipsisOnlyWhenTheFullTextDoesNotFit()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetText("nt_002", out var longText), Is.True);
            Assert.That(registry.TryGetText("pf_002", out var shortText), Is.True);
            var font = Font.CreateDynamicFontFromOSFont(
                new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 20);
            var compactRoot = new GameObject("CompactLongRulesCard", typeof(RectTransform), typeof(Image), typeof(CardUI));
            var shortRoot = new GameObject("CompactShortRulesCard", typeof(RectTransform), typeof(Image), typeof(CardUI));
            var detailRoot = new GameObject("DetailLongRulesCard", typeof(RectTransform), typeof(Image), typeof(CardUI));
            var discountedRoot = new GameObject("DiscountedCompactCard", typeof(RectTransform), typeof(Image), typeof(CardUI));
            var canvasRoot = new GameObject("ScaledCardCanvas", typeof(RectTransform), typeof(Canvas));
            var scaledCanvas = canvasRoot.GetComponent<Canvas>();
            scaledCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
            scaledCanvas.scaleFactor = 2f / 3f;
            compactRoot.transform.SetParent(canvasRoot.transform, false);
            shortRoot.transform.SetParent(canvasRoot.transform, false);
            detailRoot.transform.SetParent(canvasRoot.transform, false);
            discountedRoot.transform.SetParent(canvasRoot.transform, false);
            try
            {
                compactRoot.GetComponent<CardUI>().Bind(registry, "nt_002", new Vector2(166, 216), true, font, null);
                shortRoot.GetComponent<CardUI>().Bind(registry, "pf_002", new Vector2(166, 216), true, font, null);
                detailRoot.GetComponent<CardUI>().Bind(registry, "nt_002", new Vector2(250, 430), false, font, null);
                discountedRoot.GetComponent<CardUI>().Bind(registry, "ed_003", new Vector2(166, 216), true, font, null, 2);

                var compactRules = compactRoot.transform.Find("Rules").GetComponent<Text>();
                var shortRules = shortRoot.transform.Find("Rules").GetComponent<Text>();
                var detailRules = detailRoot.transform.Find("Rules").GetComponent<Text>();
                var discountText = discountedRoot.transform.Find("CostModifier").GetComponent<Text>();
                var discountBadge = discountedRoot.transform.Find("CostModifierBadge").GetComponent<RectTransform>();
                Assert.That(compactRules.resizeTextMinSize, Is.EqualTo(18), "compact card design font must compensate for a 2/3 Canvas scale");
                Assert.That(compactRules.resizeTextMinSize * scaledCanvas.scaleFactor, Is.EqualTo(12f).Within(0.01f),
                    "compact card rules must preserve their 12px screen-space minimum");
                Assert.That(detailRules.resizeTextMinSize, Is.EqualTo(18), "detail card design font must compensate for a 2/3 Canvas scale");
                Assert.That(detailRules.resizeTextMinSize * scaledCanvas.scaleFactor, Is.GreaterThanOrEqualTo(12f),
                    "detail card rules must preserve their 12px screen-space minimum");
                Assert.That(detailRules.resizeTextMinSize * scaledCanvas.scaleFactor, Is.LessThan(12.7f),
                    "detail card rules must not overshoot the intended minimum by a full pixel");
                Assert.That(discountText.text, Is.EqualTo("-1"));
                Assert.That(discountBadge.sizeDelta, Is.EqualTo(new Vector2(32f, 24f)),
                    "the compact discount badge needs enough material area to hold readable text");
                Assert.That(discountText.resizeTextMinSize, Is.EqualTo(15));
                Assert.That(discountText.resizeTextMinSize * scaledCanvas.scaleFactor, Is.EqualTo(10f).Within(0.01f),
                    "the cost change must preserve a 10px screen-space minimum at 2/3 scale");
                Assert.That(discountText.resizeTextMaxSize * scaledCanvas.scaleFactor, Is.EqualTo(12f).Within(0.01f));
                Assert.That(longText.rulesText.Length, Is.GreaterThan(compactRules.text.Length));
                Assert.That(compactRules.text, Does.EndWith("…"));
                Assert.That(shortRules.text, Is.EqualTo(shortText.rulesText), "fitting hand text stays complete");
                Assert.That(detailRules.text, Does.EndWith("…"), "long detail text is explicitly a paper-contained preview");
                Assert.That(detailRoot.GetComponent<CardUI>().FullRulesText, Is.EqualTo(longText.rulesText));

                var lateUpdate = typeof(CardUI).GetMethod("LateUpdate", BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(lateUpdate, Is.Not.Null);
                var previewAtTwoThirds = compactRules.text;
                scaledCanvas.scaleFactor = 0.5f;
                lateUpdate.Invoke(compactRoot.GetComponent<CardUI>(), null);
                lateUpdate.Invoke(detailRoot.GetComponent<CardUI>(), null);
                lateUpdate.Invoke(discountedRoot.GetComponent<CardUI>(), null);
                Assert.That(compactRules.resizeTextMinSize, Is.EqualTo(24));
                Assert.That(detailRules.resizeTextMinSize, Is.EqualTo(24));
                Assert.That(discountText.resizeTextMinSize, Is.EqualTo(20));
                Assert.That(discountText.resizeTextMaxSize, Is.EqualTo(24));
                Assert.That(compactRules.text.Length, Is.LessThanOrEqualTo(previewAtTwoThirds.Length),
                    "the preview must be remeasured when the screen scale changes");
                scaledCanvas.scaleFactor = 1f;
                lateUpdate.Invoke(compactRoot.GetComponent<CardUI>(), null);
                lateUpdate.Invoke(detailRoot.GetComponent<CardUI>(), null);
                lateUpdate.Invoke(discountedRoot.GetComponent<CardUI>(), null);
                Assert.That(compactRules.resizeTextMinSize, Is.EqualTo(12));
                Assert.That(detailRules.resizeTextMinSize, Is.EqualTo(12));
                Assert.That(discountText.resizeTextMinSize, Is.EqualTo(10));
                Assert.That(discountText.resizeTextMaxSize, Is.EqualTo(12));

                var bounds = compactRules.rectTransform.rect;
                var settings = compactRules.GetGenerationSettings(bounds.size);
                settings.resizeTextForBestFit = false;
                settings.fontSize = compactRules.resizeTextMinSize;
                settings.verticalOverflow = VerticalWrapMode.Overflow;
                settings.horizontalOverflow = HorizontalWrapMode.Wrap;
                Assert.That(new TextGenerator().GetPreferredHeight(compactRules.text, settings),
                    Is.LessThanOrEqualTo(bounds.height + 0.5f), "the preview including its ellipsis fits at the minimum readable size");

                var detailBounds = detailRules.rectTransform.rect;
                var detailSettings = detailRules.GetGenerationSettings(detailBounds.size);
                detailSettings.resizeTextForBestFit = false;
                detailSettings.fontSize = detailRules.resizeTextMinSize;
                detailSettings.verticalOverflow = VerticalWrapMode.Overflow;
                detailSettings.horizontalOverflow = HorizontalWrapMode.Wrap;
                Assert.That(new TextGenerator().GetPreferredHeight(detailRules.text, detailSettings),
                    Is.LessThanOrEqualTo(detailBounds.height + 0.5f), "the rule preview fits the real paper at its minimum readable size");
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(compactRoot);
                UnityEngine.Object.DestroyImmediate(shortRoot);
                UnityEngine.Object.DestroyImmediate(detailRoot);
                UnityEngine.Object.DestroyImmediate(discountedRoot);
                UnityEngine.Object.DestroyImmediate(canvasRoot);
                if (font != null) UnityEngine.Object.DestroyImmediate(font);
            }
        }

        [Test]
        public void PowderSnowUsesTargetingAndExpiresAfterTheSkippedOpponentTurn()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "si_006" });
            Assert.That(registry.TryGetDefinition("si_006", out var powderSnow), Is.True);
            Assert.That(powderSnow.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            Assert.That(registry.TryGetDefinition("nt_003", out var blaze), Is.True);
            match.ResetOpponent(new[] { blaze });
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);

            var missing = match.ApplyPlayCard(powderSnow, match.CreatePlayCardCommand("si_006"));
            Assert.That(missing.Accepted, Is.False);
            Assert.That(match.Hand, Does.Contain("si_006"));

            var played = match.ApplyPlayCard(powderSnow,
                match.CreatePlayCardCommand("si_006", "UNIT", target.InstanceId));
            Assert.That(played.Accepted, Is.True);
            Assert.That(target.Attack, Is.EqualTo(1));
            Assert.That(target.HasStatus("SLOW"), Is.True);
            Assert.That(target.Statuses.Single().remainingDuration, Is.EqualTo(1));

            match.EndPlayerTurn();
            Assert.That(target.HasStatus("SLOW"), Is.True, "Slow belongs to the enemy and must not expire at the caster end phase.");
            match.BeginNextPlayerTurn();
            Assert.That(target.HasStatus("SLOW"), Is.False);
            Assert.That(target.Attack, Is.EqualTo(3));
        }

        [Test]
        public void StrayRequiresADeploymentTargetAndSharesSlowWithoutStackingTheBucketPenalty()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "si_003", "si_006" });
            Assert.That(registry.TryGetDefinition("si_003", out var stray), Is.True);
            Assert.That(stray.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            Assert.That(registry.TryGetDefinition("si_006", out var powderSnow), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            match.ResetOpponent(new[] { bee });
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);

            var missing = match.ApplyDeploy(stray, match.CreateDeployCommand("si_003", DemoSlotKind.Unit, 0));
            Assert.That(missing.Accepted, Is.False);
            Assert.That(missing.Code, Is.EqualTo(DemoCommandRejectionCode.InvalidTarget));
            Assert.That(match.UnitSlots[0], Is.Null.Or.Empty);
            Assert.That(match.Hand, Does.Contain("si_003"));

            var deployed = match.ApplyDeploy(stray,
                match.CreateDeployCommand("si_003", DemoSlotKind.Unit, 0, MatchPaymentMethods.Redstone, "UNIT", target.InstanceId));
            Assert.That(deployed.Accepted, Is.True);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0).CardId, Is.EqualTo("si_003"));
            Assert.That(target.HasStatus("SLOW"), Is.True);
            Assert.That(target.Statuses.Single().sourceInstanceId, Is.EqualTo(match.GetObject(true, DemoSlotKind.Unit, 0).InstanceId));
            Assert.That(target.Statuses.Single().attackModifier, Is.Zero);

            var bucket = match.ApplyPlayCard(powderSnow,
                match.CreatePlayCardCommand("si_006", "UNIT", target.InstanceId));
            Assert.That(bucket.Accepted, Is.True);
            Assert.That(target.Attack, Is.Zero);
            Assert.That(target.Statuses.Single().attackModifier, Is.EqualTo(-1));
            Assert.That(target.Statuses.Single().boundAttackModifier, Is.EqualTo(-2));
            Assert.That(target.Statuses.Single().sourceCardId, Is.EqualTo("si_006"));
            Assert.That(target.Statuses.Single().sourceInstanceId, Is.Empty);

            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(target.Attack, Is.EqualTo(1));
            Assert.That(target.Statuses, Is.Empty);
        }

        [Test]
        public void RiptideTridentEquipsHeroAndMovesASurvivingTargetToAnAdjacentWorldSlot()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "or_006" });
            Assert.That(registry.TryGetDefinition("or_006", out var trident), Is.True);
            Assert.That(trident.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            Assert.That(registry.TryGetDefinition("si_005", out var polarBear), Is.True);
            match.ResetOpponent(new[] { polarBear, polarBear });

            var equipped = match.ApplyPlayCard(trident, match.CreatePlayCardCommand("or_006"));
            Assert.That(equipped.Accepted, Is.True);
            Assert.That(match.PlayerEquipment.CardId, Is.EqualTo("or_006"));
            Assert.That(match.PlayerEquipment.Durability, Is.EqualTo(3));
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var target = match.GetObject(false, DemoSlotKind.Unit, 2);
            var attacked = match.ApplyAttack(match.CreateAttackCommand(MatchAttackerIds.Hero, "UNIT", target.InstanceId));
            Assert.That(attacked.Accepted, Is.True);
            Assert.That(match.PlayerHeroHasAttacked, Is.True);
            Assert.That(match.PlayerEquipment.Durability, Is.EqualTo(2));
            Assert.That(match.PlayerLife, Is.EqualTo(27));
            Assert.That(match.PendingChoice.kind, Is.EqualTo("MOVE_UNIT"));
            Assert.That(match.PendingChoice.options.Select(value => value.slotIndex), Is.EquivalentTo(new[] { 1, 3 }));

            var moved = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, 0));
            Assert.That(moved.Accepted, Is.True);
            Assert.That(match.GetObject(false, DemoSlotKind.Unit, 1).InstanceId, Is.EqualTo(target.InstanceId));
            Assert.That(match.OpponentUnitSlots[2], Is.Null.Or.Empty);
        }

        [Test]
        public void SalmonSchoolMovesAcrossFriendlyWorldSlotsAndItsAttackBonusExpiresAtEndOfTurn()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "or_001" });
            Assert.That(registry.TryGetDefinition("or_001", out var salmon), Is.True);
            Assert.That(salmon.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            var deployed = match.ApplyDeploy(salmon, match.CreateDeployCommand("or_001", DemoSlotKind.Unit, 1));
            Assert.That(deployed.Accepted, Is.True);
            Assert.That(match.PendingChoice.kind, Is.EqualTo("MOVE_UNIT"));
            Assert.That(match.PendingChoice.effectId, Is.EqualTo("effect.or_001.01"));
            Assert.That(match.PendingChoice.options.Select(value => value.slotIndex), Is.EquivalentTo(new[] { 0, 2 }));

            var unit = match.GetObject(true, DemoSlotKind.Unit, 1);
            var moved = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, 1));
            Assert.That(moved.Accepted, Is.True);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 2).InstanceId, Is.EqualTo(unit.InstanceId));
            Assert.That(unit.Attack, Is.EqualTo(2));
            Assert.That(unit.TemporaryAttackModifier, Is.EqualTo(1));

            match.EndPlayerTurn();
            Assert.That(unit.Attack, Is.EqualTo(1));
            Assert.That(unit.TemporaryAttackModifier, Is.Zero);
        }

        [Test]
        public void DolphinGuideBuffsOnlyTheFirstOtherFriendlyUnitMovedEachTurn()
        {
            var registry = CardContentLoader.Load();
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { "or_002", "or_001", "or_001" });
            var salmonInstanceId = match.HandCards[1].handCardInstanceId;
            Assert.That(registry.TryGetDefinition("or_002", out var guide), Is.True);
            Assert.That(registry.TryGetDefinition("or_001", out var salmon), Is.True);
            Assert.That(guide.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            Assert.That(match.ApplyDeploy(guide, match.CreateDeployCommand("or_002", DemoSlotKind.Unit, 0)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(salmon, match.CreateDeployCommand("or_001", DemoSlotKind.Unit, 2,
                handCardInstanceId: salmonInstanceId)).Accepted, Is.True);
            var firstMove = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, 1));
            Assert.That(firstMove.Accepted, Is.True);
            Assert.That(firstMove.Message, Does.Contain("海豚向导"));
            var firstSalmon = match.GetObject(true, DemoSlotKind.Unit, 3);
            Assert.That(firstSalmon.Attack, Is.EqualTo(3));
            Assert.That(firstSalmon.TemporaryAttackModifier, Is.EqualTo(2));

            Assert.That(match.ApplyDeploy(salmon, match.CreateDeployCommand("or_001", DemoSlotKind.Unit, 2)).Accepted, Is.True);
            var secondMove = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, 0));
            Assert.That(secondMove.Accepted, Is.True);
            Assert.That(secondMove.Message, Does.Not.Contain("海豚向导触发"));
            var secondSalmon = match.GetObject(true, DemoSlotKind.Unit, 1);
            Assert.That(secondSalmon.Attack, Is.EqualTo(2));

            match.EndPlayerTurn();
            Assert.That(firstSalmon.Attack, Is.EqualTo(1));
            Assert.That(secondSalmon.Attack, Is.EqualTo(1));
        }

        [Test]
        public void DrownedRequiresAndDamagesAnEnemyTargetOnlyBesideAnAquaticAlly()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("or_001", out var salmon), Is.True);
            Assert.That(registry.TryGetDefinition("or_003", out var drowned), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(drowned.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            var active = CreateScenarioMatch();
            active.ResetHand(new[] { "or_001", "or_003" });
            active.ResetOpponent(new[] { sheep });
            Assert.That(active.ApplyDeploy(salmon, active.CreateDeployCommand("or_001", DemoSlotKind.Unit, 1)).Accepted, Is.True);
            Assert.That(active.ApplyResolveChoice(active.CreateResolveChoiceCommand(active.PendingChoice.choiceId, -1)).Accepted, Is.True);
            var target = active.GetObject(false, DemoSlotKind.Unit, 0);
            var energyBefore = active.Energy;
            var missingTarget = active.ApplyDeploy(drowned, active.CreateDeployCommand("or_003", DemoSlotKind.Unit, 2));
            Assert.That(missingTarget.Accepted, Is.False);
            Assert.That(missingTarget.Code, Is.EqualTo(DemoCommandRejectionCode.InvalidTarget));
            Assert.That(active.Energy, Is.EqualTo(energyBefore));
            Assert.That(active.Hand, Does.Contain("or_003"));

            var deployed = active.ApplyDeploy(drowned, active.CreateDeployCommand(
                "or_003", DemoSlotKind.Unit, 2, MatchPaymentMethods.Redstone, "UNIT", target.InstanceId));
            Assert.That(deployed.Accepted, Is.True);
            Assert.That(deployed.Message, Does.Contain("造成 1 点伤害"));
            Assert.That(target.Health, Is.EqualTo(sheep.health - 1));
            Assert.That(active.GetObject(true, DemoSlotKind.Unit, 2).HasTag("aquatic"), Is.True);

            var inactive = CreateScenarioMatch();
            inactive.ResetHand(new[] { "or_003" });
            inactive.ResetOpponent(new[] { sheep });
            var inactiveTarget = inactive.GetObject(false, DemoSlotKind.Unit, 0);
            var inactiveDeploy = inactive.ApplyDeploy(drowned, inactive.CreateDeployCommand("or_003", DemoSlotKind.Unit, 2));
            Assert.That(inactiveDeploy.Accepted, Is.True);
            Assert.That(inactiveDeploy.Message, Does.Contain("战吼未触发"));
            Assert.That(inactiveTarget.Health, Is.EqualTo(sheep.health));
        }

        [Test]
        public void GuardianPunishesOnlyTheFirstActualEnemyMovementAndDropsPrismarineShard()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("or_001", out var salmon), Is.True);
            Assert.That(registry.TryGetDefinition("or_004", out var guardian), Is.True);
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            Assert.That(guardian.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            var movement = CreateScenarioMatch();
            movement.ResetHand(new[] { "or_001", "or_001", "or_001" });
            var salmonInstanceIds = movement.HandCards.Select(card => card.handCardInstanceId).ToArray();
            movement.ResetOpponent(new[] { guardian });
            Assert.That(movement.ApplyDeploy(salmon,
                movement.CreateDeployCommand("or_001", DemoSlotKind.Unit, 0,
                    handCardInstanceId: salmonInstanceIds[0])).Accepted, Is.True);
            var stayed = movement.ApplyResolveChoice(
                movement.CreateResolveChoiceCommand(movement.PendingChoice.choiceId, -1));
            Assert.That(stayed.Accepted, Is.True);
            var guardianObject = movement.GetObject(false, DemoSlotKind.Unit, 0);
            Assert.That(movement.HasTriggeredEffect(false, guardianObject.InstanceId, "effect.or_004.01"), Is.False);

            Assert.That(movement.ApplyDeploy(salmon,
                movement.CreateDeployCommand("or_001", DemoSlotKind.Unit, 2,
                    handCardInstanceId: salmonInstanceIds[1])).Accepted, Is.True);
            var firstMove = movement.ApplyResolveChoice(
                movement.CreateResolveChoiceCommand(movement.PendingChoice.choiceId, 1));
            Assert.That(firstMove.Accepted, Is.True);
            Assert.That(firstMove.Message, Does.Contain("守卫者射线触发 1 次"));
            Assert.That(movement.GetObject(true, DemoSlotKind.Unit, 3).Health, Is.EqualTo(1));
            Assert.That(movement.HasTriggeredEffect(false, guardianObject.InstanceId, "effect.or_004.01"), Is.True);

            Assert.That(movement.ApplyDeploy(salmon,
                movement.CreateDeployCommand("or_001", DemoSlotKind.Unit, 2,
                    handCardInstanceId: salmonInstanceIds[2])).Accepted, Is.True);
            var secondMove = movement.ApplyResolveChoice(
                movement.CreateResolveChoiceCommand(movement.PendingChoice.choiceId, 0));
            Assert.That(secondMove.Accepted, Is.True);
            Assert.That(secondMove.Message, Does.Not.Contain("守卫者射线触发"));
            Assert.That(movement.GetObject(true, DemoSlotKind.Unit, 1).Health, Is.EqualTo(2));

            var loot = CreateScenarioMatch();
            loot.ResetHand(new[] { ironGolem.id });
            loot.ResetOpponent(new[] { guardian });
            Assert.That(loot.ApplyDeploy(ironGolem,
                loot.CreateDeployCommand(ironGolem.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            loot.EndPlayerTurn();
            loot.BeginNextPlayerTurn();
            Assert.That(loot.ApplyEnterCombat(loot.CreateEnterCombatCommand()).Accepted, Is.True);
            var lootTarget = loot.GetObject(false, DemoSlotKind.Unit, 0);
            var killed = loot.ApplyAttack(loot.CreateAttackCommand(
                loot.GetObject(true, DemoSlotKind.Unit, 0).InstanceId, "UNIT", lootTarget.InstanceId));
            Assert.That(killed.Accepted, Is.True);
            Assert.That(killed.Message, Does.Contain("海晶碎片"));
            Assert.That(loot.Hand, Does.Contain("tk_012"));
        }

        [Test]
        public void PrismarineShardRequiresMovableAquaticTargetThenHealsBeforeMovementReactions()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("or_001", out var salmon), Is.True);
            Assert.That(registry.TryGetDefinition("or_002", out var dolphin), Is.True);
            Assert.That(registry.TryGetDefinition("or_004", out var guardian), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(registry.TryGetDefinition("tk_012", out var shard), Is.True);
            Assert.That(shard.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            var invalid = CreateScenarioMatch();
            invalid.ResetHand(new[] { bee.id, shard.id });
            Assert.That(invalid.ApplyDeploy(bee,
                invalid.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            var handBefore = invalid.Hand.Count;
            var rejected = invalid.ApplyPlayCard(shard,
                invalid.CreatePlayCardCommand(shard.id, "UNIT", invalid.GetObject(true, DemoSlotKind.Unit, 0).InstanceId));
            Assert.That(rejected.Accepted, Is.False);
            Assert.That(rejected.Code, Is.EqualTo(DemoCommandRejectionCode.InvalidTarget));
            Assert.That(invalid.Hand.Count, Is.EqualTo(handBefore));
            Assert.That(invalid.DiscardPile, Does.Not.Contain(shard.id));

            var match = CreateScenarioMatch();
            match.ResetHand(new[] { dolphin.id, salmon.id });
            match.ResetOpponent(new[] { guardian });
            Assert.That(match.ApplyDeploy(dolphin,
                match.CreateDeployCommand(dolphin.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(salmon,
                match.CreateDeployCommand(salmon.id, DemoSlotKind.Unit, 1)).Accepted, Is.True);
            Assert.That(match.ApplyResolveChoice(
                match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, -1)).Accepted, Is.True);
            var target = match.GetObject(true, DemoSlotKind.Unit, 1);
            target.Health = 1;
            match.ResetHand(new[] { shard.id });

            var played = match.ApplyPlayCard(shard,
                match.CreatePlayCardCommand(shard.id, "UNIT", target.InstanceId));
            Assert.That(played.Accepted, Is.True);
            Assert.That(match.PendingChoice.effectId, Is.EqualTo("effect.tk_012.01"));
            Assert.That(match.PendingChoice.sourceInstanceId, Does.StartWith("effect-"));
            Assert.That(match.PendingChoice.options.Select(value => value.slotIndex), Is.EqualTo(new[] { 2 }));
            var stay = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, -1));
            Assert.That(stay.Accepted, Is.False);
            Assert.That(match.PendingChoice, Is.Not.Null);

            var moved = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, 0));
            Assert.That(moved.Accepted, Is.True);
            Assert.That(moved.Message, Does.Contain("恢复 1 点生命"));
            Assert.That(moved.Message, Does.Contain("海豚向导"));
            Assert.That(moved.Message, Does.Contain("守卫者射线"));
            Assert.That(target.SlotIndex, Is.EqualTo(2));
            Assert.That(target.Attack, Is.EqualTo(2));
            Assert.That(target.Health, Is.EqualTo(1));
        }

        [Test]
        public void TurtleAuraTracksAdjacencyAndCanKillBeforePrismarineHealing()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("or_001", out var salmon), Is.True);
            Assert.That(registry.TryGetDefinition("or_005", out var turtle), Is.True);
            Assert.That(registry.TryGetDefinition("tk_012", out var shard), Is.True);
            Assert.That(turtle.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            var match = CreateScenarioMatch();
            match.ResetHand(new[] { turtle.id, salmon.id });
            Assert.That(match.ApplyDeploy(turtle,
                match.CreateDeployCommand(turtle.id, DemoSlotKind.Unit, 1)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(salmon,
                match.CreateDeployCommand(salmon.id, DemoSlotKind.Unit, 2)).Accepted, Is.True);
            Assert.That(match.ApplyResolveChoice(
                match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, -1)).Accepted, Is.True);
            var target = match.GetObject(true, DemoSlotKind.Unit, 2);
            Assert.That(target.Health, Is.EqualTo(3));
            Assert.That(target.MaxHealth, Is.EqualTo(3));
            Assert.That(target.AdjacencyHealthModifier, Is.EqualTo(1));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 1).AdjacencyHealthModifier, Is.Zero);

            target.Health = 1;
            match.ResetHand(new[] { shard.id });
            Assert.That(match.ApplyPlayCard(shard,
                match.CreatePlayCardCommand(shard.id, "UNIT", target.InstanceId)).Accepted, Is.True);
            var moved = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, 0));
            Assert.That(moved.Accepted, Is.True);
            Assert.That(moved.Message, Does.Contain("失去海龟光环而死亡"));
            Assert.That(match.PlayerBattlefield.Any(value => value.InstanceId == target.InstanceId), Is.False);
            Assert.That(match.UnitSlots[3], Is.Null.Or.Empty);
        }

        [Test]
        public void WoodlandNurseryGrowsOnlyTheFirstAnimalAndResetsNextTurn()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_005", out var nursery), Is.True);
            Assert.That(registry.TryGetDefinition("db_001", out var husk), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(nursery.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            var match = CreateScenarioMatch();
            match.ResetHand(new[] { nursery.id, husk.id, sheep.id, bee.id });
            Assert.That(match.ApplyDeploy(nursery,
                match.CreateDeployCommand(nursery.id, DemoSlotKind.Building, 0)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(husk,
                match.CreateDeployCommand(husk.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            var first = match.ApplyDeploy(sheep,
                match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 1));
            Assert.That(first.Accepted, Is.True);
            Assert.That(first.Message, Does.Contain("苗圃培育触发 1 次"));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 1).MaxHealth, Is.EqualTo(4));
            var source = match.GetObject(true, DemoSlotKind.Building, 0);
            Assert.That(match.HasTriggeredEffect(true, source.InstanceId, "effect.pf_005.01"), Is.True);

            var second = match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 2));
            Assert.That(second.Accepted, Is.True);
            Assert.That(second.Message, Does.Not.Contain("苗圃培育"));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 2).MaxHealth, Is.EqualTo(2));

            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.HasTriggeredEffect(true, source.InstanceId, "effect.pf_005.01"), Is.False);
            match.ResetHand(new[] { sheep.id });
            var nextTurn = match.ApplyDeploy(sheep,
                match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 3));
            Assert.That(nextTurn.Accepted, Is.True);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 3).MaxHealth, Is.EqualTo(4));
        }

        [Test]
        public void WoodlandNurseriesStackBeforeCoralGrowthLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_005", out var nursery), Is.True);
            Assert.That(registry.TryGetDefinition("or_007", out var reef), Is.True);
            Assert.That(registry.TryGetDefinition("or_005", out var turtle), Is.True);

            var match = CreateScenarioMatch();
            match.ResetHand(new[] { nursery.id, reef.id });
            Assert.That(match.ApplyDeploy(nursery,
                match.CreateDeployCommand(nursery.id, DemoSlotKind.Building, 1)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(reef,
                match.CreateDeployCommand(reef.id, DemoSlotKind.Building, 2)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            match.ResetHand(new[] { nursery.id, turtle.id });
            Assert.That(match.ApplyDeploy(nursery,
                match.CreateDeployCommand(nursery.id, DemoSlotKind.Building, 0)).Accepted, Is.True);

            var deployed = match.ApplyDeploy(turtle,
                match.CreateDeployCommand(turtle.id, DemoSlotKind.Unit, 0));
            Assert.That(deployed.Accepted, Is.True);
            Assert.That(deployed.Message, Does.Contain("苗圃培育触发 2 次"));
            Assert.That(deployed.Message, Does.Contain("珊瑚滋养触发 1 次"));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0).MaxHealth, Is.EqualTo(9));
            Assert.That(match.PlayerBattlefield.Where(value => value.CardId == nursery.id).All(value =>
                match.HasTriggeredEffect(true, value.InstanceId, "effect.pf_005.01")), Is.True);
        }

        [Test]
        public void BreedingSeasonGrowsTwoCanonicalAnimalsAndSummonsNurseryGrownJuvenile()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_005", out var nursery), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("pf_006", out var breeding), Is.True);

            var match = CreateScenarioMatch();
            match.ResetHand(new[] { nursery.id, bee.id, sheep.id });
            Assert.That(match.ApplyDeploy(nursery,
                match.CreateDeployCommand(nursery.id, DemoSlotKind.Building, 0)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(sheep,
                match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 2)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            match.ResetHand(new[] { breeding.id });
            var beeTarget = match.GetObject(true, DemoSlotKind.Unit, 0);
            var sheepTarget = match.GetObject(true, DemoSlotKind.Unit, 2);

            var result = match.ApplyPlayCard(breeding,
                match.CreatePlayCardCommand(breeding.id, "UNIT", "", new[] { sheepTarget.InstanceId, beeTarget.InstanceId }));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(beeTarget.MaxHealth, Is.EqualTo(4));
            Assert.That(sheepTarget.MaxHealth, Is.EqualTo(4));
            var juvenile = match.GetObject(true, DemoSlotKind.Unit, 1);
            Assert.That(juvenile.CardId, Is.EqualTo("tk_003"));
            Assert.That(juvenile.Attack, Is.EqualTo(2));
            Assert.That(juvenile.MaxHealth, Is.EqualTo(3));
            Assert.That(result.Message, Does.Contain("幼体已在单位格 2 召唤"));
            Assert.That(match.Hand, Is.Empty);
            Assert.That(match.DiscardPile, Does.Contain("pf_006"));
        }

        [Test]
        public void BreedingSeasonKeepsBothGrowthBuffsWhenUnitRowIsFull()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("pf_003", out var wolf), Is.True);
            Assert.That(registry.TryGetDefinition("tk_003", out var juvenileDefinition), Is.True);
            Assert.That(registry.TryGetDefinition("pf_006", out var breeding), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { bee.id, sheep.id, wolf.id, juvenileDefinition.id });
            Assert.That(match.ApplyDeploy(bee, match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(sheep, match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 1)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(wolf, match.CreateDeployCommand(wolf.id, DemoSlotKind.Unit, 2)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(juvenileDefinition,
                match.CreateDeployCommand(juvenileDefinition.id, DemoSlotKind.Unit, 3)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            match.ResetHand(new[] { breeding.id });
            var first = match.GetObject(true, DemoSlotKind.Unit, 0);
            var second = match.GetObject(true, DemoSlotKind.Unit, 1);

            var result = match.ApplyPlayCard(breeding,
                match.CreatePlayCardCommand(breeding.id, "UNIT", "", new[] { second.InstanceId, first.InstanceId }));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(first.MaxHealth, Is.EqualTo(3));
            Assert.That(second.MaxHealth, Is.EqualTo(4));
            Assert.That(match.PlayerBattlefield, Has.Count.EqualTo(4));
            Assert.That(result.Message, Does.Contain("单位格已满"));
        }

        [Test]
        public void BreedingSeasonRejectsDuplicateNonAnimalAndEnemyTargetsAtomically()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("pf_004", out var farmer), Is.True);
            Assert.That(registry.TryGetDefinition("pf_003", out var enemyWolf), Is.True);
            Assert.That(registry.TryGetDefinition("pf_006", out var breeding), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { bee.id, farmer.id, sheep.id });
            Assert.That(match.ApplyDeploy(bee, match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(farmer, match.CreateDeployCommand(farmer.id, DemoSlotKind.Unit, 1)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(sheep, match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 2)).Accepted, Is.True);
            match.ResetOpponent(new[] { enemyWolf }, new[] { 0 });
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            match.ResetHand(new[] { breeding.id });
            var animal = match.GetObject(true, DemoSlotKind.Unit, 0);
            var nonAnimal = match.GetObject(true, DemoSlotKind.Unit, 1);
            var otherAnimal = match.GetObject(true, DemoSlotKind.Unit, 2);
            var enemy = match.GetObject(false, DemoSlotKind.Unit, 0);
            var energyBefore = match.Energy;
            var revisionBefore = match.Revision;
            var invalidTargets = new[]
            {
                new[] { animal.InstanceId, animal.InstanceId },
                new[] { animal.InstanceId, nonAnimal.InstanceId },
                new[] { animal.InstanceId, enemy.InstanceId }
            };

            foreach (var targets in invalidTargets)
            {
                var result = match.ApplyPlayCard(breeding,
                    match.CreatePlayCardCommand(breeding.id, "UNIT", "", targets));
                Assert.That(result.Accepted, Is.False);
                Assert.That(match.Energy, Is.EqualTo(energyBefore));
                Assert.That(match.Revision, Is.EqualTo(revisionBefore));
                Assert.That(match.Hand, Is.EqualTo(new[] { breeding.id }));
                Assert.That(match.DiscardPile, Is.Empty);
                Assert.That(animal.MaxHealth, Is.EqualTo(2));
                Assert.That(otherAnimal.MaxHealth, Is.EqualTo(3));
            }
        }

        [Test]
        public void WoodlandRallySummonsTwoCompanionsAndNurseryGrowsOnlyTheFirst()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_005", out var nursery), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("pf_007", out var rally), Is.True);
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { nursery.id, bee.id, sheep.id }, new[] { "pf_001" });
            Assert.That(match.ApplyDeploy(nursery,
                match.CreateDeployCommand(nursery.id, DemoSlotKind.Building, 0)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(sheep,
                match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 2)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            match.ResetDeckAndHand(new[] { rally.id }, new[] { "pf_003" });

            var result = match.ApplyPlayCard(rally, match.CreatePlayCardCommand(rally.id));

            Assert.That(result.Accepted, Is.True, result.Message);
            var companions = match.PlayerBattlefield.Where(value => value.CardId == "tk_004")
                .OrderBy(value => value.SlotIndex).ToArray();
            Assert.That(companions, Has.Length.EqualTo(2));
            Assert.That(companions[0].SlotIndex, Is.EqualTo(1));
            Assert.That(companions[0].MaxHealth, Is.EqualTo(3));
            Assert.That(companions[1].SlotIndex, Is.EqualTo(3));
            Assert.That(companions[1].MaxHealth, Is.EqualTo(2));
            Assert.That(result.Message, Does.Contain("召唤 2 个林地伙伴"));
        }

        [Test]
        public void WoodlandRallySummonsOnceThenDrawsWhenOnlyOneSlotIsOpen()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("pf_003", out var wolf), Is.True);
            Assert.That(registry.TryGetDefinition("pf_007", out var rally), Is.True);
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { bee.id, sheep.id, wolf.id }, new[] { "pf_001" });
            Assert.That(match.ApplyDeploy(bee, match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(sheep, match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 1)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(wolf, match.CreateDeployCommand(wolf.id, DemoSlotKind.Unit, 3)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            match.ResetDeckAndHand(new[] { rally.id }, new[] { "pf_001" });

            var result = match.ApplyPlayCard(rally, match.CreatePlayCardCommand(rally.id));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 2).CardId, Is.EqualTo("tk_004"));
            Assert.That(match.Hand, Is.EqualTo(new[] { "pf_001" }));
            Assert.That(result.Message, Does.Contain("召唤 1 个林地伙伴"));
            Assert.That(result.Message, Does.Contain("抽取 1 张牌"));
        }

        [Test]
        public void WoodlandRallyDrawsTwiceWhenTheUnitRowIsFull()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("pf_003", out var wolf), Is.True);
            Assert.That(registry.TryGetDefinition("tk_003", out var juvenile), Is.True);
            Assert.That(registry.TryGetDefinition("pf_007", out var rally), Is.True);
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { bee.id, sheep.id, wolf.id, juvenile.id }, new[] { "pf_001" });
            Assert.That(match.ApplyDeploy(bee, match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(sheep, match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 1)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(wolf, match.CreateDeployCommand(wolf.id, DemoSlotKind.Unit, 2)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(juvenile,
                match.CreateDeployCommand(juvenile.id, DemoSlotKind.Unit, 3)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            match.ResetDeckAndHand(new[] { rally.id }, new[] { "pf_001", "pf_002" });

            var result = match.ApplyPlayCard(rally, match.CreatePlayCardCommand(rally.id));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.Hand, Is.EqualTo(new[] { "pf_002", "pf_001" }));
            Assert.That(match.PlayerBattlefield, Has.Count.EqualTo(4));
            Assert.That(result.Message, Does.Contain("召唤 0 个林地伙伴"));
            Assert.That(result.Message, Does.Contain("抽取 2 张牌"));
        }

        [Test]
        public void CoralReefGrowsOnlyTheFirstAquaticUnitAndResetsNextTurn()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("or_007", out var reef), Is.True);
            Assert.That(registry.TryGetDefinition("or_001", out var salmon), Is.True);
            Assert.That(reef.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            var match = CreateScenarioMatch();
            match.ResetHand(new[] { reef.id, salmon.id, salmon.id });
            var firstSalmonInstanceId = match.HandCards[1].handCardInstanceId;
            Assert.That(match.ApplyDeploy(reef,
                match.CreateDeployCommand(reef.id, DemoSlotKind.Building, 0)).Accepted, Is.True);
            var first = match.ApplyDeploy(salmon,
                match.CreateDeployCommand(salmon.id, DemoSlotKind.Unit, 0,
                    handCardInstanceId: firstSalmonInstanceId));
            Assert.That(first.Accepted, Is.True);
            Assert.That(first.Message, Does.Contain("珊瑚滋养触发 1 次"));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0).MaxHealth, Is.EqualTo(3));
            Assert.That(match.HasTriggeredEffect(true, match.GetObject(true, DemoSlotKind.Building, 0).InstanceId,
                "effect.or_007.01"), Is.True);
            Assert.That(match.ApplyResolveChoice(
                match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, -1)).Accepted, Is.True);

            var second = match.ApplyDeploy(salmon,
                match.CreateDeployCommand(salmon.id, DemoSlotKind.Unit, 1));
            Assert.That(second.Accepted, Is.True);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 1).MaxHealth, Is.EqualTo(2));
            Assert.That(match.ApplyResolveChoice(
                match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, -1)).Accepted, Is.True);

            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.HasTriggeredEffect(true, match.GetObject(true, DemoSlotKind.Building, 0).InstanceId,
                "effect.or_007.01"), Is.False);
            match.ResetHand(new[] { salmon.id });
            var nextTurn = match.ApplyDeploy(salmon,
                match.CreateDeployCommand(salmon.id, DemoSlotKind.Unit, 2));
            Assert.That(nextTurn.Accepted, Is.True);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 2).MaxHealth, Is.EqualTo(3));
        }

        [Test]
        public void OceanMonumentEndPhaseDamagesOnlyTheIsolatedOpponent()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("or_008", out var monument), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(registry.TryGetDefinition("pf_003", out var wolf), Is.True);
            Assert.That(monument.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(System.Array.Empty<string>(), new[] { bee.id });
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            match.ResetHand(new[] { monument.id });
            Assert.That(match.ApplyDeploy(monument,
                match.CreateDeployCommand(monument.id, DemoSlotKind.Building, 0)).Accepted, Is.True);
            match.ResetOpponent(new[] { bee, bee, wolf }, new[] { 0, 1, 3 });

            var ended = match.ApplyEndTurn(match.CreateEndTurnCommand());
            Assert.That(ended.Accepted, Is.True);
            Assert.That(ended.Message, Does.Contain("1 个孤立敌方生物"));
            Assert.That(match.GetObject(false, DemoSlotKind.Unit, 0).Health, Is.EqualTo(2));
            Assert.That(match.GetObject(false, DemoSlotKind.Unit, 1).Health, Is.EqualTo(2));
            Assert.That(match.GetObject(false, DemoSlotKind.Unit, 3).Health, Is.EqualTo(1));
            Assert.That(match.BuildingSlots, Is.EqualTo(new[] { monument.id, monument.id, monument.id }));
        }

        [Test]
        public void TamedWolfBattlecryPermanentlyGrowsBesideAnAnimal()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("pf_003", out var wolf), Is.True);
            Assert.That(wolf.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { sheep.id, wolf.id });
            Assert.That(match.ApplyDeploy(sheep,
                match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 1)).Accepted, Is.True);

            var deployed = match.ApplyDeploy(wolf,
                match.CreateDeployCommand(wolf.id, DemoSlotKind.Unit, 2));
            Assert.That(deployed.Accepted, Is.True);
            Assert.That(deployed.Message, Does.Contain("忠诚战吼触发"));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 2).Health, Is.EqualTo(3));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 2).MaxHealth, Is.EqualTo(3));
        }

        [Test]
        public void TamedWolfBattlecryTriggersOnceAndIgnoresNonAdjacentAnimals()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("pf_003", out var wolf), Is.True);
            Assert.That(registry.TryGetDefinition("pf_004", out var villager), Is.True);

            var doubleMatch = CreateScenarioMatch();
            doubleMatch.ResetHand(new[] { bee.id, sheep.id, wolf.id });
            Assert.That(doubleMatch.ApplyDeploy(bee,
                doubleMatch.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            Assert.That(doubleMatch.ApplyDeploy(sheep,
                doubleMatch.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 2)).Accepted, Is.True);
            Assert.That(doubleMatch.ApplyDeploy(wolf,
                doubleMatch.CreateDeployCommand(wolf.id, DemoSlotKind.Unit, 1)).Accepted, Is.True);
            Assert.That(doubleMatch.GetObject(true, DemoSlotKind.Unit, 1).MaxHealth, Is.EqualTo(3));

            var ignoredMatch = CreateScenarioMatch();
            ignoredMatch.ResetHand(new[] { bee.id, villager.id, wolf.id });
            Assert.That(ignoredMatch.ApplyDeploy(bee,
                ignoredMatch.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            Assert.That(ignoredMatch.ApplyDeploy(villager,
                ignoredMatch.CreateDeployCommand(villager.id, DemoSlotKind.Unit, 1)).Accepted, Is.True);
            var ignored = ignoredMatch.ApplyDeploy(wolf,
                ignoredMatch.CreateDeployCommand(wolf.id, DemoSlotKind.Unit, 2));
            Assert.That(ignored.Accepted, Is.True);
            Assert.That(ignored.Message, Does.Contain("未触发"));
            Assert.That(ignoredMatch.GetObject(true, DemoSlotKind.Unit, 2).MaxHealth, Is.EqualTo(2));
        }

        [Test]
        public void VillagerFarmerBattlecryGeneratesWheatLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_004", out var farmer), Is.True);
            Assert.That(farmer.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { farmer.id });

            var deployed = match.ApplyDeploy(farmer,
                match.CreateDeployCommand(farmer.id, DemoSlotKind.Unit, 1));
            Assert.That(deployed.Accepted, Is.True);
            Assert.That(deployed.Message, Does.Contain("小麦置入手牌"));
            Assert.That(match.Hand, Is.EqualTo(new[] { "tk_002" }));
            Assert.That(match.HandCards.Single().cardId, Is.EqualTo("tk_002"));
            Assert.That(match.HandCards.Single().handCardInstanceId, Does.StartWith("local-hand-"));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 1).CardId, Is.EqualTo(farmer.id));
        }

        [Test]
        public void VillagerFarmerReplacesItselfAtTheLocalHandLimit()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_004", out var farmer), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { farmer.id, "tk_003", "tk_003", "tk_003", "tk_003", "tk_003", "tk_003" });

            var deployed = match.ApplyDeploy(farmer,
                match.CreateDeployCommand(farmer.id, DemoSlotKind.Unit, 0));
            Assert.That(deployed.Accepted, Is.True);
            Assert.That(match.Hand.Count, Is.EqualTo(7));
            Assert.That(match.Hand.Count(cardId => cardId == "tk_002"), Is.EqualTo(1));
            Assert.That(match.DiscardCount, Is.Zero);
        }

        [Test]
        public void SnowGolemBattlecryGeneratesPlayableSnowballLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("si_002", out var snowGolem), Is.True);
            Assert.That(registry.TryGetDefinition("si_001", out var snowball), Is.True);
            Assert.That(snowGolem.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { snowGolem.id });

            var deployed = match.ApplyDeploy(snowGolem,
                match.CreateDeployCommand(snowGolem.id, DemoSlotKind.Unit, 2));
            Assert.That(deployed.Accepted, Is.True);
            Assert.That(deployed.Message, Does.Contain("雪球置入手牌"));
            Assert.That(match.Hand, Is.EqualTo(new[] { snowball.id }));

            match.ResetOpponent(new[] { snowGolem }, new[] { 0 });
            var target = match.GetObject(false, DemoSlotKind.Unit, 0);
            var played = match.ApplyPlayCard(snowball,
                match.CreatePlayCardCommand(snowball.id, "UNIT", target.InstanceId));
            Assert.That(played.Accepted, Is.True);
            Assert.That(target.Attack, Is.Zero);
            Assert.That(match.Hand, Is.Empty);
        }

        [Test]
        public void WheatHealsFriendlyAnimalAndItsAttackExpiresAtTurnEndLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("tk_002", out var wheat), Is.True);
            Assert.That(wheat.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { sheep.id });
            Assert.That(match.ApplyDeploy(sheep,
                match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 1)).Accepted, Is.True);
            var target = match.GetObject(true, DemoSlotKind.Unit, 1);
            target.Health = 2;
            match.ResetHand(new[] { wheat.id });

            var played = match.ApplyPlayCard(wheat,
                match.CreatePlayCardCommand(wheat.id, "UNIT", target.InstanceId));
            Assert.That(played.Accepted, Is.True);
            Assert.That(played.Message, Does.Contain("恢复 1 点生命"));
            Assert.That(played.Message, Does.Contain("+1 攻击力"));
            Assert.That(target.Health, Is.EqualTo(3));
            Assert.That(target.Attack, Is.EqualTo(3));
            Assert.That(target.TemporaryAttackModifier, Is.EqualTo(1));

            match.EndPlayerTurn();
            Assert.That(target.Attack, Is.EqualTo(2));
            Assert.That(target.TemporaryAttackModifier, Is.Zero);
        }

        [Test]
        public void WoolStacksOnFriendlyUnitAndExpiresWithHealthClampedLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("tk_001", out var wool), Is.True);
            Assert.That(wool.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { sheep.id });
            Assert.That(match.ApplyDeploy(sheep,
                match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 1)).Accepted, Is.True);
            var target = match.GetObject(true, DemoSlotKind.Unit, 1);
            match.ResetDeckAndHand(new[] { wool.id, wool.id }, System.Array.Empty<string>());
            var woolInstanceIds = match.HandCards.Select(card => card.handCardInstanceId).ToArray();

            var first = match.ApplyPlayCard(wool,
                match.CreatePlayCardCommand(wool.id, "UNIT", target.InstanceId,
                    handCardInstanceId: woolInstanceIds[0]));
            var second = match.ApplyPlayCard(wool,
                match.CreatePlayCardCommand(wool.id, "UNIT", target.InstanceId,
                    handCardInstanceId: woolInstanceIds[1]));

            Assert.That(first.Accepted, Is.True, first.Message);
            Assert.That(second.Accepted, Is.True, second.Message);
            Assert.That(target.Health, Is.EqualTo(5));
            Assert.That(target.MaxHealth, Is.EqualTo(5));
            Assert.That(target.TemporaryHealthModifier, Is.EqualTo(2));
            Assert.That(target.TemporaryHealthModifierExpiresOnRound, Is.EqualTo(match.Round));
            Assert.That(match.DiscardPile, Is.EqualTo(new[] { wool.id, wool.id }));
            Assert.That(match.CardsPlayedThisTurn(true), Is.EqualTo(2));
            target.Health -= 1;

            match.EndPlayerTurn();

            Assert.That(target.Health, Is.EqualTo(3));
            Assert.That(target.MaxHealth, Is.EqualTo(3));
            Assert.That(target.TemporaryHealthModifier, Is.Zero);
            Assert.That(target.TemporaryHealthModifierExpiresOnRound, Is.Zero);
        }

        [Test]
        public void WoolPreservesDamageAndRejectsInvalidTargetsAtomicallyLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(registry.TryGetDefinition("db_004", out var fence), Is.True);
            Assert.That(registry.TryGetDefinition("tk_001", out var wool), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { bee.id });
            Assert.That(match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            var friendly = match.GetObject(true, DemoSlotKind.Unit, 0);
            friendly.Health = 1;
            match.ResetHand(new[] { fence.id });
            Assert.That(match.ApplyDeploy(fence,
                match.CreateDeployCommand(fence.id, DemoSlotKind.Building, 0)).Accepted, Is.True);
            var building = match.GetObject(true, DemoSlotKind.Building, 0);
            match.ResetOpponent(new[] { bee }, new[] { 0 });
            var enemy = match.GetObject(false, DemoSlotKind.Unit, 0);
            match.ResetDeckAndHand(new[] { wool.id }, System.Array.Empty<string>());
            var revisionBefore = match.Revision;
            var energyBefore = match.Energy;
            var invalidCommands = new[]
            {
                match.CreatePlayCardCommand(wool.id),
                match.CreatePlayCardCommand(wool.id, "UNIT", enemy.InstanceId),
                match.CreatePlayCardCommand(wool.id, "UNIT", building.InstanceId),
                match.CreatePlayCardCommand(wool.id, "HERO", friendly.InstanceId),
                match.CreatePlayCardCommand(wool.id, "UNIT", "departed-object")
            };

            foreach (var invalidCommand in invalidCommands)
            {
                var rejected = match.ApplyPlayCard(wool, invalidCommand);
                Assert.That(rejected.Accepted, Is.False);
                Assert.That(rejected.Code, Is.EqualTo(DemoCommandRejectionCode.InvalidTarget));
                Assert.That(match.Revision, Is.EqualTo(revisionBefore));
                Assert.That(match.Energy, Is.EqualTo(energyBefore));
                Assert.That(match.Hand, Is.EqualTo(new[] { wool.id }));
                Assert.That(match.DiscardPile, Is.Empty);
                Assert.That(match.CardsPlayedThisTurn(true), Is.Zero);
                Assert.That(friendly.Health, Is.EqualTo(1));
                Assert.That(friendly.TemporaryHealthModifier, Is.Zero);
            }

            var accepted = match.ApplyPlayCard(wool,
                match.CreatePlayCardCommand(wool.id, "UNIT", friendly.InstanceId));
            Assert.That(accepted.Accepted, Is.True, accepted.Message);
            Assert.That(friendly.Health, Is.EqualTo(2));
            Assert.That(friendly.MaxHealth, Is.EqualTo(3));
            match.EndPlayerTurn();
            Assert.That(friendly.Health, Is.EqualTo(2));
            Assert.That(friendly.MaxHealth, Is.EqualTo(2));
        }

        [Test]
        public void OceanMonumentLethalDamageAwardsTheEnemyDropLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("or_008", out var monument), Is.True);
            Assert.That(registry.TryGetDefinition("db_001", out var husk), Is.True);
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(System.Array.Empty<string>(), new[] { husk.id });
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            match.ResetHand(new[] { monument.id });
            Assert.That(match.ApplyDeploy(monument,
                match.CreateDeployCommand(monument.id, DemoSlotKind.Building, 0)).Accepted, Is.True);
            match.ResetOpponent(new[] { husk }, new[] { 3 });

            var ended = match.ApplyEndTurn(match.CreateEndTurnCommand());
            Assert.That(ended.Accepted, Is.True);
            Assert.That(match.GetObject(false, DemoSlotKind.Unit, 3), Is.Null);
            Assert.That(match.Hand, Does.Contain("tk_005"));
            Assert.That(ended.Message, Does.Contain("腐肉"));
        }

        [Test]
        public void IronGolemGainsPermanentStatsWhenAPlayerBuildingIsAlive()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_005", out var nursery), Is.True);
            Assert.That(registry.TryGetDefinition("pf_008", out var golemDefinition), Is.True);
            Assert.That(golemDefinition.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { nursery.id });
            Assert.That(match.ApplyDeploy(nursery,
                match.CreateDeployCommand(nursery.id, DemoSlotKind.Building, 0)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            match.ResetHand(new[] { golemDefinition.id });

            var result = match.ApplyDeploy(golemDefinition,
                match.CreateDeployCommand(golemDefinition.id, DemoSlotKind.Unit, 1));

            Assert.That(result.Accepted, Is.True, result.Message);
            var golem = match.GetObject(true, DemoSlotKind.Unit, 1);
            Assert.That(golem.Attack, Is.EqualTo(6));
            Assert.That(golem.Health, Is.EqualTo(8));
            Assert.That(golem.MaxHealth, Is.EqualTo(8));
            Assert.That(golem.Keywords, Does.Contain("TAUNT"));
            Assert.That(result.Message, Does.Contain("建筑共鸣战吼触发"));
        }

        [Test]
        public void IronGolemTreatsStructuresAsBuildingsAndDoesNotStackPerObject()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("db_007", out var temple), Is.True);
            Assert.That(registry.TryGetDefinition("pf_008", out var golemDefinition), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { temple.id });
            Assert.That(match.ApplyDeploy(temple,
                match.CreateDeployCommand(temple.id, DemoSlotKind.Building, 0)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            match.ResetHand(new[] { golemDefinition.id });

            var result = match.ApplyDeploy(golemDefinition,
                match.CreateDeployCommand(golemDefinition.id, DemoSlotKind.Unit, 0));

            Assert.That(result.Accepted, Is.True, result.Message);
            var golem = match.GetObject(true, DemoSlotKind.Unit, 0);
            Assert.That(golem.Attack, Is.EqualTo(6));
            Assert.That(golem.MaxHealth, Is.EqualTo(8));
        }

        [Test]
        public void IronGolemStaysAtBaseStatsWithoutAFriendlyBuilding()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_008", out var golemDefinition), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { golemDefinition.id });

            var result = match.ApplyDeploy(golemDefinition,
                match.CreateDeployCommand(golemDefinition.id, DemoSlotKind.Unit, 0));

            Assert.That(result.Accepted, Is.True, result.Message);
            var golem = match.GetObject(true, DemoSlotKind.Unit, 0);
            Assert.That(golem.Attack, Is.EqualTo(5));
            Assert.That(golem.Health, Is.EqualTo(7));
            Assert.That(golem.MaxHealth, Is.EqualTo(7));
            Assert.That(result.Message, Does.Contain("建筑共鸣战吼未触发"));
        }

        [Test]
        public void PolarBearAndWoolPreviewSequenceDistinguishesPermanentAndTemporaryStats()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("si_005", out var bearDefinition), Is.True);
            Assert.That(registry.TryGetDefinition("tk_001", out var woolDefinition), Is.True);
            var match = CreateScenarioMatch();
            match.ResetPlayerLife(15);
            match.ResetHand(new[] { bearDefinition.id });
            Assert.That(match.ApplyDeploy(bearDefinition,
                match.CreateDeployCommand(bearDefinition.id, DemoSlotKind.Unit, 1)).Accepted, Is.True);
            var bear = match.GetObject(true, DemoSlotKind.Unit, 1);
            match.ResetDeckAndHand(new[] { woolDefinition.id, woolDefinition.id }, System.Array.Empty<string>());
            var woolInstanceId = match.HandCards[0].handCardInstanceId;
            Assert.That(match.ApplyPlayCard(woolDefinition,
                match.CreatePlayCardCommand(woolDefinition.id, "UNIT", bear.InstanceId,
                    handCardInstanceId: woolInstanceId)).Accepted, Is.True);

            Assert.That(bear.Attack, Is.EqualTo(4));
            Assert.That(bear.TemporaryAttackModifier, Is.Zero);
            Assert.That(bear.Health, Is.EqualTo(7));
            Assert.That(bear.MaxHealth, Is.EqualTo(7));
            Assert.That(bear.TemporaryHealthModifier, Is.EqualTo(1));
            Assert.That(bear.Keywords, Does.Contain("TAUNT"));
            Assert.That(match.Hand, Is.EqualTo(new[] { woolDefinition.id }));

            match.EndPlayerTurn();
            Assert.That(bear.Attack, Is.EqualTo(4));
            Assert.That(bear.MaxHealth, Is.EqualTo(6));
            Assert.That(bear.Health, Is.EqualTo(6));
            Assert.That(bear.TemporaryHealthModifier, Is.Zero);
            Assert.That(bear.Keywords, Does.Contain("TAUNT"));
        }

        [Test]
        public void PolarBearChecksTheFifteenLifeThresholdOnlyWhenDeployed()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("si_005", out var polarBearDefinition), Is.True);
            Assert.That(polarBearDefinition.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            var lowLife = CreateScenarioMatch();
            lowLife.ResetPlayerLife(15);
            lowLife.ResetHand(new[] { polarBearDefinition.id });
            var empowered = lowLife.ApplyDeploy(polarBearDefinition,
                lowLife.CreateDeployCommand(polarBearDefinition.id, DemoSlotKind.Unit, 0));
            Assert.That(empowered.Accepted, Is.True, empowered.Message);
            var empoweredBear = lowLife.GetObject(true, DemoSlotKind.Unit, 0);
            Assert.That(empoweredBear.Attack, Is.EqualTo(4));
            Assert.That(empoweredBear.Health, Is.EqualTo(6));
            Assert.That(empoweredBear.MaxHealth, Is.EqualTo(6));
            Assert.That(empoweredBear.Keywords, Does.Contain("TAUNT"));
            Assert.That(empowered.Message, Does.Contain("寒地护卫触发"));
            lowLife.ResetPlayerLife(20);
            lowLife.EndPlayerTurn();
            Assert.That(empoweredBear.Attack, Is.EqualTo(4));

            var highLife = CreateScenarioMatch();
            highLife.ResetPlayerLife(16);
            highLife.ResetHand(new[] { polarBearDefinition.id });
            var normal = highLife.ApplyDeploy(polarBearDefinition,
                highLife.CreateDeployCommand(polarBearDefinition.id, DemoSlotKind.Unit, 0));
            Assert.That(normal.Accepted, Is.True, normal.Message);
            var normalBear = highLife.GetObject(true, DemoSlotKind.Unit, 0);
            Assert.That(normalBear.Attack, Is.EqualTo(3));
            Assert.That(normal.Message, Does.Contain("寒地护卫未触发"));
            highLife.ResetPlayerLife(15);
            highLife.EndPlayerTurn();
            Assert.That(normalBear.Attack, Is.EqualTo(3));
        }

        [Test]
        public void PolarBearResolvesAfterAnimalEntryGrowthLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_005", out var nursery), Is.True);
            Assert.That(registry.TryGetDefinition("si_005", out var polarBearDefinition), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { nursery.id });
            Assert.That(match.ApplyDeploy(nursery,
                match.CreateDeployCommand(nursery.id, DemoSlotKind.Building, 0)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            match.ResetPlayerLife(15);
            match.ResetHand(new[] { polarBearDefinition.id });

            var result = match.ApplyDeploy(polarBearDefinition,
                match.CreateDeployCommand(polarBearDefinition.id, DemoSlotKind.Unit, 0));

            Assert.That(result.Accepted, Is.True, result.Message);
            var polarBear = match.GetObject(true, DemoSlotKind.Unit, 0);
            Assert.That(polarBear.Attack, Is.EqualTo(4));
            Assert.That(polarBear.Health, Is.EqualTo(7));
            Assert.That(polarBear.MaxHealth, Is.EqualTo(7));
            Assert.That(result.Message.IndexOf("苗圃培育", System.StringComparison.Ordinal),
                Is.LessThan(result.Message.IndexOf("寒地护卫", System.StringComparison.Ordinal)));
        }

        [Test]
        public void VindicatorBuildingBattlecryExpiresAtTurnEndLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("cd_004", out var sensor), Is.True);
            Assert.That(registry.TryGetDefinition("cd_005", out var vindicatorDefinition), Is.True);
            Assert.That(vindicatorDefinition.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { sensor.id });
            Assert.That(match.ApplyDeploy(sensor,
                match.CreateDeployCommand(sensor.id, DemoSlotKind.Building, 0)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            match.ResetHand(new[] { vindicatorDefinition.id });

            var deployed = match.ApplyDeploy(vindicatorDefinition,
                match.CreateDeployCommand(vindicatorDefinition.id, DemoSlotKind.Unit, 1));
            Assert.That(deployed.Accepted, Is.True, deployed.Message);
            var vindicator = match.GetObject(true, DemoSlotKind.Unit, 1);
            Assert.That(vindicator.Attack, Is.EqualTo(6));
            Assert.That(vindicator.Health, Is.EqualTo(3));
            Assert.That(vindicator.TemporaryAttackModifier, Is.EqualTo(2));
            Assert.That(deployed.Message, Does.Contain("建筑伏击战吼触发"));

            match.EndPlayerTurn();
            Assert.That(vindicator.Attack, Is.EqualTo(4));
            Assert.That(vindicator.TemporaryAttackModifier, Is.Zero);
        }

        [Test]
        public void CactusFenceDamagesOnlyTheFirstUnitThatAttacksTheHeroEachTurn()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("db_004", out var fence), Is.True);
            Assert.That(fence.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { bee.id, sheep.id }, new[] { "pf_003" });
            match.ResetOpponent(new[] { fence });
            Assert.That(match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(sheep,
                match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 1)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            var firstAttacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var secondAttacker = match.GetObject(true, DemoSlotKind.Unit, 1);

            var first = match.ApplyAttack(match.CreateAttackCommand(firstAttacker.InstanceId, "HERO"));
            var second = match.ApplyAttack(match.CreateAttackCommand(secondAttacker.InstanceId, "HERO"));

            Assert.That(first.Accepted, Is.True, first.Message);
            Assert.That(firstAttacker.Health, Is.EqualTo(1));
            Assert.That(first.Message, Does.Contain("仙人掌围栏反击 1 次"));
            Assert.That(second.Accepted, Is.True, second.Message);
            Assert.That(secondAttacker.Health, Is.EqualTo(3));
            var source = match.GetObject(false, DemoSlotKind.Building, 0);
            Assert.That(match.HasTriggeredEffect(false, source.InstanceId, "effect.db_004.01"), Is.True);
            match.EndPlayerTurn();
            Assert.That(match.HasTriggeredEffect(false, source.InstanceId, "effect.db_004.01"), Is.False);
        }

        [Test]
        public void CactusFenceIgnoresHeroEquipmentAttacks()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("or_006", out var trident), Is.True);
            Assert.That(registry.TryGetDefinition("db_004", out var fence), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { trident.id });
            match.ResetOpponent(new[] { fence });
            Assert.That(match.ApplyPlayCard(trident,
                match.CreatePlayCardCommand(trident.id)).Accepted, Is.True);
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var result = match.ApplyAttack(match.CreateAttackCommand(MatchAttackerIds.Hero, "HERO"));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.OpponentLife, Is.EqualTo(28));
            Assert.That(match.PlayerEquipment.Durability, Is.EqualTo(2));
            var source = match.GetObject(false, DemoSlotKind.Building, 0);
            Assert.That(source.Health, Is.EqualTo(5));
            Assert.That(match.HasTriggeredEffect(false, source.InstanceId, "effect.db_004.01"), Is.False);
            Assert.That(result.Message, Does.Not.Contain("仙人掌围栏反击"));
        }

        [Test]
        public void CombatHintFontMaintainsMinimumScreenSizeWithoutInflatingReferenceResolution()
        {
            Assert.That(DemoUiMetrics.GetScreenReadableFontSize(15, 12f, 1f), Is.EqualTo(15));
            Assert.That(DemoUiMetrics.GetScreenReadableFontSize(15, 12f, 2f / 3f), Is.EqualTo(18));
            Assert.That(DemoUiMetrics.GetScreenReadableFontSize(15, 12f, 0.5f), Is.EqualTo(24));
        }

        [TestCase(1f, 14)]
        [TestCase(2f / 3f, 18)]
        [TestCase(0.5f, 24)]
        public void HudTypographyUsesScreenPixelsAndRestoresAuthoredSize(float scale, int expectedSize)
        {
            var root = new GameObject("ResponsiveHudText", typeof(RectTransform), typeof(Text));
            try
            {
                var text = root.GetComponent<Text>();
                var typography = root.AddComponent<DemoHudTypography>();
                typography.Configure(14);
                typography.ApplyScale(scale);
                Assert.That(text.fontSize, Is.EqualTo(expectedSize));
                Assert.That(text.fontSize * scale, Is.GreaterThanOrEqualTo(12f));
                Assert.That(text.resizeTextForBestFit, Is.False);
                typography.ApplyScale(1f);
                Assert.That(text.fontSize, Is.EqualTo(14), "Resize must not accumulate font inflation.");
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(1f)]
        [TestCase(2f / 3f)]
        public void HudEssentialLabelsFitAtReferenceAnd720pFontSizes(float scale)
        {
            var root = new GameObject("HudLabelReadingTest");
            try
            {
                root.AddComponent<DemoSceneController>().BuildNow();
                var canvas = root.transform.Find("DemoCanvas");
                var paths = new[] { "OnlineStatusPanel/Account", "OnlineStatusPanel/Deck",
                    "OnlineStatusPanel/Status", "OpponentEnergyPlate/Resource", "OpponentEquipment/Label",
                    "PlayerEquipment/Label", "EnergyPlate/Energy", "RoundPlate/Round", "HandLabel", "InspectHand/Label",
                    "FactionRail/Faction_plains_forest/Label" };
                foreach (var path in paths)
                {
                    var text = canvas.Find(path).GetComponent<Text>();
                    var typography = text.GetComponent<DemoHudTypography>();
                    Assert.That(typography, Is.Not.Null, path);
                    typography.ApplyScale(scale);
                    Assert.That(text.fontSize * scale, Is.GreaterThanOrEqualTo(12f), path);
                    var messages = path == "OnlineStatusPanel/Status"
                        ? new[] { "本地模式", "连接\n服务器", "寻找对手中\n沙漠", "正在重连\n第 999 次", "版本不兼容\n检查两端" }
                        : path == "OnlineStatusPanel/Account"
                            ? new[] { "游客 · 未登录", "游客 · 守护者…", "账户异常·重试" }
                        : path == "HandLabel"
                            ? new[] { "手牌 7/7 · 牌库 25（掩埋 10）· 弃牌 99" }
                        : path.EndsWith("Equipment/Label")
                            ? new[] { "装备槽 · 未装备", "激流三叉戟\n攻击 2 · 耐久 3/3" }
                        : path == "EnergyPlate/Energy"
                            ? new[] { "红石 ◆ 1/1", "红石 ◆ 12/10\n临时 +2" }
                        : new[] { text.text };
                    foreach (var message in messages)
                    {
                        text.text = message;
                        var settings = text.GetGenerationSettings(text.rectTransform.rect.size);
                        settings.verticalOverflow = VerticalWrapMode.Overflow;
                        var requiredHeight = new TextGenerator().GetPreferredHeight(message, settings) / text.pixelsPerUnit;
                        Assert.That(requiredHeight, Is.LessThanOrEqualTo(text.rectTransform.rect.height + 0.5f), path + ": " + message);
                    }
                }
                var stats = canvas.Find("HandLabel").GetComponent<RectTransform>();
                var energy = canvas.Find("EnergyPlate").GetComponent<RectTransform>();
                Assert.That(stats.anchoredPosition.x - stats.sizeDelta.x / 2f,
                    Is.GreaterThan(energy.anchoredPosition.x + energy.sizeDelta.x / 2f));
            }
            finally { Object.DestroyImmediate(root); }
        }

        [TestCase(DemoUiStyleClass.BasePanel)]
        [TestCase(DemoUiStyleClass.SecondaryButton)]
        [TestCase(DemoUiStyleClass.PrimaryActionButton)]
        public void HudReadingSurfacesAreOpaqueAndKeepActionHierarchy(DemoUiStyleClass style)
        {
            Assert.That(DemoUiStyleCatalog.GetBodyTint(style).a, Is.EqualTo(1f));
            Assert.That(DemoUiStyleCatalog.GetFrameTextureKey(style), Is.EqualTo("stone_bricks"));
            var neutral = DemoUiStyleCatalog.GetBodyTint(DemoUiStyleClass.BasePanel);
            var secondary = DemoUiStyleCatalog.GetBodyTint(DemoUiStyleClass.SecondaryButton);
            var primary = DemoUiStyleCatalog.GetBodyTint(DemoUiStyleClass.PrimaryActionButton);
            Assert.That(secondary.grayscale, Is.GreaterThan(neutral.grayscale));
            Assert.That(primary.r, Is.GreaterThan(primary.g * 2f));
        }

        [TestCase(1, 29, 0)]
        [TestCase(3, 30, 1)]
        public void HeroEquipmentAttackAgainstHeroConsumesArmorBeforeLife(int startingArmor, int expectedLife, int expectedArmor)
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("or_006", out var trident), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { trident.id });
            Assert.That(match.ApplyPlayCard(trident,
                match.CreatePlayCardCommand(trident.id)).Accepted, Is.True);
            SetPrivateProperty(match, nameof(DemoLocalMatch.OpponentArmor), startingArmor);
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var result = match.ApplyAttack(match.CreateAttackCommand(MatchAttackerIds.Hero, "HERO"));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.OpponentLife, Is.EqualTo(expectedLife));
            Assert.That(match.OpponentArmor, Is.EqualTo(expectedArmor));
            Assert.That(match.PlayerEquipment.Durability, Is.EqualTo(2));
        }

        [TestCase(1, 30, 0, 2)]
        [TestCase(0, 29, 0, 3)]
        public void UnitAttackAgainstHeroUsesArmorAndOnlyTriggersLifeLossEffectsForActualLifeDamage(
            int startingArmor, int expectedLife, int expectedArmor, int expectedPiglinAttack)
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(registry.TryGetDefinition("nt_002", out var piglinDefinition), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { bee.id });
            match.ResetOpponent(new[] { piglinDefinition });
            Assert.That(match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            SetPrivateProperty(match, nameof(DemoLocalMatch.OpponentArmor), startingArmor);
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);

            var beeObject = match.GetObject(true, DemoSlotKind.Unit, 0);
            var result = match.ApplyAttack(match.CreateAttackCommand(beeObject.InstanceId, "HERO"));
            var piglin = match.GetObject(false, DemoSlotKind.Unit, 0);

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.OpponentLife, Is.EqualTo(expectedLife));
            Assert.That(match.OpponentArmor, Is.EqualTo(expectedArmor));
            Assert.That(piglin.Attack, Is.EqualTo(expectedPiglinAttack));
            Assert.That(piglin.Health, Is.EqualTo(expectedPiglinAttack));
            Assert.That(piglin.MaxHealth, Is.EqualTo(expectedPiglinAttack));
        }

        [Test]
        public void CactusFencesStopAfterLethalDamageAndAwardTheDropToTheOpponent()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("db_001", out var husk), Is.True);
            Assert.That(registry.TryGetDefinition("db_004", out var fence), Is.True);
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { husk.id }, new[] { "db_002" });
            match.ResetOpponent(new[] { fence, fence });
            Assert.That(match.ApplyDeploy(husk,
                match.CreateDeployCommand(husk.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var firstFence = match.GetObject(false, DemoSlotKind.Building, 0);
            var secondFence = match.GetObject(false, DemoSlotKind.Building, 1);

            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "HERO"));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0), Is.Null);
            Assert.That(match.HasTriggeredEffect(false, firstFence.InstanceId, "effect.db_004.01"), Is.True);
            Assert.That(match.HasTriggeredEffect(false, secondFence.InstanceId, "effect.db_004.01"), Is.False);
            Assert.That(result.Message, Does.Contain("对手获得一张腐肉"));
        }

        [Test]
        public void EchoingDarknessAppliesOpponentStatusAndDrawsLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("cd_006", out var darkness), Is.True);
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { darkness.id }, new[] { "pf_001" });

            var result = match.ApplyPlayCard(darkness, match.CreatePlayCardCommand(darkness.id));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.HasPlayerStatus(false, "DARK"), Is.True);
            Assert.That(match.Hand, Does.Contain("pf_001"));
            Assert.That(match.CardsPlayedThisTurn(true), Is.EqualTo(1));
        }

        [Test]
        public void SculkSensorAppliesDarknessAfterSecondCardAndOnlyRowEdgesAreInitiallyLegal()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("cd_004", out var sensor), Is.True);
            Assert.That(registry.TryGetDefinition("db_002", out var sand), Is.True);
            Assert.That(registry.TryGetDefinition("si_001", out var snowball), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("pf_003", out var wolf), Is.True);
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { sand.id, sand.id }, System.Array.Empty<string>());
            var sandInstanceIds = match.HandCards.Select(card => card.handCardInstanceId).ToArray();
            match.ResetOpponent(new[] { bee, sheep, wolf, sensor }, new[] { 0, 1, 3 });

            Assert.That(match.ApplyPlayCard(sand, match.CreatePlayCardCommand(sand.id,
                handCardInstanceId: sandInstanceIds[0])).Accepted, Is.True);
            Assert.That(match.CardsPlayedThisTurn(true), Is.EqualTo(1));
            Assert.That(match.HasPlayerStatus(true, "DARK"), Is.False);
            var second = match.ApplyPlayCard(sand,
                match.CreatePlayCardCommand(sand.id, handCardInstanceId: sandInstanceIds[1]));
            Assert.That(second.Accepted, Is.True, second.Message);
            Assert.That(match.CardsPlayedThisTurn(true), Is.EqualTo(2));
            Assert.That(match.HasPlayerStatus(true, "DARK"), Is.True);

            match.ResetHand(new[] { snowball.id, snowball.id });
            var snowballInstanceId = match.HandCards[0].handCardInstanceId;
            var left = match.GetObject(false, DemoSlotKind.Unit, 0);
            var middle = match.GetObject(false, DemoSlotKind.Unit, 1);
            var right = match.GetObject(false, DemoSlotKind.Unit, 3);
            Assert.That(DemoCardTargeting.TryGetRule(snowball, out var rule), Is.True);
            Assert.That(DemoCardTargeting.IsLegalTarget(match, rule, false, DemoSlotKind.Unit, left), Is.True);
            Assert.That(DemoCardTargeting.IsLegalTarget(match, rule, false, DemoSlotKind.Unit, middle), Is.False);
            Assert.That(DemoCardTargeting.IsLegalTarget(match, rule, false, DemoSlotKind.Unit, right), Is.True);

            var rejected = match.ApplyPlayCard(snowball,
                match.CreatePlayCardCommand(snowball.id, "UNIT", middle.InstanceId,
                    handCardInstanceId: snowballInstanceId));
            Assert.That(rejected.Accepted, Is.False);
            Assert.That(match.HasTargetedEnemyObjectThisTurn(true), Is.False);
            var accepted = match.ApplyPlayCard(snowball,
                match.CreatePlayCardCommand(snowball.id, "UNIT", left.InstanceId,
                    handCardInstanceId: snowballInstanceId));
            Assert.That(accepted.Accepted, Is.True, accepted.Message);
            Assert.That(match.HasTargetedEnemyObjectThisTurn(true), Is.True);
            Assert.That(DemoCardTargeting.IsLegalTarget(match, rule, false, DemoSlotKind.Unit, middle), Is.True);

            match.EndPlayerTurn();
            Assert.That(match.HasPlayerStatus(true, "DARK"), Is.False);
            Assert.That(match.CardsPlayedThisTurn(true), Is.Zero);
            Assert.That(match.HasTargetedEnemyObjectThisTurn(true), Is.False);
        }

        [Test]
        public void AbandonedMineGeneratesCobblestoneOnlyAfterExactlyOneCardWasPlayed()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("cd_007", out var mine), Is.True);
            Assert.That(mine.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { mine.id }, System.Array.Empty<string>());

            var deployed = match.ApplyDeploy(mine,
                match.CreateDeployCommand(mine.id, DemoSlotKind.Building, 0));
            Assert.That(deployed.Accepted, Is.True, deployed.Message);
            Assert.That(match.CardsPlayedThisTurn(true), Is.EqualTo(1));

            var ended = match.ApplyEndTurn(match.CreateEndTurnCommand());
            Assert.That(ended.Accepted, Is.True, ended.Message);
            Assert.That(match.Hand, Does.Contain("tk_010"));
            Assert.That(ended.Message, Does.Contain("废弃矿井生成了 1 张圆石"));
        }

        [Test]
        public void AbandonedMineSendsCobblestoneToDiscardWhenTheHandIsFull()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("cd_007", out var mine), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { mine.id });
            Assert.That(match.ApplyDeploy(mine,
                match.CreateDeployCommand(mine.id, DemoSlotKind.Building, 0)).Accepted, Is.True);
            match.ResetHand(new[] { "pf_001", "pf_002", "pf_003", "pf_004", "db_001", "cd_001", "or_001" });

            var ended = match.ApplyEndTurn(match.CreateEndTurnCommand());

            Assert.That(ended.Accepted, Is.True, ended.Message);
            Assert.That(match.Hand, Has.Count.EqualTo(7));
            Assert.That(match.DiscardPile, Does.Contain("tk_010"));
        }

        [Test]
        public void WoodlandMansionSummonsVindicatorRecruitIntoTheLeftmostFreeUnitSlot()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("cd_008", out var mansion), Is.True);
            Assert.That(registry.TryGetDefinition("tk_011", out var recruit), Is.True);
            Assert.That(mansion.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(System.Array.Empty<string>(), new[] { "pf_001" });
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            match.ResetHand(new[] { mansion.id });
            Assert.That(match.ApplyDeploy(mansion,
                match.CreateDeployCommand(mansion.id, DemoSlotKind.Building, 0)).Accepted, Is.True);

            var ended = match.ApplyEndTurn(match.CreateEndTurnCommand());

            Assert.That(ended.Accepted, Is.True, ended.Message);
            var summoned = match.GetObject(true, DemoSlotKind.Unit, 0);
            Assert.That(summoned, Is.Not.Null);
            Assert.That(summoned.CardId, Is.EqualTo(recruit.id));
            Assert.That(summoned.Attack, Is.EqualTo(2));
            Assert.That(summoned.Health, Is.EqualTo(2));
            Assert.That(ended.Message, Does.Contain("林地府邸召唤了 1 个卫道士新兵"));
        }

        [Test]
        public void OpponentWoodlandMansionResolvesDuringTheSimulatedOpponentEndPhase()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("cd_008", out var mansion), Is.True);
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(System.Array.Empty<string>(), new[] { "pf_001" });
            match.ResetOpponent(new[] { mansion });

            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();

            var summoned = match.GetObject(false, DemoSlotKind.Unit, 0);
            Assert.That(summoned, Is.Not.Null);
            Assert.That(summoned.CardId, Is.EqualTo("tk_011"));
        }

        [Test]
        public void IceSpireSlowsUnitsSummonedIntoTheCurrentEmptyRowEdgeLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("si_008", out var iceSpire), Is.True);
            Assert.That(registry.TryGetDefinition("pf_007", out var rally), Is.True);
            Assert.That(iceSpire.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { rally.id }, new[] { "pf_001" });
            match.ResetOpponent(new[] { iceSpire });

            var result = match.ApplyPlayCard(rally, match.CreatePlayCardCommand(rally.id));

            Assert.That(result.Accepted, Is.True, result.Message);
            var companions = match.PlayerBattlefield.Where(value => value.CardId == "tk_004")
                .OrderBy(value => value.SlotIndex).ToArray();
            Assert.That(companions, Has.Length.EqualTo(2));
            Assert.That(companions.All(value => value.HasStatus("SLOW")), Is.True);
            Assert.That(companions.All(value => value.Attack == 2), Is.True);
            Assert.That(companions[0].Statuses[0].sourceCardId, Is.EqualTo("si_008"));
            Assert.That(companions[0].Statuses[0].sourcePlayerId, Is.EqualTo("local-opponent"));
        }

        [Test]
        public void IceSpireDoesNotSlowAUnitDeployedFromHandLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("si_008", out var iceSpire), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { sheep.id });
            match.ResetOpponent(new[] { iceSpire });

            var result = match.ApplyDeploy(sheep,
                match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 0));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0).HasStatus("SLOW"), Is.False);
        }

        [Test]
        public void PlayerIceSpireReactsToTheSimulatedOpponentMansionSummon()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("si_008", out var iceSpire), Is.True);
            Assert.That(registry.TryGetDefinition("cd_008", out var mansion), Is.True);
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { iceSpire.id }, new[] { "si_001" });
            match.ResetOpponent(new[] { mansion });
            Assert.That(match.ApplyDeploy(iceSpire,
                match.CreateDeployCommand(iceSpire.id, DemoSlotKind.Building, 0)).Accepted, Is.True);

            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();

            var recruit = match.GetObject(false, DemoSlotKind.Unit, 0);
            Assert.That(recruit, Is.Not.Null);
            Assert.That(recruit.CardId, Is.EqualTo("tk_011"));
            Assert.That(recruit.HasStatus("SLOW"), Is.True);
            Assert.That(recruit.Statuses[0].sourcePlayerId, Is.EqualTo("local-player"));
        }

        [Test]
        public void GoatVaultsAnAdjacentFriendlyUnitAndItsAttackBonusExpiresAtEndOfTurn()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("si_004", out var goat), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(goat.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { sheep.id });
            Assert.That(match.ApplyDeploy(sheep,
                match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            var target = match.GetObject(true, DemoSlotKind.Unit, 0);
            match.ResetHand(new[] { goat.id });

            Assert.That(DemoCardTargeting.TryGetRule(goat, out var rule), Is.True);
            Assert.That(rule.Optional, Is.True);
            Assert.That(DemoCardTargeting.HasLegalTarget(match, rule), Is.True);
            Assert.That(DemoCardTargeting.IsLegalTarget(match, rule, true, DemoSlotKind.Unit, target), Is.True);
            var result = match.ApplyDeploy(goat,
                match.CreateDeployCommand(goat.id, DemoSlotKind.Unit, 1, MatchPaymentMethods.Redstone, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0), Is.Null);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 2), Is.SameAs(target));
            var deployedGoat = match.GetObject(true, DemoSlotKind.Unit, 1);
            Assert.That(deployedGoat.CardId, Is.EqualTo(goat.id));
            Assert.That(deployedGoat.Attack, Is.EqualTo(4));
            Assert.That(deployedGoat.TemporaryAttackModifier, Is.EqualTo(1));
            Assert.That(match.PendingChoice, Is.Null);
            Assert.That(result.Message, Does.Contain("山羊将"));

            match.EndPlayerTurn();
            Assert.That(deployedGoat.Attack, Is.EqualTo(3));
            Assert.That(deployedGoat.TemporaryAttackModifier, Is.Zero);
        }

        [Test]
        public void TemporaryAttackAndHealthModifiersExpireTogetherInOfflineRules()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { bee.id });
            Assert.That(match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            var target = match.GetObject(true, DemoSlotKind.Unit, 0);
            var baseAttack = target.Attack;
            var baseMaxHealth = target.MaxHealth;
            target.Attack += 2;
            target.TemporaryAttackModifier = 2;
            target.TemporaryAttackModifierExpiresOnRound = match.Round;
            target.MaxHealth += 2;
            target.Health = target.MaxHealth;
            target.TemporaryHealthModifier = 2;
            target.TemporaryHealthModifierExpiresOnRound = match.Round;

            match.EndPlayerTurn();

            Assert.That(target.Attack, Is.EqualTo(baseAttack));
            Assert.That(target.MaxHealth, Is.EqualTo(baseMaxHealth));
            Assert.That(target.Health, Is.EqualTo(baseMaxHealth));
            Assert.That(target.TemporaryAttackModifier, Is.Zero);
            Assert.That(target.TemporaryAttackModifierExpiresOnRound, Is.Zero);
            Assert.That(target.TemporaryHealthModifier, Is.Zero);
            Assert.That(target.TemporaryHealthModifierExpiresOnRound, Is.Zero);
        }

        [Test]
        public void GoatMaySkipItsOptionalVaultAndRemainAtBaseAttack()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("si_004", out var goat), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { sheep.id });
            Assert.That(match.ApplyDeploy(sheep,
                match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            match.ResetHand(new[] { goat.id });

            var result = match.ApplyDeploy(goat,
                match.CreateDeployCommand(goat.id, DemoSlotKind.Unit, 1));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0).CardId, Is.EqualTo(sheep.id));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 1).Attack, Is.EqualTo(3));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 2), Is.Null);
            Assert.That(result.Message, Does.Contain("跳过"));
        }

        [Test]
        public void GoatRejectsABlockedVaultBeforeSpendingEnergyOrRemovingTheCard()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("si_004", out var goat), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { sheep.id, bee.id });
            Assert.That(match.ApplyDeploy(sheep,
                match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 2)).Accepted, Is.True);
            var target = match.GetObject(true, DemoSlotKind.Unit, 0);
            match.ResetHand(new[] { goat.id });
            var energyBefore = match.Energy;
            var revisionBefore = match.Revision;

            var result = match.ApplyDeploy(goat,
                match.CreateDeployCommand(goat.id, DemoSlotKind.Unit, 1, MatchPaymentMethods.Redstone, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.False);
            Assert.That(result.Code, Is.EqualTo(DemoCommandRejectionCode.InvalidTarget));
            Assert.That(match.Energy, Is.EqualTo(energyBefore));
            Assert.That(match.Revision, Is.EqualTo(revisionBefore));
            Assert.That(match.Hand, Does.Contain(goat.id));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 1), Is.Null);
        }

        [Test]
        public void GoatVaultFeedsExistingDolphinAndGuardianMovementReactions()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("si_004", out var goat), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("or_002", out var dolphin), Is.True);
            Assert.That(registry.TryGetDefinition("or_004", out var guardian), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { sheep.id, dolphin.id });
            Assert.That(match.ApplyDeploy(sheep,
                match.CreateDeployCommand(sheep.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(dolphin,
                match.CreateDeployCommand(dolphin.id, DemoSlotKind.Unit, 3)).Accepted, Is.True);
            match.ResetOpponent(new[] { guardian });
            var target = match.GetObject(true, DemoSlotKind.Unit, 0);
            match.ResetHand(new[] { goat.id });

            var result = match.ApplyDeploy(goat,
                match.CreateDeployCommand(goat.id, DemoSlotKind.Unit, 1, MatchPaymentMethods.Redstone, "UNIT", target.InstanceId));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(target.SlotIndex, Is.EqualTo(2));
            Assert.That(target.Attack, Is.EqualTo(3));
            Assert.That(target.Health, Is.EqualTo(2));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 1).Attack, Is.EqualTo(4));
            Assert.That(result.Message, Does.Contain("海豚向导触发 1 次"));
            Assert.That(result.Message, Does.Contain("守卫者射线触发 1 次"));
        }

        [Test]
        public void SnowHutOffersAnInWorldChoiceForTiedMostInjuredUnits()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("si_007", out var hut), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(hut.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { hut.id, bee.id, bee.id }, new[] { "pf_001" });
            var firstBeeInstanceId = match.HandCards[1].handCardInstanceId;
            Assert.That(match.ApplyDeploy(hut,
                match.CreateDeployCommand(hut.id, DemoSlotKind.Building, 0)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0,
                    handCardInstanceId: firstBeeInstanceId)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 2)).Accepted, Is.True);
            match.GetObject(true, DemoSlotKind.Unit, 0).Health = 1;
            match.GetObject(true, DemoSlotKind.Unit, 2).Health = 1;

            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();

            Assert.That(match.PendingChoice, Is.Not.Null);
            Assert.That(match.PendingChoice.kind, Is.EqualTo("HEAL_UNIT"));
            Assert.That(match.PendingChoice.options.Select(value => value.slotIndex), Is.EqualTo(new[] { 0, 2 }));
            var result = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, 1));
            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.PendingChoice, Is.Null);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0).Health, Is.EqualTo(1));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 2).Health, Is.EqualTo(2));
        }

        [Test]
        public void MultipleSnowHutsRecomputeTheMostInjuredUnitAfterAChoice()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("si_007", out var hut), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            var match = new DemoLocalMatch();
            match.ResetDeckAndHand(System.Array.Empty<string>(), new[] { "pf_001", "pf_001", "pf_001" });
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.MaxEnergy, Is.EqualTo(3), "two completed player turns grow the starting capacity from one to three");
            SetPrivateProperty(match, nameof(DemoLocalMatch.MaxEnergy), 8);
            SetPrivateProperty(match, nameof(DemoLocalMatch.Energy), 8);
            match.ResetHand(new[] { hut.id, hut.id, bee.id, bee.id });
            var handInstanceIds = match.HandCards.Select(card => card.handCardInstanceId).ToArray();
            Assert.That(match.ApplyDeploy(hut,
                match.CreateDeployCommand(hut.id, DemoSlotKind.Building, 0,
                    handCardInstanceId: handInstanceIds[0])).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(hut,
                match.CreateDeployCommand(hut.id, DemoSlotKind.Building, 1,
                    handCardInstanceId: handInstanceIds[1])).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0,
                    handCardInstanceId: handInstanceIds[2])).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(bee,
                match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 1,
                    handCardInstanceId: handInstanceIds[3])).Accepted, Is.True);
            match.GetObject(true, DemoSlotKind.Unit, 0).Health = 1;
            match.GetObject(true, DemoSlotKind.Unit, 1).Health = 1;

            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.PendingChoice, Is.Not.Null);
            var result = match.ApplyResolveChoice(match.CreateResolveChoiceCommand(match.PendingChoice.choiceId, 0));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.PendingChoice, Is.Null);
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0).Health, Is.EqualTo(2));
            Assert.That(match.GetObject(true, DemoSlotKind.Unit, 1).Health, Is.EqualTo(2));
            Assert.That(match.HasTriggeredEffect(true, match.GetObject(true, DemoSlotKind.Building, 0).InstanceId,
                "effect.si_007.01"), Is.True);
            Assert.That(match.HasTriggeredEffect(true, match.GetObject(true, DemoSlotKind.Building, 1).InstanceId,
                "effect.si_007.01"), Is.True);
        }

        [Test]
        public void SimulatedOpponentSnowHutUsesCanonicalLeftmostTieBreaker()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("si_007", out var hut), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(System.Array.Empty<string>(), new[] { "pf_001" });
            match.ResetOpponent(new[] { hut, bee, bee });
            match.GetObject(false, DemoSlotKind.Unit, 0).Health = 1;
            match.GetObject(false, DemoSlotKind.Unit, 2).Health = 1;

            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();

            Assert.That(match.GetObject(false, DemoSlotKind.Unit, 0).Health, Is.EqualTo(2));
            Assert.That(match.GetObject(false, DemoSlotKind.Unit, 2).Health, Is.EqualTo(1));
            Assert.That(match.PendingChoice, Is.Null);
        }

        [Test]
        public void EndCrystalDealsNormalDamageThroughArmorAtLocalEndPhase()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("ed_007", out var crystal), Is.True);
            Assert.That(crystal.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { crystal.id });
            Assert.That(match.ApplyDeploy(crystal,
                match.CreateDeployCommand(crystal.id, DemoSlotKind.Building, 0)).Accepted, Is.True);
            SetPrivateProperty(match, nameof(DemoLocalMatch.OpponentLife), 10);
            SetPrivateProperty(match, nameof(DemoLocalMatch.OpponentArmor), 1);

            var result = match.ApplyEndTurn(match.CreateEndTurnCommand());

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.OpponentArmor, Is.EqualTo(0));
            Assert.That(match.OpponentLife, Is.EqualTo(9));
            Assert.That(result.Message, Does.Contain("末影水晶"));
        }

        [Test]
        public void LethalLocalEndCrystalStopsBeforeTurnHandoff()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("ed_007", out var crystal), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { crystal.id });
            Assert.That(match.ApplyDeploy(crystal,
                match.CreateDeployCommand(crystal.id, DemoSlotKind.Building, 0)).Accepted, Is.True);
            SetPrivateProperty(match, nameof(DemoLocalMatch.OpponentLife), 2);

            var result = match.ApplyEndTurn(match.CreateEndTurnCommand());

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.IsFinished, Is.True);
            Assert.That(match.IsPlayerTurn, Is.True);
            Assert.That(match.OpponentLife, Is.EqualTo(0));
            Assert.That(result.Message, Does.Contain("胜利"));
        }

        [Test]
        public void DestroyedOpponentEndCrystalDealsTrueDamageToItsOwnerLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            Assert.That(registry.TryGetDefinition("ed_007", out var crystal), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { ironGolem.id });
            match.ResetOpponent(new[] { crystal });
            Assert.That(match.ApplyDeploy(ironGolem,
                match.CreateDeployCommand(ironGolem.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            SetPrivateProperty(match, nameof(DemoLocalMatch.OpponentLife), 2);
            SetPrivateProperty(match, nameof(DemoLocalMatch.OpponentArmor), 6);
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = match.GetObject(false, DemoSlotKind.Building, 0);

            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "BUILDING", target.InstanceId));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.IsFinished, Is.True);
            Assert.That(match.OpponentLife, Is.EqualTo(0));
            Assert.That(match.OpponentArmor, Is.EqualTo(6));
            Assert.That(result.Message, Does.Contain("末影水晶亡语"));
        }

        [Test]
        public void ImplementedPiglinGrowsOnlyOnFirstNonlethalSelfLossPerLocalTurn()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("nt_002", out var piglinDefinition), Is.True);
            Assert.That(registry.TryGetDefinition("nt_006", out var sacrifice), Is.True);
            Assert.That(piglinDefinition.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { "nt_006", "nt_006", "nt_002" },
                new[] { "nt_001", "nt_001", "nt_001", "nt_001" });
            var sacrificeInstanceIds = match.HandCards.Take(2).Select(card => card.handCardInstanceId).ToArray();
            Assert.That(match.ApplyDeploy(piglinDefinition,
                match.CreateDeployCommand("nt_002", DemoSlotKind.Unit, 0)).Accepted, Is.True);
            var piglin = match.GetObject(true, DemoSlotKind.Unit, 0);
            piglin.Attack = 4;
            piglin.Health = 1;
            piglin.TemporaryAttackModifier = 2;
            piglin.TemporaryAttackModifierExpiresOnRound = 1;

            Assert.That(match.TryCast(sacrifice, out _, sacrificeInstanceIds[0]), Is.True);
            Assert.That(piglin.Attack, Is.EqualTo(5));
            Assert.That(piglin.Health, Is.EqualTo(2));
            Assert.That(piglin.MaxHealth, Is.EqualTo(3));
            Assert.That(piglin.TemporaryAttackModifier, Is.EqualTo(2));
            Assert.That(match.TryCast(sacrifice, out _, sacrificeInstanceIds[1]), Is.True);
            Assert.That(piglin.Attack, Is.EqualTo(5));

            match.EndPlayerTurn();
            Assert.That(piglin.Attack, Is.EqualTo(3));
            Assert.That(piglin.TemporaryAttackModifier, Is.Zero);
            match.BeginNextPlayerTurn();
            match.ResetHand(new[] { "nt_006" });
            Assert.That(match.TryCast(sacrifice, out _), Is.True);
            Assert.That(piglin.Attack, Is.EqualTo(4));
            Assert.That(piglin.Health, Is.EqualTo(3));
            Assert.That(piglin.MaxHealth, Is.EqualTo(4));
        }

        [Test]
        public void PiglinMagmaSpendsTemporaryEnergyFirstAndExpiresOnlyTheRemainderLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("nt_002", out var piglinDefinition), Is.True);
            var match = CreateScenarioMatch();
            match.ResetHand(new[] { piglinDefinition.id });
            Assert.That(match.ApplyDeploy(piglinDefinition,
                match.CreateDeployCommand(piglinDefinition.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            SetPrivateProperty(match, nameof(DemoLocalMatch.Energy), 2);
            SetPrivateField(match, "_temporaryEnergy", 1);

            var result = match.ApplyEndTurn(match.CreateEndTurnCommand());

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.OpponentLife, Is.EqualTo(29));
            Assert.That(match.Energy, Is.EqualTo(1));
            Assert.That(match.TemporaryEnergy, Is.Zero);
            Assert.That(result.Message, Does.Contain("僵尸猪灵"));
        }

        [Test]
        public void PiglinMagmaUsesArmorAndLethalDamageStopsLaterPiglinsLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("nt_002", out var piglinDefinition), Is.True);
            var armored = CreateScenarioMatch();
            armored.ResetHand(new[] { piglinDefinition.id });
            Assert.That(armored.ApplyDeploy(piglinDefinition,
                armored.CreateDeployCommand(piglinDefinition.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            SetPrivateProperty(armored, nameof(DemoLocalMatch.Energy), 1);
            SetPrivateProperty(armored, nameof(DemoLocalMatch.OpponentArmor), 1);
            Assert.That(armored.ApplyEndTurn(armored.CreateEndTurnCommand()).Accepted, Is.True);
            Assert.That(armored.OpponentLife, Is.EqualTo(30));
            Assert.That(armored.OpponentArmor, Is.Zero);

            var lethal = CreateScenarioMatch();
            lethal.ResetHand(new[] { piglinDefinition.id, piglinDefinition.id });
            var piglinInstanceIds = lethal.HandCards.Select(card => card.handCardInstanceId).ToArray();
            Assert.That(lethal.ApplyDeploy(piglinDefinition,
                lethal.CreateDeployCommand(piglinDefinition.id, DemoSlotKind.Unit, 3,
                    handCardInstanceId: piglinInstanceIds[0])).Accepted, Is.True);
            Assert.That(lethal.ApplyDeploy(piglinDefinition,
                lethal.CreateDeployCommand(piglinDefinition.id, DemoSlotKind.Unit, 0,
                    handCardInstanceId: piglinInstanceIds[1])).Accepted, Is.True);
            SetPrivateProperty(lethal, nameof(DemoLocalMatch.Energy), 2);
            SetPrivateProperty(lethal, nameof(DemoLocalMatch.OpponentLife), 1);
            var lethalResult = lethal.ApplyEndTurn(lethal.CreateEndTurnCommand());
            Assert.That(lethalResult.Accepted, Is.True, lethalResult.Message);
            Assert.That(lethal.IsFinished, Is.True);
            Assert.That(lethal.OpponentLife, Is.Zero);
            Assert.That(lethal.Energy, Is.EqualTo(1));
            Assert.That(lethal.IsPlayerTurn, Is.True);
        }

        [Test]
        public void RespawnAnchorsGrantOnceInBuildingOrderAndTemporaryEnergyPaysFirstLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("nt_007", out var anchor), Is.True);
            Assert.That(registry.TryGetDefinition("nt_006", out var sacrifice), Is.True);
            Assert.That(anchor.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { anchor.id, anchor.id, sacrifice.id, sacrifice.id },
                new[] { "nt_001" });
            var handInstanceIds = match.HandCards.Select(card => card.handCardInstanceId).ToArray();
            Assert.That(match.ApplyDeploy(anchor,
                match.CreateDeployCommand(anchor.id, DemoSlotKind.Building, 2,
                    handCardInstanceId: handInstanceIds[0])).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(anchor,
                match.CreateDeployCommand(anchor.id, DemoSlotKind.Building, 0,
                    handCardInstanceId: handInstanceIds[1])).Accepted, Is.True);
            SetPrivateProperty(match, nameof(DemoLocalMatch.Energy), 2);

            Assert.That(match.TryCast(sacrifice, out _, handInstanceIds[2]), Is.True);
            Assert.That(match.TemporaryEnergy, Is.EqualTo(2));
            Assert.That(match.Energy, Is.EqualTo(3));
            Assert.That(match.TryCast(sacrifice, out _, handInstanceIds[3]), Is.True);
            Assert.That(match.TemporaryEnergy, Is.EqualTo(1));
            Assert.That(match.Energy, Is.EqualTo(2));

            var ended = match.ApplyEndTurn(match.CreateEndTurnCommand());
            Assert.That(ended.Accepted, Is.True, ended.Message);
            Assert.That(match.TemporaryEnergy, Is.Zero);
            Assert.That(match.Energy, Is.EqualTo(1));
        }

        [Test]
        public void RespawnAnchorIgnoresOpponentTurnDamageAndTriggersOnOwnTurnFatigueLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("nt_007", out var anchor), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);

            var offTurn = CreateScenarioMatch();
            offTurn.ResetDeckAndHand(new[] { bee.id }, new[] { "pf_002" });
            offTurn.ResetOpponent(new[] { anchor }, new[] { 0 });
            Assert.That(offTurn.ApplyDeploy(bee,
                offTurn.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            offTurn.EndPlayerTurn();
            offTurn.BeginNextPlayerTurn();
            Assert.That(offTurn.ApplyEnterCombat(offTurn.CreateEnterCombatCommand()).Accepted, Is.True);
            var attacker = offTurn.GetObject(true, DemoSlotKind.Unit, 0);
            Assert.That(offTurn.ApplyAttack(offTurn.CreateAttackCommand(attacker.InstanceId, "HERO")).Accepted, Is.True);
            Assert.That(GetPrivateIntField(offTurn, "_opponentTemporaryEnergy"), Is.Zero);

            var fatigue = CreateScenarioMatch();
            fatigue.ResetDeckAndHand(new[] { anchor.id }, new string[0]);
            Assert.That(fatigue.ApplyDeploy(anchor,
                fatigue.CreateDeployCommand(anchor.id, DemoSlotKind.Building, 1)).Accepted, Is.True);
            fatigue.EndPlayerTurn();
            var draw = fatigue.BeginNextPlayerTurn();
            Assert.That(draw.Outcome, Is.EqualTo(DemoDrawOutcome.Fatigue));
            Assert.That(fatigue.TemporaryEnergy, Is.EqualTo(1));
            Assert.That(fatigue.Energy, Is.EqualTo(fatigue.MaxEnergy + 1));
            fatigue.EndPlayerTurn();
            Assert.That(fatigue.TemporaryEnergy, Is.Zero);
            Assert.That(fatigue.Energy, Is.EqualTo(fatigue.MaxEnergy));
        }

        [Test]
        public void NetherTriggerLifecycleGrowsPiglinSpendsOneAnchorEnergyAndExpiresTheOtherLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("nt_002", out var piglinDefinition), Is.True);
            Assert.That(registry.TryGetDefinition("nt_007", out var anchorDefinition), Is.True);
            var match = new DemoLocalMatch();
            match.ResetDeckAndHand(new string[0], new[] { "nt_001", "nt_001", "nt_001" });
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.MaxEnergy, Is.EqualTo(3), "two completed player turns grow the starting capacity from one to three");
            SetPrivateProperty(match, nameof(DemoLocalMatch.MaxEnergy), 8);
            SetPrivateProperty(match, nameof(DemoLocalMatch.Energy), 8);

            match.ResetDeckAndHand(
                new[] { piglinDefinition.id, anchorDefinition.id, anchorDefinition.id }, new string[0]);
            var anchorInstanceIds = match.HandCards.Skip(1).Select(card => card.handCardInstanceId).ToArray();
            Assert.That(match.ApplyDeploy(piglinDefinition,
                match.CreateDeployCommand(piglinDefinition.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(anchorDefinition,
                match.CreateDeployCommand(anchorDefinition.id, DemoSlotKind.Building, 0,
                    handCardInstanceId: anchorInstanceIds[0])).Accepted, Is.True);
            Assert.That(match.ApplyDeploy(anchorDefinition,
                match.CreateDeployCommand(anchorDefinition.id, DemoSlotKind.Building, 2,
                    handCardInstanceId: anchorInstanceIds[1])).Accepted, Is.True);
            Assert.That(match.Energy, Is.Zero);

            match.EndPlayerTurn();
            var fatigue = match.BeginNextPlayerTurn();
            var piglin = match.GetObject(true, DemoSlotKind.Unit, 0);
            Assert.That(fatigue.Outcome, Is.EqualTo(DemoDrawOutcome.Fatigue));
            Assert.That(match.PlayerLife, Is.EqualTo(29));
            Assert.That(piglin.Attack, Is.EqualTo(3));
            Assert.That(piglin.Health, Is.EqualTo(3));
            Assert.That(piglin.MaxHealth, Is.EqualTo(3));
            Assert.That(match.MaxEnergy, Is.EqualTo(9));
            Assert.That(match.Energy, Is.EqualTo(11));
            Assert.That(match.TemporaryEnergy, Is.EqualTo(2));

            var ended = match.ApplyEndTurn(match.CreateEndTurnCommand());
            Assert.That(ended.Accepted, Is.True, ended.Message);
            Assert.That(match.OpponentLife, Is.EqualTo(29));
            Assert.That(match.Energy, Is.EqualTo(9));
            Assert.That(match.TemporaryEnergy, Is.Zero);
            Assert.That(ended.Message, Does.Contain("僵尸猪灵"));
        }

        [Test]
        public void NetherTriggerLifecyclePreviewLocksHandHintsDuringOpponentTurn()
        {
            var root = new GameObject("NetherTriggerLifecyclePreviewTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                typeof(DemoSceneController).GetMethod("SetupNetherTriggerLifecyclePreview",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);

                var lockedHint = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/TurnLockedHint");
                Assert.That(lockedHint, Is.Not.Null);
                Assert.That(lockedHint.GetComponent<UnityEngine.UI.Text>().text, Does.Contain("对手行动中"));
                var unitMarker = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Unit_1/InteractiveGround");
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.Zero,
                    "deployment highlights stay disabled during the opponent turn");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void NetherStatusSummonPreviewReplaysTheCompleteDeterministicChain()
        {
            var root = new GameObject("NetherStatusSummonPreviewTest");
            try
            {
                var battlefield = root.AddComponent<DemoBattlefield3D>();
                battlefield.Configure(
                    Shader.Find("Standard") ?? Shader.Find("Universal Render Pipeline/Lit"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                typeof(DemoSceneController).GetMethod("SetupNetherStatusSummonPreview",
                    BindingFlags.Instance | BindingFlags.NonPublic).Invoke(controller, null);
                var matchField = typeof(DemoSceneController).GetField("_match",
                    BindingFlags.Instance | BindingFlags.NonPublic);
                Assert.That(matchField, Is.Not.Null);
                var match = (DemoLocalMatch)matchField.GetValue(controller);

                Assert.That(match.Round, Is.EqualTo(5));
                Assert.That(match.IsPlayerTurn, Is.True);
                Assert.That(match.Phase, Is.EqualTo(DemoTurnPhase.Main));
                Assert.That(match.Hand, Is.EqualTo(new[] { "nt_004", "nt_005", "nt_008" }));
                Assert.That(match.GetObject(true, DemoSlotKind.Building, 0).CardId, Is.EqualTo("nt_008"));
                Assert.That(match.GetObject(true, DemoSlotKind.Unit, 0).CardId, Is.EqualTo("tk_015"));
                Assert.That(match.GetObject(true, DemoSlotKind.Unit, 1).CardId, Is.EqualTo("nt_005"));
                Assert.That(match.GetObject(true, DemoSlotKind.Unit, 2).CardId, Is.EqualTo("nt_004"));
                var sheep = match.GetObject(true, DemoSlotKind.Unit, 3);
                Assert.That(sheep, Is.Not.Null,
                    string.Join(", ", match.PlayerBattlefield.Select(value => $"{value.CardId}@{value.SlotIndex}")));
                Assert.That(sheep.CardId, Is.EqualTo("pf_002"));
                Assert.That(sheep.Health, Is.EqualTo(sheep.MaxHealth));
                Assert.That(sheep.HasStatus("FIRE"), Is.False);
                var turtle = match.GetObject(false, DemoSlotKind.Unit, 2);
                Assert.That(turtle.Health, Is.EqualTo(1));
                Assert.That(turtle.Statuses.Single(value => value.statusId == "WITHER").remainingDuration, Is.EqualTo(1));
                Assert.That(root.transform.Find("BattlefieldPieces/Piece_" + turtle.InstanceId + "_or_005/WitherStatusFx"), Is.Not.Null);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void SeededOpponentPiglinGrowsWhenItsHeroLosesLifeDuringPlayerCombat()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("nt_002", out var piglinDefinition), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { bee.id }, new[] { "pf_002" });
            match.ResetOpponent(new[] { piglinDefinition }, new[] { 1 });
            Assert.That(match.ApplyDeploy(bee, match.CreateDeployCommand(bee.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            match.EndPlayerTurn();
            match.BeginNextPlayerTurn();
            Assert.That(match.ApplyEnterCombat(match.CreateEnterCombatCommand()).Accepted, Is.True);
            var attacker = match.GetObject(true, DemoSlotKind.Unit, 0);
            var piglin = match.GetObject(false, DemoSlotKind.Unit, 1);

            var result = match.ApplyAttack(match.CreateAttackCommand(attacker.InstanceId, "HERO"));

            Assert.That(result.Accepted, Is.True, result.Message);
            Assert.That(match.OpponentLife, Is.LessThan(30));
            Assert.That(piglin.Attack, Is.EqualTo(3));
            Assert.That(piglin.Health, Is.EqualTo(3));
            Assert.That(piglin.MaxHealth, Is.EqualTo(3));
        }

        [Test]
        public void WitherSkeletonAppliesAfterActiveAndRetaliationDamageEvenWhenTheSourceDiesLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("nt_005", out var witherSkeleton), Is.True);
            Assert.That(registry.TryGetDefinition("pf_008", out var ironGolem), Is.True);
            Assert.That(witherSkeleton.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));

            var active = CreateScenarioMatch();
            active.ResetDeckAndHand(new[] { witherSkeleton.id }, new[] { "nt_001" });
            active.ResetOpponent(new[] { ironGolem });
            Assert.That(active.ApplyDeploy(witherSkeleton,
                active.CreateDeployCommand(witherSkeleton.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            active.EndPlayerTurn();
            active.BeginNextPlayerTurn();
            Assert.That(active.ApplyEnterCombat(active.CreateEnterCombatCommand()).Accepted, Is.True);
            var activeSource = active.GetObject(true, DemoSlotKind.Unit, 0);
            var activeTarget = active.GetObject(false, DemoSlotKind.Unit, 0);
            var activeResult = active.ApplyAttack(active.CreateAttackCommand(activeSource.InstanceId, "UNIT", activeTarget.InstanceId));
            Assert.That(activeResult.Accepted, Is.True, activeResult.Message);
            Assert.That(active.GetObject(true, DemoSlotKind.Unit, 0), Is.Null);
            Assert.That(activeTarget.Statuses.Single().statusId, Is.EqualTo("WITHER"));
            Assert.That(activeTarget.Statuses.Single().sourceInstanceId, Is.EqualTo(activeSource.InstanceId));
            Assert.That(activeResult.Message, Does.Contain("凋零"));

            var retaliation = CreateScenarioMatch();
            retaliation.ResetDeckAndHand(new[] { ironGolem.id }, new[] { "pf_001" });
            retaliation.ResetOpponent(new[] { witherSkeleton });
            Assert.That(retaliation.ApplyDeploy(ironGolem,
                retaliation.CreateDeployCommand(ironGolem.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            retaliation.EndPlayerTurn();
            retaliation.BeginNextPlayerTurn();
            Assert.That(retaliation.ApplyEnterCombat(retaliation.CreateEnterCombatCommand()).Accepted, Is.True);
            var retaliationAttacker = retaliation.GetObject(true, DemoSlotKind.Unit, 0);
            var retaliationSource = retaliation.GetObject(false, DemoSlotKind.Unit, 0);
            var retaliationResult = retaliation.ApplyAttack(
                retaliation.CreateAttackCommand(retaliationAttacker.InstanceId, "UNIT", retaliationSource.InstanceId));
            Assert.That(retaliationResult.Accepted, Is.True, retaliationResult.Message);
            Assert.That(retaliation.GetObject(false, DemoSlotKind.Unit, 0), Is.Null);
            Assert.That(retaliationAttacker.Statuses.Single().statusId, Is.EqualTo("WITHER"));
            Assert.That(retaliationAttacker.Statuses.Single().sourceInstanceId, Is.EqualTo(retaliationSource.InstanceId));
        }

        [Test]
        public void SeededWitherTicksTwiceAtItsControllersEndPhaseWithoutDeploymentTriggeringIt()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("pf_004", out var targetDefinition), Is.True);
            Assert.That(registry.TryGetDefinition("nt_005", out var witherSkeleton), Is.True);
            Assert.That(witherSkeleton.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { targetDefinition.id }, new[] { "pf_001" });
            Assert.That(match.ApplyDeploy(targetDefinition,
                match.CreateDeployCommand(targetDefinition.id, DemoSlotKind.Unit, 0)).Accepted, Is.True);
            var target = match.GetObject(true, DemoSlotKind.Unit, 0);
            target.Statuses = new[]
            {
                new BattlefieldStatusStateDto
                {
                    statusId = "WITHER", remainingDuration = 2, sourcePlayerId = "opponent",
                    sourceCardId = "nt_005", sourceInstanceId = "object-99", effectId = "effect.nt_005.01"
                }
            };
            var healthBefore = target.Health;

            var first = match.ApplyEndTurn(match.CreateEndTurnCommand());
            Assert.That(first.Accepted, Is.True, first.Message);
            Assert.That(target.Health, Is.EqualTo(healthBefore - 1));
            Assert.That(target.Statuses.Single().remainingDuration, Is.EqualTo(1));
            Assert.That(first.Message, Does.Contain("凋零造成 1 点真实伤害"));

            match.BeginNextPlayerTurn();
            var second = match.ApplyEndTurn(match.CreateEndTurnCommand());
            Assert.That(second.Accepted, Is.True, second.Message);
            Assert.That(target.Health, Is.EqualTo(healthBefore - 2));
            Assert.That(target.Statuses, Is.Empty);
            Assert.That(second.Message, Does.Contain("凋零造成 1 点真实伤害"));
        }

        [Test]
        public void NetherFortressSummonsTheTokenIntoTheLeftmostOpenOpponentSlotLocally()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("nt_008", out var fortress), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(fortress.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { "pf_001" }, new[] { "pf_002" });
            match.ResetOpponent(new[] { fortress, bee }, new[] { 2 });

            match.BeginNextPlayerTurn();

            var summoned = match.GetObject(false, DemoSlotKind.Unit, 0);
            Assert.That(summoned, Is.Not.Null);
            Assert.That(summoned.CardId, Is.EqualTo("tk_015"));
            Assert.That(summoned.Attack, Is.EqualTo(3));
            Assert.That(summoned.Health, Is.EqualTo(3));
            Assert.That(summoned.SummonedRound, Is.EqualTo(1));
            Assert.That(match.GetObject(false, DemoSlotKind.Building, 0).CardId, Is.EqualTo("nt_008"));
        }

        [Test]
        public void NetherFortressDoesNotSpendOrAllocateWhenTheLocalUnitRowIsFull()
        {
            var registry = CardContentLoader.Load();
            Assert.That(registry.TryGetDefinition("nt_008", out var fortress), Is.True);
            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(registry.TryGetDefinition("pf_002", out var sheep), Is.True);
            Assert.That(registry.TryGetDefinition("pf_003", out var wolf), Is.True);
            Assert.That(registry.TryGetDefinition("pf_004", out var farmer), Is.True);
            var match = CreateScenarioMatch();
            match.ResetDeckAndHand(new[] { "pf_001" }, new[] { "pf_002" });
            match.ResetOpponent(new[] { fortress, bee, sheep, wolf, farmer }, new[] { 0, 1, 2, 3 });

            match.BeginNextPlayerTurn();

            Assert.That(match.OpponentBattlefield.Count(value => value.CardId == "tk_015"), Is.Zero);
            Assert.That(match.OpponentBattlefield.Count(value => value.SlotKind == DemoSlotKind.Unit), Is.EqualTo(4));
        }

        [Test]
        public void AttackPresentationDescribesSidesDamageAndRetaliationFromTheViewersPerspective()
        {
            var format = typeof(DemoSceneController).GetMethod(
                "FormatAttackResolvedStatus", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(format, Is.Not.Null);
            var payload = new MatchEventPayloadDto
            {
                attackerPlayerId = "alice",
                targetPlayerId = "bob",
                attackerInstanceId = "attacker-1",
                targetType = "UNIT",
                damageToTarget = 4,
                damageToAttacker = 2
            };

            var aliceMessage = (string)format.Invoke(null, new object[] { payload, "alice", "铁傀儡", "僵尸" });
            var bobMessage = (string)format.Invoke(null, new object[] { payload, "bob", "铁傀儡", "僵尸" });

            Assert.That(aliceMessage, Is.EqualTo("己方铁傀儡攻击敌方单位僵尸，造成 4 点伤害；反击造成 2 点伤害。"));
            Assert.That(bobMessage, Is.EqualTo("敌方铁傀儡攻击己方单位僵尸，造成 4 点伤害；反击造成 2 点伤害。"));
        }

        [Test]
        public void AttackPresentationNamesHeroTargetsAndDoesNotShowEmptyRetaliation()
        {
            var format = typeof(DemoSceneController).GetMethod(
                "FormatAttackResolvedStatus", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(format, Is.Not.Null);
            var payload = new MatchEventPayloadDto
            {
                attackerPlayerId = "alice",
                targetPlayerId = "bob",
                targetType = "HERO",
                damageToTarget = 3,
                damageToAttacker = 0
            };

            var message = (string)format.Invoke(null, new object[] { payload, "alice", "弓箭手", "英雄" });

            Assert.That(message, Is.EqualTo("己方弓箭手攻击敌方英雄，造成 3 点伤害。"));
        }

        [Test]
        public void LocalCommandStatusShowsPlayerMessageWithoutInternalRevision()
        {
            var root = new GameObject("Local command status test");
            try
            {
                var controller = root.AddComponent<DemoSceneController>();
                var statusObject = new GameObject("Status", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text));
                statusObject.transform.SetParent(root.transform, false);
                var status = statusObject.GetComponent<Text>();
                SetControllerField(controller, "_statusText", status);
                var showResult = typeof(DemoSceneController).GetMethod(
                    "ShowLocalCommandResultStatus", BindingFlags.NonPublic | BindingFlags.Instance);
                Assert.That(showResult, Is.Not.Null);

                showResult.Invoke(controller, new object[]
                {
                    DemoCommandResult.Accept("造成 5 点伤害；反击造成 2 点伤害，目标死亡。", 4), null
                });
                Assert.That(status.text, Is.EqualTo("造成 5 点伤害；反击造成 2 点伤害，目标死亡。"));
                Assert.That(status.text, Does.Not.Contain("状态 r"));

                showResult.Invoke(controller, new object[]
                {
                    DemoCommandResult.Reject(DemoCommandRejectionCode.InvalidCommand, "目标当前不可用。", 5), null
                });
                Assert.That(status.text, Is.EqualTo("目标当前不可用。"));
                Assert.That(status.text, Does.Not.Contain("状态 r"));
            }
            finally
            {
                UnityEngine.Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void AttackLungePointMovesTowardTargetWithoutChangingElevationOrOvershooting()
        {
            var getLungePoint = typeof(DemoSceneController).GetMethod(
                "GetAttackLungePoint", BindingFlags.NonPublic | BindingFlags.Static);
            Assert.That(getLungePoint, Is.Not.Null);
            var start = new Vector3(1f, 0.22f, -2f);
            var target = new Vector3(3f, 1.4f, 4f);

            var impact = (Vector3)getLungePoint.Invoke(null, new object[] { start, target });

            Assert.That(impact.y, Is.EqualTo(start.y), "The lunge follows the board plane and must not jump vertically.");
            Assert.That(Vector3.Distance(start, impact), Is.EqualTo(0.95f).Within(0.001f),
                "Long attacks should cap their travel distance to avoid overlapping the target model.");
            Assert.That(Vector3.Dot(impact - start, new Vector3(target.x - start.x, 0f, target.z - start.z)),
                Is.GreaterThan(0f), "The attacker must lunge toward the target across the 2.5D board.");
        }

        private static CardUI FindHandCard(GameObject root, string cardId) =>
            root.transform.Find("DemoCanvas/HandPlate/HandCards").GetComponentsInChildren<CardUI>(true)
                .First(card => card.CardId == cardId && !string.IsNullOrEmpty(card.HandCardInstanceId));

        // Most effect tests need a stable resource budget so they can isolate card rules;
        // turn-economy tests intentionally construct DemoLocalMatch directly from 1/1.
        private static DemoLocalMatch CreateScenarioMatch(string arenaId = ArenaLayouts.DefaultArenaId)
        {
            var match = new DemoLocalMatch(arenaId);
            SetPrivateProperty(match, nameof(DemoLocalMatch.MaxEnergy), 6);
            SetPrivateProperty(match, nameof(DemoLocalMatch.Energy), 6);
            return match;
        }

        private static void SetControllerField(DemoSceneController controller, string fieldName, object value)
        {
            var field = typeof(DemoSceneController).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            field.SetValue(controller, value);
        }

        private static object GetControllerField(DemoSceneController controller, string fieldName)
        {
            var field = typeof(DemoSceneController).GetField(fieldName, BindingFlags.Instance | BindingFlags.NonPublic);
            Assert.That(field, Is.Not.Null, fieldName);
            return field.GetValue(controller);
        }

        private static float ProjectedWidth(Camera camera, Transform surface, Vector3[] vertices)
        {
            var min = vertices.Min(vertex => camera.WorldToViewportPoint(surface.TransformPoint(vertex)).x);
            var max = vertices.Max(vertex => camera.WorldToViewportPoint(surface.TransformPoint(vertex)).x);
            return max - min;
        }

        private sealed class FakeMatchGateway : IMatchGateway
        {
            public MatchConnectionStatus CurrentStatus { get; private set; }

            public event System.Action<MatchEventBatchDto> EventBatchReceived { add { } remove { } }
            public event System.Action<MatchStateDto> SnapshotReceived { add { } remove { } }
            public event System.Action<CommandRejectionDto> CommandRejected { add { } remove { } }
            public event System.Action<System.Exception> Faulted { add { } remove { } }
            public event System.Action<MatchConnectionStatus> ConnectionStateChanged { add { } remove { } }

            public FakeMatchGateway(MatchConnectionStatus status) => CurrentStatus = status;
            public void SetStatus(MatchConnectionStatus status) => CurrentStatus = status;
            public Task ConnectAsync() => Task.CompletedTask;
            public Task SendCommandAsync(MatchCommandDto command) => Task.CompletedTask;
            public Task DisconnectAsync() => Task.CompletedTask;
            public void Dispose() { }
        }
    }
}
