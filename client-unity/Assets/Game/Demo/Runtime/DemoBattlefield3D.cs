using System;
using System.Collections.Generic;
using System.Linq;
using BiomeRivals.Content;
using UnityEngine;
using UnityEngine.Rendering;

namespace BiomeRivals.Demo
{
    public sealed class DemoBattlefield3D : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        private static readonly float[] PlayerUnitX = { -4.65f, -1.55f, 1.55f, 4.65f };
        private static readonly float[] PlayerBuildingX = { -4.35f, 0f, 4.35f };
        private static readonly float[] OpponentUnitX = { -4.8f, -1.6f, 1.6f, 4.8f };
        private static readonly float[] OpponentBuildingX = { -4.5f, 0f, 4.5f };

        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>(StringComparer.Ordinal);
        private readonly Dictionary<string, SlotMarker> _slotMarkers = new Dictionary<string, SlotMarker>(StringComparer.Ordinal);
        private readonly List<Floater> _floaters = new List<Floater>();
        [SerializeField] private Shader blockShader;
        [SerializeField] private Shader backdropShader;
        [SerializeField] private Texture2D illustratedBackdrop;
        private Transform _terrainRoot;
        private Transform _piecesRoot;
        private Camera _camera;
        private bool _built;
        private string _pieceSignature = string.Empty;

        public Camera BoardCamera => _camera;

        public void Configure(Shader worldShader, Shader unlitBackdropShader, Texture2D backdrop)
        {
            blockShader = worldShader;
            backdropShader = unlitBackdropShader;
            illustratedBackdrop = backdrop;
        }

        public void BuildNow()
        {
            if (_built) return;
            _built = true;
            CreateRoots();
            CreateMaterials();
            ConfigureWorld();
            BuildIllustratedBackdrop();
            if (illustratedBackdrop == null) BuildTerrain();
            BuildSlotPads();
            if (illustratedBackdrop == null) BuildBiomeDecor();
        }

        public Vector3 GetSlotWorldPosition(bool player, DemoSlotKind kind, int index)
        {
            var xValues = player
                ? kind == DemoSlotKind.Unit ? PlayerUnitX : PlayerBuildingX
                : kind == DemoSlotKind.Unit ? OpponentUnitX : OpponentBuildingX;
            if (index < 0 || index >= xValues.Length) throw new ArgumentOutOfRangeException(nameof(index));
            var z = player
                ? kind == DemoSlotKind.Unit ? -2.15f : -4.35f
                : kind == DemoSlotKind.Unit ? 2.05f : 4.15f;
            var y = player ? 0.22f : 0.30f;
            return new Vector3(xValues[index], y, z);
        }

        public Vector2 GetSlotReferencePosition(bool player, DemoSlotKind kind, int index)
        {
            BuildNow();
            var viewport = _camera.WorldToViewportPoint(GetSlotWorldPosition(player, kind, index));
            return new Vector2((viewport.x - 0.5f) * ReferenceWidth, (viewport.y - 0.5f) * ReferenceHeight);
        }

        public void SetSlotState(bool player, DemoSlotKind kind, int index, bool validTarget, bool occupied)
        {
            BuildNow();
            if (!_slotMarkers.TryGetValue(SlotKey(player, kind, index), out var marker)) return;
            marker.ValidTarget = validTarget;
            marker.Occupied = occupied;
        }

        public void SetSlotHovered(bool player, DemoSlotKind kind, int index, bool hovered)
        {
            BuildNow();
            if (_slotMarkers.TryGetValue(SlotKey(player, kind, index), out var marker)) marker.Hovered = hovered;
        }

        public void SyncPieces(
            IReadOnlyList<string> playerUnits,
            IReadOnlyList<string> playerBuildings,
            IReadOnlyList<string> opponentUnits,
            IReadOnlyList<string> opponentBuildings,
            CardContentRegistry registry)
        {
            BuildNow();
            var signature = string.Join("|", playerUnits.Concat(playerBuildings).Concat(opponentUnits).Concat(opponentBuildings));
            if (signature == _pieceSignature) return;
            _pieceSignature = signature;
            ClearChildren(_piecesRoot);
            _floaters.Clear();
            CreateSidePieces(true, DemoSlotKind.Unit, playerUnits, registry);
            CreateSidePieces(true, DemoSlotKind.Building, playerBuildings, registry);
            CreateSidePieces(false, DemoSlotKind.Unit, opponentUnits, registry);
            CreateSidePieces(false, DemoSlotKind.Building, opponentBuildings, registry);
        }

