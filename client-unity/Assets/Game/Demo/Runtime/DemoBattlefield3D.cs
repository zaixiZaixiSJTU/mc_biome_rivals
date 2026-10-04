using System;
using System.Collections.Generic;
using System.Linq;
using BiomeRivals.Content;
using BiomeRivals.Core;
using UnityEngine;
using UnityEngine.Rendering;

namespace BiomeRivals.Demo
{
    public enum DemoEngineReadyKind
    {
        None,
        Nursery,
        Coral,
        Cactus,
        Sculk,
        Temple,
        Mine,
        Mansion,
        IceSpire,
        SnowHut,
        EndCrystal,
        NetherFortress,
        Blaze
    }

    public sealed class DemoBattlefield3D : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float SlotSurfaceLocalY = 0.072f;
        private const float SlotInteractionColliderThickness = 0.02f;
        private static readonly Color FriendlyTargetLow = Hex("#587F33");
        private static readonly Color FriendlyTargetHigh = Hex("#B8DD6B");
        private static readonly Color EnemyTargetLow = Hex("#92522A");
        private static readonly Color EnemyTargetHigh = Hex("#E4A348");

        private readonly Dictionary<string, Material> _materials = new Dictionary<string, Material>(StringComparer.Ordinal);
        private readonly Dictionary<string, SlotMarker> _slotMarkers = new Dictionary<string, SlotMarker>(StringComparer.Ordinal);
        private readonly Dictionary<string, Transform> _pieceTransforms = new Dictionary<string, Transform>(StringComparer.Ordinal);
        private readonly List<Floater> _floaters = new List<Floater>();
        private readonly List<CombatDamagePopup> _combatDamagePopups = new List<CombatDamagePopup>();
        private readonly List<MeshRenderer> _playerGroundRenderers = new List<MeshRenderer>();
        private readonly List<MeshRenderer> _opponentGroundRenderers = new List<MeshRenderer>();
        private readonly List<MeshRenderer> _playerFoundationRenderers = new List<MeshRenderer>();
        private readonly List<MeshRenderer> _opponentFoundationRenderers = new List<MeshRenderer>();
        private readonly RaycastHit[] _slotRaycastHits = new RaycastHit[64];
        [SerializeField] private Shader blockShader;
        [SerializeField] private Shader groundSurfaceShader;
        private Transform _terrainRoot;
        private Transform _piecesRoot;
        private Transform _decorRoot;
        private Transform _combatFeedbackRoot;
        private Camera _camera;
        private Camera _letterboxCamera;
        private Light _opponentEnvironmentLight;
        private Light _playerEnvironmentLight;
        private int _viewportWidth = -1;
        private int _viewportHeight = -1;
        private bool _built;
        private string _pieceSignature = string.Empty;
        private ArenaLayoutDefinition _arenaLayout = ArenaLayouts.Default;
        private string _playerFactionId = "plains_forest";
        private string _opponentFactionId = "nether";

        public Camera BoardCamera => _camera;
        public Camera LetterboxCamera => _letterboxCamera;
        public string PlayerFactionId => _playerFactionId;
        public string OpponentFactionId => _opponentFactionId;
        public string ArenaId => _arenaLayout.Id;
        public bool HasActiveGameplayHighlights => _slotMarkers.Values.Any(marker =>
            marker.ValidTarget || marker.PriorityTarget || marker.EndPhaseThreat ||
            marker.EngineReadyKind != DemoEngineReadyKind.None || marker.Hovered || marker.Pressed ||
            marker.HoverRejected || marker.PressRejected);

        public int GetSlotCount(DemoSlotKind kind) =>
            kind == DemoSlotKind.Unit ? _arenaLayout.UnitSlotCount : _arenaLayout.BuildingSlotCount;

        public void ConfigureArena(string arenaId)
        {
            if (!ArenaLayouts.TryGet(arenaId, out var arenaLayout))
                throw new ArgumentOutOfRangeException(nameof(arenaId), arenaId, "Arena is not registered.");
            if (_built && ArenaId != arenaLayout.Id)
                throw new InvalidOperationException("Arena layout must be configured before the battlefield is built.");
            _arenaLayout = arenaLayout;
        }

        // Snapshot-controlled match transitions rebuild the entire slot topology together.
        // Keep ConfigureArena's pre-build guard for all ordinary callers.
        public void ApplyAuthoritativeArena(string arenaId)
        {
            if (!ArenaLayouts.TryGet(arenaId, out var layout))
                throw new ArgumentOutOfRangeException(nameof(arenaId), arenaId, "Arena is not registered.");
            if (ArenaId == arenaId) return;
            if (!_built) { ConfigureArena(arenaId); return; }
            ClearSlotInteractions();
            foreach (var marker in _slotMarkers.Values)
            {
                marker.Root.gameObject.SetActive(false); // Destroy is deferred in Player: disable raycasts immediately.
                // DemoGeneratedMeshOwner releases both generated meshes with their objects.
                RetireArenaObject(marker.SurfaceMaterial);
                RetireArenaObject(marker.RiserMaterial);
                marker.Root.name = "Retired_" + marker.Root.name;
                RetireArenaObject(marker.Root.gameObject);
            }
            _slotMarkers.Clear();
            foreach (var key in _materials.Keys.Where(key => key.StartsWith("ground_surface_", StringComparison.Ordinal) ||
                key.StartsWith("ground_riser_", StringComparison.Ordinal)).ToArray()) _materials.Remove(key);
            _arenaLayout = layout;
            // Coordinates changed even when object ids/stats have not.
            _pieceSignature = null;
            foreach (Transform piece in _piecesRoot) piece.gameObject.SetActive(false);
            ClearChildren(_piecesRoot);
            _pieceTransforms.Clear();
            _floaters.Clear();
            _combatDamagePopups.Clear();
            foreach (Transform feedback in _combatFeedbackRoot) feedback.gameObject.SetActive(false);
            ClearChildren(_combatFeedbackRoot);
            BuildSlotPads();
            Physics.SyncTransforms();
        }

        private static void RetireArenaObject(UnityEngine.Object value)
        {
            if (Application.isPlaying) Destroy(value);
            else DestroyImmediate(value);
        }

        public static Rect CalculateAspectViewport(float targetAspect, float contentAspect = 16f / 9f)
        {
            if (targetAspect <= 0f || contentAspect <= 0f)
                throw new ArgumentOutOfRangeException(nameof(targetAspect), "Target and content aspect ratios must be positive.");

            if (targetAspect < contentAspect)
            {
                var height = targetAspect / contentAspect;
                return new Rect(0f, (1f - height) * 0.5f, 1f, height);
            }

            var width = contentAspect / targetAspect;
            return new Rect((1f - width) * 0.5f, 0f, width, 1f);
        }

        public void RefreshCameraViewport()
        {
            if (_camera == null) return;
            var width = _camera.targetTexture != null ? _camera.targetTexture.width : Screen.width;
            var height = _camera.targetTexture != null ? _camera.targetTexture.height : Screen.height;
            if (width <= 0 || height <= 0) return;

            _camera.rect = CalculateAspectViewport(width / (float)height);
            _viewportWidth = width;
            _viewportHeight = height;
        }

        public void Configure(Shader worldShader, Shader interactiveGroundShader)
        {
            blockShader = worldShader;
            groundSurfaceShader = interactiveGroundShader;
        }

        public void BuildNow()
        {
            if (_built) return;
            _built = true;
            CreateRoots();
            CreateMaterials();
            ConfigureWorld();
            BuildTerrain();
            BuildSlotPads();
            RebuildDecor();
            Physics.SyncTransforms();
        }

        public void SetBattlefieldThemes(string playerFactionId, string opponentFactionId)
        {
            BuildNow();
            var playerTheme = DemoBattlefieldThemeCatalog.Get(playerFactionId);
            var opponentTheme = DemoBattlefieldThemeCatalog.Get(opponentFactionId);
            var playerTexture = DemoWorldAssetProvider.LoadBlockTexture(playerTheme.PrimaryTextureKey);
            var opponentTexture = DemoWorldAssetProvider.LoadBlockTexture(opponentTheme.PrimaryTextureKey);

            _playerFactionId = playerFactionId;
            _opponentFactionId = opponentFactionId;

            foreach (var marker in _slotMarkers.Values)
            {
                marker.SurfaceMaterial.mainTexture = marker.Player ? playerTexture : opponentTexture;
                var markerTheme = marker.Player ? playerTheme : opponentTheme;
                DemoWorldAssetProvider.SetMaterialColor(marker.SurfaceMaterial, markerTheme.GroundColor);
            }

            if (_playerEnvironmentLight != null) _playerEnvironmentLight.color = playerTheme.EnvironmentLight;
            if (_opponentEnvironmentLight != null) _opponentEnvironmentLight.color = opponentTheme.EnvironmentLight;
            ApplySkyColor();
            ApplyTerrainThemes();
            RebuildDecor();
        }

        private void ApplyTerrainThemes()
        {
            var playerTheme = DemoBattlefieldThemeCatalog.Get(_playerFactionId);
            var opponentTheme = DemoBattlefieldThemeCatalog.Get(_opponentFactionId);
            var playerGround = GetTerrainMaterial("terrain_ground_" + _playerFactionId, playerTheme.GroundColor, playerTheme.PrimaryTextureKey);
            var opponentGround = GetTerrainMaterial("terrain_ground_" + _opponentFactionId, opponentTheme.GroundColor, opponentTheme.PrimaryTextureKey);
            var playerFoundation = GetTerrainMaterial("terrain_foundation_" + _playerFactionId, playerTheme.FoundationColor, playerTheme.FoundationTextureKey);
            var opponentFoundation = GetTerrainMaterial("terrain_foundation_" + _opponentFactionId, opponentTheme.FoundationColor, opponentTheme.FoundationTextureKey);
            foreach (var renderer in _playerGroundRenderers) renderer.sharedMaterial = playerGround;
            foreach (var renderer in _opponentGroundRenderers) renderer.sharedMaterial = opponentGround;
            foreach (var renderer in _playerFoundationRenderers) renderer.sharedMaterial = playerFoundation;
            foreach (var renderer in _opponentFoundationRenderers) renderer.sharedMaterial = opponentFoundation;
        }

