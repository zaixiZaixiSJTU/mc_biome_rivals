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
                Assert.That(root.transform.Find("DemoCanvas"), Is.Not.Null);
                Assert.That(GameObject.Find("EndTurn"), Is.Not.Null);
                Assert.That(GameObject.Find("Faction_plains_forest"), Is.Not.Null);

                var playerUnit = battlefield.GetSlotReferencePosition(true, DemoSlotKind.Unit, 0);
                var opponentUnit = battlefield.GetSlotReferencePosition(false, DemoSlotKind.Unit, 0);
                Assert.That(playerUnit.y, Is.LessThan(opponentUnit.y));
                Assert.That(playerUnit, Is.Not.EqualTo(opponentUnit));
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }
    }
}