        private void Update()
        {
            var time = Time.unscaledTime;
            foreach (var floater in _floaters)
            {
                if (floater.Transform == null) continue;
                var position = floater.Transform.localPosition;
                position.y = floater.BaseY + Mathf.Sin(time * 2.1f + floater.Phase) * 0.055f;
                floater.Transform.localPosition = position;
                floater.Transform.localRotation = Quaternion.Euler(0, Mathf.Sin(time * 0.7f + floater.Phase) * 5f, 0);
            }

            foreach (var marker in _slotMarkers.Values) UpdateSlotMarker(marker, time);
        }

        private static void UpdateSlotMarker(SlotMarker marker, float time)
        {
            if (marker.Root == null || marker.Material == null) return;
            var pulse = 0.5f + Mathf.Sin(time * 4.6f + marker.Phase) * 0.5f;
            var actionableHover = marker.Hovered && marker.ValidTarget && !marker.Occupied;
            var color = actionableHover
                ? Hex("#F1C96A")
                : marker.ValidTarget && !marker.Occupied
                    ? Color.Lerp(Hex("#3D9E8F"), Hex("#79E0CB"), pulse)
                    : marker.Occupied
                        ? Hex("#3A3028")
                        : marker.Hovered ? Hex("#6B685D") : Hex("#3B3C37");
            var emission = actionableHover
                ? color * 0.75f
                : marker.ValidTarget && !marker.Occupied
                    ? color * (0.38f + pulse * 0.48f)
                    : Color.black;
            SetMaterialColor(marker.Material, color, emission);
            var position = marker.BasePosition;
            position.y += actionableHover ? 0.085f : marker.ValidTarget && !marker.Occupied ? pulse * 0.045f : marker.Hovered ? 0.018f : 0f;
            marker.Root.localPosition = position;
        }

        private void CreateRoots()
        {
            _terrainRoot = NewRoot("BattlefieldGeometry");
            _piecesRoot = NewRoot("BattlefieldPieces");
        }

        private Transform NewRoot(string name)
        {
            var root = new GameObject(name).transform;
            root.SetParent(transform, false);
            return root;
        }

        private void CreateMaterials()
        {
            AddMaterial("dirt", "#6C4D32", "dirt");
            AddMaterial("grass", "#6A873E", "grass_block_top");
            AddMaterial("moss", "#61754B", "mossy_stone_bricks");
            AddMaterial("oak", "#85623B", "oak_planks");
            AddMaterial("stone", "#666962", "stone_bricks");
            AddMaterial("blackstone", "#29272D", "polished_blackstone_bricks");
            AddMaterial("nether", "#5A2428", "nether_bricks");
            AddMaterial("netherrack", "#6A2E2C", "netherrack");
            AddMaterial("basalt", "#333238", "basalt_top");
            AddMaterial("water", "#2B8D8D", "water_still", "#123B3B");
            AddMaterial("magma", "#C66828", "magma", "#7A2608");
            AddMaterial("player_slot", "#243A34", string.Empty);
            AddMaterial("enemy_slot", "#432B2A", string.Empty);
            AddMaterial("slot_edge", "#8E8268", string.Empty);
            AddMaterial("leaf", "#426034", "oak_leaves");
            AddMaterial("ember", "#E18B42", string.Empty, "#8A2C0A");
        }

        private void AddMaterial(string key, string color, string texture, string emission = "#000000")
        {
            ColorUtility.TryParseHtmlString(color, out var baseColor);
            ColorUtility.TryParseHtmlString(emission, out var emissionColor);
            _materials[key] = DemoWorldAssetProvider.CreateBlockMaterial("DemoWorld_" + key, baseColor, texture, emissionColor, blockShader);
        }