        private Material GetTerrainMaterial(string key, Color fallback, string textureKey)
        {
            if (_materials.TryGetValue(key, out var material)) return material;
            material = DemoWorldAssetProvider.CreateBlockMaterial("Demo_" + key, fallback, textureKey, Color.black, blockShader);
            _materials[key] = material;
            return material;
        }

        public Vector3 GetSlotWorldPosition(bool player, DemoSlotKind kind, int index)
        {
            var slotCount = GetSlotCount(kind);
            if (index < 0 || index >= slotCount) throw new ArgumentOutOfRangeException(nameof(index));
            var spacing = kind == DemoSlotKind.Unit
                ? player ? 3.1f : 3.2f
                : player ? 4.35f : 4.5f;
            // Four building cells share the standard three-cell row's outer anchors.
            // Reusing the standard spacing pushes the end models under the details HUD.
            // 2.90/3.00 spacing still exceeds the 2.85-wide ground pad.
            if (kind == DemoSlotKind.Building && slotCount == 4) spacing *= 2f / 3f;
            var x = (index - (slotCount - 1) * 0.5f) * spacing;
            var z = player
                ? kind == DemoSlotKind.Unit ? -2.15f : -4.35f
                : kind == DemoSlotKind.Unit ? 2.05f : 4.15f;
            var y = player ? 0.22f : 0.30f;
            return new Vector3(x, y, z);
        }

        public Vector3 GetSlotInteractionWorldPosition(bool player, DemoSlotKind kind, int index)
        {
            BuildNow();
            if (!_slotMarkers.TryGetValue(SlotKey(player, kind, index), out var marker))
                throw new ArgumentOutOfRangeException(nameof(index));
            return marker.Root.TransformPoint(new Vector3(0f, SlotSurfaceLocalY, 0f));
        }

        public Transform FindPieceTransform(string instanceId)
        {
            BuildNow();
            return !string.IsNullOrEmpty(instanceId) && _pieceTransforms.TryGetValue(instanceId, out var piece)
                ? piece
                : null;
        }

        public void ShowCombatDamageNumber(bool player, DemoSlotKind kind, int startIndex, int occupiedSlots, int amount)
        {
            if (amount <= 0) return;
            var slotCount = GetSlotCount(kind);
            if (startIndex < 0 || startIndex >= slotCount) return;
            BuildNow();

            var range = Mathf.Clamp(occupiedSlots, 1, slotCount - startIndex);
            var root = new GameObject("CombatDamage_" + (player ? "Player" : "Opponent"));
            root.transform.SetParent(_combatFeedbackRoot, false);
            var basePosition = GetOccupiedWorldPosition(player, kind, startIndex, range) + Vector3.up * 2.1f;
            root.transform.localPosition = basePosition;

            var font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
            var shadow = CreateDamageText(root.transform, "Shadow", amount, font, new Vector3(0.035f, -0.035f, -0.015f), Hex("#1A1110"));
            var label = CreateDamageText(root.transform, "Label", amount, font, Vector3.zero, Hex("#FF6752"));
            _combatDamagePopups.Add(new CombatDamagePopup(root.transform, label, shadow, basePosition.y, 0.92f));
        }

