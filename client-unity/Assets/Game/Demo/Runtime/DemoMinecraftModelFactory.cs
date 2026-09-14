using System;
using System.Collections.Generic;
using UnityEngine;

namespace BiomeRivals.Demo
{
    /// <summary>
    /// Builds battlefield creature pieces from vanilla Minecraft entity
    /// geometry ("minecraft:geometry" files) matched with entity textures.
    /// Geometry/texture pairs are extracted from Mojang's official
    /// bedrock-samples repository by scripts/extract-minecraft-entity-models.ps1
    /// into Assets/Generated/MinecraftWorldTextures/Resources/DemoWorld (never
    /// committed). When the local extraction has not been run, TryBuild returns
    /// false and the battlefield falls back to the generic block creature.
    /// </summary>
    public static class DemoMinecraftModelFactory
    {
        private const string ModelResourceRoot = "DemoWorld/entity_models/";

        private sealed class EntitySpec
        {
            public string GeometryId;
            public string TextureKey;
            public float TargetHeight;
            public float TargetWidth;
            public float HoverY;
            public float ExtraScale = 1f;
            public string OverlayTextureKey;
            public float OverlayInflate;
            public Dictionary<string, float[]> BonePivotOverrides;
            public Dictionary<string, float[]> BoneRotationOverrides;
            public DemoEntityIdleAnimator.IdleTrackSpec[] IdleTracks;
        }

