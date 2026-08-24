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
            { "nt_003", "entity_blaze" }
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
                case "nt_003": BuildBlaze(root, material); break;
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
