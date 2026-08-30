using System.Collections.Generic;
using UnityEngine;

namespace BiomeRivals.Demo
{
    public static class DemoMinecraftModelFactory
    {
        private static readonly Dictionary<string, string> TextureKeys = new Dictionary<string, string>
        {
            { "pf_001", "entity_bee" },
            { "pf_002", "entity_sheep" },
            { "pf_003", "entity_wolf" },
            { "pf_004", "entity_villager" },
            { "nt_001", "entity_magma_cube" },
            { "tk_014", "entity_magma_cube" },
            { "nt_003", "entity_blaze" },
            { "si_003", "entity_stray" },
            { "or_001", "entity_salmon" },
            { "or_002", "entity_dolphin" },
            { "or_003", "entity_drowned" },
            { "or_004", "entity_guardian" }
        };

        public static bool TryGetTextureKey(string cardId, out string textureKey) =>
            TextureKeys.TryGetValue(cardId, out textureKey);

        public static bool TryBuild(Transform root, string cardId, bool player, Material material)
        {
            if (!TextureKeys.ContainsKey(cardId)) return false;
            root.localRotation = Quaternion.Euler(0f, player ? 180f : 0f, 0f);

            switch (cardId)
            {
                case "pf_001": BuildBee(root, material); break;
                case "pf_002": BuildSheep(root, material); break;
                case "pf_003": BuildWolf(root, material); break;
                case "pf_004": BuildVillager(root, material); break;
                case "nt_001": BuildMagmaCube(root, material); break;
                case "tk_014":
                    root.localScale = Vector3.one * 0.62f;
                    BuildMagmaCube(root, material);
                    break;
                case "nt_003": BuildBlaze(root, material); break;
                case "si_003": BuildStray(root, material); break;
                case "or_001": BuildSalmon(root, material); break;
                case "or_002": BuildDolphin(root, material); break;
                case "or_003": BuildDrowned(root, material); break;
                case "or_004": BuildGuardian(root, material); break;
                default: return false;
            }
            return true;
        }

        private static void BuildBee(Transform root, Material material)
        {
            Cuboid(root, "Body", new Vector3(0f, 0.88f, 0f), new Vector3(0.78f, 0.58f, 1.08f), material, 0, 0, 7, 5, 10, 64, 64);
            Cuboid(root, "LeftWing", new Vector3(-0.53f, 1.15f, 0.12f), new Vector3(0.62f, 0.055f, 0.80f), material, 0, 18, 1, 1, 1, 64, 64, Quaternion.Euler(0f, 0f, -9f));
            Cuboid(root, "RightWing", new Vector3(0.53f, 1.15f, 0.12f), new Vector3(0.62f, 0.055f, 0.80f), material, 0, 18, 1, 1, 1, 64, 64, Quaternion.Euler(0f, 0f, 9f));
            Cuboid(root, "Stinger", new Vector3(0f, 0.87f, 0.64f), new Vector3(0.16f, 0.16f, 0.22f), material, 26, 7, 1, 1, 2, 64, 64);
            Cuboid(root, "LeftAntenna", new Vector3(-0.22f, 1.27f, -0.54f), new Vector3(0.10f, 0.30f, 0.10f), material, 0, 0, 1, 3, 1, 64, 64, Quaternion.Euler(-20f, 0f, 0f));
            Cuboid(root, "RightAntenna", new Vector3(0.22f, 1.27f, -0.54f), new Vector3(0.10f, 0.30f, 0.10f), material, 0, 0, 1, 3, 1, 64, 64, Quaternion.Euler(-20f, 0f, 0f));
        }

        private static void BuildSheep(Transform root, Material material)
        {
            Cuboid(root, "Body", new Vector3(0f, 0.82f, 0.05f), new Vector3(0.84f, 1.42f, 0.62f), material, 28, 8, 8, 16, 6, 64, 32, Quaternion.Euler(90f, 0f, 0f));
            Cuboid(root, "Head", new Vector3(0f, 1.02f, -0.73f), new Vector3(0.62f, 0.66f, 0.58f), material, 0, 0, 6, 6, 6, 64, 32);
            Leg(root, "FrontLeftLeg", -0.29f, -0.43f, material, 0, 16, 64, 32);
            Leg(root, "FrontRightLeg", 0.29f, -0.43f, material, 0, 16, 64, 32);
            Leg(root, "BackLeftLeg", -0.29f, 0.52f, material, 0, 16, 64, 32);
            Leg(root, "BackRightLeg", 0.29f, 0.52f, material, 0, 16, 64, 32);
        }