        private static TextMesh CreateDamageText(Transform parent, string name, int amount, Font font, Vector3 offset, Color color)
        {
            var textObject = new GameObject(name);
            textObject.transform.SetParent(parent, false);
            textObject.transform.localPosition = offset;
            var text = textObject.AddComponent<TextMesh>();
            text.text = "-" + amount;
            text.anchor = TextAnchor.MiddleCenter;
            text.alignment = TextAlignment.Center;
            text.font = font;
            text.fontSize = 72;
            text.characterSize = 0.105f;
            text.fontStyle = FontStyle.Bold;
            text.color = color;
            text.richText = false;
            var renderer = textObject.GetComponent<MeshRenderer>();
            renderer.shadowCastingMode = ShadowCastingMode.Off;
            renderer.receiveShadows = false;
            renderer.sortingOrder = 310;
            return text;
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

        public void SetSlotEngineReady(
            bool player,
            DemoSlotKind kind,
            int index,
            DemoEngineReadyKind readyKind,
            string synchronizedInstanceId = null)
        {
            BuildNow();
            if (!_slotMarkers.TryGetValue(SlotKey(player, kind, index), out var marker)) return;
            marker.EngineReadyKind = readyKind;
            marker.EnginePhase = string.IsNullOrEmpty(synchronizedInstanceId)
                ? marker.Phase
                : StablePulsePhase(synchronizedInstanceId);
            UpdateSlotMarker(marker, Time.unscaledTime, 0f);
        }

        public void SetSlotEndPhaseThreat(bool player, DemoSlotKind kind, int index, bool threatened)
        {
            BuildNow();
            if (!_slotMarkers.TryGetValue(SlotKey(player, kind, index), out var marker)) return;
            marker.EndPhaseThreat = threatened;
            UpdateSlotMarker(marker, Time.unscaledTime, 0f);
        }

        public void SetSlotPoisoned(bool player, DemoSlotKind kind, int index, bool poisoned)
        {
            BuildNow();
            if (!_slotMarkers.TryGetValue(SlotKey(player, kind, index), out var marker)) return;
            marker.Poisoned = poisoned;
            UpdateSlotMarker(marker, Time.unscaledTime, 0f);
        }

        public void SetSlotBurning(bool player, DemoSlotKind kind, int index, bool burning)
        {
            BuildNow();
            if (!_slotMarkers.TryGetValue(SlotKey(player, kind, index), out var marker)) return;
            marker.Burning = burning;
            UpdateSlotMarker(marker, Time.unscaledTime, 0f);
        }

        public void SetSlotWithered(bool player, DemoSlotKind kind, int index, bool withered)
        {
            BuildNow();
            if (!_slotMarkers.TryGetValue(SlotKey(player, kind, index), out var marker)) return;
            marker.Withered = withered;
            UpdateSlotMarker(marker, Time.unscaledTime, 0f);
        }

        // Clear input affordances without erasing final objects, passive statuses, or event pulses.
        public void ClearSlotInteractions()
        {
            BuildNow();
            foreach (var marker in _slotMarkers.Values)
            {
                marker.ValidTarget = false;
                marker.PriorityTarget = false;
                marker.Hovered = false;
                marker.Pressed = false;
                marker.HoverRejected = false;
                marker.PressRejected = false;
                UpdateSlotMarker(marker, Time.unscaledTime, 0f);
            }
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

        public void PulseSlotRange(
            bool player,
            DemoSlotKind kind,
            int startIndex,
            int occupiedSlots,
            Color color,
            float duration = 0.95f)
        {
            BuildNow();
            var slotCount = GetSlotCount(kind);
            var rangeEnd = Mathf.Min(slotCount, startIndex + Mathf.Max(1, occupiedSlots));
            var startTime = Time.unscaledTime;
            for (var index = Mathf.Max(0, startIndex); index < rangeEnd; index++)
            {
                if (!_slotMarkers.TryGetValue(SlotKey(player, kind, index), out var marker)) continue;
                marker.PresentationPulseColor = color;
                marker.PresentationPulseStartedAt = startTime;
                marker.PresentationPulseDuration = Mathf.Max(0.05f, duration);
                UpdateSlotMarker(marker, startTime, 0f);
            }
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
            var slotCount = GetSlotCount(kind);
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
                .Select(value => $"{value.InstanceId}:{value.CardId}:{value.SlotKind}:{value.SlotIndex}:{value.OccupiedSlots}:" +
                    string.Join(",", (value.Statuses ?? Array.Empty<BattlefieldStatusStateDto>())
                        .Where(status => status != null).Select(status => status.statusId).OrderBy(statusId => statusId, StringComparer.Ordinal))));
            if (signature == _pieceSignature) return;
            _pieceSignature = signature;
            ClearChildren(_piecesRoot);
            _pieceTransforms.Clear();
            _floaters.Clear();
            CreateSidePieces(true, playerObjects, registry);
            CreateSidePieces(false, opponentObjects, registry);
        }

        private void Update()
        {
            var outputWidth = _camera != null && _camera.targetTexture != null ? _camera.targetTexture.width : Screen.width;
            var outputHeight = _camera != null && _camera.targetTexture != null ? _camera.targetTexture.height : Screen.height;
            if (outputWidth != _viewportWidth || outputHeight != _viewportHeight)
                RefreshCameraViewport();

            var time = Time.unscaledTime;
            foreach (var floater in _floaters)
            {
                if (floater.Transform == null) continue;
                var position = floater.Transform.localPosition;
                position.y = floater.BaseY + Mathf.Sin(time * 2.1f + floater.Phase) * 0.055f;
                floater.Transform.localPosition = position;
                floater.Transform.localRotation = floater.BaseRotation * Quaternion.Euler(0, Mathf.Sin(time * 0.7f + floater.Phase) * 5f, 0);
            }

            for (var index = _combatDamagePopups.Count - 1; index >= 0; index--)
            {
                var popup = _combatDamagePopups[index];
                if (popup.Root == null)
                {
                    _combatDamagePopups.RemoveAt(index);
                    continue;
                }

                popup.Age += Time.unscaledDeltaTime;
                var progress = Mathf.Clamp01(popup.Age / popup.Duration);
                var position = popup.Root.localPosition;
                position.y = popup.BaseY + progress * 0.82f;
                popup.Root.localPosition = position;
                popup.Root.localScale = Vector3.one * (progress < 0.16f
                    ? Mathf.Lerp(0.72f, 1.08f, progress / 0.16f)
                    : Mathf.Lerp(1.08f, 0.94f, Mathf.InverseLerp(0.16f, 1f, progress)));
                if (_camera != null)
                    popup.Root.rotation = Quaternion.LookRotation(_camera.transform.position - popup.Root.position, _camera.transform.up);

                var alpha = 1f - Mathf.SmoothStep(0.38f, 1f, progress);
                var labelColor = popup.Label.color;
                labelColor.a = alpha;
                popup.Label.color = labelColor;
                var shadowColor = popup.Shadow.color;
                shadowColor.a = alpha * 0.9f;
                popup.Shadow.color = shadowColor;
                if (progress >= 1f)
                {
                    if (Application.isPlaying) Destroy(popup.Root.gameObject);
                    else DestroyImmediate(popup.Root.gameObject);
                    _combatDamagePopups.RemoveAt(index);
                }
            }

            foreach (var marker in _slotMarkers.Values) UpdateSlotMarker(marker, time, Time.unscaledDeltaTime);
        }

        private static void UpdateSlotMarker(SlotMarker marker, float time, float deltaTime)
        {
            if (marker.Root == null || marker.SurfaceMaterial == null) return;
            var pulsePhase = marker.EngineReadyKind == DemoEngineReadyKind.None ? marker.Phase : marker.EnginePhase;
            var pulse = 0.5f + Mathf.Sin(time * 4.6f + pulsePhase) * 0.5f;
            var rejectedPreview = marker.Hovered && marker.HoverRejected || marker.Pressed && marker.PressRejected;
            var actionableHover = marker.Hovered && marker.ValidTarget;
            var actionablePress = marker.Pressed && marker.ValidTarget;
            var engineReady = marker.EngineReadyKind != DemoEngineReadyKind.None;
            var presentationPulseAge = time - marker.PresentationPulseStartedAt;
            var presentationPulseActive = marker.PresentationPulseDuration > 0f &&
                presentationPulseAge >= 0f && presentationPulseAge < marker.PresentationPulseDuration;
            var presentationPulseFade = presentationPulseActive
                ? 1f - Mathf.Clamp01(presentationPulseAge / marker.PresentationPulseDuration)
                : 0f;
            var engineColor = marker.EngineReadyKind == DemoEngineReadyKind.Nursery
                ? Color.Lerp(Hex("#41672D"), Hex("#A8D66D"), pulse)
                : marker.EngineReadyKind == DemoEngineReadyKind.Cactus
                    ? Color.Lerp(Hex("#8A6424"), Hex("#C7D65A"), pulse)
                    : marker.EngineReadyKind == DemoEngineReadyKind.Sculk
                        ? Color.Lerp(Hex("#07596A"), Hex("#36E0CF"), pulse)
                    : marker.EngineReadyKind == DemoEngineReadyKind.Temple
                        ? Color.Lerp(Hex("#8C5B24"), Hex("#F2C66D"), pulse)
                    : marker.EngineReadyKind == DemoEngineReadyKind.Mine
                        ? Color.Lerp(Hex("#4A4238"), Hex("#E1B96A"), pulse)
                    : marker.EngineReadyKind == DemoEngineReadyKind.Mansion
                        ? Color.Lerp(Hex("#3D4C37"), Hex("#9CCF70"), pulse)
                    : marker.EngineReadyKind == DemoEngineReadyKind.IceSpire
                        ? Color.Lerp(Hex("#3A6682"), Hex("#BDEEFF"), pulse)
                    : marker.EngineReadyKind == DemoEngineReadyKind.SnowHut
                        ? Color.Lerp(Hex("#477A8C"), Hex("#E5FAFF"), pulse)
                    : marker.EngineReadyKind == DemoEngineReadyKind.EndCrystal
                        ? Color.Lerp(Hex("#5A2B78"), Hex("#F2A4FF"), pulse)
                    : marker.EngineReadyKind == DemoEngineReadyKind.NetherFortress
                        ? Color.Lerp(Hex("#72200F"), Hex("#FFB347"), pulse)
                    : marker.EngineReadyKind == DemoEngineReadyKind.Blaze
                        ? Color.Lerp(Hex("#9A3B0A"), Hex("#FFD35C"), pulse)
                        : Color.Lerp(Hex("#8E3F72"), Hex("#F08FB4"), pulse);
            var highlightColor = rejectedPreview
                ? Color.Lerp(Hex("#B41635"), Hex("#FF3157"), pulse)
                : actionablePress || actionableHover
                ? Hex("#F1C96A")
                     : marker.ValidTarget
                         ? marker.PriorityTarget
                             ? Color.Lerp(Hex("#A97727"), Hex("#FFE08A"), pulse)
                             : marker.Player
                                 ? Color.Lerp(FriendlyTargetLow, FriendlyTargetHigh, pulse)
                                 : Color.Lerp(EnemyTargetLow, EnemyTargetHigh, pulse)
                    : marker.EndPhaseThreat
                        ? Color.Lerp(Hex("#8A2E24"), Hex("#FF8865"), pulse)
                     : marker.Burning
                         ? Color.Lerp(Hex("#B92D08"), Hex("#FFB52E"), pulse)
                     : marker.Withered
                         ? Color.Lerp(Hex("#3B1B59"), Hex("#C27CFF"), pulse)
                     : marker.Poisoned
                         ? Color.Lerp(Hex("#00883D"), Hex("#00FF70"), pulse)
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
                         : marker.Burning
                             ? 0.68f + pulse * 0.22f
                         : marker.Withered
                             ? 0.64f + pulse * 0.20f
                         : marker.Poisoned
                             ? 0.62f + pulse * 0.20f
                         : marker.AuraLayers > 0
                             ? 0.10f + Mathf.Min(2, marker.AuraLayers) * 0.05f + pulse * 0.05f
                        : engineReady
                            ? 0.13f + pulse * 0.07f
                        : marker.Hovered && !marker.Occupied ? 0.12f : 0f;
            if (presentationPulseFade > 0f && !rejectedPreview)
            {
                // Event feedback must remain legible even while target highlights are active.
                var pulseWeight = presentationPulseFade * 0.92f;
                highlightColor = Color.Lerp(highlightColor, marker.PresentationPulseColor, pulseWeight);
                highlightStrength = Mathf.Max(highlightStrength, 0.22f + presentationPulseFade * 0.72f);
            }
            SetGroundHighlight(marker.SurfaceMaterial, highlightColor, highlightStrength);
            if (marker.RiserRenderer != null)
                marker.RiserRenderer.enabled = (actionableHover && !actionablePress || rejectedPreview) && !marker.Occupied;
            if (marker.RiserMaterial != null)
                SetMaterialColor(marker.RiserMaterial,
                    rejectedPreview ? Hex("#7D142E") : actionableHover ? Hex("#8F642B") : Hex("#332A20"),
                    rejectedPreview ? Hex("#E9274C") : actionableHover ? Hex("#6B4318") : Color.black);
            var targetPosition = marker.BasePosition;
            var interactionLift = rejectedPreview && !marker.Occupied ? 0.045f : actionablePress && !marker.Occupied ? 0.025f : actionableHover && !marker.Occupied ? 0.085f : marker.ValidTarget && !marker.Occupied ? pulse * 0.012f : marker.Hovered && !marker.Occupied ? 0.018f : 0f;
            var presentationLift = presentationPulseFade > 0f && !marker.Occupied && !rejectedPreview && !actionablePress && !actionableHover
                ? presentationPulseFade * 0.035f
                : 0f;
            targetPosition.y += interactionLift + presentationLift;
            var targetScale = rejectedPreview && !marker.Occupied ? new Vector3(0.992f, 1f, 0.992f) : actionablePress && !marker.Occupied ? new Vector3(0.985f, 1f, 0.985f) : actionableHover && !marker.Occupied ? new Vector3(1.018f, 1f, 1.018f) : Vector3.one;
            if (presentationLift > 0f) targetScale = new Vector3(1f + presentationPulseFade * 0.018f, 1f, 1f + presentationPulseFade * 0.018f);
            var blend = deltaTime <= 0f ? 0f : 1f - Mathf.Exp(-16f * deltaTime);
            marker.Root.localPosition = Vector3.Lerp(marker.Root.localPosition, targetPosition, blend);
            marker.Root.localScale = Vector3.Lerp(marker.Root.localScale, targetScale, blend);
        }

        private void CreateRoots()
        {
            _terrainRoot = NewRoot("BattlefieldGeometry");
            _piecesRoot = NewRoot("BattlefieldPieces");
            _decorRoot = NewRoot("BattlefieldDecor");
            _combatFeedbackRoot = NewRoot("CombatFeedback");
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

            var skyColor = DemoBattlefieldThemeCatalog.GetSkyColor(_playerFactionId, _opponentFactionId);
            var letterboxCameraObject = new GameObject("BattlefieldLetterboxCamera", typeof(Camera));
            letterboxCameraObject.transform.SetParent(transform, false);
            _letterboxCamera = letterboxCameraObject.GetComponent<Camera>();
            _letterboxCamera.clearFlags = CameraClearFlags.SolidColor;
            _letterboxCamera.backgroundColor = skyColor;
            _letterboxCamera.cullingMask = 0;
            _letterboxCamera.depth = -1f;
            _letterboxCamera.useOcclusionCulling = false;

            var cameraObject = new GameObject("BattlefieldCamera", typeof(Camera));
            cameraObject.tag = "MainCamera";
            cameraObject.transform.SetParent(transform, false);
            _camera = cameraObject.GetComponent<Camera>();
            _camera.clearFlags = CameraClearFlags.SolidColor;
            _camera.backgroundColor = skyColor;
            _camera.orthographic = false;
            _camera.fieldOfView = 42.5f;
            _camera.nearClipPlane = 0.1f;
            _camera.farClipPlane = 80f;
            _camera.allowHDR = true;
            RefreshCameraViewport();
            _camera.transform.position = new Vector3(0, 12.8f, -14.2f);
            _camera.transform.LookAt(new Vector3(0, -0.1f, 0.25f));

            var sun = new GameObject("BlockSun", typeof(Light));
            sun.transform.SetParent(transform, false);
            // Lower elevation produces long, readable shadows across the voxel field.
            sun.transform.rotation = Quaternion.Euler(38f, -30f, 0);
            var sunLight = sun.GetComponent<Light>();
            sunLight.type = LightType.Directional;
            sunLight.color = Hex("#F4D7B0");
            sunLight.intensity = 1.2f;
            sunLight.shadows = LightShadows.Soft;
            sunLight.shadowStrength = 0.82f;

            _opponentEnvironmentLight = CreatePointLight("OpponentEnvironmentLight", new Vector3(0, 4.2f, 5.5f), Hex("#FF6A2B"), 7.5f, 2.4f);
            _playerEnvironmentLight = CreatePointLight("PlayerEnvironmentLight", new Vector3(-3.5f, 4.8f, -4.5f), Hex("#8FC7B7"), 8f, 1.25f);
        }

        private void ApplySkyColor()
        {
            var skyColor = DemoBattlefieldThemeCatalog.GetSkyColor(_playerFactionId, _opponentFactionId);
            if (_camera != null) _camera.backgroundColor = skyColor;
            if (_letterboxCamera != null) _letterboxCamera.backgroundColor = skyColor;
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
            var playerTheme = DemoBattlefieldThemeCatalog.Get(_playerFactionId);
            var opponentTheme = DemoBattlefieldThemeCatalog.Get(_opponentFactionId);
            var playerGround = GetTerrainMaterial("terrain_ground_" + _playerFactionId, playerTheme.GroundColor, playerTheme.PrimaryTextureKey);
            var opponentGround = GetTerrainMaterial("terrain_ground_" + _opponentFactionId, opponentTheme.GroundColor, opponentTheme.PrimaryTextureKey);
            var playerFoundation = GetTerrainMaterial("terrain_foundation_" + _playerFactionId, playerTheme.FoundationColor, playerTheme.FoundationTextureKey);
            var opponentFoundation = GetTerrainMaterial("terrain_foundation_" + _opponentFactionId, opponentTheme.FoundationColor, opponentTheme.FoundationTextureKey);
            var playerSecondary = GetTerrainMaterial("terrain_secondary_" + _playerFactionId, playerTheme.GroundColor, playerTheme.SecondaryTextureKey);
            var opponentSecondary = GetTerrainMaterial("terrain_secondary_" + _opponentFactionId, opponentTheme.GroundColor, opponentTheme.SecondaryTextureKey);
            var playerTertiary = GetTerrainMaterial("terrain_tertiary_" + _playerFactionId, playerTheme.GroundColor, playerTheme.TertiaryTextureKey);
            var opponentTertiary = GetTerrainMaterial("terrain_tertiary_" + _opponentFactionId, opponentTheme.GroundColor, opponentTheme.TertiaryTextureKey);

            _playerFoundationRenderers.Add(CreateBlock(_terrainRoot, "PlayerFoundation", new Vector3(0, -0.68f, -4.55f), new Vector3(24.4f, 1.25f, 9.6f), playerFoundation).GetComponent<MeshRenderer>());
            _opponentFoundationRenderers.Add(CreateBlock(_terrainRoot, "OpponentFoundation", new Vector3(0, -0.58f, 4.65f), new Vector3(24.4f, 1.45f, 9.6f), opponentFoundation).GetComponent<MeshRenderer>());

            for (var x = -11; x <= 11; x++)
            {
                for (var z = -8; z <= -1; z++)
                {
                    var hash = Mathf.Abs(x * 31 + z * 17);
                    var material = hash % 9 == 0 ? playerTertiary : hash % 5 == 0 ? playerSecondary : playerGround;
                    // Rows near the rim rise slightly, giving the field a gentle terrace.
                    var height = (hash % 13 == 0 ? 0.16f : 0.08f) + (z <= -7 ? 0.07f : 0f);
                    _playerGroundRenderers.Add(CreateBlock(_terrainRoot, $"Ground_Player_{x}_{z}", new Vector3(x, height * 0.5f, z + 0.35f), new Vector3(1.02f, height, 1.02f), material).GetComponent<MeshRenderer>());
                }
                for (var z = 1; z <= 8; z++)
                {
                    var hash = Mathf.Abs(x * 29 + z * 19);
                    var material = hash % 9 == 0 ? opponentTertiary : hash % 5 == 0 ? opponentSecondary : opponentGround;
                    var height = (hash % 11 == 0 ? 0.23f : 0.15f) + (z >= 7 ? 0.07f : 0f);
                    _opponentGroundRenderers.Add(CreateBlock(_terrainRoot, $"Ground_Opponent_{x}_{z}", new Vector3(x, height * 0.5f + 0.06f, z - 0.35f), new Vector3(1.02f, height, 1.02f), material).GetComponent<MeshRenderer>());
                }
            }

            var gravel = GetWorldMaterial("decor_gravel", "gravel", Hex("#8A8781"));
            for (var x = -11; x <= 11; x++)
            {
                CreateBlock(_terrainRoot, "River_" + x, new Vector3(x, 0.05f, 0), new Vector3(1.02f, 0.13f, 0.72f), _materials["water"]);
                if (x % 3 == 0) CreateBlock(_terrainRoot, "RiverStone_" + x, new Vector3(x + 0.35f, 0.13f, 0), new Vector3(0.28f, 0.18f, 0.74f), _materials["stone"]);
                if (x % 4 == 1) CreateBlock(_terrainRoot, "RiverBank_" + x, new Vector3(x, 0.08f, -0.62f), new Vector3(1.02f, 0.11f, 0.36f), gravel);
                else if (x % 4 == 3) CreateBlock(_terrainRoot, "RiverBank_" + x, new Vector3(x, 0.08f, 0.62f), new Vector3(1.02f, 0.11f, 0.36f), gravel);
            }

            CreateBlock(_terrainRoot, "LeftRim", new Vector3(-11.65f, 0.15f, 0), new Vector3(0.6f, 0.8f, 17.9f), _materials["stone"]);
            CreateBlock(_terrainRoot, "RightRim", new Vector3(11.65f, 0.15f, 0), new Vector3(0.6f, 0.8f, 17.9f), _materials["stone"]);
            _opponentFoundationRenderers.Add(CreateBlock(_terrainRoot, "FarRim", new Vector3(0, 0.28f, 8.6f), new Vector3(24.6f, 1.1f, 0.6f), opponentFoundation).GetComponent<MeshRenderer>());
            _playerFoundationRenderers.Add(CreateBlock(_terrainRoot, "NearRim", new Vector3(0, 0.08f, -8.6f), new Vector3(24.6f, 0.75f, 0.6f), playerFoundation).GetComponent<MeshRenderer>());
        }

        private void BuildSlotPads()
        {
            for (var i = 0; i < GetSlotCount(DemoSlotKind.Unit); i++)
            {
                CreateSlotPad(true, DemoSlotKind.Unit, i, new Vector3(2.35f, 0.10f, 1.62f));
                CreateSlotPad(false, DemoSlotKind.Unit, i, new Vector3(2.35f, 0.10f, 1.62f));
            }
            for (var i = 0; i < GetSlotCount(DemoSlotKind.Building); i++)
            {
                CreateSlotPad(true, DemoSlotKind.Building, i, new Vector3(2.85f, 0.10f, 1.20f));
                CreateSlotPad(false, DemoSlotKind.Building, i, new Vector3(2.85f, 0.10f, 1.20f));
            }
        }

        private void CreateSlotPad(bool player, DemoSlotKind kind, int index, Vector3 size)
        {
            var position = GetSlotWorldPosition(player, kind, index);
            var markerRoot = NewChildRoot(_terrainRoot, $"SlotMarker_{(player ? "Player" : "Opponent")}_{kind}_{index}", position);
            var factionTheme = DemoBattlefieldThemeCatalog.Get(player ? _playerFactionId : _opponentFactionId);
            var groundTexture = DemoWorldAssetProvider.LoadBlockTexture(factionTheme.PrimaryTextureKey);
            var surfaceMaterial = DemoWorldAssetProvider.CreateGroundSurfaceMaterial(
                $"DemoGroundSurface_{(player ? "Player" : "Opponent")}_{kind}_{index}",
                groundTexture,
                false,
                groundSurfaceShader);
            // Several vanilla block textures (grass tops, sea lanterns) ship grayscale and rely on a biome tint.
            DemoWorldAssetProvider.SetMaterialColor(surfaceMaterial, factionTheme.GroundColor);
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

            var interactionFootprint = new GameObject("InteractionFootprint", typeof(BoxCollider), typeof(DemoBattlefieldSlotTarget));
            interactionFootprint.transform.SetParent(parent, false);
            var footprintCollider = interactionFootprint.GetComponent<BoxCollider>();
            footprintCollider.center = new Vector3(0f, SlotSurfaceLocalY, 0f);
            footprintCollider.size = new Vector3(size.x, SlotInteractionColliderThickness, size.z);
            interactionFootprint.GetComponent<DemoBattlefieldSlotTarget>().Configure(player, kind, index);
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
                new Vector3(left, SlotSurfaceLocalY, near),
                new Vector3(left, SlotSurfaceLocalY, far),
                new Vector3(right, SlotSurfaceLocalY, far),
                new Vector3(right, SlotSurfaceLocalY, near)
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

        private static float StablePulsePhase(string value)
        {
            unchecked
            {
                var hash = 17;
                foreach (var character in value) hash = hash * 31 + character;
                return (uint)hash % 6283u / 1000f;
            }
        }

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
            if (material.HasProperty("_HighlightColor")) material.SetColor("_HighlightColor", color);
            if (material.HasProperty("_HighlightStrength")) material.SetFloat("_HighlightStrength", strength);
        }

        /// <summary>
        /// Faction decorations live in their own subtree and are rebuilt on every
        /// theme change, so the scenery always matches the selected factions.
        /// </summary>
        private void RebuildDecor()
        {
            if (_decorRoot == null) return;
            ClearChildren(_decorRoot);
            BuildSideDecor(true, _playerFactionId);
            BuildSideDecor(false, _opponentFactionId);
        }

        private void BuildSideDecor(bool player, string factionId)
        {
            var side = player ? -1f : 1f;
            var theme = DemoBattlefieldThemeCatalog.Get(factionId);
            switch (factionId)
            {
                case "plains_forest": BuildForestDecor(player, side); break;
                case "desert_badlands": BuildCactusDecor(player, side); break;
                case "snow_ice": BuildIceSpikeDecor(player, side); break;
                case "cave_dark_forest": BuildBoulderDecor(player, side); break;
                case "ocean_river": BuildCoralDecor(player, side); break;
                case "nether": BuildNetherDecor(player, side); break;
                case "end": BuildEndDecor(player, side); break;
            }
            BuildLamp(player, side, theme.EnvironmentLight, -7.3f);
            BuildLamp(player, side, theme.EnvironmentLight, 7.3f);
        }

        private void BuildForestDecor(bool player, float side)
        {
            var trunk = GetWorldMaterial("decor_oak_log", "oak_log", Hex("#6B5433"));
            var sideName = player ? "Player" : "Opponent";
            BuildTree("ForestTree_" + sideName + "_0", new Vector3(-8.7f, 0.1f, side * 5.1f), 1.05f, trunk, _materials["leaf"]);
            BuildTree("ForestTree_" + sideName + "_1", new Vector3(-7.6f, 0.1f, side * 7.1f), 0.8f, trunk, _materials["leaf"]);
            BuildTree("ForestTree_" + sideName + "_2", new Vector3(8.5f, 0.1f, side * 5.9f), 0.95f, trunk, _materials["leaf"]);
            CreateBlock(_decorRoot, "ForestBush_" + sideName + "_0", new Vector3(-6.8f, 0.14f, side * 7.4f), new Vector3(0.7f, 0.5f, 0.7f), _materials["leaf"]);
            CreateBlock(_decorRoot, "ForestBush_" + sideName + "_1", new Vector3(6.9f, 0.14f, side * 7.6f), new Vector3(0.6f, 0.42f, 0.6f), _materials["leaf"]);
        }

        private void BuildTree(string name, Vector3 position, float scale, Material trunk, Material crown)
        {
            CreateBlock(_decorRoot, name + "Trunk", position + new Vector3(0, 0.72f * scale, 0), new Vector3(0.44f, 1.45f, 0.44f) * scale, trunk);
            CreateBlock(_decorRoot, name + "Crown", position + new Vector3(0, 1.6f * scale, 0), new Vector3(1.5f, 1.05f, 1.5f) * scale, crown);
        }

        private void BuildCactusDecor(bool player, float side)
        {
            var sideName = player ? "Player" : "Opponent";
            var cactus = GetWorldMaterial("decor_cactus", "cactus_side", Hex("#4F8A3A"));
            var cactusTop = GetWorldMaterial("decor_cactus_top", "cactus_top", Hex("#7AAE50"));
            var cacti = new[] { (-8.6f, 5.2f, 1.3f), (-7.5f, 7.0f, 0.9f), (8.4f, 5.6f, 1.5f), (9.3f, 7.2f, 1.0f) };
            for (var index = 0; index < cacti.Length; index++)
            {
                var (x, zBase, height) = cacti[index];
                var z = side * zBase;
                CreateBlock(_decorRoot, "Cactus_" + sideName + "_" + index, new Vector3(x, 0.32f + height * 0.5f, z), new Vector3(0.46f, height, 0.46f), cactus);
                CreateBlock(_decorRoot, "CactusTop_" + sideName + "_" + index, new Vector3(x, 0.32f + height, z), new Vector3(0.48f, 0.08f, 0.48f), cactusTop);
            }
            var sandstone = GetWorldMaterial("decor_sandstone", "sandstone", Hex("#D8BE78"));
            CreateBlock(_decorRoot, "SandMound_" + sideName + "_0", new Vector3(6.8f, 0.14f, side * 7.5f), new Vector3(1.0f, 0.24f, 0.8f), sandstone);
        }

        private void BuildIceSpikeDecor(bool player, float side)
        {
            var sideName = player ? "Player" : "Opponent";
            var ice = GetWorldMaterial("decor_ice", "packed_ice", Hex("#8FC6DB"));
            var snow = GetWorldMaterial("decor_snow", "snow_block", Hex("#E7F1F3"));
            var spikes = new[] { (-8.5f, 5.4f, 1.9f), (8.5f, 5.0f, 2.35f), (9.3f, 6.9f, 1.35f), (-7.4f, 7.2f, 1.1f) };
            for (var index = 0; index < spikes.Length; index++)
            {
                var (x, zBase, height) = spikes[index];
                var z = side * zBase;
                CreateBlock(_decorRoot, "IceSpike_" + sideName + "_" + index, new Vector3(x, 0.1f + height * 0.5f, z), new Vector3(0.52f, height, 0.52f), ice);
                CreateBlock(_decorRoot, "IceSpikeTop_" + sideName + "_" + index, new Vector3(x, 0.1f + height + 0.14f, z), new Vector3(0.3f, 0.3f, 0.3f), snow);
            }
            CreateBlock(_decorRoot, "SnowMound_" + sideName, new Vector3(6.9f, 0.12f, side * 7.6f), new Vector3(0.9f, 0.2f, 0.8f), snow);
        }

        private void BuildBoulderDecor(bool player, float side)
        {
            var sideName = player ? "Player" : "Opponent";
            var mossy = _materials["moss"];
            var stone = _materials["stone"];
            var boulders = new[] { (-8.5f, 5.3f, 0.9f, mossy), (8.6f, 5.6f, 1.1f, stone), (-7.5f, 7.0f, 0.7f, stone), (9.2f, 7.3f, 0.8f, mossy) };
            for (var index = 0; index < boulders.Length; index++)
            {
                var (x, zBase, size, material) = boulders[index];
                CreateBlock(_decorRoot, "Boulder_" + sideName + "_" + index, new Vector3(x, size * 0.4f, side * zBase), new Vector3(size, size * 0.8f, size), material);
            }
            var darkTrunk = GetWorldMaterial("decor_dark_oak", "dark_oak_planks", Hex("#4A3424"));
            CreateBlock(_decorRoot, "CaveTrunk_" + sideName, new Vector3(-8.9f, 0.8f, side * 6.4f), new Vector3(0.4f, 1.6f, 0.4f), darkTrunk);
        }

        private void BuildCoralDecor(bool player, float side)
        {
            var sideName = player ? "Player" : "Opponent";
            var coral = GetWorldMaterial("decor_coral", "tube_coral_block", Hex("#6E6FCF"));
            var lantern = GetWorldMaterial("decor_lantern", "sea_lantern", Hex("#D8F2D2"));
            var stacks = new[] { (-8.5f, 5.3f, 2), (8.6f, 5.7f, 3), (-7.6f, 7.1f, 1), (9.3f, 7.0f, 2) };
            for (var index = 0; index < stacks.Length; index++)
            {
                var (x, zBase, cubes) = stacks[index];
                for (var layer = 0; layer < cubes; layer++)
                {
                    CreateBlock(_decorRoot, "CoralStack_" + sideName + "_" + index + "_" + layer,
                        new Vector3(x, 0.35f + layer * 0.5f, side * zBase), new Vector3(0.5f, 0.5f, 0.5f), coral);
                }
            }
            CreateBlock(_decorRoot, "SeaLanternDecor_" + sideName, new Vector3(6.9f, 0.25f, side * 7.5f), new Vector3(0.5f, 0.5f, 0.5f), lantern);
        }

        private void BuildNetherDecor(bool player, float side)
        {
            var sideName = player ? "Player" : "Opponent";
            var basalt = GetWorldMaterial("decor_basalt", "basalt_top", Hex("#333238"));
            for (var index = 0; index < 4; index++)
            {
                var x = index % 2 == 0 ? -8.5f : 8.5f;
                var zBase = index < 2 ? 5.2f : 7.0f;
                var height = 1.5f + index * 0.22f;
                CreateBlock(_decorRoot, "BasaltPillar_" + sideName + "_" + index, new Vector3(x, 0.65f, side * zBase), new Vector3(0.72f, height, 0.72f), basalt);
                CreateBlock(_decorRoot, "Ember_" + sideName + "_" + index, new Vector3(x, 0.65f + height * 0.5f, side * zBase), new Vector3(0.3f, 0.3f, 0.3f), _materials["ember"]);
            }
            CreateBlock(_decorRoot, "MagmaPool_" + sideName, new Vector3(6.8f, 0.27f, side * 5.4f), new Vector3(1.4f, 0.13f, 1.1f), _materials["magma"]);
        }

        private void BuildEndDecor(bool player, float side)
        {
            var sideName = player ? "Player" : "Opponent";
            var obsidian = GetWorldMaterial("decor_obsidian", "obsidian", Hex("#17121E"));
            var purpur = GetWorldMaterial("decor_purpur", "purpur_block", Hex("#A878AE"));
            var pillars = new[] { (-8.5f, 5.3f, 2.1f), (8.6f, 5.6f, 1.6f), (-7.6f, 7.1f, 1.2f) };
            for (var index = 0; index < pillars.Length; index++)
            {
                var (x, zBase, height) = pillars[index];
                CreateBlock(_decorRoot, "ObsidianPillar_" + sideName + "_" + index, new Vector3(x, 0.1f + height * 0.5f, side * zBase), new Vector3(0.6f, height, 0.6f), obsidian);
            }
            CreateBlock(_decorRoot, "PurpurBlock_" + sideName, new Vector3(9.2f, 0.4f, side * 7.2f), new Vector3(0.7f, 0.6f, 0.7f), purpur);
        }

        private void BuildLamp(bool player, float side, Color glowColor, float x)
        {
            var sideName = player ? "Player" : "Opponent";
            var corner = x < 0 ? "L" : "R";
            var post = GetWorldMaterial("decor_basalt", "basalt_top", Hex("#333238"));
            var glowstone = GetWorldMaterial("decor_glowstone", "glowstone", Hex("#F5D76E"));
            var z = side * 4.9f;
            CreateBlock(_decorRoot, "DecorLamp_" + sideName + "_" + corner, new Vector3(x, 0.5f, z), new Vector3(0.2f, 0.9f, 0.2f), post);
            CreateBlock(_decorRoot, "DecorLampHead_" + sideName + "_" + corner, new Vector3(x, 1.08f, z), new Vector3(0.34f, 0.34f, 0.34f), glowstone);
            var lampLight = new GameObject("DecorLight_" + sideName + "_" + corner, typeof(Light));
            lampLight.transform.SetParent(_decorRoot, false);
            lampLight.transform.localPosition = new Vector3(x, 1.7f, z);
            var light = lampLight.GetComponent<Light>();
            light.type = LightType.Point;
            light.color = glowColor;
            light.range = 5.5f;
            light.intensity = 0.85f;
            light.shadows = LightShadows.None;
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
                _pieceTransforms[battlefieldObject.InstanceId] = instance.transform;
                if (battlefieldObject.HasStatus("FIRE")) BuildFireStatusEffect(instance.transform, battlefieldObject.InstanceId);
                if (battlefieldObject.HasStatus("WITHER")) BuildWitherStatusEffect(instance.transform, battlefieldObject.InstanceId);
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
            _pieceTransforms[battlefieldObject.InstanceId] = root;
            if (battlefieldObject.SlotKind == DemoSlotKind.Building)
            {
                var footprintWidth = GetOccupiedWorldWidth(
                    player,
                    battlefieldObject.SlotKind,
                    battlefieldObject.SlotIndex,
                    battlefieldObject.OccupiedSlots);
                if (cardId == "pf_005") BuildWoodlandNursery(root, footprintWidth);
                else if (cardId == "db_004") BuildCactusFence(root, material, footprintWidth);
                else if (cardId == "db_007") BuildDesertTemple(root, footprintWidth);
                else if (cardId == "cd_004") BuildSculkSensor(root, footprintWidth);
                else if (cardId == "cd_007") BuildAbandonedMine(root, footprintWidth);
                else if (cardId == "cd_008") BuildWoodlandMansion(root, footprintWidth);
                else if (cardId == "ed_007") BuildEndCrystal(root, footprintWidth, battlefieldObject.InstanceId);
                else if (cardId == "nt_007") BuildRespawnAnchor(root, footprintWidth);
                else if (cardId == "nt_008") BuildNetherFortress(root, footprintWidth);
                else if (cardId == "si_007") BuildSnowHut(root, footprintWidth);
                else if (cardId == "si_008") BuildIceSpire(root, footprintWidth);
                else if (cardId == "or_007") BuildCoralReef(root, material, footprintWidth);
                else if (cardId == "or_008") BuildOceanMonument(root, material, footprintWidth);
                else BuildBlockStructure(root, material, theme.Accent, footprintWidth);
            }
            else BuildBlockCreature(root, material, theme.Accent, cardId, player, battlefieldObject.SlotIndex);
            if (battlefieldObject.HasStatus("FIRE")) BuildFireStatusEffect(root, battlefieldObject.InstanceId);
            if (battlefieldObject.HasStatus("WITHER")) BuildWitherStatusEffect(root, battlefieldObject.InstanceId);
        }

        private void BuildFireStatusEffect(Transform parent, string instanceId)
        {
            var root = NewChildRoot(parent, "FireStatusFx", Vector3.zero);
            var ember = GetAccentMaterial("status_fire_ember", Hex("#FF5A12"));
            var flame = GetAccentMaterial("status_fire_flame", Hex("#FFD04A"));
            CreateBlock(root, "FlameL", new Vector3(-0.62f, 0.66f, -0.24f), new Vector3(0.24f, 0.94f, 0.24f), ember);
            CreateBlock(root, "FlameR", new Vector3(0.60f, 0.78f, 0.22f), new Vector3(0.22f, 1.12f, 0.22f), flame);
            CreateBlock(root, "FlameFront", new Vector3(0.16f, 0.54f, -0.56f), new Vector3(0.26f, 0.76f, 0.26f), flame);
            CreateBlock(root, "FlameBack", new Vector3(-0.24f, 0.62f, 0.52f), new Vector3(0.22f, 0.84f, 0.22f), ember);
            CreateBlock(root, "FlameHigh", new Vector3(-0.10f, 1.26f, 0.04f), new Vector3(0.18f, 0.74f, 0.18f), ember);
            _floaters.Add(new Floater(root, root.localPosition.y, StablePulsePhase(instanceId)));
        }

        private void BuildWitherStatusEffect(Transform parent, string instanceId)
        {
            var root = NewChildRoot(parent, "WitherStatusFx", Vector3.zero);
            var shadow = GetAccentMaterial("status_wither_shadow", Hex("#321943"));
            var pulse = GetAccentMaterial("status_wither_pulse", Hex("#B968E8"));
            CreateBlock(root, "WitherLeft", new Vector3(-0.58f, 0.72f, -0.28f), new Vector3(0.18f, 0.62f, 0.18f), shadow);
            CreateBlock(root, "WitherRight", new Vector3(0.56f, 0.92f, 0.20f), new Vector3(0.20f, 0.76f, 0.20f), pulse);
            CreateBlock(root, "WitherFront", new Vector3(0.12f, 0.48f, -0.56f), new Vector3(0.22f, 0.44f, 0.22f), pulse);
            CreateBlock(root, "WitherHigh", new Vector3(-0.18f, 1.36f, 0.08f), new Vector3(0.16f, 0.52f, 0.16f), shadow);
            _floaters.Add(new Floater(root, root.localPosition.y, StablePulsePhase(instanceId) + 1.7f));
        }

        private void BuildBlockCreature(Transform root, Material material, Color accent, string cardId, bool player, int index)
        {
            if (DemoMinecraftModelFactory.TryGetTextureKey(cardId, out var textureKey))
            {
                if (DemoMinecraftModelFactory.TryBuild(root, cardId, player, GetEntityMaterial))
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

        private Material GetEntityMaterial(string textureKey)
        {
            var key = "entity_tex_" + textureKey;
            if (_materials.TryGetValue(key, out var material)) return material;
            // Vanilla entity textures are pre-colored; keep the tint white and only
            // lift fire creatures with an unshaded emissive boost.
            var emissiveBoost = textureKey == "entity_blaze" || textureKey == "entity_magma_cube" ? 0.3f : 0f;
            material = DemoWorldAssetProvider.CreateEntityMaterial("DemoEntity_" + textureKey, Color.white, textureKey, null, emissiveBoost);
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

        private void BuildRespawnAnchor(Transform root, float footprintWidth)
        {
            var width = Mathf.Min(Mathf.Max(1.56f, footprintWidth - 0.42f), 1.96f);
            var obsidian = GetWorldMaterial("anchor_obsidian", "obsidian", Hex("#24152D"));
            var side = GetWorldMaterial("anchor_side", "respawn_anchor_side1", Hex("#321A48"));
            var top = GetWorldMaterial("anchor_top", "respawn_anchor_top", Hex("#6125A0"));
            var glow = GetWorldMaterial("anchor_glow", "glowstone", Hex("#FFB34A"));
            CreateBlock(root, "RespawnAnchorFoot", new Vector3(0f, 0.12f, 0f),
                new Vector3(width, 0.18f, 0.84f), obsidian);
            CreateBlock(root, "RespawnAnchorBody", new Vector3(0f, 0.50f, 0f),
                new Vector3(width * 0.92f, 0.62f, 0.76f), side);
            CreateBlock(root, "RespawnAnchorTop", new Vector3(0f, 0.84f, 0f),
                new Vector3(width * 0.96f, 0.10f, 0.80f), top);
            CreateBlock(root, "RespawnAnchorCore", new Vector3(0f, 0.51f, -0.40f),
                new Vector3(width * 0.38f, 0.30f, 0.08f), glow);
        }

        private void BuildNetherFortress(Transform root, float footprintWidth)
        {
            var width = Mathf.Max(9.6f, footprintWidth - 0.30f);
            var brick = GetWorldMaterial("fortress_nether_brick", "nether_bricks", Hex("#3B171A"));
            var blackstone = GetWorldMaterial("fortress_blackstone", "polished_blackstone_bricks", Hex("#201B24"));
            var magma = GetWorldMaterial("fortress_magma", "magma", Hex("#D75819"));
            var towerOffset = width * 0.40f;

            CreateBlock(root, "FortressBlackstoneFoot", new Vector3(0f, 0.12f, 0f),
                new Vector3(width, 0.20f, 1.02f), blackstone);
            CreateBlock(root, "FortressBridge", new Vector3(0f, 0.42f, 0f),
                new Vector3(width * 0.94f, 0.42f, 0.88f), brick);
            CreateBlock(root, "FortressParapetBack", new Vector3(0f, 0.78f, 0.36f),
                new Vector3(width * 0.92f, 0.34f, 0.20f), brick);
            CreateBlock(root, "FortressGateLeft", new Vector3(-0.72f, 1.04f, -0.04f),
                new Vector3(0.44f, 1.28f, 0.58f), blackstone);
            CreateBlock(root, "FortressGateRight", new Vector3(0.72f, 1.04f, -0.04f),
                new Vector3(0.44f, 1.28f, 0.58f), blackstone);
            CreateBlock(root, "FortressGateLintel", new Vector3(0f, 1.58f, -0.04f),
                new Vector3(1.82f, 0.30f, 0.62f), brick);
            CreateBlock(root, "FortressLeftTower", new Vector3(-towerOffset, 1.05f, 0f),
                new Vector3(1.12f, 1.72f, 0.98f), brick);
            CreateBlock(root, "FortressRightTower", new Vector3(towerOffset, 1.05f, 0f),
                new Vector3(1.12f, 1.72f, 0.98f), brick);
            CreateBlock(root, "FortressLeftMagma", new Vector3(-towerOffset, 1.97f, 0f),
                new Vector3(0.64f, 0.24f, 0.64f), magma);
            CreateBlock(root, "FortressRightMagma", new Vector3(towerOffset, 1.97f, 0f),
                new Vector3(0.64f, 0.24f, 0.64f), magma);
            for (var index = 0; index < 7; index++)
            {
                var x = Mathf.Lerp(-width * 0.43f, width * 0.43f, index / 6f);
                if (Mathf.Abs(x) < 1.05f || Mathf.Abs(Mathf.Abs(x) - towerOffset) < 0.75f) continue;
                CreateBlock(root, "FortressMerlon_" + index, new Vector3(x, 1.08f, 0.36f),
                    new Vector3(0.42f, 0.48f, 0.24f), brick);
            }
        }

        private void BuildSculkSensor(Transform root, float footprintWidth)
        {
            var width = Mathf.Min(Mathf.Max(1.52f, footprintWidth - 0.56f), 1.90f);
            var bottom = GetWorldMaterial("sculk_sensor_bottom", "sculk_sensor_bottom", Hex("#20343A"));
            var side = GetWorldMaterial("sculk_sensor_side", "sculk_sensor_side", Hex("#173E48"));
            var topColor = Hex("#167E82");
            if (!_materials.TryGetValue("sculk_sensor_top", out var top))
            {
                top = DemoWorldAssetProvider.CreateBlockMaterial(
                    "Demo_SculkSensor_Top", topColor, "sculk_sensor_top", Hex("#073E43"), blockShader);
                _materials["sculk_sensor_top"] = top;
            }
            var tendrilColor = Hex("#58D6C3");
            if (!_materials.TryGetValue("sculk_sensor_tendril", out var tendril))
            {
                tendril = DemoWorldAssetProvider.CreateBlockMaterial(
                    "Demo_SculkSensor_Tendril", tendrilColor, "sculk_sensor_tendril_inactive", Hex("#167E82"), blockShader);
                _materials["sculk_sensor_tendril"] = tendril;
            }

            CreateBlock(root, "SensorFoot", new Vector3(0f, 0.12f, 0f), new Vector3(width, 0.18f, 0.82f), bottom);
            CreateBlock(root, "SensorBody", new Vector3(0f, 0.37f, 0f), new Vector3(width * 0.90f, 0.38f, 0.74f), side);
            CreateBlock(root, "SensorTop", new Vector3(0f, 0.60f, 0f), new Vector3(width * 0.86f, 0.10f, 0.70f), top);
            CreateSculkTendril(root, "FrontLeftTendril", -width * 0.34f, -0.25f, -17f, tendril);
            CreateSculkTendril(root, "FrontRightTendril", width * 0.34f, -0.25f, 17f, tendril);
            CreateSculkTendril(root, "BackLeftTendril", -width * 0.34f, 0.25f, -17f, tendril);
            CreateSculkTendril(root, "BackRightTendril", width * 0.34f, 0.25f, 17f, tendril);
        }

        private static void CreateSculkTendril(
            Transform root,
            string name,
            float x,
            float z,
            float roll,
            Material material)
        {
            var stem = CreateBlock(root, name, new Vector3(x, 0.93f, z), new Vector3(0.12f, 0.68f, 0.12f), material);
            stem.transform.localRotation = Quaternion.Euler(0f, 0f, roll);
            CreateBlock(stem.transform, "Tip", new Vector3(0f, 0.48f, 0f), new Vector3(2.15f, 0.24f, 2.15f), material);
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

        private void BuildDesertTemple(Transform root, float footprintWidth)
        {
            var sandstone = GetWorldMaterial("desert_temple_sandstone", "sandstone", Hex("#D8BE78"));
            var cutSandstone = GetWorldMaterial("desert_temple_cut", "cut_sandstone", Hex("#CDAE66"));
            var chiseledSandstone = GetWorldMaterial("desert_temple_chiseled", "chiseled_sandstone", Hex("#E0C781"));
            var orangeTerracotta = GetWorldMaterial("desert_temple_orange", "orange_terracotta", Hex("#A85324"));
            var width = Mathf.Max(4.65f, footprintWidth);
            var foundationWidth = width - 0.34f;
            var towerOffset = width * 0.31f;
            CreateBlock(root, "TempleFoundation", new Vector3(0f, 0.16f, 0f),
                new Vector3(foundationWidth, 0.28f, 0.90f), cutSandstone);
            CreateBlock(root, "TempleLowerTerrace", new Vector3(0f, 0.42f, 0f),
                new Vector3(width - 0.70f, 0.26f, 0.78f), sandstone);
            CreateBlock(root, "TempleUpperTerrace", new Vector3(0f, 0.67f, 0.03f),
                new Vector3(width - 1.12f, 0.25f, 0.86f), cutSandstone);
            CreateBlock(root, "TempleLeftTower", new Vector3(-towerOffset, 1.12f, 0.05f),
                new Vector3(0.88f, 1.34f, 0.82f), sandstone);
            CreateBlock(root, "TempleRightTower", new Vector3(towerOffset, 1.12f, 0.05f),
                new Vector3(0.88f, 1.34f, 0.82f), sandstone);
            CreateBlock(root, "TempleLeftCrown", new Vector3(-towerOffset, 1.86f, 0.05f),
                new Vector3(1.08f, 0.18f, 0.96f), cutSandstone);
            CreateBlock(root, "TempleRightCrown", new Vector3(towerOffset, 1.86f, 0.05f),
                new Vector3(1.08f, 0.18f, 0.96f), cutSandstone);
            CreateBlock(root, "TempleCentralShrine", new Vector3(0f, 1.04f, 0.07f),
                new Vector3(1.26f, 0.92f, 0.78f), chiseledSandstone);
            CreateBlock(root, "TempleShrineCap", new Vector3(0f, 1.57f, 0.07f),
                new Vector3(1.52f, 0.16f, 0.90f), cutSandstone);
            CreateBlock(root, "TempleEntrance", new Vector3(0f, 0.70f, -0.46f),
                new Vector3(0.48f, 0.62f, 0.12f), orangeTerracotta);
            CreateBlock(root, "TempleGlyphLeft", new Vector3(-towerOffset, 1.18f, -0.39f),
                new Vector3(0.34f, 0.34f, 0.08f), orangeTerracotta);
            CreateBlock(root, "TempleGlyphRight", new Vector3(towerOffset, 1.18f, -0.39f),
                new Vector3(0.34f, 0.34f, 0.08f), orangeTerracotta);
        }

        private Material GetWorldMaterial(string key, string textureKey, Color fallback)
        {
            if (_materials.TryGetValue(key, out var material)) return material;
            material = DemoWorldAssetProvider.CreateBlockMaterial("Demo_" + key, fallback, textureKey, Color.black, blockShader);
            _materials[key] = material;
            return material;
        }

        private void BuildAbandonedMine(Transform root, float footprintWidth)
        {
            var cobblestone = GetWorldMaterial("abandoned_mine_cobble", "cobblestone", Hex("#77746E"));
            var darkOak = GetWorldMaterial("abandoned_mine_dark_oak", "dark_oak_planks", Hex("#4A3424"));
            var deepslate = GetWorldMaterial("abandoned_mine_deepslate", "deepslate_bricks", Hex("#343438"));
            var metal = GetAccentMaterial("abandoned_mine_rail", Hex("#A99E82"));
            var width = Mathf.Max(4.65f, footprintWidth);
            var supportOffset = width * 0.34f;

            CreateBlock(root, "MineFoundation", new Vector3(0f, 0.14f, 0f), new Vector3(width, 0.24f, 0.94f), cobblestone);
            CreateBlock(root, "MineTunnel", new Vector3(0f, 0.73f, 0.18f), new Vector3(width - 0.48f, 1.06f, 0.52f), deepslate);
            CreateBlock(root, "MineLeftSupport", new Vector3(-supportOffset, 0.88f, -0.18f), new Vector3(0.30f, 1.48f, 0.30f), darkOak);
            CreateBlock(root, "MineRightSupport", new Vector3(supportOffset, 0.88f, -0.18f), new Vector3(0.30f, 1.48f, 0.30f), darkOak);
            CreateBlock(root, "MineHeaderBeam", new Vector3(0f, 1.52f, -0.18f), new Vector3(width * 0.76f, 0.30f, 0.34f), darkOak);
            CreateBlock(root, "MineEntrance", new Vector3(0f, 0.79f, -0.47f), new Vector3(width * 0.42f, 0.94f, 0.08f), GetAccentMaterial("abandoned_mine_void", Hex("#17171A")));
            CreateBlock(root, "MineRailLeft", new Vector3(-0.24f, 0.32f, -0.38f), new Vector3(0.09f, 0.06f, 0.92f), metal);
            CreateBlock(root, "MineRailRight", new Vector3(0.24f, 0.32f, -0.38f), new Vector3(0.09f, 0.06f, 0.92f), metal);
            CreateBlock(root, "MineCart", new Vector3(width * 0.28f, 0.56f, -0.30f), new Vector3(0.70f, 0.48f, 0.62f), cobblestone);
            CreateBlock(root, "MineCartLoad", new Vector3(width * 0.28f, 0.87f, -0.30f), new Vector3(0.48f, 0.24f, 0.42f), deepslate);
        }

        private void BuildWoodlandMansion(Transform root, float footprintWidth)
        {
            var cobblestone = GetWorldMaterial("mansion_cobble", "cobblestone", Hex("#77746E"));
            var darkOak = GetWorldMaterial("mansion_dark_oak", "dark_oak_planks", Hex("#4A3424"));
            var window = GetAccentMaterial("mansion_window", Hex("#789E91"));
            var shadow = GetAccentMaterial("mansion_shadow", Hex("#211D1B"));
            var width = Mathf.Max(6.15f, footprintWidth);
            var wingOffset = width * 0.31f;

            CreateBlock(root, "MansionFoundation", new Vector3(0f, 0.16f, 0f), new Vector3(width, 0.28f, 0.98f), cobblestone);
            CreateBlock(root, "MansionLowerHall", new Vector3(0f, 0.70f, 0.04f), new Vector3(width - 0.46f, 0.92f, 0.86f), darkOak);
            CreateBlock(root, "MansionLeftWing", new Vector3(-wingOffset, 1.28f, 0.05f), new Vector3(width * 0.29f, 0.80f, 0.82f), darkOak);
            CreateBlock(root, "MansionRightWing", new Vector3(wingOffset, 1.28f, 0.05f), new Vector3(width * 0.29f, 0.80f, 0.82f), darkOak);
            CreateBlock(root, "MansionCentralHall", new Vector3(0f, 1.42f, 0.05f), new Vector3(width * 0.28f, 1.20f, 0.86f), darkOak);
            CreateBlock(root, "MansionLeftRoof", new Vector3(-wingOffset, 1.78f, 0.05f), new Vector3(width * 0.34f, 0.20f, 1.00f), cobblestone);
            CreateBlock(root, "MansionRightRoof", new Vector3(wingOffset, 1.78f, 0.05f), new Vector3(width * 0.34f, 0.20f, 1.00f), cobblestone);
            CreateBlock(root, "MansionCentralRoof", new Vector3(0f, 2.10f, 0.05f), new Vector3(width * 0.34f, 0.22f, 1.02f), cobblestone);
            CreateBlock(root, "MansionEntrance", new Vector3(0f, 0.70f, -0.46f), new Vector3(0.62f, 0.76f, 0.08f), shadow);
            CreateBlock(root, "MansionLeftWindow", new Vector3(-wingOffset, 1.29f, -0.43f), new Vector3(0.44f, 0.42f, 0.07f), window);
            CreateBlock(root, "MansionRightWindow", new Vector3(wingOffset, 1.29f, -0.43f), new Vector3(0.44f, 0.42f, 0.07f), window);
            CreateBlock(root, "MansionCentralWindow", new Vector3(0f, 1.58f, -0.43f), new Vector3(0.52f, 0.46f, 0.07f), window);
        }

        private void BuildIceSpire(Transform root, float footprintWidth)
        {
            var packedIce = GetWorldMaterial("ice_spire_packed", "packed_ice", Hex("#86B9D1"));
            var deepIce = GetAccentMaterial("ice_spire_deep", Hex("#315E7B"));
            var frost = GetAccentMaterial("ice_spire_frost", Hex("#D6F5FF"));
            var width = Mathf.Max(4.65f, footprintWidth);
            var quarter = width * 0.25f;

            CreateBlock(root, "IceSpireFoundation", new Vector3(0f, 0.14f, 0f), new Vector3(width, 0.24f, 0.98f), deepIce);
            CreateBlock(root, "IceSpireShelf", new Vector3(0f, 0.38f, 0.02f), new Vector3(width - 0.44f, 0.30f, 0.84f), packedIce);
            CreateBlock(root, "IceSpireLeft", new Vector3(-quarter, 1.02f, 0.06f), new Vector3(0.76f, 1.38f, 0.72f), packedIce);
            CreateBlock(root, "IceSpireCenter", new Vector3(0f, 1.40f, 0.04f), new Vector3(0.88f, 2.12f, 0.78f), packedIce);
            CreateBlock(root, "IceSpireRight", new Vector3(quarter, 0.88f, 0.02f), new Vector3(0.70f, 1.10f, 0.68f), packedIce);
            CreateBlock(root, "IceSpireLeftTip", new Vector3(-quarter, 1.84f, 0.06f), new Vector3(0.42f, 0.30f, 0.42f), frost);
            CreateBlock(root, "IceSpireCenterTip", new Vector3(0f, 2.62f, 0.04f), new Vector3(0.46f, 0.36f, 0.46f), frost);
            CreateBlock(root, "IceSpireRightTip", new Vector3(quarter, 1.56f, 0.02f), new Vector3(0.38f, 0.28f, 0.38f), frost);
            CreateBlock(root, "IceSpireRune", new Vector3(0f, 0.72f, -0.47f), new Vector3(0.62f, 0.30f, 0.07f), frost);
        }

        private void BuildSnowHut(Transform root, float footprintWidth)
        {
            var snow = GetWorldMaterial("snow_hut_snow", "snow_block", Hex("#E7F1F3"));
            var packedIce = GetWorldMaterial("snow_hut_ice", "packed_ice", Hex("#8FC6DB"));
            var shadow = GetAccentMaterial("snow_hut_shadow", Hex("#1D3440"));
            var warmLight = GetAccentMaterial("snow_hut_light", Hex("#F2D58A"));
            var width = Mathf.Max(2.32f, footprintWidth);

            CreateBlock(root, "SnowHutFoundation", new Vector3(0f, 0.13f, 0f),
                new Vector3(width, 0.22f, 0.96f), packedIce);
            CreateBlock(root, "SnowHutLower", new Vector3(0f, 0.52f, 0.05f),
                new Vector3(width - 0.18f, 0.66f, 0.86f), snow);
            CreateBlock(root, "SnowHutMiddle", new Vector3(0f, 0.98f, 0.08f),
                new Vector3(width - 0.62f, 0.34f, 0.74f), snow);
            CreateBlock(root, "SnowHutCrown", new Vector3(0f, 1.29f, 0.10f),
                new Vector3(width - 1.18f, 0.30f, 0.58f), snow);
            CreateBlock(root, "SnowHutEntranceRoof", new Vector3(0f, 0.72f, -0.48f),
                new Vector3(0.82f, 0.74f, 0.34f), snow);
            CreateBlock(root, "SnowHutEntrance", new Vector3(0f, 0.55f, -0.67f),
                new Vector3(0.48f, 0.48f, 0.08f), shadow);
            CreateBlock(root, "SnowHutWarmCore", new Vector3(0f, 0.54f, -0.715f),
                new Vector3(0.24f, 0.20f, 0.035f), warmLight);
            CreateBlock(root, "SnowHutLeftWindow", new Vector3(-width * 0.31f, 0.78f, -0.395f),
                new Vector3(0.24f, 0.24f, 0.06f), packedIce);
            CreateBlock(root, "SnowHutRightWindow", new Vector3(width * 0.31f, 0.78f, -0.395f),
                new Vector3(0.24f, 0.24f, 0.06f), packedIce);
        }

        private void BuildEndCrystal(Transform root, float footprintWidth, string instanceId)
        {
            var obsidian = GetWorldMaterial("end_crystal_obsidian", "obsidian", Hex("#17121E"));
            var purpur = GetWorldMaterial("end_crystal_purpur", "purpur_block", Hex("#A878AE"));
            var core = GetAccentMaterial("end_crystal_core", Hex("#F07BFF"));
            var cage = GetAccentMaterial("end_crystal_cage", Hex("#E8D7EF"));
            var width = Mathf.Max(2.05f, footprintWidth);
            CreateBlock(root, "EndCrystalFoundation", new Vector3(0f, 0.13f, 0f),
                new Vector3(width, 0.24f, 0.96f), obsidian);
            CreateBlock(root, "EndCrystalPedestal", new Vector3(0f, 0.39f, 0f),
                new Vector3(0.92f, 0.38f, 0.74f), purpur);

            var assembly = NewChildRoot(root, "EndCrystalFloatingAssembly", new Vector3(0f, 1.34f, 0f));
            var coreBlock = CreateBlock(assembly, "EndCrystalCore", Vector3.zero,
                new Vector3(0.58f, 0.58f, 0.58f), core);
            coreBlock.transform.localRotation = Quaternion.Euler(22.5f, 45f, 22.5f);
            var cageRoot = NewChildRoot(assembly, "EndCrystalWireCage", Vector3.zero);
            cageRoot.localRotation = Quaternion.Euler(0f, 45f, 0f);
            const float half = 0.62f;
            const float size = half * 2f;
            const float thickness = 0.075f;
            var signs = new[] { -1f, 1f };
            var edge = 0;
            foreach (var first in signs)
            {
                foreach (var second in signs)
                {
                    CreateBlock(cageRoot, "EndCrystalCageX_" + edge++, new Vector3(0f, first * half, second * half),
                        new Vector3(size, thickness, thickness), cage);
                    CreateBlock(cageRoot, "EndCrystalCageY_" + edge++, new Vector3(first * half, 0f, second * half),
                        new Vector3(thickness, size, thickness), cage);
                    CreateBlock(cageRoot, "EndCrystalCageZ_" + edge++, new Vector3(first * half, second * half, 0f),
                        new Vector3(thickness, thickness, size), cage);
                }
            }
            _floaters.Add(new Floater(assembly, assembly.localPosition.y, StablePulsePhase(instanceId)));
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
            _combatDamagePopups.Clear();
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
            public readonly Quaternion BaseRotation;

            public Floater(Transform transform, float baseY, float phase)
            {
                Transform = transform;
                BaseY = baseY;
                Phase = phase;
                BaseRotation = transform.localRotation;
            }
        }

        private sealed class CombatDamagePopup
        {
            public readonly Transform Root;
            public readonly TextMesh Label;
            public readonly TextMesh Shadow;
            public readonly float BaseY;
            public readonly float Duration;
            public float Age;

            public CombatDamagePopup(Transform root, TextMesh label, TextMesh shadow, float baseY, float duration)
            {
                Root = root;
                Label = label;
                Shadow = shadow;
                BaseY = baseY;
                Duration = duration;
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
            public float EnginePhase;
            public bool ValidTarget;
            public bool Occupied;
            public bool PriorityTarget;
            public int AuraLayers;
            public DemoEngineReadyKind EngineReadyKind;
            public bool EndPhaseThreat;
            public bool Burning;
            public bool Withered;
            public bool Poisoned;
            public bool Hovered;
            public bool Pressed;
            public bool HoverRejected;
            public bool PressRejected;
            public Color PresentationPulseColor;
            public float PresentationPulseStartedAt = float.NegativeInfinity;
            public float PresentationPulseDuration;

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
                EnginePhase = phase;
            }
        }
    }
}
