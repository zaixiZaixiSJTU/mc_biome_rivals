using System;
using System.Collections.Generic;
using System.Linq;
using BiomeRivals.Content;
using UnityEngine;
using UnityEngine.Rendering;

namespace BiomeRivals.Demo
{
    public enum DemoEngineReadyKind
    {
        None,
        Nursery,
        Coral,
        Cactus
    }

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
        private readonly RaycastHit[] _slotRaycastHits = new RaycastHit[64];
        [SerializeField] private Shader blockShader;
        [SerializeField] private Shader backdropShader;
        [SerializeField] private Shader groundSurfaceShader;
        [SerializeField] private Texture2D illustratedBackdrop;
        private Transform _terrainRoot;
        private Transform _piecesRoot;
        private Camera _camera;
        private Light _opponentEnvironmentLight;
        private Light _playerEnvironmentLight;
        private Material _backdropMaterial;
        private bool _built;
        private string _pieceSignature = string.Empty;
        private string _playerFactionId = "plains_forest";
        private string _opponentFactionId = "nether";

        public Camera BoardCamera => _camera;
        public string PlayerFactionId => _playerFactionId;
        public string OpponentFactionId => _opponentFactionId;

        public void Configure(Shader worldShader, Shader unlitBackdropShader, Shader interactiveGroundShader, Texture2D backdrop)
        {
            blockShader = worldShader;
            backdropShader = unlitBackdropShader;
            groundSurfaceShader = interactiveGroundShader;
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
            if (illustratedBackdrop != null) SetBattlefieldThemes(_playerFactionId, _opponentFactionId);
        }