        private static void BuildWolf(Transform root, Material material)
        {
            Cuboid(root, "Body", new Vector3(0f, 0.82f, 0.12f), new Vector3(0.64f, 0.72f, 1.18f), material, 18, 14, 6, 9, 6, 64, 32, Quaternion.Euler(90f, 0f, 0f));
            Cuboid(root, "Head", new Vector3(0f, 1.13f, -0.66f), new Vector3(0.64f, 0.64f, 0.58f), material, 0, 0, 6, 6, 4, 64, 32);
            Cuboid(root, "Muzzle", new Vector3(0f, 1.00f, -1.00f), new Vector3(0.34f, 0.28f, 0.28f), material, 0, 10, 3, 3, 3, 64, 32);
            Cuboid(root, "LeftEar", new Vector3(-0.22f, 1.52f, -0.66f), new Vector3(0.18f, 0.30f, 0.16f), material, 16, 14, 2, 2, 1, 64, 32);
            Cuboid(root, "RightEar", new Vector3(0.22f, 1.52f, -0.66f), new Vector3(0.18f, 0.30f, 0.16f), material, 16, 14, 2, 2, 1, 64, 32);
            Leg(root, "FrontLeftLeg", -0.23f, -0.35f, material, 0, 18, 64, 32);
            Leg(root, "FrontRightLeg", 0.23f, -0.35f, material, 0, 18, 64, 32);
            Leg(root, "BackLeftLeg", -0.23f, 0.48f, material, 0, 18, 64, 32);
            Leg(root, "BackRightLeg", 0.23f, 0.48f, material, 0, 18, 64, 32);
            Cuboid(root, "Tail", new Vector3(0f, 1.02f, 0.86f), new Vector3(0.22f, 0.22f, 0.78f), material, 9, 18, 2, 8, 2, 64, 32, Quaternion.Euler(-38f, 0f, 0f));
        }

        private static void BuildVillager(Transform root, Material material)
        {
            Cuboid(root, "Head", new Vector3(0f, 1.64f, 0f), new Vector3(0.72f, 0.72f, 0.72f), material, 0, 0, 8, 8, 8, 64, 64);
            Cuboid(root, "Nose", new Vector3(0f, 1.55f, -0.45f), new Vector3(0.18f, 0.34f, 0.18f), material, 24, 0, 2, 4, 2, 64, 64);
            Cuboid(root, "Body", new Vector3(0f, 0.94f, 0f), new Vector3(0.72f, 0.96f, 0.48f), material, 16, 20, 8, 12, 6, 64, 64);
            Cuboid(root, "CrossedArms", new Vector3(0f, 1.06f, -0.40f), new Vector3(0.92f, 0.28f, 0.28f), material, 44, 22, 8, 4, 4, 64, 64, Quaternion.Euler(-28f, 0f, 0f));
            Cuboid(root, "LeftLeg", new Vector3(-0.19f, 0.29f, 0f), new Vector3(0.32f, 0.72f, 0.38f), material, 0, 22, 4, 8, 4, 64, 64);
            Cuboid(root, "RightLeg", new Vector3(0.19f, 0.29f, 0f), new Vector3(0.32f, 0.72f, 0.38f), material, 0, 22, 4, 8, 4, 64, 64);
        }

        private static void BuildMagmaCube(Transform root, Material material)
        {
            for (var slice = 0; slice < 8; slice++)
            {
                Cuboid(root, "BodySlice_" + slice, new Vector3(0f, 0.24f + slice * 0.135f, 0f), new Vector3(1.18f, 0.135f, 1.18f), material, 0, slice, 8, 1, 8, 64, 32);
            }
            Cuboid(root, "InnerCore", new Vector3(0f, 0.70f, 0f), new Vector3(0.58f, 0.58f, 0.58f), material, 24, 10, 4, 4, 4, 64, 32);
        }

        private static void BuildBlaze(Transform root, Material material)
        {
            Cuboid(root, "Head", new Vector3(0f, 1.36f, 0f), new Vector3(0.72f, 0.72f, 0.72f), material, 0, 0, 8, 8, 8, 64, 32);
            for (var rod = 0; rod < 12; rod++)
            {
                var ring = rod / 4;
                var angle = (rod % 4) * Mathf.PI * 0.5f + ring * 0.58f;
                var radius = ring == 1 ? 0.48f : 0.68f;
                var y = 0.52f + ring * 0.39f;
                var position = new Vector3(Mathf.Cos(angle) * radius, y, Mathf.Sin(angle) * radius);
                Cuboid(root, "Rod_" + rod, position, new Vector3(0.16f, 0.72f, 0.16f), material, 0, 16, 2, 8, 2, 64, 32);
            }
        }

