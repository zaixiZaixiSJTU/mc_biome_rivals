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

            var root = new GameObject("DemoTestRoot");
            try
            {
                var controller = root.AddComponent<DemoSceneController>();
                controller.BuildNow();
                var battlefield = root.GetComponent<DemoBattlefield3D>();
                Assert.That(battlefield, Is.Not.Null);
                Assert.That(battlefield.BoardCamera, Is.Not.Null);
                Assert.That(battlefield.BoardCamera.orthographic, Is.True);
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
                Assert.That(unitMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.GreaterThan(0f));
                Assert.That(buildingMarker.GetComponent<MeshRenderer>().sharedMaterial.GetFloat("_HighlightStrength"), Is.Zero);
                Assert.That(root.transform.Find("DemoCanvas"), Is.Not.Null);
                Assert.That(GameObject.Find("EndTurn"), Is.Not.Null);
                Assert.That(GameObject.Find("Faction_plains_forest"), Is.Not.Null);

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