        public void SetBattlefieldThemes(string playerFactionId, string opponentFactionId)
        {
            BuildNow();
            var playerTheme = DemoBattlefieldThemeCatalog.Get(playerFactionId);
            var opponentTheme = DemoBattlefieldThemeCatalog.Get(opponentFactionId);
            var playerTexture = DemoBattlefieldThemeCatalog.LoadNearTexture(playerFactionId);
            var opponentTexture = DemoBattlefieldThemeCatalog.LoadFarTexture(opponentFactionId);

            _playerFactionId = playerFactionId;
            _opponentFactionId = opponentFactionId;
            if (_backdropMaterial != null)
            {
                _backdropMaterial.SetTexture("_PlayerTex", playerTexture);
                _backdropMaterial.SetTexture("_OpponentTex", opponentTexture);
                _backdropMaterial.SetTexture("_NeutralTex", illustratedBackdrop);
                _backdropMaterial.SetColor("_PlayerTint", Color.white);
                _backdropMaterial.SetColor("_OpponentTint", Color.white);
            }

            foreach (var marker in _slotMarkers.Values)
            {
                marker.SurfaceMaterial.mainTexture = marker.Player ? playerTexture : opponentTexture;
            }

            if (_playerEnvironmentLight != null) _playerEnvironmentLight.color = playerTheme.EnvironmentLight;
            if (_opponentEnvironmentLight != null) _opponentEnvironmentLight.color = opponentTheme.EnvironmentLight;
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

        public bool TryRaycastSlot(Vector2 screenPosition, out DemoBattlefieldSlotTarget target)
        {
            BuildNow();
            target = null;
            if (_camera == null) return false;
            var ray = _camera.ScreenPointToRay(screenPosition);
            var hitCount = Physics.RaycastNonAlloc(ray, _slotRaycastHits, 200f, ~0, QueryTriggerInteraction.Ignore);
            var nearestDistance = float.PositiveInfinity;
            for (var index = 0; index < hitCount; index++)
            {
                var hit = _slotRaycastHits[index];
                var candidate = hit.collider.GetComponent<DemoBattlefieldSlotTarget>();
                if (candidate == null || hit.distance >= nearestDistance) continue;
                target = candidate;
                nearestDistance = hit.distance;
            }
            return target != null;
        }

        public void SetSlotState(bool player, DemoSlotKind kind, int index, bool validTarget, bool occupied, bool priorityTarget = false)
        {
            BuildNow();
            if (!_slotMarkers.TryGetValue(SlotKey(player, kind, index), out var marker)) return;
            marker.ValidTarget = validTarget;
            marker.Occupied = occupied;
            marker.PriorityTarget = priorityTarget;
            UpdateSlotMarker(marker, Time.unscaledTime, 0f);
        }

        public void SetSlotAura(bool player, DemoSlotKind kind, int index, int auraLayers)
        {
            BuildNow();
            if (!_slotMarkers.TryGetValue(SlotKey(player, kind, index), out var marker)) return;
            marker.AuraLayers = Mathf.Max(0, auraLayers);
            UpdateSlotMarker(marker, Time.unscaledTime, 0f);
        }

        public void SetSlotEngineReady(bool player, DemoSlotKind kind, int index, DemoEngineReadyKind readyKind)
        {
            BuildNow();
            if (!_slotMarkers.TryGetValue(SlotKey(player, kind, index), out var marker)) return;
            marker.EngineReadyKind = readyKind;
            UpdateSlotMarker(marker, Time.unscaledTime, 0f);
        }

        public void SetSlotEndPhaseThreat(bool player, DemoSlotKind kind, int index, bool threatened)
        {
            BuildNow();
            if (!_slotMarkers.TryGetValue(SlotKey(player, kind, index), out var marker)) return;
            marker.EndPhaseThreat = threatened;
            UpdateSlotMarker(marker, Time.unscaledTime, 0f);
        }

        public void SetSlotHovered(bool player, DemoSlotKind kind, int index, bool hovered)
        {
            BuildNow();
            if (!_slotMarkers.TryGetValue(SlotKey(player, kind, index), out var marker)) return;
            marker.Hovered = hovered;
            UpdateSlotMarker(marker, Time.unscaledTime, 0f);
        }

        public void SetSlotPressed(bool player, DemoSlotKind kind, int index, bool pressed)
        {
            BuildNow();
            if (!_slotMarkers.TryGetValue(SlotKey(player, kind, index), out var marker)) return;
            marker.Pressed = pressed;
            UpdateSlotMarker(marker, Time.unscaledTime, 0f);
        }

        public void SetSlotRangeHovered(bool player, DemoSlotKind kind, int startIndex, int occupiedSlots, bool hovered, bool rejected)
        {
            SetSlotRangeInteraction(player, kind, startIndex, occupiedSlots, hovered, false, rejected);
        }

        public void SetSlotRangePressed(bool player, DemoSlotKind kind, int startIndex, int occupiedSlots, bool pressed, bool rejected)
        {
            SetSlotRangeInteraction(player, kind, startIndex, occupiedSlots, pressed, true, rejected);
        }

        private void SetSlotRangeInteraction(
            bool player,
            DemoSlotKind kind,
            int startIndex,
            int occupiedSlots,
            bool active,
            bool pressed,
            bool rejected)
        {
            BuildNow();
            var slotCount = kind == DemoSlotKind.Unit ? 4 : 3;
            var rangeEnd = Mathf.Min(slotCount, startIndex + Mathf.Max(1, occupiedSlots));
            for (var index = Mathf.Max(0, startIndex); index < rangeEnd; index++)
            {
                if (!_slotMarkers.TryGetValue(SlotKey(player, kind, index), out var marker)) continue;
                if (pressed)
                {
                    marker.Pressed = active;
                    marker.PressRejected = active && rejected;
                }
                else
                {
                    marker.Hovered = active;
                    marker.HoverRejected = active && rejected;
                }
                UpdateSlotMarker(marker, Time.unscaledTime, 0f);
            }
        }

        public void SyncPieces(
            IReadOnlyList<DemoBattlefieldObject> playerObjects,
            IReadOnlyList<DemoBattlefieldObject> opponentObjects,
            CardContentRegistry registry)
        {
            BuildNow();
            var signature = string.Join("|", (playerObjects ?? Array.Empty<DemoBattlefieldObject>())
                .Concat(opponentObjects ?? Array.Empty<DemoBattlefieldObject>())
                .Where(value => value != null)
                .OrderBy(value => value.Player ? 0 : 1)
                .ThenBy(value => value.SlotKind)
                .ThenBy(value => value.SlotIndex)
                .Select(value => $"{value.InstanceId}:{value.CardId}:{value.SlotKind}:{value.SlotIndex}:{value.OccupiedSlots}"));
            if (signature == _pieceSignature) return;
            _pieceSignature = signature;
            ClearChildren(_piecesRoot);
            _floaters.Clear();
            CreateSidePieces(true, playerObjects, registry);
            CreateSidePieces(false, opponentObjects, registry);
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

            foreach (var marker in _slotMarkers.Values) UpdateSlotMarker(marker, time, Time.unscaledDeltaTime);
        }

        private static void UpdateSlotMarker(SlotMarker marker, float time, float deltaTime)
        {
            if (marker.Root == null || marker.SurfaceMaterial == null) return;
            var pulse = 0.5f + Mathf.Sin(time * 4.6f + marker.Phase) * 0.5f;
            var rejectedPreview = marker.Hovered && marker.HoverRejected || marker.Pressed && marker.PressRejected;
            var actionableHover = marker.Hovered && marker.ValidTarget;
            var actionablePress = marker.Pressed && marker.ValidTarget;
            var engineReady = marker.EngineReadyKind != DemoEngineReadyKind.None;
            var engineColor = marker.EngineReadyKind == DemoEngineReadyKind.Nursery
                ? Color.Lerp(Hex("#41672D"), Hex("#A8D66D"), pulse)
                : marker.EngineReadyKind == DemoEngineReadyKind.Cactus
                    ? Color.Lerp(Hex("#8A6424"), Hex("#C7D65A"), pulse)
                    : Color.Lerp(Hex("#8E3F72"), Hex("#F08FB4"), pulse);
            var highlightColor = rejectedPreview
                ? Color.Lerp(Hex("#B41635"), Hex("#FF3157"), pulse)
                : actionablePress || actionableHover
                ? Hex("#F1C96A")
                     : marker.ValidTarget
                         ? marker.PriorityTarget
                             ? Color.Lerp(Hex("#A97727"), Hex("#FFE08A"), pulse)
                             : Color.Lerp(Hex("#3D9E8F"), Hex("#79E0CB"), pulse)
                    : marker.EndPhaseThreat
                        ? Color.Lerp(Hex("#8A2E24"), Hex("#FF8865"), pulse)
                     : marker.AuraLayers > 0
                         ? Color.Lerp(Hex("#216F72"), Hex("#69D7C7"), pulse)
                    : engineReady
                        ? engineColor
                    : Hex("#777263");
            var highlightStrength = rejectedPreview
                    ? marker.Pressed ? 0.98f : 0.84f + pulse * 0.12f
                    : actionablePress
                    ? 0.92f
                    : actionableHover
                    ? 0.78f
                     : marker.ValidTarget
                         ? marker.PriorityTarget
                             ? (marker.Occupied ? 0.55f + pulse * 0.20f : 0.32f + pulse * 0.18f)
                             : (marker.Occupied ? 0.48f + pulse * 0.18f : 0.18f + pulse * 0.12f)
                        : marker.EndPhaseThreat
                            ? 0.22f + pulse * 0.10f
                         : marker.AuraLayers > 0
                             ? 0.10f + Mathf.Min(2, marker.AuraLayers) * 0.05f + pulse * 0.05f
                        : engineReady
                            ? 0.13f + pulse * 0.07f
                        : marker.Hovered && !marker.Occupied ? 0.12f : 0f;
            SetGroundHighlight(marker.SurfaceMaterial, highlightColor, highlightStrength);
            if (marker.RiserRenderer != null)
                marker.RiserRenderer.enabled = (actionableHover && !actionablePress || rejectedPreview) && !marker.Occupied;
            if (marker.RiserMaterial != null)
                SetMaterialColor(marker.RiserMaterial,
                    rejectedPreview ? Hex("#7D142E") : actionableHover ? Hex("#8F642B") : Hex("#332A20"),
                    rejectedPreview ? Hex("#E9274C") : actionableHover ? Hex("#6B4318") : Color.black);
            var targetPosition = marker.BasePosition;
            targetPosition.y += rejectedPreview && !marker.Occupied ? 0.045f : actionablePress && !marker.Occupied ? 0.025f : actionableHover && !marker.Occupied ? 0.085f : marker.ValidTarget && !marker.Occupied ? pulse * 0.012f : marker.Hovered && !marker.Occupied ? 0.018f : 0f;
            var targetScale = rejectedPreview && !marker.Occupied ? new Vector3(0.992f, 1f, 0.992f) : actionablePress && !marker.Occupied ? new Vector3(0.985f, 1f, 0.985f) : actionableHover && !marker.Occupied ? new Vector3(1.018f, 1f, 1.018f) : Vector3.one;
            var blend = deltaTime <= 0f ? 0f : 1f - Mathf.Exp(-16f * deltaTime);
            marker.Root.localPosition = Vector3.Lerp(marker.Root.localPosition, targetPosition, blend);
            marker.Root.localScale = Vector3.Lerp(marker.Root.localScale, targetScale, blend);
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
            _camera.orthographic = false;
            _camera.fieldOfView = 42.5f;
            _camera.aspect = 16f / 9f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 80f;
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

            _opponentEnvironmentLight = CreatePointLight("OpponentEnvironmentLight", new Vector3(0, 4.2f, 5.5f), Hex("#FF6A2B"), 7.5f, 2.4f);
            _playerEnvironmentLight = CreatePointLight("PlayerEnvironmentLight", new Vector3(-3.5f, 4.8f, -4.5f), Hex("#8FC7B7"), 8f, 1.25f);
        }

        private void BuildIllustratedBackdrop()
        {
            if (illustratedBackdrop == null) return;
            if (backdropShader == null) throw new MissingReferenceException("The illustrated battlefield backdrop shader is not configured.");
            RenderSettings.fog = false;

            _backdropMaterial = new Material(backdropShader) { name = "DemoIllustratedBattlefield" };
            _backdropMaterial.SetTexture("_NeutralTex", illustratedBackdrop);
            _materials["illustrated_backdrop"] = _backdropMaterial;

            var quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
            quad.name = "IllustratedBattlefieldBackdrop";
            quad.transform.SetParent(_camera.transform, false);
            const float backdropDistance = 45f;
            quad.transform.localPosition = new Vector3(0, 0, backdropDistance);
            quad.transform.localRotation = Quaternion.identity;
            var backdropHeight = 2f * backdropDistance * Mathf.Tan(_camera.fieldOfView * 0.5f * Mathf.Deg2Rad);
            quad.transform.localScale = new Vector3(backdropHeight * _camera.aspect, backdropHeight, 1f);
            var renderer = quad.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = _backdropMaterial;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            var collider = quad.GetComponent<Collider>();
            if (collider != null)
            {
                if (Application.isPlaying) Destroy(collider);
                else DestroyImmediate(collider);
            }
        }

        private Light CreatePointLight(string name, Vector3 position, Color color, float range, float intensity)
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
            return light;
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
            var markerRoot = NewChildRoot(_terrainRoot, $"SlotMarker_{(player ? "Player" : "Opponent")}_{kind}_{index}", position);
            var groundTexture = illustratedBackdrop != null
                ? illustratedBackdrop
                : DemoWorldAssetProvider.LoadBlockTexture(player ? "grass_block_top" : "polished_blackstone_bricks");
            var surfaceMaterial = DemoWorldAssetProvider.CreateGroundSurfaceMaterial(
                $"DemoGroundSurface_{(player ? "Player" : "Opponent")}_{kind}_{index}",
                groundTexture,
                illustratedBackdrop != null,
                groundSurfaceShader);
            var riserMaterial = DemoWorldAssetProvider.CreateBlockMaterial(
                $"DemoGroundRiser_{(player ? "Player" : "Opponent")}_{kind}_{index}",
                player ? Hex("#3F3425") : Hex("#392526"),
                string.Empty,
                Color.black,
                blockShader);
            _materials[$"ground_surface_{player}_{kind}_{index}"] = surfaceMaterial;
            _materials[$"ground_riser_{player}_{kind}_{index}"] = riserMaterial;
            var surfaceRenderer = CreateGroundSurface(markerRoot, player, kind, index, size, surfaceMaterial);
            var riserRenderer = CreateGroundRiser(markerRoot, kind, size, riserMaterial);
            riserRenderer.enabled = false;
            _slotMarkers[SlotKey(player, kind, index)] = new SlotMarker(
                player,
                markerRoot,
                surfaceRenderer,
                riserRenderer,
                surfaceMaterial,
                riserMaterial,
                position,
                index * 0.77f + (player ? 0f : 2.4f));
        }