        private static readonly Dictionary<string, EntitySpec> Entities = new Dictionary<string, EntitySpec>
        {
            {
                "pf_001", new EntitySpec
                { GeometryId = "geometry.bee", TextureKey = "entity_bee",
                    TargetHeight = 1.5f, HoverY = 0.32f
                }
            },
            {
                "pf_002", new EntitySpec
                { GeometryId = "geometry.sheep.sheared.v1.8", TextureKey = "entity_sheep",
                    TargetHeight = 1.45f, OverlayTextureKey = "entity_sheep_wool", OverlayInflate = 1.2f
                }
            },
            {
                "tk_003", new EntitySpec
                { GeometryId = "geometry.sheep.sheared.v1.8", TextureKey = "entity_sheep",
                    TargetHeight = 1.45f, ExtraScale = 0.62f, OverlayTextureKey = "entity_sheep_wool", OverlayInflate = 1.2f
                }
            },
            {
                "tk_004", new EntitySpec
                { GeometryId = "geometry.wolf", TextureKey = "entity_wolf",
                    TargetHeight = 1.4f, ExtraScale = 0.82f
                }
            },
            {
                "pf_003", new EntitySpec
                { GeometryId = "geometry.wolf", TextureKey = "entity_wolf", TargetHeight = 1.4f
                }
            },
            {
                "pf_004", new EntitySpec
                { GeometryId = "geometry.villager_v2", TextureKey = "entity_villager",
                    TargetHeight = 2.05f
                }
            },
            {
                "cd_005", new EntitySpec
                { GeometryId = "geometry.vindicator.v1.8", TextureKey = "entity_vindicator",
                    TargetHeight = 2.15f, ExtraScale = 0.96f
                }
            },
            {
                "tk_011", new EntitySpec
                { GeometryId = "geometry.vindicator.v1.8", TextureKey = "entity_vindicator",
                    TargetHeight = 2.15f, ExtraScale = 0.82f
                }
            },
            {
                "pf_008", new EntitySpec
                { GeometryId = "geometry.irongolem", TextureKey = "entity_iron_golem",
                    TargetHeight = 2.6f, ExtraScale = 0.9f
                }
            },
            {
                "si_002", new EntitySpec
                { GeometryId = "geometry.snowgolem.v1.8", TextureKey = "entity_snow_golem",
                    TargetHeight = 2f, ExtraScale = 0.92f
                }
            },
            {
                "si_004", new EntitySpec
                { GeometryId = "geometry.goat", TextureKey = "entity_goat",
                    TargetHeight = 1.8f, ExtraScale = 0.9f
                }
            },
            {
                "cd_001", new EntitySpec
                { GeometryId = "geometry.bat_v2", TextureKey = "entity_bat",
                    TargetHeight = 1.05f, HoverY = 0.55f
                }
            },
            {
                "cd_002", new EntitySpec
                { GeometryId = "geometry.spider.v1.8", TextureKey = "entity_cave_spider",
                    // Spiders span far wider than they are tall; fit the leg spread instead of the height.
                    TargetWidth = 2.2f, ExtraScale = 0.9f
                }
            },
            {
                "nt_001", new EntitySpec
                { GeometryId = "geometry.slime", TextureKey = "entity_magma_cube",
                    TargetHeight = 1.7f
                }
            },
            {
                "tk_014", new EntitySpec
                { GeometryId = "geometry.slime", TextureKey = "entity_magma_cube",
                    TargetHeight = 1.7f, ExtraScale = 0.62f
                }
            },
            {
                "nt_003", new EntitySpec
                { GeometryId = "geometry.blaze", TextureKey = "entity_blaze",
                    TargetHeight = 2.05f, BonePivotOverrides = BlazeRodPivots()
                }
            },
            {
                "si_003", new EntitySpec
                { GeometryId = "geometry.skeleton.stray.v1.8", TextureKey = "entity_stray",
                    TargetHeight = 2.1f, OverlayTextureKey = "entity_stray_overlay", OverlayInflate = 0.55f
                }
            },
            {
                "or_001", new EntitySpec
                { GeometryId = "geometry.salmon", TextureKey = "entity_salmon",
                    TargetHeight = 1f, HoverY = 0.45f
                }
            },
            {
                "or_002", new EntitySpec
                { GeometryId = "geometry.dolphin", TextureKey = "entity_dolphin",
                    TargetHeight = 1.25f, HoverY = 0.35f
                }
            },
            {
                "or_003", new EntitySpec
                { GeometryId = "geometry.zombie.drowned.v1.16", TextureKey = "entity_drowned",
                    TargetHeight = 2.1f
                }
            },
            {
                "or_004", new EntitySpec
                { GeometryId = "geometry.guardian.v1.8", TextureKey = "entity_guardian",
                    TargetHeight = 1.6f, HoverY = 0.4f
                }
            },
            {
                "db_001", new EntitySpec
                { GeometryId = "geometry.zombie.husk.v1.8", TextureKey = "entity_husk",
                    TargetHeight = 2f
                }
            },
            {
                "db_003", new EntitySpec
                { GeometryId = "geometry.villager_v2", TextureKey = "entity_villager_desert",
                    TargetHeight = 2.05f
                }
            },
            {
                "db_005", new EntitySpec
                { GeometryId = "geometry.pillager", TextureKey = "entity_pillager",
                    TargetHeight = 2f
                }
            },
            {
                "db_008", new EntitySpec
                { GeometryId = "geometry.camel", TextureKey = "entity_camel",
                    // The camel silhouette is taller than it is wide; fit the height.
                    TargetHeight = 2.2f
                }
            },
            {
                "si_005", new EntitySpec
                { GeometryId = "geometry.polarbear", TextureKey = "entity_polar_bear",
                    TargetHeight = 2f
                }
            },
            {
                "cd_003", new EntitySpec
                { GeometryId = "geometry.skeleton.v1.8", TextureKey = "entity_skeleton",
                    TargetHeight = 2f
                }
            },
            {
                "or_005", new EntitySpec
                { GeometryId = "geometry.turtle", TextureKey = "entity_turtle",
                    TargetWidth = 1.8f, HoverY = 0.05f
                }
            },
            {
                "nt_002", new EntitySpec
                { GeometryId = "geometry.pigzombie.v1.8", TextureKey = "entity_zombie_pigman",
                    TargetHeight = 2f
                }
            },
            {
                "nt_004", new EntitySpec
                { GeometryId = "geometry.strider", TextureKey = "entity_strider",
                    TargetHeight = 2f
                }
            },
            {
                "nt_005", new EntitySpec
                { GeometryId = "geometry.skeleton.wither.v1.8", TextureKey = "entity_wither_skeleton",
                    TargetHeight = 2.3f
                }
            },
            {
                "ed_001", new EntitySpec
                { GeometryId = "geometry.endermite", TextureKey = "entity_endermite",
                    TargetHeight = 0.75f
                }
            },
            {
                "ed_003", new EntitySpec
                { GeometryId = "geometry.enderman.v1.8", TextureKey = "entity_enderman",
                    TargetHeight = 2.8f
                }
            },
            {
                "ed_004", new EntitySpec
                { GeometryId = "geometry.shulker.v1.8", TextureKey = "entity_shulker",
                    TargetHeight = 1.15f, HoverY = 0.05f
                }
            },
            {
                "tk_015", new EntitySpec
                { GeometryId = "geometry.skeleton.wither.v1.8", TextureKey = "entity_wither_skeleton",
                    TargetHeight = 2.3f
                }
            },
            {
                "tk_017", new EntitySpec
                { GeometryId = "geometry.dragon", TextureKey = "entity_dragon",
                    // Vanilla spreads the right wing via animation; bake the yaw and fit the wingspan.
                    TargetWidth = 3.6f, HoverY = 0.55f,
                    BoneRotationOverrides = new Dictionary<string, float[]> { { "wing1", new[] { 0f, 180f, 0f } } }
                }
            }
        };

