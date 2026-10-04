using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace BiomeRivals.Demo
{
    /// <summary>
    /// Builds a Unity voxel model from a parsed vanilla entity geometry
    /// (Minecraft "minecraft:geometry" / Blockbench format, sourced from
    /// Mojang's official bedrock-samples repository).
    ///
    /// Conventions (validated against vanilla renderers such as the classic
    /// ModelBox layout and open-source loaders like skinview3d/Blockbench):
    /// - Model space: 1 pixel = 1/16 block, entity faces -Z, +Y up.
    /// - Bedrock geometry stores X mirrored relative to Java models, so X is
    ///   negated once when converting into Unity space; the box-UV side
    ///   regions below already follow that converted orientation.
    /// - In the X-mirrored model frame, source rotations negate X/Y and retain Z.
    ///   Axis and combined-coordinate fixtures cover bones and cube rotations.
    /// - Box UV layout per cube (u, v, dx, dy, dz):
    ///     up    [u+dz,     v,      dx, dz]
    ///     down  [u+dz+dx,  v,      dx, dz]
    ///     -X    [u+dz+dx,  v+dz,   dz, dy]
    ///     +X    [u,        v+dz,   dz, dy]
    ///     north [u+dz,     v+dz,   dx, dy]
    ///     south [u+2dz+dx, v+dz,   dx, dy]
    /// </summary>
    public static class DemoMinecraftEntityModelBuilder
    {
        private const float PixelsPerUnit = 16f;

        public sealed class BuildOptions
        {
            /// <summary>Texture key → material resolver; called once per cube layer.</summary>
            public Func<string, Material> MaterialProvider;
            public string PrimaryTextureKey;
            public int TextureWidth = 64;
            public int TextureHeight = 64;
            /// <summary>Target model height in Unity units; 0 keeps the raw 1px = 1/16 scale.</summary>
            public float TargetHeight;
            /// <summary>When set, fits the model to this X extent instead of the height (wide creatures such as spiders).</summary>
            public float TargetWidth;
            /// <summary>World Y placed under the model's lowest point.</summary>
            public float BaseY = 0f;
            /// <summary>Opt-in grounding for inflated meshes even when BaseY is zero.</summary>
            public bool GroundToBaseY;
            /// <summary>Per-bone pivot overrides in bedrock model units, used to bake static poses (blaze rod rings).</summary>
            public Dictionary<string, float[]> BonePivotOverrides;
            /// <summary>Per-bone rotation overrides in bedrock degrees, baking poses that vanilla applies via animation.</summary>
            public Dictionary<string, float[]> BoneRotationOverrides;
            /// <summary>Registered static pose offsets in source pixels; inherited by children, not baked into vertices.</summary>
            public Dictionary<string, float[]> BonePositionOffsets;
            /// <summary>Mesh-only binding pose about the bone pivot; never inherited by child bones.</summary>
            public Dictionary<string, float[]> BoneMeshBindPoseOverrides;
            /// <summary>Overlay layers (e.g. sheep wool) rendered on top of the primary texture.</summary>
            public List<OverlayLayer> Layers = new List<OverlayLayer>();
        }

        public sealed class OverlayLayer
        {
            /// <summary>Optional source cubes attached to the existing skeleton, rather than copying all base cubes.</summary>
            public DemoEntityGeometry Geometry;
            public string TextureKey;
            public float Inflate;
            /// <summary>Optional atlas size; zero inherits the base geometry dimensions.</summary>
            public int TextureWidth;
            public int TextureHeight;
        }

        public static bool TryBuild(Transform parent, DemoEntityGeometry geometry, BuildOptions options)
        {
            if (geometry?.Bones == null || geometry.Bones.Count == 0 || options?.MaterialProvider == null) return false;

            // Some vanilla files reference parents with inconsistent casing (rightArm vs rightarm).
            var boneTransforms = new Dictionary<string, Transform>(StringComparer.OrdinalIgnoreCase);
            var bonePivots = new Dictionary<string, float[]>(StringComparer.OrdinalIgnoreCase);
            var pending = new List<DemoEntityBone>(geometry.Bones);
            foreach (var bone in geometry.Bones)
            {
                if (bone.Parent != null && !geometry.Bones.Any(candidate =>
                        string.Equals(candidate.Name, bone.Parent, StringComparison.OrdinalIgnoreCase)))
                    throw new FormatException($"Bone '{bone.Name}' references unknown parent '{bone.Parent}'.");
            }

            var model = new GameObject("Model");
            model.transform.SetParent(parent, false);
            while (pending.Count > 0)
            {
                var progressed = false;
                for (var index = pending.Count - 1; index >= 0; index--)
                {
                    var bone = pending[index];
                    if (bone.Parent != null && !boneTransforms.ContainsKey(bone.Parent)) continue;
                    CreateBone(bone, model.transform, options, boneTransforms, bonePivots);
                    pending.RemoveAt(index);
                    progressed = true;
                }
                if (!progressed) throw new FormatException("Geometry bones contain a parent cycle.");
            }

            FitModel(model, options);
            return true;
        }

        private static void CreateBone(
            DemoEntityBone bone,
            Transform modelRoot,
            BuildOptions options,
            IDictionary<string, Transform> boneTransforms,
            IDictionary<string, float[]> bonePivots)
        {
            var pivot = options.BonePivotOverrides != null && options.BonePivotOverrides.TryGetValue(bone.Name, out var overridden)
                ? overridden
                : bone.Pivot;
            var parentPivot = bone.Parent == null ? new[] { 0f, 0f, 0f } : bonePivots[bone.Parent];

            var gameObject = new GameObject("Bone_" + bone.Name);
            gameObject.transform.SetParent(bone.Parent == null ? modelRoot : boneTransforms[bone.Parent], false);
            gameObject.transform.localPosition = ConvertPosition(pivot, parentPivot);
            if (options.BonePositionOffsets != null && options.BonePositionOffsets.TryGetValue(bone.Name, out var offset))
                gameObject.transform.localPosition += ConvertPosition(offset, new[] { 0f, 0f, 0f });
            var rotation = options.BoneRotationOverrides != null && options.BoneRotationOverrides.TryGetValue(bone.Name, out var overriddenRotation)
                ? overriddenRotation
                : bone.Rotation;
            if (rotation != null) gameObject.transform.localRotation = ConvertBoneRotation(rotation);

            boneTransforms[bone.Name] = gameObject.transform;
            bonePivots[bone.Name] = pivot;

            foreach (var cube in bone.Cubes)
            {
                AddCubeLayer(gameObject.transform, pivot, cube, options.PrimaryTextureKey, cube.Inflate, options, bone.BindPoseRotation);
                foreach (var layer in options.Layers.Where(candidate => candidate.Geometry == null))
                {
                    // An overlay atlas need not have the base texture's aspect ratio.
                    var layerOptions = new BuildOptions
                    {
                        MaterialProvider = options.MaterialProvider,
                        BoneMeshBindPoseOverrides = options.BoneMeshBindPoseOverrides,
                        TextureWidth = layer.TextureWidth > 0 ? layer.TextureWidth : options.TextureWidth,
                        TextureHeight = layer.TextureHeight > 0 ? layer.TextureHeight : options.TextureHeight
                    };
                    AddCubeLayer(gameObject.transform, pivot, cube, layer.TextureKey, cube.Inflate + layer.Inflate, layerOptions, bone.BindPoseRotation);
                }
            }
            foreach (var layer in options.Layers.Where(candidate => candidate.Geometry != null))
            {
                var overlayBone = layer.Geometry.Bones.SingleOrDefault(candidate => candidate.Name == bone.Name);
                if (overlayBone == null) continue;
                var layerOptions = new BuildOptions
                {
                    MaterialProvider = options.MaterialProvider,
                    BoneMeshBindPoseOverrides = options.BoneMeshBindPoseOverrides,
                    TextureWidth = layer.Geometry.TextureWidth,
                    TextureHeight = layer.Geometry.TextureHeight
                };
                foreach (var cube in overlayBone.Cubes)
                    AddCubeLayer(gameObject.transform, pivot, cube, layer.TextureKey, cube.Inflate + layer.Inflate,
                        layerOptions, overlayBone.BindPoseRotation ?? bone.BindPoseRotation, "Overlay_");
            }
        }

        private static void AddCubeLayer(
            Transform bone, float[] bonePivot, DemoEntityCube cube, string textureKey, float inflate, BuildOptions options, float[] sourceBindPose, string prefix = "Cube_")
        {
            var vertices = new List<Vector3>(24);
            var uv = new List<Vector2>(24);
            var colors = new List<Color32>(24);
            var triangles = new List<int>(36);
            AppendCube(vertices, uv, colors, triangles, bonePivot, cube, inflate, options);
            if (triangles.Count == 0) return;
            var bindPose = sourceBindPose;
            if (options.BoneMeshBindPoseOverrides != null &&
                options.BoneMeshBindPoseOverrides.TryGetValue(bone.name.Substring("Bone_".Length), out var overridePose))
                bindPose = overridePose;
            if (bindPose != null)
            {
                // Vertices are already bone-local, so zero is the source bone pivot.
                // Bake after cube rotations; leave bone transforms/child pivots/UVs alone.
                var rotation = ConvertBoneRotation(bindPose);
                for (var index = 0; index < vertices.Count; index++) vertices[index] = rotation * vertices[index];
            }

            var mesh = new Mesh { name = $"{bone.name}_{textureKey}_Mesh" };
            mesh.SetVertices(vertices);
            mesh.SetUVs(0, uv);
            mesh.SetColors(colors);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();

            var gameObject = new GameObject(prefix + textureKey, typeof(MeshFilter), typeof(MeshRenderer), typeof(DemoGeneratedMeshOwner));
            gameObject.transform.SetParent(bone, false);
            gameObject.GetComponent<MeshFilter>().sharedMesh = mesh;
            gameObject.GetComponent<MeshRenderer>().sharedMaterial = options.MaterialProvider(textureKey);
            gameObject.GetComponent<DemoGeneratedMeshOwner>().Configure(mesh);
        }

        private static void AppendCube(
            ICollection<Vector3> vertices, ICollection<Vector2> uv, ICollection<Color32> colors, ICollection<int> triangles,
            float[] bonePivot, DemoEntityCube cube, float inflate, BuildOptions options)
        {
            var size = cube.Size;
            var hx = (size[0] + inflate * 2f) * 0.5f;
            var hy = (size[1] + inflate * 2f) * 0.5f;
            var hz = (size[2] + inflate * 2f) * 0.5f;
            // Bedrock → Unity mirrors X, so the cube center flips around x=0.
            var center = new Vector3(-(cube.Origin[0] + size[0] * 0.5f), cube.Origin[1] + size[1] * 0.5f, cube.Origin[2] + size[2] * 0.5f);
            var half = new Vector3(hx, hy, hz);

            var dx = Mathf.RoundToInt(size[0]);
            var dy = Mathf.RoundToInt(size[1]);
            var dz = Mathf.RoundToInt(size[2]);
            var u = cube.Uv[0];
            var v = cube.Uv[1];

            var rotation = BuildCubeRotation(cube);
            var bonePivotMirrored = new Vector3(-bonePivot[0], bonePivot[1], bonePivot[2]);
            var rotationPivotMirrored = cube.RotationPivot != null
                ? new Vector3(-cube.RotationPivot[0], cube.RotationPivot[1], cube.RotationPivot[2])
                : Vector3.zero;

            // Vanilla entity shading: unlit per-face brightness (top 100%, N/S 80%, E/W 60%, bottom 50%).
            AddFace(vertices, uv, colors, triangles, bonePivotMirrored, rotationPivotMirrored, rotation, center, half, Face.North, new Vector4(u + dz, v + dz, dx, dy), FaceShade.NorthSouth, cube, options);
            AddFace(vertices, uv, colors, triangles, bonePivotMirrored, rotationPivotMirrored, rotation, center, half, Face.South, new Vector4(u + 2 * dz + dx, v + dz, dx, dy), FaceShade.NorthSouth, cube, options);
            // Source mirror swaps side regions as well as their horizontal direction.
            AddFace(vertices, uv, colors, triangles, bonePivotMirrored, rotationPivotMirrored, rotation, center, half, Face.West, new Vector4(cube.Mirror ? u : u + dz + dx, v + dz, dz, dy), FaceShade.EastWest, cube, options);
            AddFace(vertices, uv, colors, triangles, bonePivotMirrored, rotationPivotMirrored, rotation, center, half, Face.East, new Vector4(cube.Mirror ? u + dz + dx : u, v + dz, dz, dy), FaceShade.EastWest, cube, options);
            AddFace(vertices, uv, colors, triangles, bonePivotMirrored, rotationPivotMirrored, rotation, center, half, Face.Up, new Vector4(u + dz, v, dx, dz), FaceShade.Up, cube, options);
            AddFace(vertices, uv, colors, triangles, bonePivotMirrored, rotationPivotMirrored, rotation, center, half, Face.Down, new Vector4(u + dz + dx, v, dx, dz), FaceShade.Down, cube, options);
        }

        private enum FaceShade
        {
            Up = 255,
            NorthSouth = 204,
            EastWest = 153,
            Down = 128
        }

        private enum Face { North, South, West, East, Up, Down }

        private static Quaternion BuildCubeRotation(DemoEntityCube cube)
        {
            if (cube.RotationAxis == null && cube.CompositeRotation == null) return Quaternion.identity;
            if (cube.CompositeRotation != null) return ConvertBoneRotation(cube.CompositeRotation);
            return cube.RotationAxis switch
            {
                "x" => Quaternion.AngleAxis(-cube.RotationAngle, Vector3.right),
                "y" => Quaternion.AngleAxis(-cube.RotationAngle, Vector3.up),
                "z" => Quaternion.AngleAxis(cube.RotationAngle, Vector3.forward),
                _ => Quaternion.identity
            };
        }

        private static void AddFace(
            ICollection<Vector3> vertices, ICollection<Vector2> uv, ICollection<Color32> colors, ICollection<int> triangles,
            Vector3 bonePivotMirrored, Vector3 rotationPivotMirrored, Quaternion cubeRotation,
            Vector3 center, Vector3 half, Face face, Vector4 region, FaceShade shade, DemoEntityCube cube, BuildOptions options)
        {
            if (region.z <= 0f || region.w <= 0f) return;
            Vector3 bottomLeft, topLeft, topRight, bottomRight;
            switch (face)
            {
                case Face.North:
                    bottomLeft = new Vector3(-1, -1, -1); topLeft = new Vector3(-1, 1, -1); topRight = new Vector3(1, 1, -1); bottomRight = new Vector3(1, -1, -1);
                    break;
                case Face.South:
                    bottomLeft = new Vector3(1, -1, 1); topLeft = new Vector3(1, 1, 1); topRight = new Vector3(-1, 1, 1); bottomRight = new Vector3(-1, -1, 1);
                    break;
                case Face.West:
                    bottomLeft = new Vector3(-1, -1, 1); topLeft = new Vector3(-1, 1, 1); topRight = new Vector3(-1, 1, -1); bottomRight = new Vector3(-1, -1, -1);
                    break;
                case Face.East:
                    bottomLeft = new Vector3(1, -1, -1); topLeft = new Vector3(1, 1, -1); topRight = new Vector3(1, 1, 1); bottomRight = new Vector3(1, -1, 1);
                    break;
                case Face.Up:
                    bottomLeft = new Vector3(-1, 1, -1); topLeft = new Vector3(-1, 1, 1); topRight = new Vector3(1, 1, 1); bottomRight = new Vector3(1, 1, -1);
                    break;
                default: // Down
                    bottomLeft = new Vector3(-1, -1, 1); topLeft = new Vector3(-1, -1, -1); topRight = new Vector3(1, -1, -1); bottomRight = new Vector3(1, -1, 1);
                    break;
            }

            var left = region.x;
            var right = region.x + region.z;
            var top = region.y;
            var bottom = region.y + region.w;
            // Physical vertices already reflect source X. Unmirrored source U
            // therefore runs opposite our corner order; mirror cancels that flip.
            if (!cube.Mirror) { (left, right) = (right, left); }
            // The bottom box-UV island has the opposite V direction to the top.
            if (face == Face.Down) { (top, bottom) = (bottom, top); }

            var start = vertices.Count;
            var corners = new[] { bottomLeft, topLeft, topRight, bottomRight };
            var uvCorners = new[]
            {
                new Vector2(left, bottom),
                new Vector2(left, top),
                new Vector2(right, top),
                new Vector2(right, bottom)
            };
            for (var index = 0; index < 4; index++)
            {
                var vertexMirrored = Vector3.Scale(corners[index], half) + center;
                vertices.Add(TransformCubeVertex(vertexMirrored, bonePivotMirrored, rotationPivotMirrored, cubeRotation));
                uv.Add(new Vector2(
                    uvCorners[index].x / Mathf.Max(1, options.TextureWidth),
                    1f - uvCorners[index].y / Mathf.Max(1, options.TextureHeight)));
                colors.Add(new Color32((byte)shade, (byte)shade, (byte)shade, 255));
            }
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        /// <summary>
        /// Converts an absolute mirrored model-space vertex into bone-local
        /// Unity units (1px = 1/16), baking the optional cube rotation around
        /// its pivot.
        /// </summary>
        private static Vector3 TransformCubeVertex(
            Vector3 vertexMirrored, Vector3 bonePivotMirrored, Vector3 rotationPivotMirrored, Quaternion cubeRotation)
        {
            var vertexLocal = (vertexMirrored - bonePivotMirrored) / PixelsPerUnit;
            if (cubeRotation.Equals(Quaternion.identity)) return vertexLocal;
            var pivotLocal = (rotationPivotMirrored - bonePivotMirrored) / PixelsPerUnit;
            return pivotLocal + cubeRotation * (vertexLocal - pivotLocal);
        }

        private static Vector3 ConvertPosition(float[] modelPosition, float[] parentModelPosition) =>
            new Vector3(-(modelPosition[0] - parentModelPosition[0]), modelPosition[1] - parentModelPosition[1], modelPosition[2] - parentModelPosition[2]) / PixelsPerUnit;

        /// <summary>Source degrees in the X-mirrored frame: negate X/Y, retain Z. Bedrock ZYX product applies X, then Y, then Z.</summary>
        public static Quaternion ConvertBoneRotation(float[] rotationDegrees)
        {
            return Quaternion.AngleAxis(rotationDegrees[2], Vector3.forward) *
                   Quaternion.AngleAxis(-rotationDegrees[1], Vector3.up) *
                   Quaternion.AngleAxis(-rotationDegrees[0], Vector3.right);
        }

        private static void FitModel(GameObject model, BuildOptions options)
        {
            var renderers = model.GetComponentsInChildren<MeshRenderer>();
            if (renderers.Length == 0) return;
            var bounds = RenderersBounds(renderers);
            if (options.TargetWidth > 0f)
            {
                var width = Mathf.Max(0.0001f, bounds.size.x);
                model.transform.localScale = Vector3.one * (options.TargetWidth / width);
                bounds = RenderersBounds(renderers);
            }
            else if (options.TargetHeight > 0f)
            {
                var height = Mathf.Max(0.0001f, bounds.size.y);
                model.transform.localScale = Vector3.one * (options.TargetHeight / height);
                bounds = RenderersBounds(renderers);
            }
            if (options.GroundToBaseY || !Mathf.Approximately(options.BaseY, 0f))
            {
                model.transform.localPosition += new Vector3(0f, options.BaseY - bounds.min.y, 0f);
            }
        }

        private static Bounds RenderersBounds(IReadOnlyList<MeshRenderer> renderers)
        {
            var bounds = renderers[0].bounds;
            for (var index = 1; index < renderers.Count; index++) bounds.Encapsulate(renderers[index].bounds);
            return bounds;
        }
    }
}