        private static MeshRenderer CreateGroundSurface(Transform parent, bool player, DemoSlotKind kind, int index, Vector3 size, Material material)
        {
            var columns = kind == DemoSlotKind.Unit ? 5 : 6;
            const int rows = 3;
            const float fill = 0.96f;
            var cellWidth = size.x / columns;
            var cellDepth = size.z / rows;
            var halfWidth = cellWidth * fill * 0.5f;
            var halfDepth = cellDepth * fill * 0.5f;
            var vertices = new List<Vector3>(columns * rows * 4);
            var triangles = new List<int>(columns * rows * 6);
            var normals = new List<Vector3>(columns * rows * 4);
            var projectedUv = new List<Vector2>(columns * rows * 4);
            var cellUv = new List<Vector2>(columns * rows * 4);

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var centerX = -size.x * 0.5f + cellWidth * (column + 0.5f);
                    var centerZ = -size.z * 0.5f + cellDepth * (row + 0.5f);
                    var left = centerX - halfWidth;
                    var right = centerX + halfWidth;
                    var near = centerZ - halfDepth;
                    var far = centerZ + halfDepth;
                    AddGroundTopQuad(vertices, normals, triangles, projectedUv, cellUv, left, right, near, far);
                }
            }

            var mesh = new Mesh { name = $"Demo_{kind}_InteractiveGround" };
            mesh.SetVertices(vertices);
            mesh.SetNormals(normals);
            mesh.SetUVs(0, projectedUv);
            mesh.SetUVs(1, cellUv);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateBounds();
            var surface = new GameObject(
                "InteractiveGround",
                typeof(MeshFilter),
                typeof(MeshRenderer),
                typeof(MeshCollider),
                typeof(DemoBattlefieldSlotTarget),
                typeof(DemoGeneratedMeshOwner));
            surface.transform.SetParent(parent, false);
            surface.GetComponent<MeshFilter>().sharedMesh = mesh;
            surface.GetComponent<MeshCollider>().sharedMesh = mesh;
            surface.GetComponent<DemoBattlefieldSlotTarget>().Configure(player, kind, index);
            var renderer = surface.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            surface.GetComponent<DemoGeneratedMeshOwner>().Configure(mesh);
            return renderer;
        }

        private static void AddGroundTopQuad(
            ICollection<Vector3> vertices,
            ICollection<Vector3> normals,
            ICollection<int> triangles,
            ICollection<Vector2> projectedUv,
            ICollection<Vector2> cellUv,
            float left,
            float right,
            float near,
            float far)
        {
            var localVertices = new[]
            {
                new Vector3(left, 0.072f, near),
                new Vector3(left, 0.072f, far),
                new Vector3(right, 0.072f, far),
                new Vector3(right, 0.072f, near)
            };
            var start = vertices.Count;
            var fallbackUv = new[] { Vector2.zero, Vector2.up, Vector2.one, Vector2.right };
            for (var vertex = 0; vertex < localVertices.Length; vertex++)
            {
                vertices.Add(localVertices[vertex]);
                normals.Add(Vector3.up);
                projectedUv.Add(fallbackUv[vertex]);
                cellUv.Add(fallbackUv[vertex]);
            }
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
        }

        private static MeshRenderer CreateGroundRiser(Transform parent, DemoSlotKind kind, Vector3 size, Material material)
        {
            var columns = kind == DemoSlotKind.Unit ? 5 : 6;
            const int rows = 3;
            const float fill = 0.96f;
            const float top = 0.071f;
            const float bottom = -0.018f;
            var cellWidth = size.x / columns;
            var cellDepth = size.z / rows;
            var halfWidth = cellWidth * fill * 0.5f;
            var halfDepth = cellDepth * fill * 0.5f;
            var vertices = new List<Vector3>(columns * rows * 16);
            var triangles = new List<int>(columns * rows * 24);

            for (var row = 0; row < rows; row++)
            {
                for (var column = 0; column < columns; column++)
                {
                    var centerX = -size.x * 0.5f + cellWidth * (column + 0.5f);
                    var centerZ = -size.z * 0.5f + cellDepth * (row + 0.5f);
                    var left = centerX - halfWidth;
                    var right = centerX + halfWidth;
                    var near = centerZ - halfDepth;
                    var far = centerZ + halfDepth;
                    AddRiserFace(vertices, triangles, new Vector3(left, bottom, near), new Vector3(left, top, near), new Vector3(right, top, near), new Vector3(right, bottom, near));
                    AddRiserFace(vertices, triangles, new Vector3(right, bottom, far), new Vector3(right, top, far), new Vector3(left, top, far), new Vector3(left, bottom, far));
                    AddRiserFace(vertices, triangles, new Vector3(left, bottom, far), new Vector3(left, top, far), new Vector3(left, top, near), new Vector3(left, bottom, near));
                    AddRiserFace(vertices, triangles, new Vector3(right, bottom, near), new Vector3(right, top, near), new Vector3(right, top, far), new Vector3(right, bottom, far));
                }
            }

            var mesh = new Mesh { name = $"Demo_{kind}_GroundRiser" };
            mesh.SetVertices(vertices);
            mesh.SetTriangles(triangles, 0);
            mesh.RecalculateNormals();
            mesh.RecalculateBounds();
            var riser = new GameObject("GroundRiser", typeof(MeshFilter), typeof(MeshRenderer), typeof(DemoGeneratedMeshOwner));
            riser.transform.SetParent(parent, false);
            riser.GetComponent<MeshFilter>().sharedMesh = mesh;
            var renderer = riser.GetComponent<MeshRenderer>();
            renderer.sharedMaterial = material;
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            riser.GetComponent<DemoGeneratedMeshOwner>().Configure(mesh);
            return renderer;
        }

        private static void AddRiserFace(ICollection<Vector3> vertices, ICollection<int> triangles, Vector3 a, Vector3 b, Vector3 c, Vector3 d)
        {
            var start = vertices.Count;
            vertices.Add(a);
            vertices.Add(b);
            vertices.Add(c);
            vertices.Add(d);
            triangles.Add(start);
            triangles.Add(start + 1);
            triangles.Add(start + 2);
            triangles.Add(start);
            triangles.Add(start + 2);
            triangles.Add(start + 3);
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

        private static void SetGroundHighlight(Material material, Color color, float strength)
        {
            if (material.HasProperty("_Color")) material.SetColor("_Color", Color.white);
            if (material.HasProperty("_HighlightColor")) material.SetColor("_HighlightColor", color);
            if (material.HasProperty("_HighlightStrength")) material.SetFloat("_HighlightStrength", strength);
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

        private void CreateSidePieces(bool player, IReadOnlyList<DemoBattlefieldObject> objects, CardContentRegistry registry)
        {
            foreach (var battlefieldObject in objects ?? Array.Empty<DemoBattlefieldObject>())
            {
                if (battlefieldObject == null || string.IsNullOrEmpty(battlefieldObject.CardId)) continue;
                var position = GetOccupiedWorldPosition(
                    player,
                    battlefieldObject.SlotKind,
                    battlefieldObject.SlotIndex,
                    battlefieldObject.OccupiedSlots);
                CreatePiece(battlefieldObject, position, player, registry);
            }
        }

        private Vector3 GetOccupiedWorldPosition(bool player, DemoSlotKind kind, int startIndex, int occupiedSlots)
        {
            var first = GetSlotWorldPosition(player, kind, startIndex);
            var endIndex = startIndex + Mathf.Max(1, occupiedSlots) - 1;
            var last = GetSlotWorldPosition(player, kind, endIndex);
            return (first + last) * 0.5f;
        }

        private float GetOccupiedWorldWidth(bool player, DemoSlotKind kind, int startIndex, int occupiedSlots)
        {
            if (occupiedSlots <= 1) return kind == DemoSlotKind.Building ? 2.45f : 2.05f;
            var first = GetSlotWorldPosition(player, kind, startIndex);
            var last = GetSlotWorldPosition(player, kind, startIndex + occupiedSlots - 1);
            return Mathf.Abs(last.x - first.x) + (kind == DemoSlotKind.Building ? 2.45f : 2.05f);
        }

        private void CreatePiece(DemoBattlefieldObject battlefieldObject, Vector3 position, bool player, CardContentRegistry registry)
        {
            var cardId = battlefieldObject.CardId;
            var prefab = DemoWorldAssetProvider.LoadCardPrefab(cardId);
            if (prefab != null)
            {
                var instance = Instantiate(prefab, _piecesRoot);
                instance.name = "Piece_" + battlefieldObject.InstanceId + "_" + cardId;
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

            var root = NewChildRoot(_piecesRoot, "Piece_" + battlefieldObject.InstanceId + "_" + cardId, position);
            if (battlefieldObject.SlotKind == DemoSlotKind.Building)
            {
                var footprintWidth = GetOccupiedWorldWidth(
                    player,
                    battlefieldObject.SlotKind,
                    battlefieldObject.SlotIndex,
                    battlefieldObject.OccupiedSlots);
                if (cardId == "pf_005") BuildWoodlandNursery(root, footprintWidth);
                else if (cardId == "db_004") BuildCactusFence(root, material, footprintWidth);
                else if (cardId == "or_007") BuildCoralReef(root, material, footprintWidth);
                else if (cardId == "or_008") BuildOceanMonument(root, material, footprintWidth);
                else BuildBlockStructure(root, material, theme.Accent, footprintWidth);
            }
            else BuildBlockCreature(root, material, theme.Accent, cardId, player, battlefieldObject.SlotIndex);
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

        private void BuildBlockStructure(Transform root, Material material, Color accent, float footprintWidth)
        {
            var accentMaterial = GetAccentMaterial("structure_" + accent, accent);
            var width = Mathf.Max(2.05f, footprintWidth);
            var towerOffset = Mathf.Max(0.55f, width * 0.5f - 0.46f);
            CreateBlock(root, "Foundation", new Vector3(0, 0.28f, 0), new Vector3(width, 0.45f, 0.82f), material);
            CreateBlock(root, "TowerL", new Vector3(-towerOffset, 0.9f, 0), new Vector3(0.48f, 1.25f, 0.55f), material);
            CreateBlock(root, "TowerR", new Vector3(towerOffset, 0.9f, 0), new Vector3(0.48f, 1.25f, 0.55f), material);
            CreateBlock(root, "Core", new Vector3(0, 0.82f, 0), new Vector3(0.50f, 0.50f, 0.60f), accentMaterial);
        }

        private void BuildWoodlandNursery(Transform root, float footprintWidth)
        {
            var width = Mathf.Max(2.05f, footprintWidth);
            var bedWidth = width - 0.42f;
            CreateBlock(root, "NurseryFoundation", new Vector3(0f, 0.14f, 0f),
                new Vector3(width, 0.24f, 0.96f), _materials["oak"]);
            CreateBlock(root, "NurserySoil", new Vector3(0f, 0.30f, 0f),
                new Vector3(bedWidth, 0.18f, 0.70f), _materials["dirt"]);
            CreateBlock(root, "NurseryFrontRail", new Vector3(0f, 0.42f, -0.43f),
                new Vector3(width, 0.20f, 0.13f), _materials["oak"]);
            CreateBlock(root, "NurseryBackRail", new Vector3(0f, 0.42f, 0.43f),
                new Vector3(width, 0.20f, 0.13f), _materials["oak"]);
            CreateBlock(root, "NurseryLeftPost", new Vector3(-width * 0.43f, 0.68f, 0f),
                new Vector3(0.16f, 0.72f, 0.16f), _materials["oak"]);
            CreateBlock(root, "NurseryRightPost", new Vector3(width * 0.43f, 0.68f, 0f),
                new Vector3(0.16f, 0.72f, 0.16f), _materials["oak"]);
            BuildNurserySapling(root, "Left", -bedWidth * 0.31f, 0.64f, 0.44f);
            BuildNurserySapling(root, "Center", 0f, 0.76f, 0.56f);
            BuildNurserySapling(root, "Right", bedWidth * 0.31f, 0.60f, 0.40f);
        }

        private void BuildNurserySapling(Transform root, string name, float x, float topY, float leafSize)
        {
            var trunkHeight = Mathf.Max(0.24f, topY - 0.36f);
            CreateBlock(root, $"Sapling{name}Trunk", new Vector3(x, 0.36f + trunkHeight * 0.5f, 0f),
                new Vector3(0.13f, trunkHeight, 0.13f), _materials["oak"]);
            CreateBlock(root, $"Sapling{name}Leaves", new Vector3(x, topY, 0f),
                new Vector3(leafSize, leafSize * 0.72f, leafSize), _materials["leaf"]);
        }

        private void BuildCoralReef(Transform root, Material foundationMaterial, float footprintWidth)
        {
            const string materialKey = "coral_reef_or007";
            if (!_materials.TryGetValue(materialKey, out var coralMaterial))
            {
                var coral = Hex("#6E6FCF");
                coralMaterial = DemoWorldAssetProvider.CreateBlockMaterial(
                    "Demo_CoralReef_OR007", coral, "tube_coral_block", Color.Lerp(Color.black, coral, 0.18f), blockShader);
                _materials[materialKey] = coralMaterial;
            }
            var width = Mathf.Max(2.05f, footprintWidth);
            CreateBlock(root, "PrismarineBed", new Vector3(0f, 0.18f, 0f), new Vector3(width, 0.28f, 0.92f), foundationMaterial);
            CreateBlock(root, "CoralCore", new Vector3(0f, 0.55f, 0f), new Vector3(0.58f, 0.72f, 0.54f), coralMaterial);
            CreateBlock(root, "CoralLeft", new Vector3(-0.62f, 0.48f, 0.04f), new Vector3(0.34f, 0.58f, 0.34f), coralMaterial);
            CreateBlock(root, "CoralRight", new Vector3(0.64f, 0.43f, -0.02f), new Vector3(0.38f, 0.48f, 0.38f), coralMaterial);
            CreateBlock(root, "CoralTop", new Vector3(0.08f, 1.00f, 0f), new Vector3(0.30f, 0.38f, 0.30f), coralMaterial);
            CreateBlock(root, "CoralArmL", new Vector3(-0.35f, 0.78f, 0f), new Vector3(0.58f, 0.24f, 0.30f), coralMaterial);
            CreateBlock(root, "CoralArmR", new Vector3(0.42f, 0.68f, 0.04f), new Vector3(0.62f, 0.22f, 0.28f), coralMaterial);
        }

        private void BuildCactusFence(Transform root, Material sandstoneMaterial, float footprintWidth)
        {
            const string sideKey = "cactus_fence_side";
            if (!_materials.TryGetValue(sideKey, out var cactusSide))
            {
                cactusSide = DemoWorldAssetProvider.CreateBlockMaterial(
                    "Demo_CactusFence_Side", Hex("#4F8A3A"), "cactus_side", Color.black, blockShader);
                _materials[sideKey] = cactusSide;
            }
            const string topKey = "cactus_fence_top";
            if (!_materials.TryGetValue(topKey, out var cactusTop))
            {
                cactusTop = DemoWorldAssetProvider.CreateBlockMaterial(
                    "Demo_CactusFence_Top", Hex("#7AAE50"), "cactus_top", Color.black, blockShader);
                _materials[topKey] = cactusTop;
            }
            var width = Mathf.Max(2.05f, footprintWidth);
            CreateBlock(root, "CactusFenceFoundation", new Vector3(0f, 0.16f, 0f),
                new Vector3(width, 0.28f, 0.92f), sandstoneMaterial);
            var postXs = new[] { -width * 0.34f, 0f, width * 0.34f };
            for (var index = 0; index < postXs.Length; index++)
            {
                var height = index == 1 ? 1.16f : 0.92f;
                var y = 0.30f + height * 0.5f;
                CreateBlock(root, "CactusPost_" + index, new Vector3(postXs[index], y, 0f),
                    new Vector3(0.46f, height, 0.46f), cactusSide);
                CreateBlock(root, "CactusCap_" + index, new Vector3(postXs[index], 0.31f + height, 0f),
                    new Vector3(0.47f, 0.06f, 0.47f), cactusTop);
            }
            CreateBlock(root, "CactusRailFront", new Vector3(0f, 0.68f, -0.30f),
                new Vector3(width * 0.78f, 0.20f, 0.20f), cactusSide);
            CreateBlock(root, "CactusRailBack", new Vector3(0f, 0.68f, 0.30f),
                new Vector3(width * 0.78f, 0.20f, 0.20f), cactusSide);
        }

        private void BuildOceanMonument(Transform root, Material prismarineBricks, float footprintWidth)
        {
            const string darkKey = "ocean_monument_dark_prismarine";
            if (!_materials.TryGetValue(darkKey, out var darkPrismarine))
            {
                darkPrismarine = DemoWorldAssetProvider.CreateBlockMaterial(
                    "Demo_OceanMonument_DarkPrismarine", Hex("#315D59"), "dark_prismarine", Color.black, blockShader);
                _materials[darkKey] = darkPrismarine;
            }

            const string lanternKey = "ocean_monument_sea_lantern";
            if (!_materials.TryGetValue(lanternKey, out var seaLantern))
            {
                var lanternColor = Hex("#D8F2D2");
                seaLantern = DemoWorldAssetProvider.CreateBlockMaterial(
                    "Demo_OceanMonument_SeaLantern", lanternColor, "sea_lantern", Hex("#5FAE94"), blockShader);
                _materials[lanternKey] = seaLantern;
            }

            var width = Mathf.Max(6.15f, footprintWidth);
            var wingOffset = width * 0.28f;
            var sideTowerOffset = width * 0.5f - 0.48f;
            CreateBlock(root, "MonumentFoundation", new Vector3(0f, 0.18f, 0f), new Vector3(width, 0.30f, 1.00f), darkPrismarine);
            CreateBlock(root, "MonumentLowerTerrace", new Vector3(0f, 0.43f, 0f), new Vector3(width - 0.48f, 0.26f, 0.86f), prismarineBricks);
            CreateBlock(root, "MonumentLeftWing", new Vector3(-wingOffset, 0.69f, 0f), new Vector3(width * 0.34f, 0.40f, 0.72f), prismarineBricks);
            CreateBlock(root, "MonumentRightWing", new Vector3(wingOffset, 0.69f, 0f), new Vector3(width * 0.34f, 0.40f, 0.72f), prismarineBricks);
            CreateBlock(root, "MonumentLeftTower", new Vector3(-sideTowerOffset, 1.05f, 0f), new Vector3(0.64f, 1.24f, 0.68f), darkPrismarine);
            CreateBlock(root, "MonumentRightTower", new Vector3(sideTowerOffset, 1.05f, 0f), new Vector3(0.64f, 1.24f, 0.68f), darkPrismarine);
            CreateBlock(root, "MonumentLeftCrown", new Vector3(-sideTowerOffset, 1.72f, 0f), new Vector3(0.84f, 0.18f, 0.84f), prismarineBricks);
            CreateBlock(root, "MonumentRightCrown", new Vector3(sideTowerOffset, 1.72f, 0f), new Vector3(0.84f, 0.18f, 0.84f), prismarineBricks);
            CreateBlock(root, "MonumentCore", new Vector3(0f, 1.02f, 0f), new Vector3(1.58f, 1.46f, 0.82f), prismarineBricks);
            CreateBlock(root, "MonumentCoreCap", new Vector3(0f, 1.82f, 0f), new Vector3(1.92f, 0.22f, 0.96f), darkPrismarine);
            CreateBlock(root, "MonumentCoreSpire", new Vector3(0f, 2.08f, 0f), new Vector3(0.62f, 0.42f, 0.62f), prismarineBricks);
            CreateBlock(root, "MonumentEntrance", new Vector3(0f, 0.78f, -0.43f), new Vector3(0.54f, 0.64f, 0.08f), darkPrismarine);
            CreateBlock(root, "MonumentLanternLeft", new Vector3(-0.57f, 1.31f, -0.44f), new Vector3(0.26f, 0.28f, 0.10f), seaLantern);
            CreateBlock(root, "MonumentLanternRight", new Vector3(0.57f, 1.31f, -0.44f), new Vector3(0.26f, 0.28f, 0.10f), seaLantern);
            CreateBlock(root, "MonumentBeacon", new Vector3(0f, 2.34f, 0f), new Vector3(0.30f, 0.20f, 0.30f), seaLantern);
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
            public readonly bool Player;
            public readonly Transform Root;
            public readonly MeshRenderer SurfaceRenderer;
            public readonly MeshRenderer RiserRenderer;
            public readonly Material SurfaceMaterial;
            public readonly Material RiserMaterial;
            public readonly Vector3 BasePosition;
            public readonly float Phase;
            public bool ValidTarget;
            public bool Occupied;
            public bool PriorityTarget;
            public int AuraLayers;
            public DemoEngineReadyKind EngineReadyKind;
            public bool EndPhaseThreat;
            public bool Hovered;
            public bool Pressed;
            public bool HoverRejected;
            public bool PressRejected;

            public SlotMarker(
                bool player,
                Transform root,
                MeshRenderer surfaceRenderer,
                MeshRenderer riserRenderer,
                Material surfaceMaterial,
                Material riserMaterial,
                Vector3 basePosition,
                float phase)
            {
                Player = player;
                Root = root;
                SurfaceRenderer = surfaceRenderer;
                RiserRenderer = riserRenderer;
                SurfaceMaterial = surfaceMaterial;
                RiserMaterial = riserMaterial;
                BasePosition = basePosition;
                Phase = phase;
            }
        }
    }
}