        static DemoMinecraftModelFactory()
        {
            var flap = Vector3.forward;
            Entities["pf_001"].IdleTracks = new[]
            {
                Track("rightwing_bone", 35f, flap, 13f),
                Track("leftwing_bone", -35f, flap, 13f)
            };
            Entities["cd_001"].IdleTracks = new[]
            {
                Track("rightWing", 45f, flap, 6.5f),
                Track("leftWing", -45f, flap, 6.5f),
                Track("rightWingTip", 24f, flap, 6.5f, 0.6f),
                Track("leftWingTip", -24f, flap, 6.5f, 0.6f)
            };
            var blazeRods = new List<DemoEntityIdleAnimator.IdleTrackSpec>();
            for (var rod = 0; rod < 12; rod++)
            {
                blazeRods.Add(Track("upperBodyParts" + rod, 0f, Vector3.up, 1.7f, rod * 0.55f, 0.045f));
                blazeRods.Add(Track("upperBodyParts" + rod, 16f, Vector3.up, 0.9f, rod * 0.55f));
            }
            blazeRods.Add(Track("head", 5f, Vector3.right, 0.7f));
            Entities["nt_003"].IdleTracks = blazeRods.ToArray();
            Entities["or_004"].IdleTracks = new[]
            {
                Track("tailpart0", 10f, Vector3.up, 1.1f),
                Track("tailpart1", 14f, Vector3.up, 1.1f, 0.55f),
                Track("tailpart2", 18f, Vector3.up, 1.1f, 1.1f),
                Track("head", 4f, Vector3.right, 0.6f)
            };
            Entities["or_002"].IdleTracks = new[]
            {
                Track("tail", 9f, Vector3.up, 1.5f),
                Track("tail_fin", 13f, Vector3.up, 1.5f, 0.5f)
            };
            Entities["or_001"].IdleTracks = new[] { Track("tailfin", 12f, Vector3.up, 2.2f) };
            var wolfTail = new[] { Track("tail", 15f, Vector3.up, 2.4f), Track("head", 3f, Vector3.right, 0.55f) };
            Entities["pf_003"].IdleTracks = wolfTail;
            Entities["tk_004"].IdleTracks = wolfTail;
            var spiderLegs = new List<DemoEntityIdleAnimator.IdleTrackSpec>();
            for (var leg = 0; leg < 8; leg++) spiderLegs.Add(Track("leg" + leg, 3f, Vector3.up, 1.9f, leg * 0.7f));
            Entities["cd_002"].IdleTracks = spiderLegs.ToArray();
            var undeadArms = new[]
            {
                Track("rightArm", 4f, Vector3.right, 1f),
                Track("leftArm", 4f, Vector3.right, 1f, Mathf.PI),
                Track("head", 5f, Vector3.up, 0.42f)
            };
            Entities["si_003"].IdleTracks = undeadArms;
            Entities["or_003"].IdleTracks = undeadArms;
            var raiderArms = new[]
            {
                Track("rightArm", 3f, Vector3.right, 1f),
                Track("leftArm", 3f, Vector3.right, 1f, Mathf.PI),
                Track("head", 4f, Vector3.up, 0.4f)
            };
            Entities["cd_005"].IdleTracks = raiderArms;
            Entities["tk_011"].IdleTracks = raiderArms;
            var grazeHead = new[] { Track("head", 4f, Vector3.right, 0.5f) };
            Entities["pf_002"].IdleTracks = grazeHead;
            Entities["tk_003"].IdleTracks = grazeHead;
            Entities["si_004"].IdleTracks = new[] { Track("head", 3.5f, Vector3.right, 0.55f) };
            Entities["pf_008"].IdleTracks = new[]
            {
                Track("arm0", 2.5f, Vector3.right, 0.8f),
                Track("arm1", 2.5f, Vector3.right, 0.8f, Mathf.PI),
                Track("head", 4f, Vector3.up, 0.35f)
            };
            Entities["si_002"].IdleTracks = new[]
            {
                Track("head", 6f, Vector3.up, 0.65f),
                Track("arm1", 5f, Vector3.right, 1.2f),
                Track("arm2", 5f, Vector3.right, 1.2f, Mathf.PI)
            };
            Entities["pf_004"].IdleTracks = new[] { Track("head", 5f, Vector3.up, 0.45f) };
            Entities["db_003"].IdleTracks = new[] { Track("head", 5f, Vector3.up, 0.45f) };
            Entities["db_005"].IdleTracks = new[]
            {
                Track("rightarm", 4f, Vector3.right, 1f),
                Track("leftarm", 4f, Vector3.right, 1f, Mathf.PI),
                Track("head", 5f, Vector3.up, 0.4f)
            };
            Entities["db_008"].IdleTracks = new[]
            {
                Track("head", 3f, Vector3.right, 0.5f),
                Track("tail", 8f, Vector3.up, 1.8f)
            };
            Entities["si_005"].IdleTracks = new[]
            {
                Track("head", 3.5f, Vector3.right, 0.5f),
                Track("leg0", 1.5f, Vector3.right, 0.8f),
                Track("leg1", 1.5f, Vector3.right, 0.8f, Mathf.PI),
                Track("leg2", 1.5f, Vector3.right, 0.8f),
                Track("leg3", 1.5f, Vector3.right, 0.8f, Mathf.PI)
            };
            Entities["or_005"].IdleTracks = new[]
            {
                Track("head", 6f, Vector3.up, 0.4f),
                Track("leg0", 1.5f, Vector3.right, 0.7f),
                Track("leg1", 1.5f, Vector3.right, 0.7f, Mathf.PI),
                Track("leg2", 1.5f, Vector3.right, 0.7f),
                Track("leg3", 1.5f, Vector3.right, 0.7f, Mathf.PI)
            };
            Entities["nt_004"].IdleTracks = new[]
            {
                Track("right_leg", 2f, Vector3.right, 1.2f),
                Track("left_leg", 2f, Vector3.right, 1.2f, Mathf.PI),
                Track("bristle0", 5f, Vector3.forward, 1.4f),
                Track("bristle1", -5f, Vector3.forward, 1.4f, 0.5f),
                Track("bristle2", 5f, Vector3.forward, 1.4f, 1f),
                Track("bristle3", -5f, Vector3.forward, 1.4f, 1.5f),
                Track("bristle4", 5f, Vector3.forward, 1.4f, 2f),
                Track("bristle5", -5f, Vector3.forward, 1.4f, 2.5f)
            };
            Entities["ed_001"].IdleTracks = new[]
            {
                Track("section_0", 9f, Vector3.up, 1.6f),
                Track("section_1", -9f, Vector3.up, 1.6f, 0.8f),
                Track("section_2", 9f, Vector3.up, 1.6f, 1.6f),
                Track("section_3", -9f, Vector3.up, 1.6f, 2.4f)
            };
            Entities["ed_003"].IdleTracks = new[]
            {
                Track("rightArm", 3f, Vector3.right, 1f),
                Track("leftArm", 3f, Vector3.right, 1f, Mathf.PI),
                Track("head", 6f, Vector3.up, 0.4f)
            };
            Entities["ed_004"].IdleTracks = new[] { Track("head", 7f, Vector3.right, 0.5f) };
            Entities["tk_017"].IdleTracks = new[]
            {
                Track("wing", 9f, Vector3.forward, 0.5f),
                Track("wing1", -9f, Vector3.forward, 0.5f),
                Track("wingtip", 7f, Vector3.forward, 0.5f, 0.4f),
                Track("wingtip1", -7f, Vector3.forward, 0.5f, 0.4f),
                Track("head", 6f, Vector3.up, 0.35f),
                Track("neck", 5f, Vector3.up, 0.35f, 0.3f),
                Track("jaw", 6f, Vector3.right, 0.35f, 0.3f)
            };
        }

