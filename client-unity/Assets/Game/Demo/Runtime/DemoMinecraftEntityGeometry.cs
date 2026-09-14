using System;
using System.Collections.Generic;
using System.Linq;

namespace BiomeRivals.Demo
{
    public sealed class DemoEntityGeometry
    {
        public string Identifier;
        public int TextureWidth = 64;
        public int TextureHeight = 64;
        public List<DemoEntityBone> Bones = new List<DemoEntityBone>();
    }

    public sealed class DemoEntityBone
    {
        public string Name;
        public string Parent;
        public float[] Pivot = { 0f, 0f, 0f };
        /// <summary>Bind rotation in bedrock degrees, exactly as stored in the file.</summary>
        public float[] Rotation;
        public List<DemoEntityCube> Cubes = new List<DemoEntityCube>();
    }

    public sealed class DemoEntityCube
    {
        public float[] Origin = { 0f, 0f, 0f };
        public float[] Size = { 1f, 1f, 1f };
        public float[] Uv = { 0f, 0f };
        public bool Mirror;
        public float Inflate;
        /// <summary>Optional cube rotation: bedrock axis name and angle in degrees around Pivot.</summary>
        public string RotationAxis;
        public float RotationAngle;
        public float[] RotationPivot;
        /// <summary>Bedrock also allows [x, y, z] degree rotations on cubes.</summary>
        public float[] CompositeRotation;
    }

    /// <summary>
    /// Parses vanilla "minecraft:geometry" entity model files (Blockbench
    /// compatible, as published in Mojang/bedrock-samples). Supports both the
    /// 1.12+ format with a "minecraft:geometry" array and the legacy 1.8.0
    /// format whose root keys are named "geometry.&lt;identifier&gt;".
    /// </summary>
    public static class DemoMinecraftEntityGeometryParser
    {
        public static DemoEntityGeometry Parse(string json, string preferredIdentifier = null)
        {
            var root = DemoJsonParser.ParseObject(json);
            if (root.TryGetValue("minecraft:geometry", out var geometryList) && geometryList is List<object> geometries && geometries.Count > 0)
            {
                Dictionary<string, object> chosen = null;
                foreach (var candidate in geometries.OfType<Dictionary<string, object>>())
                {
                    var description = candidate.TryGetValue("description", out var descriptionValue)
                        ? descriptionValue as Dictionary<string, object>
                        : null;
                    var identifier = description?.TryGetValue("identifier", out var identifierValue) == true
                        ? identifierValue as string
                        : null;
                    if (preferredIdentifier != null && string.Equals(identifier, preferredIdentifier, StringComparison.Ordinal))
                    {
                        chosen = candidate;
                        break;
                    }
                    chosen ??= candidate;
                }
                if (chosen == null) throw new FormatException("No minecraft:geometry entry could be selected.");
                return ParseModernGeometry(chosen);
            }

            foreach (var pair in root)
            {
                if (!pair.Key.StartsWith("geometry.", StringComparison.Ordinal)) continue;
                if (pair.Key.Contains(":")) continue; // alias entries such as "geometry.a:geometry.b"
                if (pair.Value is Dictionary<string, object> legacy) return ParseLegacyGeometry(pair.Key, legacy);
            }
            throw new FormatException("No geometry definition found in entity model file.");
        }

        private static DemoEntityGeometry ParseModernGeometry(Dictionary<string, object> geometry)
        {
            var description = geometry.TryGetValue("description", out var descriptionValue)
                ? descriptionValue as Dictionary<string, object>
                : null;
            var result = new DemoEntityGeometry
            {
                Identifier = description?.TryGetValue("identifier", out var identifier) == true ? identifier as string : null,
                TextureWidth = (int)ReadNumber(description, "texture_width", 64),
                TextureHeight = (int)ReadNumber(description, "texture_height", 64)
            };
            if (geometry.TryGetValue("bones", out var bonesValue) && bonesValue is List<object> bones)
            {
                foreach (var bone in bones.OfType<Dictionary<string, object>>()) result.Bones.Add(ParseBone(bone));
            }
            return result;
        }

        private static DemoEntityGeometry ParseLegacyGeometry(string identifier, Dictionary<string, object> geometry)
        {
            var result = new DemoEntityGeometry
            {
                Identifier = identifier,
                TextureWidth = (int)ReadNumber(geometry, "texturewidth", 64),
                TextureHeight = (int)ReadNumber(geometry, "textureheight", 64)
            };
            if (geometry.TryGetValue("bones", out var bonesValue) && bonesValue is List<object> bones)
            {
                foreach (var bone in bones.OfType<Dictionary<string, object>>()) result.Bones.Add(ParseBone(bone));
            }
            return result;
        }

