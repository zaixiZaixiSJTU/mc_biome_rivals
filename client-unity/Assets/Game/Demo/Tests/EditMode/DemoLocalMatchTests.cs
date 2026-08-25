using BiomeRivals.Content;
using BiomeRivals.Demo.Editor;
using NUnit.Framework;
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
            match.ResetHand(new[] { "pf_001", "pf_005", "pf_006" });

            Assert.That(registry.TryGetDefinition("pf_001", out var bee), Is.True);
            Assert.That(match.TryDeploy(bee, DemoSlotKind.Unit, 0, out _), Is.True);
            Assert.That(match.UnitSlots[0], Is.EqualTo("pf_001"));
            Assert.That(match.Energy, Is.EqualTo(5));

            Assert.That(registry.TryGetDefinition("pf_005", out var nursery), Is.True);
            Assert.That(match.TryDeploy(nursery, DemoSlotKind.Building, 0, out _), Is.True);
            Assert.That(match.BuildingSlots[0], Is.EqualTo("pf_005"));
            Assert.That(match.Energy, Is.EqualTo(3));

            Assert.That(registry.TryGetDefinition("pf_006", out var season), Is.True);
            Assert.That(match.TryCast(season, out var castMessage), Is.True);
            Assert.That(castMessage, Does.Contain("后续版本"));
            Assert.That(match.Energy, Is.Zero);

            match.EndPlayerTurn();
            Assert.That(match.IsPlayerTurn, Is.False);
            match.BeginNextPlayerTurn();
            Assert.That(match.IsPlayerTurn, Is.True);
            Assert.That(match.Round, Is.EqualTo(2));
            Assert.That(match.Energy, Is.EqualTo(7));
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
                var buildingMarker = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Building_0/InteractiveGround");
                var unitRiser = root.transform.Find("BattlefieldGeometry/SlotMarker_Player_Unit_0/GroundRiser");
                Assert.That(unitMarker, Is.Not.Null);
                Assert.That(buildingMarker, Is.Not.Null);
                Assert.That(unitRiser, Is.Not.Null);
                Assert.That(unitMarker.GetComponent<MeshFilter>().sharedMesh.vertexCount, Is.EqualTo(60));
                Assert.That(unitMarker.GetComponent<MeshRenderer>().enabled, Is.True);
                Assert.That(buildingMarker.GetComponent<MeshRenderer>().enabled, Is.True);
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.shader.name, Is.EqualTo("BiomeRivals/Demo/GroundSurface"));
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_UseScreenProjection"), Is.Zero);
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.GreaterThan(0f));
                Assert.That(buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.Zero);
                Assert.That(root.transform.Find("DemoCanvas"), Is.Not.Null);
                var canvas = root.transform.Find("DemoCanvas").GetComponent<UnityEngine.Canvas>();
                var scaler = root.transform.Find("DemoCanvas").GetComponent<UnityEngine.UI.CanvasScaler>();
                Assert.That(canvas.pixelPerfect, Is.True);
                Assert.That(scaler.referencePixelsPerUnit, Is.EqualTo(DemoUiMetrics.PixelsPerUnit));
                Assert.That(GameObject.Find("EndTurnButton"), Is.Not.Null);
                Assert.That(GameObject.Find("Faction_plains_forest"), Is.Not.Null);
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
                Assert.That(root.transform.Find("DemoCanvas/CardDetailsPanel/FrameSlice").GetComponent<UnityEngine.UI.Image>().sprite.texture.name, Does.Contain("stone_bricks"));
                Assert.That(root.transform.Find("DemoCanvas/HandPlate/FrameSlice").GetComponent<UnityEngine.UI.Image>().sprite.texture.name, Does.Contain("stone_bricks"));
                Assert.That(root.transform.Find("DemoCanvas/EndTurnButton/FrameSlice").GetComponent<UnityEngine.UI.Image>().sprite.texture.name, Does.Contain("prismarine_bricks"));
                var endTurn = root.transform.Find("DemoCanvas/EndTurnButton");
                Assert.That(endTurn.GetComponent<PrimaryActionButton>(), Is.Not.Null);
                Assert.That(endTurn.GetComponent<SecondaryButton>(), Is.Null);
                Assert.That(root.GetComponentsInChildren<PrimaryActionButton>(true), Has.Length.EqualTo(1));
                var factionButtons = root.GetComponentsInChildren<SecondaryButton>(true);
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
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