        private static DemoEntityIdleAnimator.IdleTrackSpec Track(
            string boneName, float amplitudeDegrees, Vector3 axis, float frequency, float phase = 0f, float positionAmplitude = 0f)
        {
            return new DemoEntityIdleAnimator.IdleTrackSpec
            {
                BoneName = boneName,
                AmplitudeDegrees = amplitudeDegrees,
                Axis = axis,
                Frequency = frequency,
                Phase = phase,
                PositionAmplitude = positionAmplitude
            };
        }

        public static bool TryGetTextureKey(string cardId, out string textureKey)
        {
            if (Entities.TryGetValue(cardId, out var spec))
            {
                textureKey = spec.TextureKey;
                return true;
            }
            textureKey = null;
            return false;
        }

        /// <summary>Geometry resource key under Resources/DemoWorld/entity_models (same key as the entity texture).</summary>
        public static bool TryGetModelKey(string cardId, out string modelKey)
        {
            return TryGetTextureKey(cardId, out modelKey);
        }

        /// <summary>materialProvider receives a texture key (entity_*) per cube layer.</summary>
        public static bool TryBuild(Transform root, string cardId, bool player, Func<string, Material> materialProvider)
        {
            if (!Entities.TryGetValue(cardId, out var spec)) return false;
            var geometry = LoadGeometry(spec.TextureKey, spec.GeometryId);
            if (geometry == null) return false;

            root.localRotation = Quaternion.Euler(0f, player ? 180f : 0f, 0f);

            var options = new DemoMinecraftEntityModelBuilder.BuildOptions
            {
                MaterialProvider = materialProvider,
                PrimaryTextureKey = spec.TextureKey,
                TextureWidth = geometry.TextureWidth,
                TextureHeight = geometry.TextureHeight,
                TargetHeight = spec.TargetHeight,
                TargetWidth = spec.TargetWidth,
                BaseY = spec.HoverY,
                BonePivotOverrides = spec.BonePivotOverrides,
                BoneRotationOverrides = spec.BoneRotationOverrides
            };
            if (!string.IsNullOrEmpty(spec.OverlayTextureKey))
            {
                options.Layers.Add(new DemoMinecraftEntityModelBuilder.OverlayLayer
                {
                    TextureKey = spec.OverlayTextureKey,
                    Inflate = spec.OverlayInflate
                });
            }
            if (!DemoMinecraftEntityModelBuilder.TryBuild(root, geometry, options)) return false;
            // Applied after the auto-fit so juvenile variants stay smaller than the fitted adult size.
            if (!Mathf.Approximately(spec.ExtraScale, 1f)) root.localScale = Vector3.one * spec.ExtraScale;
            if (spec.IdleTracks != null) DemoEntityIdleAnimator.Attach(root.gameObject, spec.IdleTracks);
            return true;
        }