        private static DemoEntityBone ParseBone(Dictionary<string, object> bone)
        {
            float[] rotation = null;
            if (bone.TryGetValue("rotation", out var rotationValue) && rotationValue is List<object>) rotation = ReadVector(bone, "rotation", null);
            if (rotation == null && bone.TryGetValue("bind_pose_rotation", out _)) rotation = ReadVector(bone, "bind_pose_rotation", null);
            var result = new DemoEntityBone
            {
                Name = bone.TryGetValue("name", out var nameValue) ? nameValue as string : throw new FormatException("Geometry bone is missing a name."),
                Parent = bone.TryGetValue("parent", out var parentValue) ? parentValue as string : null,
                Pivot = ReadVector(bone, "pivot", new[] { 0f, 0f, 0f }),
                Rotation = rotation
            };
            var boneInflate = bone.TryGetValue("inflate", out var inflateValue) ? (float)Convert.ToDouble(inflateValue) : 0f;
            if (bone.TryGetValue("cubes", out var cubesValue) && cubesValue is List<object> cubes)
            {
                foreach (var cube in cubes.OfType<Dictionary<string, object>>())
                {
                    var parsed = ParseCube(cube);
                    parsed.Inflate += boneInflate;
                    result.Cubes.Add(parsed);
                }
            }
            return result;
        }

        private static DemoEntityCube ParseCube(Dictionary<string, object> cube)
        {
            var result = new DemoEntityCube
            {
                Origin = ReadVector(cube, "origin", new[] { 0f, 0f, 0f }),
                Size = ReadVector(cube, "size", new[] { 1f, 1f, 1f }),
                Mirror = cube.TryGetValue("mirror", out var mirrorValue) && Convert.ToBoolean(mirrorValue),
                Inflate = cube.TryGetValue("inflate", out var inflateValue) ? (float)Convert.ToDouble(inflateValue) : 0f
            };
            if (cube.TryGetValue("uv", out var uvValue))
            {
                switch (uvValue)
                {
                    case List<object> uvRect when uvRect.Count >= 2:
                        result.Uv = new[] { (float)Convert.ToDouble(uvRect[0]), (float)Convert.ToDouble(uvRect[1]) };
                        break;
                    case Dictionary<string, object> perFace:
                        throw new FormatException("Per-face UV geometry is not supported yet; cube uses a uv object.");
                    default:
                        throw new FormatException("Unsupported cube uv encoding.");
                }
            }
            if (cube.TryGetValue("rotation", out var rotationValue))
            {
                switch (rotationValue)
                {
                    case Dictionary<string, object> rotationObject when rotationObject.TryGetValue("axis", out var axisValue):
                        result.RotationAxis = (axisValue as string ?? "x").ToLowerInvariant();
                        result.RotationAngle = (float)Convert.ToDouble(rotationObject["angle"]);
                        result.RotationPivot = ReadVector(rotationObject, "pivot", new[] { 0f, 0f, 0f });
                        break;
                    case List<object> rotationArray when rotationArray.Count >= 3:
                        result.RotationAxis = "xyz";
                        result.RotationAngle = 0f;
                        result.RotationPivot = ReadVector(cube, "pivot", new[] { 0f, 0f, 0f });
                        result.CompositeRotation = new[]
                        {
                            (float)Convert.ToDouble(rotationArray[0]),
                            (float)Convert.ToDouble(rotationArray[1]),
                            (float)Convert.ToDouble(rotationArray[2])
                        };
                        break;
                    default:
                        throw new FormatException("Unsupported cube rotation encoding.");
                }
            }
            return result;
        }

        private static float[] ReadVector(Dictionary<string, object> source, string key, float[] fallback)
        {
            if (!source.TryGetValue(key, out var value) || !(value is List<object> values) || values.Count < 3) return fallback;
            return new[]
            {
                (float)Convert.ToDouble(values[0]),
                (float)Convert.ToDouble(values[1]),
                (float)Convert.ToDouble(values[2])
            };
        }

        private static double ReadNumber(Dictionary<string, object> source, string key, double fallback)
        {
            if (source == null || !source.TryGetValue(key, out var value)) return fallback;
            return Convert.ToDouble(value);
        }
    }
}