        private static void BuildStray(Transform root, Material material)
        {
            Cuboid(root, "Head", new Vector3(0f, 1.72f, 0f), new Vector3(0.64f, 0.64f, 0.64f), material, 0, 0, 8, 8, 8, 64, 32);
            Cuboid(root, "Body", new Vector3(0f, 1.05f, 0f), new Vector3(0.52f, 0.84f, 0.30f), material, 16, 16, 8, 12, 4, 64, 32);
            Cuboid(root, "LeftArm", new Vector3(-0.39f, 1.08f, 0f), new Vector3(0.18f, 0.86f, 0.18f), material, 40, 16, 2, 12, 2, 64, 32, Quaternion.Euler(0f, 0f, 8f));
            Cuboid(root, "RightArm", new Vector3(0.39f, 1.08f, 0f), new Vector3(0.18f, 0.86f, 0.18f), material, 40, 16, 2, 12, 2, 64, 32, Quaternion.Euler(0f, 0f, -8f));
            Cuboid(root, "LeftLeg", new Vector3(-0.14f, 0.38f, 0f), new Vector3(0.19f, 0.86f, 0.19f), material, 0, 16, 2, 12, 2, 64, 32);
            Cuboid(root, "RightLeg", new Vector3(0.14f, 0.38f, 0f), new Vector3(0.19f, 0.86f, 0.19f), material, 0, 16, 2, 12, 2, 64, 32);
        }

        private static void BuildSalmon(Transform root, Material material)
        {
            root.localPosition += new Vector3(0f, 0.30f, 0f);
            Cuboid(root, "FrontBody", new Vector3(-0.24f, 0.70f, 0f), new Vector3(0.78f, 0.62f, 0.48f), material, 0, 0, 3, 4, 5, 32, 32);
            Cuboid(root, "RearBody", new Vector3(0.52f, 0.70f, 0f), new Vector3(0.76f, 0.56f, 0.45f), material, 0, 9, 3, 4, 5, 32, 32);
            Cuboid(root, "Head", new Vector3(-0.78f, 0.67f, 0f), new Vector3(0.42f, 0.48f, 0.38f), material, 22, 0, 2, 3, 3, 32, 32);
            Cuboid(root, "DorsalFin", new Vector3(0.18f, 1.05f, 0f), new Vector3(0.46f, 0.34f, 0.055f), material, 20, 10, 1, 2, 3, 32, 32, Quaternion.Euler(0f, 0f, 10f));
            Cuboid(root, "LeftFin", new Vector3(-0.08f, 0.60f, -0.32f), new Vector3(0.28f, 0.055f, 0.36f), material, 2, 2, 2, 1, 2, 32, 32, Quaternion.Euler(-22f, 0f, 0f));
            Cuboid(root, "RightFin", new Vector3(-0.08f, 0.60f, 0.32f), new Vector3(0.28f, 0.055f, 0.36f), material, 2, 2, 2, 1, 2, 32, 32, Quaternion.Euler(22f, 0f, 0f));
            Cuboid(root, "Tail", new Vector3(1.05f, 0.70f, 0f), new Vector3(0.46f, 0.72f, 0.055f), material, 20, 15, 1, 4, 3, 32, 32);
        }

