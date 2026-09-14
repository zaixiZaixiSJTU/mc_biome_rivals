using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEngine;

namespace BiomeRivals.Demo.Tests
{
    /// <summary>
    /// Validates the vanilla entity geometry pipeline: JSON parsing (modern and
    /// legacy formats), box-UV mesh construction, bone hierarchy and
    /// auto-fitting, plus integration over every locally extracted model.
    /// </summary>
    public sealed class DemoMinecraftEntityModelTests
    {
        private const string ModernGeometryJson = @"{
  ""format_version"": ""1.12.0"",
  ""minecraft:geometry"": [
    {
      ""description"": { ""identifier"": ""geometry.test_creature"", ""texture_width"": 32, ""texture_height"": 32 },
      ""bones"": [
        { ""name"": ""root"", ""pivot"": [0, 0, 0] },
        {
          ""name"": ""body"", ""parent"": ""root"", ""pivot"": [0, 4, 0],
          ""cubes"": [ { ""origin"": [-2, 0, -2], ""size"": [4, 8, 4], ""uv"": [0, 0] } ]
        },
        {
          ""name"": ""mirror_arm"", ""parent"": ""root"", ""pivot"": [3, 6, 0],
          ""cubes"": [ { ""origin"": [2, 4, -1], ""size"": [2, 4, 2], ""uv"": [8, 0], ""mirror"": true } ]
        }
      ]
    }
  ]
}";