        private static DemoEntityGeometry LoadGeometry(string resourceKey, string geometryId)
        {
            var asset = Resources.Load<TextAsset>(ModelResourceRoot + resourceKey);
            if (asset == null) return null;
            try
            {
                return DemoMinecraftEntityGeometryParser.Parse(asset.text, geometryId);
            }
            catch (FormatException error)
            {
                Debug.LogWarning($"Skipping entity model '{resourceKey}': {error.Message}");
                return null;
            }
        }

        /// <summary>
        /// The vanilla blaze geometry stacks all twelve rods on one column and
        /// relies on runtime animation; bake the resting ring pose so the
        /// static battlefield model shows the classic three-ring silhouette.
        /// </summary>
        private static Dictionary<string, float[]> BlazeRodPivots()
        {
            var pivots = new Dictionary<string, float[]>();
            for (var rod = 0; rod < 12; rod++)
            {
                var ring = rod / 4;
                var angle = (float)((rod % 4) * System.Math.PI * 0.5 + ring * 0.58);
                var radius = ring == 1 ? 10.5f : 7.5f;
                pivots["upperBodyParts" + rod] = new[]
                {
                    Mathf.Cos(angle) * radius,
                    24f - ring * 7f,
                    Mathf.Sin(angle) * radius
                };
            }
            return pivots;
        }
    }
}