        private static void BuildDolphin(Transform root, Material material)
        {
            root.localPosition += new Vector3(0f, 0.20f, 0f);
            Cuboid(root, "Body", new Vector3(0f, 0.78f, 0.05f), new Vector3(0.86f, 0.68f, 1.42f), material, 22, 0, 8, 7, 13, 64, 64);
            Cuboid(root, "Head", new Vector3(0f, 0.80f, -0.86f), new Vector3(0.78f, 0.62f, 0.58f), material, 0, 0, 8, 7, 6, 64, 64);
            Cuboid(root, "Snout", new Vector3(0f, 0.68f, -1.27f), new Vector3(0.42f, 0.25f, 0.48f), material, 0, 13, 4, 2, 4, 64, 64);
            Cuboid(root, "DorsalFin", new Vector3(0f, 1.23f, 0.12f), new Vector3(0.07f, 0.48f, 0.56f), material, 51, 0, 1, 4, 5, 64, 64, Quaternion.Euler(-18f, 0f, 0f));
            Cuboid(root, "LeftFlipper", new Vector3(-0.58f, 0.63f, -0.12f), new Vector3(0.56f, 0.07f, 0.34f), material, 48, 20, 4, 1, 3, 64, 64, Quaternion.Euler(0f, -8f, -28f));
            Cuboid(root, "RightFlipper", new Vector3(0.58f, 0.63f, -0.12f), new Vector3(0.56f, 0.07f, 0.34f), material, 48, 20, 4, 1, 3, 64, 64, Quaternion.Euler(0f, 8f, 28f));
            Cuboid(root, "TailStem", new Vector3(0f, 0.79f, 0.98f), new Vector3(0.42f, 0.38f, 0.58f), material, 0, 19, 4, 4, 5, 64, 64);
            Cuboid(root, "TailFlukes", new Vector3(0f, 0.80f, 1.37f), new Vector3(1.02f, 0.08f, 0.44f), material, 19, 20, 9, 1, 4, 64, 64);
        }

        private static void BuildDrowned(Transform root, Material material)
        {
            Cuboid(root, "Head", new Vector3(0f, 1.72f, 0f), new Vector3(0.66f, 0.66f, 0.66f), material, 0, 0, 8, 8, 8, 64, 64);
            Cuboid(root, "Body", new Vector3(0f, 1.04f, 0f), new Vector3(0.54f, 0.84f, 0.30f), material, 16, 16, 8, 12, 4, 64, 64);
            Cuboid(root, "LeftArm", new Vector3(-0.42f, 1.17f, -0.18f), new Vector3(0.20f, 0.86f, 0.20f), material, 40, 16, 4, 12, 4, 64, 64, Quaternion.Euler(-58f, 0f, 8f));
            Cuboid(root, "RightArm", new Vector3(0.42f, 1.17f, -0.18f), new Vector3(0.20f, 0.86f, 0.20f), material, 32, 48, 4, 12, 4, 64, 64, Quaternion.Euler(-58f, 0f, -8f));
            Cuboid(root, "LeftLeg", new Vector3(-0.15f, 0.38f, 0f), new Vector3(0.22f, 0.86f, 0.22f), material, 0, 16, 4, 12, 4, 64, 64);
            Cuboid(root, "RightLeg", new Vector3(0.15f, 0.38f, 0f), new Vector3(0.22f, 0.86f, 0.22f), material, 16, 48, 4, 12, 4, 64, 64);
        }

        private static void BuildGuardian(Transform root, Material material)
        {
            root.localPosition += new Vector3(0f, 0.22f, 0f);
            Cuboid(root, "Body", new Vector3(0f, 0.82f, 0f), new Vector3(1.08f, 1.02f, 1.28f), material, 0, 0, 12, 12, 16, 64, 64);
            Cuboid(root, "TailBase", new Vector3(0f, 0.78f, 0.78f), new Vector3(0.48f, 0.42f, 0.58f), material, 40, 0, 4, 4, 6, 64, 64, Quaternion.Euler(0f, 12f, 0f));
            Cuboid(root, "TailMiddle", new Vector3(0.18f, 0.76f, 1.18f), new Vector3(0.34f, 0.32f, 0.52f), material, 0, 32, 3, 3, 5, 64, 64, Quaternion.Euler(0f, 28f, 0f));
            Cuboid(root, "TailFin", new Vector3(0.43f, 0.76f, 1.48f), new Vector3(0.58f, 0.62f, 0.07f), material, 24, 32, 5, 5, 1, 64, 64, Quaternion.Euler(0f, 36f, 0f));

            for (var spike = 0; spike < 4; spike++)
            {
                var x = -0.42f + spike * 0.28f;
                Cuboid(root, "TopSpike_" + spike, new Vector3(x, 1.48f, 0f), new Vector3(0.13f, 0.42f, 0.13f), material, 0, 28, 2, 4, 2, 64, 64);
                Cuboid(root, "BottomSpike_" + spike, new Vector3(x, 0.17f, 0f), new Vector3(0.13f, 0.36f, 0.13f), material, 0, 28, 2, 4, 2, 64, 64);
            }
            for (var side = -1; side <= 1; side += 2)
            {
                Cuboid(root, side < 0 ? "LeftSpikeFront" : "RightSpikeFront", new Vector3(side * 0.70f, 1.05f, -0.30f), new Vector3(0.42f, 0.13f, 0.13f), material, 0, 28, 2, 4, 2, 64, 64);
                Cuboid(root, side < 0 ? "LeftSpikeRear" : "RightSpikeRear", new Vector3(side * 0.70f, 0.62f, 0.28f), new Vector3(0.42f, 0.13f, 0.13f), material, 0, 28, 2, 4, 2, 64, 64);
            }
        }

