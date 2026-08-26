using BiomeRivals.Content;
using BiomeRivals.Core;
using BiomeRivals.Demo.Editor;
using NUnit.Framework;
using System.Linq;
using UnityEditor;
using UnityEngine;

namespace BiomeRivals.Demo.Tests
{
    public sealed class DemoLocalMatchTests
    {
        [Test]
        public void LocalDemoSupportsDeployCastAndTurnLoop()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "pf_001", "pf_005", "tk_016" });

            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(match.TryDeploy(bee, DemoSlotKind.Unit, 0, out _), Is.True);
            Assert.That(match.UnitSlots[0], Is.EqualTo("pf_001"));
            Assert.That(match.Energy, Is.EqualTo(5));

            Assert.That(registry.TryGetDefinition("pf_005", out var nursery), Is.True);
            Assert.That(match.TryDeploy(nursery, DemoSlotKind.Building, 0, out _), Is.True);
            Assert.That(match.BuildingSlots[0], Is.EqualTo("pf_005"));
            Assert.That(match.Energy, Is.EqualTo(3));

            Assert.That(registry.TryGetDefinition("tk_016", out var shell), Is.True);
            Assert.That(match.TryCast(shell, out var castMessage), Is.True);
            Assert.That(castMessage, Does.Contain("2 点护甲"));
            Assert.That(match.Energy, Is.EqualTo(2));
            Assert.That(match.PlayerArmor, Is.EqualTo(2));
            Assert.That(match.DiscardPile, Does.Contain("tk_016"));

            match.EndPlayerTurn();
            Assert.That(match.IsPlayerTurn, Is.False);
            match.BeginNextPlayerTurn();
            Assert.That(match.IsPlayerTurn, Is.True);
            Assert.That(match.Round, Is.EqualTo(2));
            Assert.That(match.Energy, Is.EqualTo(7));
        }

        [Test]
        public void LocalTurnStartDrawsBurnsAndAppliesEscalatingFatigue()
        {
            var drawing = new DemoLocalMatch();
            drawing.ResetDeckAndHand(new[] { "pf_001" }, new[] { "pf_002", "pf_003" });
            drawing.EndPlayerTurn();
            var draw = drawing.BeginNextPlayerTurn();
            Assert.That(draw.Outcome, Is.EqualTo(DemoDrawOutcome.Drawn));
            Assert.That(draw.CardId, Is.EqualTo("pf_003"));
            Assert.That(drawing.Hand, Does.Contain("pf_003"));
            Assert.That(drawing.Deck, Has.Count.EqualTo(1));

            var burning = new DemoLocalMatch();
            burning.ResetDeckAndHand(
                new[] { "pf_001", "pf_002", "pf_003", "pf_004", "pf_005", "pf_006", "pf_007" },
                new[] { "pf_008" });
            burning.EndPlayerTurn();
            var burn = burning.BeginNextPlayerTurn();
            Assert.That(burn.Outcome, Is.EqualTo(DemoDrawOutcome.Burned));
            Assert.That(burning.Hand, Has.Count.EqualTo(7));
            Assert.That(burning.DiscardPile, Is.EqualTo(new[] { "pf_008" }));

            var fatiguing = new DemoLocalMatch();
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

            var sacrificeMatch = new DemoLocalMatch();
            sacrificeMatch.ResetDeckAndHand(new[] { "nt_006" }, new[] { "nt_001" });
            Assert.That(registry.TryGetDefinition("nt_006", out var sacrifice), Is.True);
            Assert.That(sacrifice.effectImplementationStatus, Is.EqualTo("IMPLEMENTED"));
            Assert.That(sacrificeMatch.TryCast(sacrifice, out var sacrificeMessage), Is.True);
            Assert.That(sacrificeMatch.PlayerLife, Is.EqualTo(28));
            Assert.That(sacrificeMatch.Hand, Is.EqualTo(new[] { "nt_001" }));
            Assert.That(sacrificeMessage, Does.Contain("真实伤害"));

            var fleshMatch = new DemoLocalMatch();
            fleshMatch.ResetDeckAndHand(new[] { "tk_005" }, new string[0]);
            Assert.That(registry.TryGetDefinition("tk_005", out var flesh), Is.True);
            Assert.That(fleshMatch.TryCast(flesh, out _), Is.True);
            Assert.That(fleshMatch.PlayerLife, Is.EqualTo(29));

            var pendingMatch = new DemoLocalMatch();
            pendingMatch.ResetDeckAndHand(new[] { "pf_006" }, new string[0]);
            Assert.That(registry.TryGetDefinition("pf_006", out var pending), Is.True);
            Assert.That(pendingMatch.TryCast(pending, out var pendingMessage), Is.False);
            Assert.That(pendingMatch.Hand, Does.Contain("pf_006"));
            Assert.That(pendingMatch.Energy, Is.EqualTo(6));
            Assert.That(pendingMessage, Does.Contain("尚未接入"));
        }

        [Test]
        public void TargetedSnowballUsesStableInstanceAndExpiresAtEndOfTurn()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
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
        public void StructureRequiresConsecutiveBuildingSlots()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
            match.ResetHand(new[] { "db_007" });
            Assert.That(registry.TryGetDefinition("db_007", out var temple), Is.True);

            Assert.That(match.TryDeploy(temple, DemoSlotKind.Building, 2, out var error), Is.False);
            Assert.That(error, Does.Contain("连续 2"));
            Assert.That(match.TryDeploy(temple, DemoSlotKind.Building, 1, out _), Is.True);
            Assert.That(match.BuildingSlots[1], Is.EqualTo("db_007"));
            Assert.That(match.BuildingSlots[2], Is.EqualTo("db_007"));
        }

        [Test]
        public void DeployCommandUsesSharedFieldsAndRevisionGuard()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
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
            var match = new DemoLocalMatch();
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
        public void LocalCombatCanDefeatOpponentHero()
        {
            var registry = CardContentLoader.Load();
            var match = new DemoLocalMatch();
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
                    Shader.Find("BiomeRivals/Demo/CompositeBackdrop"),
                    Shader.Find("BiomeRivals/Demo/GroundSurface"),
                    AssetDatabase.LoadAssetAtPath<Texture2D>(DemoSceneBuilder.BackgroundPath));
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                var battlefield = root.GetComponent<DemoBattlefield3D>();
                Assert.That(battlefield, Is.Not.Null);
                Assert.That(battlefield.BoardCamera, Is.Not.Null);
                Assert.That(battlefield.BoardCamera.orthographic, Is.False);
                Assert.That(battlefield.BoardCamera.fieldOfView, Is.EqualTo(42.5f).Within(0.01f));
                Assert.That(root.transform.Find("BattlefieldGeometry"), Is.Not.Null);
                Assert.That(root.transform.Find("BattlefieldPieces"), Is.Not.Null);
                var unitMarker = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Unit_0/InteractiveGround");
                var opponentUnitMarker = root.transform.Find("BattlefieldGeometry/SlotMarker_Opponent_Unit_0/InteractiveGround");
                var buildingMarker = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Building_0/InteractiveGround");
                var unitRiser = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Unit_0/GroundRiser");
                Assert.That(unitMarker, Is.Not.Null);
                Assert.That(buildingMarker, Is.Not.Null);
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
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_UseScreenProjection"), Is.EqualTo(1f));
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name, Is.EqualTo("field-plains_forest-v1"));
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name, Is.EqualTo("field-nether-far-v1"));
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.GreaterThan(0f));
                Assert.That(buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.Zero);
                Physics.SyncTransforms();
                var unitScreenPosition = battlefield.BoardCamera.WorldToScreenPoint(unitMarker.TransformPoint(unitMarker.GetComponent<MeshFilter>().sharedMesh.bounds.center));
                Assert.That(battlefield.TryRaycastSlot(unitScreenPosition, out var raycastTarget), Is.True);
                Assert.That(raycastTarget, Is.SameAs(unitTarget));
                var opponentScreenPosition = battlefield.BoardCamera.WorldToScreenPoint(opponentUnitMarker.TransformPoint(opponentUnitMarker.GetComponent<MeshFilter>().sharedMesh.bounds.center));
                Assert.That(battlefield.TryRaycastSlot(opponentScreenPosition, out var opponentRaycastTarget), Is.True);
                Assert.That(opponentRaycastTarget.Player, Is.False);
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
                Assert.That(scaler.referencePixelsPerUnit, Is.EqualTo(DemoUiMetrics.PixelsPerUnit));
                Assert.That(GameObject.Find("EndTurnButton"), Is.Not.Null);
                Assert.That(GameObject.Find("Faction_plains_forest"), Is.Not.Null);
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
                Assert.That(root.transform.Find("DemoCanvas/EndTurnButton/FrameSlice").GetComponent<UnityEngine.UI.Image>().sprite.texture.name, Does.Contain("prismarine_bricks"));
                var endTurn = root.transform.Find("DemoCanvas/EndTurnButton");
                Assert.That(endTurn.GetComponent<PrimaryActionButton>(), Is.Not.Null);
                Assert.That(endTurn.GetComponent<SecondaryButton>(), Is.Null);
                Assert.That(root.GetComponentsInChildren<PrimaryActionButton>(true), Has.Length.EqualTo(1));
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
                Assert.That(root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent").GetComponent<RectTransform>().sizeDelta, Is.EqualTo(new Vector2(286f, 701f)));
                var themedCard = GameObject.Find("Card_pf_001");
                Assert.That(themedCard, Is.Not.Null);
                var handCard = root.transform.Find("DemoCanvas/HandPlate/HandCards/Card_pf_001").GetComponent<CardUI>();
                var detailCard = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/Card_pf_001").GetComponent<CardUI>();
                var detailsView = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent").GetComponent<CardDetailsView>();
                Assert.That(handCard, Is.Not.Null);
                Assert.That(detailCard, Is.Not.Null);
                Assert.That(handCard.CardId, Is.EqualTo(detailCard.CardId));
                Assert.That(handCard.IsCompact, Is.True);
                Assert.That(detailCard.IsCompact, Is.False);
                Assert.That(detailsView.CurrentCard, Is.SameAs(detailCard));
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
                Assert.That(themedRules.rectTransform.anchoredPosition.x, Is.Zero.Within(0.001f));
                Assert.That(themedRules.rectTransform.anchoredPosition.y, Is.EqualTo(-themedCard.GetComponent<RectTransform>().sizeDelta.y * 0.21f).Within(0.01f));
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
                for (var mapping = 0; mapping < frameMappings.GetLength(0); mapping++)
                {
                    var themeId = frameMappings[mapping, 0];
                    var prefix = frameMappings[mapping, 1];
                    GameObject.Find("Faction_" + themeId).GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                    Assert.That(battlefield.PlayerFactionId, Is.EqualTo(themeId));
                    Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name, Is.EqualTo("field-" + themeId + "-v1"));
                    var mappedCardId = prefix + "_001";
                    var card = GameObject.Find("Card_" + mappedCardId);
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

                var backdropRenderer = root.transform.Find("BattlefieldCamera/IllustratedBattlefieldBackdrop").GetComponent<MeshRenderer>();
                Assert.That(backdropRenderer.sharedMaterial.shader.name, Is.EqualTo("BiomeRivals/Demo/CompositeBackdrop"));
                Assert.That(backdropRenderer.sharedMaterial.GetTexture("_PlayerTex").name, Is.EqualTo("field-end-v1"));
                Assert.That(backdropRenderer.sharedMaterial.GetTexture("_OpponentTex").name, Is.EqualTo("field-nether-far-v1"));
                var nextOpponent = root.transform.Find("DemoCanvas/OpponentFactionSelector/NextOpponentFaction").GetComponent<UnityEngine.UI.Button>();
                nextOpponent.onClick.Invoke();
                Assert.That(battlefield.OpponentFactionId, Is.EqualTo("end"));
                Assert.That(opponentUnitMarker.GetComponent<MeshRenderer>().sharedMaterial.mainTexture.name, Is.EqualTo("field-end-far-v1"));
                Assert.That(backdropRenderer.sharedMaterial.GetTexture("_OpponentTex").name, Is.EqualTo("field-end-far-v1"));
                Assert.That(root.transform.Find("DemoCanvas/OpponentFactionSelector/FactionLabel").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("敌方 · 末地"));
                Assert.That(root.transform.Find("DemoCanvas/OpponentHUD/Name").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("虚空行者"));

                GameObject.Find("Faction_nether").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                Assert.That(root.transform.Find("DemoCanvas/PlayerHUD/Name").GetComponent<UnityEngine.UI.Text>().text, Is.EqualTo("熔岩统御者"));
                root.transform.Find("DemoCanvas/HandPlate/HandCards/Card_nt_006").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                var implementedCast = root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/Cast").GetComponent<UnityEngine.UI.Button>();
                Assert.That(implementedCast.interactable, Is.True);
                Assert.That(implementedCast.GetComponentInChildren<UnityEngine.UI.Text>().text, Is.EqualTo("释放卡牌"));
                Assert.That(root.transform.Find("DemoCanvas/CardDetailsPanel/InspectorContent/Implementation").GetComponent<UnityEngine.UI.Text>().text, Does.Contain("已接入"));

                GameObject.Find("Faction_snow_ice").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
                root.transform.Find("DemoCanvas/HandPlate/HandCards/Card_si_001").GetComponent<UnityEngine.UI.Button>().onClick.Invoke();
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
                Assert.That(combatHand.alpha, Is.EqualTo(0.52f).Within(0.001f));
                Assert.That(combatHand.blocksRaycasts, Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static float ProjectedWidth(Camera camera, Transform surface, Vector3[] vertices)
        {
            var min = vertices.Min(vertex => camera.WorldToViewportPoint(surface.TransformPoint(vertex)).x);
            var max = vertices.Max(vertex => camera.WorldToViewportPoint(surface.TransformPoint(vertex)).x);
            return max - min;
        }
    }
}