        private void ConfigureWorld()
        {
            RenderSettings.ambientMode = AmbientMode.Trilight;
            RenderSettings.ambientSkyColor = Hex("#52606B");
            RenderSettings.ambientEquatorColor = Hex("#343A35");
            RenderSettings.ambientGroundColor = Hex("#151310");
            RenderSettings.fog = true;
            RenderSettings.fogColor = Hex("#111513");
            RenderSettings.fogMode = FogMode.Linear;
            RenderSettings.fogStartDistance = 16f;
            RenderSettings.fogEndDistance = 34f;

            var cameraObject = new GameObject("BattlefieldCamera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.GetComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = Hex("#090B0A");
            _camera.orthographic = true;
            _camera.orthographicSize = 7.55f;
            _camera.aspect = 16f / 9f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 60f;
            _camera.allowHDR = true;
            _camera.transform.position = new Vector3(0, 12.8f, -14.2f);
            _camera.transform.LookAt(new Vector3(0, -0.1f, 0.25f));

            var sun = new GameObject("BlockSun", typeof(Light));
            sun.transform.SetParent(transform, false);
            sun.transform.rotation = Quaternion.Euler(48f, -32f, 0);
            var sunLight = sun.GetComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = Hex("#F4D7B0");
            sunLight.intensity = 1.15f;
            sunLight.shadows = LightShadows.Soft;

            CreatePointLight("NetherGlow", new Vector3(0, 4.2f, 5.5f), Hex("#FF6A2B"), 7.5f, 2.4f);
            CreatePointLight("MeadowFill", new Vector3(-3.5f, 4.8f, -4.5f), Hex("#8FC7B7"), 8f, 1.25f);
        }

        private void BuildIllustratedBackdrop()
        {
            if (illustratedBackdrop == null) return;
            if (backdropShader == null) throw new MissingReferenceException("The illustrated battlefield backdrop shader is not configured.");
            RenderSettings.fog = false;

            var material = new Material(backdropShader) { name = "DemoIllustratedBattlefield" };
            material.mainTexture = illustratedBackdrop;
            _materials["illustrated_backdrop"] = material;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "IllustratedBattlefieldBackdrop";
            quad.transform.SetParent(_camera.transform, false);
            quad.transform.localPosition = new Vector3(0, 0, 45f);
            quad.transform.localRotation = Quaternion.identity;
            quad.transform.localScale = new Vector3(_camera.orthographicSize * 2f * _camera.aspect, _camera.orthographicSize * 2f, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
        }

        private void CreatePointLight(string name, Vector3 position, Color color, float range, float intensity)
        {
            var lightObject = new GameObject(name, typeof(Light));
            lightObject.transform.SetParent(transform, false);
            lightObject.transform.localPosition = position;
            var light = lightObject.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = color;
            light.range = range;
            light.intensity = intensity;
            light.shadows = LightShadows.None;
        }

        private void BuildTerrain()
        {
            CreateBlock(_terrainRoot, "MeadowFoundation", new Vector3(0, -0.68f, -3.35f), new Vector3(17.6f, 1.25f, 6.65f), _materials["dirt"]);
            CreateBlock(_terrainRoot, "NetherFoundation", new Vector3(0, -0.58f, 3.35f), new Vector3(17.6f, 1.45f, 6.65f), _materials["netherrack"]);

            for (var x = -8; x <= 8; x++)
            {
                for (var z = -6; z <= -1; z++)
                {
                    var hash = Mathf.Abs(x * 31 + z * 17);
                    var material = hash % 9 == 0 ? _materials["oak"] : hash % 5 == 0 ? _materials["moss"] : _materials["grass"];
                    var height = hash % 13 == 0 ? 0.16f : 0.08f;
                    CreateBlock(_terrainRoot, $"Meadow_{x}_{z}", new Vector3(x, height * 0.5f, z + 0.35f), new Vector3(1.02f, height, 1.02f), material);
                }
                for (var z = 1; z <= 6; z++)
                {
                    var hash = Mathf.Abs(x * 29 + z * 19);
                    var material = hash % 7 == 0 ? _materials["nether"] : hash % 5 == 0 ? _materials["basalt"] : _materials["blackstone"];
                    var height = hash % 11 == 0 ? 0.23f : 0.15f;
                    CreateBlock(_terrainRoot, $"Nether_{x}_{z}", new Vector3(x, height * 0.5f + 0.06f, z - 0.35f), new Vector3(1.02f, height, 1.02f), material);
                }
            }

            for (var x = -8; x <= 8; x++)
            {
                CreateBlock(_terrainRoot, "River_" + x, new Vector3(x, 0.05f, 0), new Vector3(1.02f, 0.13f, 0.72f), _materials["water"]);
                if (x % 4 == 0) CreateBlock(_terrainRoot, "RiverStone_" + x, new Vector3(x + 0.35f, 0.13f, 0), new Vector3(0.28f, 0.18f, 0.74f), _materials["stone"]);
            }

            CreateBlock(_terrainRoot, "LeftRim", new Vector3(-8.82f, 0.15f, 0), new Vector3(0.55f, 0.8f, 13.8f), _materials["stone"]);
            CreateBlock(_terrainRoot, "RightRim", new Vector3(8.82f, 0.15f, 0), new Vector3(0.55f, 0.8f, 13.8f), _materials["stone"]);
            CreateBlock(_terrainRoot, "FarRim", new Vector3(0, 0.28f, 6.72f), new Vector3(18.2f, 1.1f, 0.55f), _materials["basalt"]);
            CreateBlock(_terrainRoot, "NearRim", new Vector3(0, 0.08f, -6.72f), new Vector3(18.2f, 0.75f, 0.55f), _materials["oak"]);
        }

        private void BuildSlotPads()
        {
            for (var i = 0; i < 4; i++)
            {
                CreateSlotPad(true, DemoSlotKind.Unit, i, new Vector3(2.35f, 0.10f, 1.62f));
                CreateSlotPad(false, DemoSlotKind.Unit, i, new Vector3(2.35f, 0.10f, 1.62f));
            }
            for (var i = 0; i < 3; i++)
            {
                CreateSlotPad(true, DemoSlotKind.Building, i, new Vector3(2.85f, 0.10f, 1.20f));
                CreateSlotPad(false, DemoSlotKind.Building, i, new Vector3(2.85f, 0.10f, 1.20f));
            }
        }

        private void CreateSlotPad(bool player, DemoSlotKind kind, int index, Vector3 size)
        {
            var position = GetSlotWorldPosition(player, kind, index);
            var material = player ? _materials["player_slot"] : _materials["enemy_slot"];
            if (illustratedBackdrop == null)
                CreateBlock(_terrainRoot, $"{(player ? "Player" : "Opponent")}_{kind}_{index}", position, size, material);

            var markerRoot = NewChildRoot(_terrainRoot, $"SlotMarker_{(player ? "Player" : "Opponent")}_{kind}_{index}", position);
            var markerMaterial = DemoWorldAssetProvider.CreateBlockMaterial(
                $"DemoSlotMarker_{(player ? "Player" : "Opponent")}_{kind}_{index}",
                Hex("#5B5A50"),
                string.Empty,
                Color.black,
                blockShader);
            _materials[$"slot_marker_{player}_{kind}_{index}"] = markerMaterial;
            var rail = 0.055f;
            var height = 0.035f;
            CreateBlock(markerRoot, "North", new Vector3(0, 0.065f, size.z * 0.5f), new Vector3(size.x, height, rail), markerMaterial);
            CreateBlock(markerRoot, "South", new Vector3(0, 0.065f, -size.z * 0.5f), new Vector3(size.x, height, rail), markerMaterial);
            CreateBlock(markerRoot, "West", new Vector3(-size.x * 0.5f, 0.065f, 0), new Vector3(rail, height, size.z), markerMaterial);
            CreateBlock(markerRoot, "East", new Vector3(size.x * 0.5f, 0.065f, 0), new Vector3(rail, height, size.z), markerMaterial);
            _slotMarkers[SlotKey(player, kind, index)] = new SlotMarker(markerRoot, markerMaterial, position, index * 0.77f + (player ? 0f : 2.4f));
        }

        private static string SlotKey(bool player, DemoSlotKind kind, int index) => $"{player}:{kind}:{index}";

        private static void SetMaterialColor(Material material, Color color, Color emission)
        {
            if (material.HasProperty("_Color")) material.SetColor("_Color", color);
            if (material.HasProperty("_BaseColor")) material.SetColor("_BaseColor", color);
            if (material.HasProperty("_EmissionColor"))
            {
                material.EnableKeyword("_EMISSION");
                material.SetColor("_EmissionColor", emission);
            }
        }

        private void BuildBiomeDecor()
        {
            for (var z = 2; z <= 6; z += 2)
            {
                CreateBlock(_terrainRoot, "BasaltPillarL", new Vector3(-7.85f, 0.65f, z - 0.25f), new Vector3(0.72f, 1.6f + z * 0.08f, 0.72f), _materials["basalt"]);
                CreateBlock(_terrainRoot, "BasaltPillarR", new Vector3(7.85f, 0.65f, z - 0.25f), new Vector3(0.72f, 1.45f + z * 0.07f, 0.72f), _materials["basalt"]);
                CreateBlock(_terrainRoot, "EmberL", new Vector3(-7.82f, 1.48f + z * 0.04f, z - 0.25f), new Vector3(0.32f, 0.32f, 0.32f), _materials["ember"]);
            }

            CreateTree(new Vector3(-7.5f, 0.35f, -4.7f), 1.0f);
            CreateTree(new Vector3(7.45f, 0.35f, -3.9f), 0.85f);
            CreateTree(new Vector3(-7.7f, 0.35f, -1.9f), 0.72f);
            CreateBlock(_terrainRoot, "MagmaPool", new Vector3(6.8f, 0.27f, 5.3f), new Vector3(1.4f, 0.13f, 1.1f), _materials["magma"]);
        }

        private void CreateTree(Vector3 position, float scale)
        {
            CreateBlock(_terrainRoot, "OakTrunk", position + new Vector3(0, 0.75f * scale, 0), new Vector3(0.48f, 1.5f, 0.48f) * scale, _materials["oak"]);
            CreateBlock(_terrainRoot, "OakCrown", position + new Vector3(0, 1.65f * scale, 0), new Vector3(1.55f, 1.05f, 1.55f) * scale, _materials["leaf"]);
        }

        private void CreateSidePieces(bool player, DemoSlotKind kind, IReadOnlyList<string> slots, CardContentRegistry registry)
        {
            for (var i = 0; i < slots.Count; i++)
            {
                var cardId = slots[i];
                if (string.IsNullOrEmpty(cardId)) continue;
                if (kind == DemoSlotKind.Building && i > 0 && slots[i - 1] == cardId) continue;
                CreatePiece(cardId, GetSlotWorldPosition(player, kind, i), player, kind, registry, i);
            }
        }

        private void CreatePiece(string cardId, Vector3 position, bool player, DemoSlotKind kind, CardContentRegistry registry, int index)
        {
            var prefab = DemoWorldAssetProvider.LoadCardPrefab(cardId);
            if (prefab != null)
            {
                var instance = Instantiate(prefab, _piecesRoot);
                instance.name = "Piece_" + cardId;
                instance.transform.localPosition = position;
                instance.transform.localRotation = Quaternion.Euler(0, player ? 0 : 180, 0);
                return;
            }

            if (!registry.TryGetDefinition(cardId, out var definition) || !registry.TryGetTheme(definition.themeId, out var theme)) return;
            var materialKey = "piece_" + theme.Id;
            if (!_materials.TryGetValue(materialKey, out var material))
            {
                var textureKey = ThemeTexture(theme.Id);
                material = DemoWorldAssetProvider.CreateBlockMaterial("DemoPiece_" + theme.Id, Color.Lerp(theme.FrameBase, Color.white, 0.08f), textureKey, Color.black, blockShader);
                _materials[materialKey] = material;
            }

            var root = NewChildRoot(_piecesRoot, "Piece_" + cardId, position);
            if (kind == DemoSlotKind.Building) BuildBlockStructure(root, material, theme.Accent, definition.buildingSlots);
            else BuildBlockCreature(root, material, theme.Accent, cardId, player, index);
        }

        private void BuildBlockCreature(Transform root, Material material, Color accent, string cardId, bool player, int index)
        {
            if (DemoMinecraftModelFactory.TryGetTextureKey(cardId, out var textureKey))
            {
                var entityMaterial = GetEntityMaterial(cardId, textureKey, material.color);
                if (DemoMinecraftModelFactory.TryBuild(root, cardId, player, entityMaterial))
                {
                    _floaters.Add(new Floater(root, root.localPosition.y + (cardId == "nt_003" ? 0.12f : 0f), index * 0.9f + (player ? 0f : 2.7f)));
                    return;
                }
            }

            var accentMaterial = GetAccentMaterial("accent_" + cardId, accent);
            CreateBlock(root, "Body", new Vector3(0, 0.72f, 0), new Vector3(0.92f, 0.78f, 0.62f), material);
            CreateBlock(root, "Head", new Vector3(0, 1.35f, player ? -0.08f : 0.08f), new Vector3(0.68f, 0.62f, 0.66f), material);
            CreateBlock(root, "EyeBand", new Vector3(0, 1.39f, player ? -0.43f : 0.43f), new Vector3(0.46f, 0.12f, 0.055f), accentMaterial);
            CreateBlock(root, "LegL", new Vector3(-0.26f, 0.24f, 0), new Vector3(0.24f, 0.48f, 0.26f), material);
            CreateBlock(root, "LegR", new Vector3(0.26f, 0.24f, 0), new Vector3(0.24f, 0.48f, 0.26f), material);
            _floaters.Add(new Floater(root, root.localPosition.y, index * 0.9f + (player ? 0 : 2.7f)));
        }

        private Material GetEntityMaterial(string cardId, string textureKey, Color fallback)
        {
            var key = "entity_" + cardId;
            if (_materials.TryGetValue(key, out var material)) return material;
            material = DemoWorldAssetProvider.CreateEntityMaterial("DemoEntity_" + cardId, fallback, textureKey, blockShader);
            _materials[key] = material;
            return material;
        }

        private void BuildBlockStructure(Transform root, Material material, Color accent, int occupiedSlots)
        {
            var accentMaterial = GetAccentMaterial("structure_" + accent, accent);
            var width = Mathf.Max(1, occupiedSlots) * 0.92f;
            CreateBlock(root, "Foundation", new Vector3(0, 0.28f, 0), new Vector3(1.55f * width, 0.45f, 0.82f), material);
            CreateBlock(root, "TowerL", new Vector3(-0.48f * width, 0.9f, 0), new Vector3(0.48f, 1.25f, 0.55f), material);
            CreateBlock(root, "TowerR", new Vector3(0.48f * width, 0.9f, 0), new Vector3(0.48f, 1.25f, 0.55f), material);
            CreateBlock(root, "Core", new Vector3(0, 0.82f, 0), new Vector3(0.50f, 0.50f, 0.60f), accentMaterial);
        }

        private Material GetAccentMaterial(string key, Color color)
        {
            if (_materials.TryGetValue(key, out var material)) return material;
            material = DemoWorldAssetProvider.CreateBlockMaterial("Demo_" + key, color, string.Empty, Color.Lerp(Color.black, color, 0.28f), blockShader);
            _materials[key] = material;
            return material;
        }

        private static string ThemeTexture(string themeId)
        {
            switch (themeId)
            {
                case "plains_forest": return "oak_planks";
                case "desert_badlands": return "red_sandstone";
                case "snow_ice": return "packed_ice";
                case "cave_dark_forest": return "deepslate_bricks";
                case "ocean_river": return "prismarine_bricks";
                case "nether": return "nether_bricks";
                case "end": return "purpur_block";
                default: return "stone_bricks";
            }
        }

        private static Transform NewChildRoot(Transform parent, string name, Vector3 position)
        {
            var root = new GameObject(name).transform;
            root.SetParent(parent, false);
            root.localPosition = position;
            return root;
        }

        private static GameObject CreateBlock(Transform parent, string name, Vector3 position, Vector3 scale, Material material)
        {
            var block = GameObject.CreatePrimitive(PrimitiveType.Cube);
            block.name = name;
            block.transform.SetParent(parent, false);
            block.transform.localPosition = position;
            block.transform.localScale = scale;
            block.GetComponent<MeshRenderer>().sharedMaterial = material;
            var collider = block.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
            return block;
        }

        private static void ClearChildren(Transform root)
        {
            for (var i = root.childCount - 1; i >= 0; i--)
            {
                var child = root.GetChild(i).gameObject;
                if (Application.isPlaying) Destroy(child);
                else DestroyImmediate(child);
            }
        }

        private void OnDestroy()
        {
            foreach (var material in _materials.Values)
            {
                if (material == null) continue;
                if (Application.isPlaying) Destroy(material);
                else DestroyImmediate(material);
            }
            _materials.Clear();
        }

        private static Color Hex(string value)
        {
            ColorUtility.TryParseHtmlString(value, out var color);
            return color;
        }

        private readonly struct Floater
        {
            public readonly Transform Transform;
            public readonly float BaseY;
            public readonly float Phase;

            public Floater(Transform transform, float baseY, float phase)
            {
                Transform = transform;
                BaseY = baseY;
                Phase = phase;
            }
        }

        private sealed class SlotMarker
        {
            public readonly Transform Root;
            public readonly Material Material;
            public readonly Vector3 BasePosition;
            public readonly float Phase;
            public bool ValidTarget;
            public bool Occupied;
            public bool Hovered;

            public SlotMarker(Transform root, Material material, Vector3 basePosition, float phase)
            {
                Root = root;
                Material = material;
                BasePosition = basePosition;
                Phase = phase;
            }
        }
    }
}