        private static void Leg(Transform root, string name, float x, float z, Material material, int u, int v, int textureWidth, int textureHeight)
        {
            Cuboid(root, name, new Vector3(x, 0.33f, z), new Vector3(0.24f, 0.68f, 0.24f), material, u, v, 2, 6, 2, textureWidth, textureHeight);
        }

        private static void Cuboid(
            Transform parent, string name, Vector3 position, Vector3 size, Material material,
            int u, int v, int dx, int dy, int dz, int textureWidth, int textureHeight, Quaternion? rotation = null)
        {
            var gameObject = new GameObject(name, typeof(MeshFilter), typeof(MeshRenderer), typeof(DemoGeneratedMeshOwner));
            gameObject.transform.SetParent(parent, false);
            gameObject.transform.localPosition = position;
            gameObject.transform.localRotation = rotation ?? Quaternion.identity;
            gameObject.GetComponent<MeshRenderer>().sharedMaterial = material;
            var mesh = CreateCuboidMesh(name + "_Mesh", size, u, v, dx, dy, dz, textureWidth, textureHeight);
            gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.GetComponent<DemoGeneratedMeshOwner>().Configure(mesh);
        }

        private static Mesh CreateCuboidMesh(string name, Vector3 size, int u, int v, int dx, int dy, int dz, int textureWidth, int textureHeight)
        {
            var vertices = new List<Vector3>(24);
            var uv = new List<Vector2>(24);
            var triangles = new List<int>(36);
            var x = size.x * 0.5f;
            var y = size.y * 0.5f;
            var z = size.z * 0.5f;

            AddFace(vertices, uv, triangles, new Vector3(-x, -y, -z), new Vector3(-x, y, -z), new Vector3(x, y, -z), new Vector3(x, -y, -z), u + dz, v + dz, dx, dy, textureWidth, textureHeight);
            AddFace(vertices, uv, triangles, new Vector3(x, -y, z), new Vector3(x, y, z), new Vector3(-x, y, z), new Vector3(-x, -y, z), u + dz + dx + dz, v + dz, dx, dy, textureWidth, textureHeight);
            AddFace(vertices, uv, triangles, new Vector3(-x, -y, z), new Vector3(-x, y, z), new Vector3(-x, y, -z), new Vector3(-x, -y, -z), u, v + dz, dz, dy, textureWidth, textureHeight);
            AddFace(vertices, uv, triangles, new Vector3(x, -y, -z), new Vector3(x, y, -z), new Vector3(x, y, z), new Vector3(x, -y, z), u + dz + dx, v + dz, dz, dy, textureWidth, textureHeight);
            AddFace(vertices, uv, triangles, new Vector3(-x, y, -z), new Vector3(-x, y, z), new Vector3(x, y, z), new Vector3(x, y, -z), u + dz, v, dx, dz, textureWidth, textureHeight);
            AddFace(vertices, uv, triangles, new Vector3(-x, -y, z), new Vector3(-x, -y, -z), new Vector3(x, -y, -z), new Vector3(x, -y, z), u + dz + dx, v, dx, dz, textureWidth, textureHeight);

            var mesh = new Mesh { name = name };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            return mesh;
        }

        private static void AddFace(
            ICollection<Vector3> vertices, ICollection<Vector2> uv, ICollection<int> triangles,
            Vector3 bottomLeft, Vector3 topLeft, Vector3 topRight, Vector3 bottomRight,
            int u, int v, int width, int height, int textureWidth, int textureHeight)
        {
            var start = vertices.Count;
            vertices.Add(bottomLeft);
            vertices.Add(topLeft);
            vertices.Add(topRight);
            vertices.Add(bottomRight);

            var left = u / (float)textureWidth;
            var right = (u + width) / (float)textureWidth;
            var top = 1f - v / (float)textureHeight;
            var bottom = 1f - (v + height) / (float)textureHeight;
            uv.Add(new Vector2(left, bottom));
            uv.Add(new Vector2(left, top));
            uv.Add(new Vector2(right, top));
            uv.Add(new Vector2(right, bottom));
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }
    }
}