        private const string LegacyGeometryJson = @"{
  ""format_version"": ""1.8.0"",
  ""geometry.legacy_creature"": {
    ""texturewidth"": 64,
    ""textureheight"": 32,
    ""bones"": [
      {
        ""name"": ""body"", ""pivot"": [0, 0, 0], ""bind_pose_rotation"": [90, 0, 0],
        ""cubes"": [ { ""origin"": [-3, 13, -5], ""size"": [8, 16, 6], ""uv"": [28, 8] } ]
      }
    ]
  }
}";

        [Test]
        public void ParsesModernGeometryWithBonesAndCubes()
        {
            var geometry = DemoMinecraftEntityGeometryParser.Parse(ModernGeometryJson, "geometry.test_creature");
            Assert.That(geometry.Identifier, Is.EqualTo("geometry.test_creature"));
            Assert.That(geometry.TextureWidth, Is.EqualTo(32));
            Assert.That(geometry.TextureHeight, Is.EqualTo(32));
            Assert.That(geometry.Bones, Has.Count.EqualTo(3));
            var body = geometry.Bones.Single(bone => bone.Name == "body");
            Assert.That(body.Parent, Is.EqualTo("root"));
            Assert.That(body.Pivot, Is.EqualTo(new[] { 0f, 4f, 0f }));
            Assert.That(body.Cubes, Has.Count.EqualTo(1));
            Assert.That(body.Cubes[0].Size, Is.EqualTo(new[] { 4f, 8f, 4f }));
            Assert.That(body.Cubes[0].Uv, Is.EqualTo(new[] { 0f, 0f }));
            Assert.That(body.Cubes[0].Mirror, Is.False);
        }

        [Test]
        public void ParsesLegacyGeometryWithBindPoseRotation()
        {
            var geometry = DemoMinecraftEntityGeometryParser.Parse(LegacyGeometryJson);
            Assert.That(geometry.Identifier, Is.EqualTo("geometry.legacy_creature"));
            Assert.That(geometry.TextureWidth, Is.EqualTo(64));
            Assert.That(geometry.TextureHeight, Is.EqualTo(32));
            var body = geometry.Bones.Single(bone => bone.Name == "body");
            Assert.That(body.Rotation, Is.EqualTo(new[] { 90f, 0f, 0f }));
            Assert.That(body.Cubes[0].Uv, Is.EqualTo(new[] { 28f, 8f }));
        }

        [Test]
        public void BuilderCreatesBoneHierarchyAndFitsModel()
        {
            var geometry = DemoMinecraftEntityGeometryParser.Parse(ModernGeometryJson);
            var root = new GameObject("EntityTestRoot");
            try
            {
                var built = DemoMinecraftEntityModelBuilder.TryBuild(root.transform, geometry, new DemoMinecraftEntityModelBuilder.BuildOptions
                {
                    MaterialProvider = _ => new Material(Shader.Find("Standard")),
                    PrimaryTextureKey = "entity_test",
                    TextureWidth = geometry.TextureWidth,
                    TextureHeight = geometry.TextureHeight,
                    TargetHeight = 2f
                });
                Assert.That(built, Is.True);
                var body = root.transform.Find("Model/Bone_root/Bone_body");
                Assert.That(body, Is.Not.Null);
                Assert.That(body.localPosition, Is.EqualTo(new Vector3(0f, 0.25f, 0f)).Within(0.0001f));
                var renderers = root.GetComponentsInChildren<MeshRenderer>();
                Assert.That(renderers, Has.Length.GreaterThanOrEqualTo(2));
                foreach (var renderer in renderers)
                {
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(mesh, Is.Not.Null);
                    Assert.That(mesh.uv.Length, Is.GreaterThan(0));
                    foreach (var uv in mesh.uv)
                    {
                        Assert.That(uv.x, Is.InRange(0f, 1f), renderer.name);
                        Assert.That(uv.y, Is.InRange(0f, 1f), renderer.name);
                    }
                }
                foreach (var renderer in renderers)
                {
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    Assert.That(mesh.colors32.Length, Is.EqualTo(mesh.vertices.Length), renderer.name);
                    // vanilla-style per-face shading must be baked into vertex colors
                    Assert.That(mesh.colors32[0].r, Is.InRange(120, 255), renderer.name);
                }
                var shades = new System.Collections.Generic.HashSet<byte>();
                foreach (var renderer in renderers)
                    foreach (var color in renderer.GetComponent<MeshFilter>().sharedMesh.colors32) shades.Add(color.r);
                Assert.That(shades.Count, Is.GreaterThan(1), "multiple face shades are baked");
                var totalBounds = EncapsulateAll(renderers);
                Assert.That(totalBounds.size.y, Is.EqualTo(2f).Within(0.02f), "model is auto-scaled to the target height");
                Assert.That(totalBounds.min.y, Is.EqualTo(0f).Within(0.02f), "model bottom rests on the slot ground");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BuilderMirrorsBedrockXAxisAndAppliesBaseY()
        {
            // asymmetric cube: origin [0,0,0], size [4,4,4] must land at x = -0.25..0 in Unity.
            var asymmetric = @"{
  ""format_version"": ""1.12.0"",
  ""minecraft:geometry"": [
    {
      ""description"": { ""identifier"": ""geometry.asymmetric"", ""texture_width"": 16, ""texture_height"": 16 },
      ""bones"": [
        { ""name"": ""body"", ""pivot"": [0, 0, 0], ""cubes"": [ { ""origin"": [0, 0, 0], ""size"": [4, 4, 4], ""uv"": [0, 0] } ] }
      ]
    }
  ]
}";
            var geometry = DemoMinecraftEntityGeometryParser.Parse(asymmetric);
            var root = new GameObject("EntityMirrorTestRoot");
            try
            {
                var built = DemoMinecraftEntityModelBuilder.TryBuild(root.transform, geometry, new DemoMinecraftEntityModelBuilder.BuildOptions
                {
                    MaterialProvider = _ => new Material(Shader.Find("Standard")),
                    PrimaryTextureKey = "entity_test",
                    TextureWidth = geometry.TextureWidth,
                    TextureHeight = geometry.TextureHeight,
                    BaseY = 0.5f
                });
                Assert.That(built, Is.True);
                var mesh = root.GetComponentInChildren<MeshFilter>().sharedMesh;
                var minX = mesh.vertices.Min(vertex => vertex.x);
                var maxX = mesh.vertices.Max(vertex => vertex.x);
                // Bedrock cube spans x 0..4; mirrored into Unity it spans -0.25..0.
                Assert.That(maxX, Is.EqualTo(0f).Within(0.0001f));
                Assert.That(minX, Is.EqualTo(-0.25f).Within(0.0001f));
                var bounds = EncapsulateAll(root.GetComponentsInChildren<MeshRenderer>());
                Assert.That(bounds.min.y, Is.EqualTo(0.5f).Within(0.01f), "BaseY lifts the model bottom to the hover height");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BedrockBoneRotationInvertsXAndZSigns()
        {
            var rotation = DemoMinecraftEntityModelBuilder.ConvertBoneRotation(new[] { 90f, 0f, 0f });
            var expected = Quaternion.AngleAxis(-90f, Vector3.right);
            Assert.That(Quaternion.Angle(rotation, expected), Is.LessThan(0.1f));
        }

        [Test]
        public void FactoryMetadataCoversEveryEntityCard()
        {
            var entityCardIds = new[]
            {
                "pf_001", "pf_002", "tk_003", "tk_004", "pf_003", "pf_004", "cd_005", "tk_011",
                "pf_008", "si_002", "si_004", "cd_001", "cd_002", "nt_001", "tk_014", "nt_003",
                "si_003", "or_001", "or_002", "or_003", "or_004",
                "db_001", "db_003", "db_005", "db_008", "si_005", "cd_003", "or_005", "nt_002",
                "nt_004", "nt_005", "ed_001", "ed_003", "ed_004", "tk_015", "tk_017"
            };
            foreach (var cardId in entityCardIds)
            {
                Assert.That(DemoMinecraftModelFactory.TryGetTextureKey(cardId, out var textureKey), Is.True, cardId);
                Assert.That(textureKey, Does.StartWith("entity_"), cardId);
            }
            Assert.That(DemoMinecraftModelFactory.TryGetTextureKey("pf_005", out _), Is.False, "buildings are not entity models");
        }

        [Test]
        public void UnknownCardIdReportsUnsupportedModel()
        {
            var root = new GameObject("EntityUnsupportedRoot");
            try
            {
                Assert.That(DemoMinecraftModelFactory.TryBuild(
                    root.transform, "pf_005", true, _ => new Material(Shader.Find("Standard"))), Is.False);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [TestCase("pf_001")]
        [TestCase("pf_002")]
        [TestCase("pf_003")]
        [TestCase("pf_004")]
        [TestCase("cd_005")]
        [TestCase("pf_008")]
        [TestCase("si_002")]
        [TestCase("si_004")]
        [TestCase("cd_001")]
        [TestCase("cd_002")]
        [TestCase("nt_001")]
        [TestCase("nt_003")]
        [TestCase("si_003")]
        [TestCase("or_001")]
        [TestCase("or_002")]
        [TestCase("or_003")]
        [TestCase("or_004")]
        [TestCase("db_001")]
        [TestCase("db_003")]
        [TestCase("db_005")]
        [TestCase("db_008")]
        [TestCase("si_005")]
        [TestCase("cd_003")]
        [TestCase("or_005")]
        [TestCase("nt_002")]
        [TestCase("nt_004")]
        [TestCase("nt_005")]
        [TestCase("ed_001")]
        [TestCase("ed_003")]
        [TestCase("ed_004")]
        [TestCase("tk_015")]
        [TestCase("tk_017")]
        public void ExtractedEntityModelsBuildWithValidGeometry(string cardId)
        {
            Assert.That(DemoMinecraftModelFactory.TryGetTextureKey(cardId, out var textureKey), Is.True);
            Assert.That(DemoMinecraftModelFactory.TryGetModelKey(cardId, out var modelKey), Is.True);
            if (Resources.Load<TextAsset>("DemoWorld/entity_models/" + modelKey) == null)
            {
                Assert.Ignore("Local entity geometry has not been extracted yet (run scripts/extract-minecraft-entity-models.ps1).");
            }

            var root = new GameObject("EntityIntegration_" + cardId);
            try
            {
                var built = DemoMinecraftModelFactory.TryBuild(
                    root.transform, cardId, player: true, key => new Material(Shader.Find("Standard")) { name = key });
                Assert.That(built, Is.True, cardId);
                var renderers = root.GetComponentsInChildren<MeshRenderer>();
                Assert.That(renderers, Has.Length.GreaterThanOrEqualTo(1), cardId);
                foreach (var renderer in renderers)
                {
                    var mesh = renderer.GetComponent<MeshFilter>().sharedMesh;
                    // Full cubes carry 6 faces (24 verts); zero-thickness slabs keep only coincident face pairs (16 or 8 verts).
                    Assert.That(mesh.vertices.Length, Is.InRange(8, 24), renderer.name);
                    Assert.That(mesh.vertices.Length % 4, Is.EqualTo(0), renderer.name);
                    Assert.That(mesh.uv.Length, Is.EqualTo(mesh.vertices.Length), renderer.name);
                    Assert.That(mesh.colors32.Length, Is.EqualTo(mesh.vertices.Length), renderer.name);
                    foreach (var uv in mesh.uv)
                    {
                        Assert.That(uv.x, Is.InRange(0f, 1f), $"{cardId}/{renderer.name}");
                        Assert.That(uv.y, Is.InRange(0f, 1f), $"{cardId}/{renderer.name}");
                    }
                }
                var bounds = EncapsulateAll(renderers);
                Assert.That(bounds.size.y, Is.GreaterThan(0.3f), cardId);
                Assert.That(bounds.size.x, Is.LessThan(5.5f), cardId);
                Assert.That(bounds.size.y, Is.LessThan(4.5f), cardId);
                Assert.That(bounds.size.z, Is.LessThan(4.5f), cardId);
                var animator = root.GetComponent<DemoEntityIdleAnimator>();
                if (animator != null) Assert.That(animator.TrackCount, Is.GreaterThan(0), cardId);
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void BeeModelFlapsWingsWithIdleAnimator()
        {
            if (Resources.Load<TextAsset>("DemoWorld/entity_models/entity_bee") == null)
                Assert.Ignore("Local entity geometry has not been extracted yet.");
            var root = new GameObject("BeeIdleRoot");
            try
            {
                Assert.That(DemoMinecraftModelFactory.TryBuild(root.transform, "pf_001", true, key => new Material(Shader.Find("Standard"))), Is.True);
                var animator = root.GetComponent<DemoEntityIdleAnimator>();
                Assert.That(animator, Is.Not.Null, "bee attaches an idle animator");
                Assert.That(animator.TrackCount, Is.GreaterThanOrEqualTo(2), "both wings flap");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        [Test]
        public void StrayModelRendersHoodOverlayLayer()
        {
            if (Resources.Load<TextAsset>("DemoWorld/entity_models/entity_stray") == null ||
                Resources.Load<Texture2D>("DemoWorld/entity_stray_overlay") == null)
                Assert.Ignore("Local entity geometry or stray overlay texture has not been extracted yet.");
            var root = new GameObject("StrayOverlayRoot");
            try
            {
                Assert.That(DemoMinecraftModelFactory.TryBuild(root.transform, "si_003", true, key => new Material(Shader.Find("Standard")) { name = key }), Is.True);
                var overlayCubes = root.GetComponentsInChildren<Transform>(true);
                Assert.That(System.Linq.Enumerable.Any(overlayCubes, transform => transform.name == "Cube_entity_stray_overlay"),
                    Is.True, "hood overlay cubes are rendered on a second layer");
            }
            finally
            {
                Object.DestroyImmediate(root);
            }
        }

        private static Bounds EncapsulateAll(IReadOnlyList<MeshRenderer> renderers)
        {
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Count; index++) bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }
    }
}
