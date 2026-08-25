using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using BiomeRivals.Content;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace BiomeRivals.Demo
{
    public sealed class DemoSceneController : MonoBehaviour
    {
        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;

        private static readonly Color Ink = Hex("#0E100E");
        private static readonly Color Panel = Hex("#20231F");
        private static readonly Color PanelRaised = Hex("#34382F");
        private static readonly Color Pale = Hex("#F1E6CB");
        private static readonly Color Muted = Hex("#B2AA96");
        private static readonly Color Cyan = Hex("#5AAE9F");
        private static readonly Color Ember = Hex("#D98545");
        private static readonly Color StoneEdge = Hex("#777263");
        private static readonly Color OakEdge = Hex("#9A6A3A");
        private static readonly Color Danger = Hex("#E05A47");

        private readonly DemoLocalMatch _match = new DemoLocalMatch();
        private readonly List<SlotView> _playerUnitSlots = new List<SlotView>();
        private readonly List<SlotView> _playerBuildingSlots = new List<SlotView>();
        private readonly List<FactionButtonView> _factionButtons = new List<FactionButtonView>();
        private readonly string[] _opponentUnits = { "nt_001", string.Empty, "nt_003", string.Empty };
        private readonly string[] _opponentBuildings = { string.Empty, "nt_007", string.Empty };

        private CardContentRegistry _registry;
        private DemoBattlefield3D _battlefield;
        private RectTransform _canvasRoot;
        private RectTransform _handRoot;
        private RectTransform _inspectorRoot;
        private RectTransform _playerHud;
        private Text _energyText;
        private Text _roundText;
        private Text _statusText;
        private Text _endTurnLabel;
        private Button _endTurnButton;
        private CanvasGroup _turnBanner;
        private Text _turnBannerText;
        private string _selectedCardId;
        private string _activeFaction = "plains_forest";
        private bool _built;
        private Font _font;
        private DemoHudMaterialFactory _hudMaterialFactory;

        private static readonly FactionSpec[] Factions =
        {
            new FactionSpec("plains_forest", "平原", "pf"),
            new FactionSpec("desert_badlands", "沙漠", "db"),
            new FactionSpec("snow_ice", "冰原", "si"),
            new FactionSpec("cave_dark_forest", "深暗", "cd"),
            new FactionSpec("ocean_river", "海洋", "or"),
            new FactionSpec("nether", "下界", "nt"),
            new FactionSpec("end", "末地", "ed")
        };

        private void Start()
        {
            Application.runInBackground = true;
            if (HasCommandLineFlag("-disableLocalCardArt")) DemoCardArtProvider.LocalArtEnabled = false;
            if (HasCommandLineFlag("-disableLocalWorldAssets")) DemoWorldAssetProvider.LocalAssetsEnabled = false;
            BuildNow();
            if (HasCommandLineFlag("-previewGroundHover")) _battlefield.SetSlotHovered(true, DemoSlotKind.Unit, 0, true);
            var capturePath = GetCommandLineValue("-captureDemo");
            if (!string.IsNullOrWhiteSpace(capturePath)) StartCoroutine(CaptureDemo(capturePath));
        }

        private void OnDestroy()
        {
            if (_hudMaterialFactory == null) return;
            _hudMaterialFactory.Dispose();
            _hudMaterialFactory = null;
        }

        public void BuildNow()
        {
            if (_built) return;
            _built = true;
            _registry = CardContentLoader.Current;
            BuildInterface();
            SelectFaction(_activeFaction);
            ShowStatus("选择手牌，再点击发光的战场格；法术和材料从右侧释放。", false);
        }

        private void BuildInterface()
        {
            EnsureEventSystem();
            _battlefield = GetComponent<DemoBattlefield3D>();
            if (_battlefield == null) _battlefield = gameObject.AddComponent<DemoBattlefield3D>();
            _battlefield.BuildNow();

            var canvasObject = new GameObject("DemoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = false;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            _canvasRoot = canvasObject.GetComponent<RectTransform>();

            CreateTintBand("BackdropWash", Vector2.zero, new Vector2(ReferenceWidth, ReferenceHeight), new Color(0.02f, 0.025f, 0.02f, 0.16f));
            CreateTintBand("OpponentTint", new Vector2(0, 270), new Vector2(ReferenceWidth, 540), new Color(0.16f, 0.035f, 0.025f, 0.10f));
            CreateTintBand("PlayerTint", new Vector2(0, -270), new Vector2(ReferenceWidth, 540), new Color(0.035f, 0.10f, 0.045f, 0.08f));
            CreatePanel(_canvasRoot, "CenterRiverBed", new Vector2(0, 0), new Vector2(1540, 6), new Color(Ink.r, Ink.g, Ink.b, 0.58f)).raycastTarget = false;
            CreatePanel(_canvasRoot, "CenterRiverGlow", new Vector2(0, 0), new Vector2(1540, 2), new Color(Cyan.r, Cyan.g, Cyan.b, 0.56f)).raycastTarget = false;

            BuildTopChrome();
            BuildFactionRail();
            BuildSlots();
            BuildHandArea();
            BuildInspector();
            BuildTurnControls();
            BuildBanner();
            _playerHud.SetAsLastSibling();
        }

        private void BuildTopChrome()
        {
            var titlePlate = CreateFramedPanel(_canvasRoot, "TitlePlate", new Vector2(0, 502), new Vector2(510, 58), new Color(Panel.r, Panel.g, Panel.b, 0.90f), StoneEdge);
            CreateText(titlePlate, "Title", Vector2.zero, new Vector2(480, 44), "群系竞逐  ·  本地战场演示", 24, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);

            var opponentHud = CreateFramedPanel(_canvasRoot, "OpponentHUD", new Vector2(-760, 455), new Vector2(315, 104), Panel, Ember);
            CreatePanel(opponentHud, "Avatar", new Vector2(-112, 0), new Vector2(70, 70), Hex("#5B2020"));
            CreateText(opponentHud, "AvatarGlyph", new Vector2(-112, 1), new Vector2(60, 60), "▣", 36, Ember, TextAnchor.MiddleCenter, FontStyle.Bold);
            CreateText(opponentHud, "Name", new Vector2(34, 22), new Vector2(190, 32), "下界远征队", 20, Pale, TextAnchor.MiddleLeft, FontStyle.Bold);
            CreateText(opponentHud, "Health", new Vector2(34, -19), new Vector2(190, 30), "❤ 20     ◆ 4/6", 17, Hex("#F4C18A"), TextAnchor.MiddleLeft, FontStyle.Normal);

            var cardBackRoot = CreateRect(_canvasRoot, "OpponentHand", new Vector2(0, 410), new Vector2(480, 130));
            for (var i = 0; i < 5; i++)
            {
                var x = (i - 2) * 72f;
                var back = CreateFramedPanel(cardBackRoot, "CardBack", new Vector2(x, Mathf.Abs(i - 2) * -4f), new Vector2(78, 112), Hex("#2B1917"), Ember);
                back.localRotation = Quaternion.Euler(0, 0, (i - 2) * -2.5f);
                CreateText(back, "Rune", Vector2.zero, new Vector2(58, 80), "◇", 32, Ember, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            _playerHud = CreateFramedPanel(_canvasRoot, "PlayerHUD", new Vector2(-782, -454), new Vector2(270, 105), new Color(Panel.r, Panel.g, Panel.b, 0.93f), OakEdge);
            CreatePanel(_playerHud, "Avatar", new Vector2(-92, 0), new Vector2(70, 70), Hex("#274C2D"));
            CreateText(_playerHud, "AvatarGlyph", new Vector2(-92, 1), new Vector2(60, 60), "▦", 34, Hex("#D3C35B"), TextAnchor.MiddleCenter, FontStyle.Bold);
            CreateText(_playerHud, "Name", new Vector2(35, 22), new Vector2(150, 30), "林地守护者", 18, Pale, TextAnchor.MiddleLeft, FontStyle.Bold);
            CreateText(_playerHud, "Health", new Vector2(35, -17), new Vector2(150, 30), "❤ 20", 18, Hex("#B8E5A9"), TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private void BuildFactionRail()
        {
            var rail = CreateFramedPanel(_canvasRoot, "FactionRail", new Vector2(-879, 45), new Vector2(150, 590), new Color(Panel.r, Panel.g, Panel.b, 0.86f), StoneEdge);
            CreateText(rail, "Header", new Vector2(0, 252), new Vector2(120, 48), "群 系", 18, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);

            for (var i = 0; i < Factions.Length; i++)
            {
                var spec = Factions[i];
                _registry.TryGetTheme(spec.Id, out var theme);
                var button = CreateButton(rail, "Faction_" + spec.Id, new Vector2(0, 188 - i * 67), new Vector2(116, 51), new Color(Ink.r, Ink.g, Ink.b, 0.82f), Color.Lerp(theme.Accent, StoneEdge, 0.55f), spec.Label, 16);
                var captured = spec.Id;
                button.onClick.AddListener(() => SelectFaction(captured));
                _factionButtons.Add(new FactionButtonView(spec.Id, button, button.targetGraphic as Image, theme));
            }
        }

        private void BuildSlots()
        {
            for (var i = 0; i < 3; i++)
                CreateOpponentSlot(DemoSlotKind.Building, i, _battlefield.GetSlotReferencePosition(false, DemoSlotKind.Building, i), new Vector2(190, 92), _opponentBuildings[i]);
            for (var i = 0; i < 4; i++)
                CreateOpponentSlot(DemoSlotKind.Unit, i, _battlefield.GetSlotReferencePosition(false, DemoSlotKind.Unit, i), new Vector2(150, 118), _opponentUnits[i]);

            for (var i = 0; i < 4; i++)
                _playerUnitSlots.Add(CreatePlayerSlot(DemoSlotKind.Unit, i, _battlefield.GetSlotReferencePosition(true, DemoSlotKind.Unit, i), new Vector2(158, 118)));
            for (var i = 0; i < 3; i++)
                _playerBuildingSlots.Add(CreatePlayerSlot(DemoSlotKind.Building, i, _battlefield.GetSlotReferencePosition(true, DemoSlotKind.Building, i), new Vector2(195, 84)));

            CreateText(_canvasRoot, "OpponentLaneLabel", new Vector2(-687, 68), new Vector2(175, 34), "敌方单位排", 13, new Color(Pale.r, Pale.g, Pale.b, 0.72f), TextAnchor.MiddleLeft, FontStyle.Bold);
            CreateText(_canvasRoot, "PlayerLaneLabel", new Vector2(-687, -66), new Vector2(175, 34), "己方单位排", 13, new Color(Pale.r, Pale.g, Pale.b, 0.72f), TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private SlotView CreatePlayerSlot(DemoSlotKind kind, int index, Vector2 position, Vector2 size)
        {
            var root = CreateRect(_canvasRoot, $"Player{kind}Slot{index}", position, size);
            var image = root.gameObject.AddComponent<Image>();
            image.color = Color.clear;
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = image;
            ConfigureInvisibleButton(button);
            var capturedKind = kind;
            var capturedIndex = index;
            button.onClick.AddListener(() => OnSlotClicked(capturedKind, capturedIndex));
            root.gameObject.AddComponent<DemoSlotPointerRelay>().Configure(
                hovered => _battlefield.SetSlotHovered(true, capturedKind, capturedIndex, hovered),
                pressed => _battlefield.SetSlotPressed(true, capturedKind, capturedIndex, pressed));
            var content = CreateRect(root, "Content", Vector2.zero, size - new Vector2(12, 12));
            var label = CreateText(content, "EmptyLabel", Vector2.zero, size - new Vector2(20, 20), kind == DemoSlotKind.Unit ? $"单位格 {index + 1}" : $"建筑格 {index + 1}", 13, Color.clear, TextAnchor.MiddleCenter, FontStyle.Bold);
            return new SlotView(kind, index, image, button, content, label);
        }

        private void CreateOpponentSlot(DemoSlotKind kind, int index, Vector2 position, Vector2 size, string cardId)
        {
            var root = CreateRect(_canvasRoot, $"Opponent{kind}Slot{index}", position, size);
            var hitArea = root.gameObject.AddComponent<Image>();
            hitArea.color = Color.clear;
            hitArea.raycastTarget = false;
            var occupied = !string.IsNullOrEmpty(cardId);
            _battlefield.SetSlotState(false, kind, index, false, occupied);
            if (string.IsNullOrEmpty(cardId))
            {
                return;
            }
            CreateWorldPieceLabel(root, cardId, size - new Vector2(10, 10), true);
        }

        private void BuildHandArea()
        {
            var handPlate = CreateFramedPanel(_canvasRoot, "HandPlate", new Vector2(35, -418), new Vector2(1360, 232), new Color(Ink.r, Ink.g, Ink.b, 0.72f), OakEdge);
            _handRoot = CreateRect(handPlate, "HandCards", new Vector2(-20, 14), new Vector2(1250, 225));
            CreateText(_canvasRoot, "HandLabel", new Vector2(-584, -508), new Vector2(130, 25), "手牌  5/7", 13, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private void BuildInspector()
        {
            var panel = CreateFramedPanel(_canvasRoot, "InspectorPanel", new Vector2(785, 60), new Vector2(300, 715), new Color(Panel.r, Panel.g, Panel.b, 0.92f), StoneEdge);
            _inspectorRoot = CreateRect(panel, "InspectorContent", Vector2.zero, new Vector2(280, 695));
        }

        private void BuildTurnControls()
        {
            var energyPlate = CreateFramedPanel(_canvasRoot, "EnergyPlate", new Vector2(-570, -454), new Vector2(150, 72), new Color(Panel.r, Panel.g, Panel.b, 0.92f), OakEdge);
            _energyText = CreateText(energyPlate, "Energy", Vector2.zero, new Vector2(130, 54), "◆ 6/6", 22, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);

            var roundPlate = CreateFramedPanel(_canvasRoot, "RoundPlate", new Vector2(710, 472), new Vector2(155, 54), Panel, Ember);
            _roundText = CreateText(roundPlate, "Round", Vector2.zero, new Vector2(135, 38), "第 1 回合", 17, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);

            _endTurnButton = CreateButton(_canvasRoot, "EndTurn", new Vector2(786, -355), new Vector2(230, 86), Hex("#245D50"), Cyan, "结束回合", 23);
            _endTurnLabel = _endTurnButton.GetComponentInChildren<Text>();
            _endTurnButton.onClick.AddListener(OnEndTurn);
            _endTurnButton.gameObject.AddComponent<DemoHoverScale>().Configure(1.04f, 16f);

            var statusPlate = CreateFramedPanel(_canvasRoot, "StatusPlate", new Vector2(780, -462), new Vector2(315, 94), new Color(Ink.r, Ink.g, Ink.b, 0.86f), StoneEdge);
            _statusText = CreateText(statusPlate, "Status", Vector2.zero, new Vector2(285, 70), string.Empty, 14, Pale, TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        private void BuildBanner()
        {
            var banner = CreateFramedPanel(_canvasRoot, "TurnBanner", new Vector2(0, 8), new Vector2(570, 112), new Color(Ink.r, Ink.g, Ink.b, 0.96f), Cyan);
            _turnBanner = banner.gameObject.AddComponent<CanvasGroup>();
            _turnBanner.alpha = 0f;
            _turnBanner.blocksRaycasts = false;
            _turnBannerText = CreateText(banner, "Text", Vector2.zero, new Vector2(530, 82), string.Empty, 30, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void SelectFaction(string factionId)
        {
            _activeFaction = factionId;
            var spec = Factions.First(item => item.Id == factionId);
            var ids = Enumerable.Range(1, 5).Select(index => $"{spec.Prefix}_{index:000}");
            _match.ResetHand(ids);
            _selectedCardId = _match.Hand.FirstOrDefault();
            RefreshAll();
            ShowStatus($"已切换到{spec.Label}牌组；效果文本已注册，复杂规则暂为展示。", false);
        }

        private void RefreshAll()
        {
            RefreshFactionButtons();
            RefreshHand();
            RefreshPlayerSlots();
            _battlefield.SyncPieces(_match.UnitSlots, _match.BuildingSlots, _opponentUnits, _opponentBuildings, _registry);
            RefreshInspector();
            _energyText.text = $"◆ {_match.Energy}/{_match.MaxEnergy}";
            _roundText.text = $"第 {_match.Round} 回合";
            _endTurnButton.interactable = _match.IsPlayerTurn;
            _endTurnLabel.text = _match.IsPlayerTurn ? "结束回合" : "对手行动中";
        }

        private void RefreshFactionButtons()
        {
            foreach (var view in _factionButtons)
            {
                var active = view.Id == _activeFaction;
                view.Image.color = active
                    ? Color.Lerp(view.Theme.FrameBase, PanelRaised, 0.22f)
                    : new Color(Ink.r, Ink.g, Ink.b, 0.82f);
            }
        }

        private void RefreshHand()
        {
            ClearChildren(_handRoot);
            var count = _match.Hand.Count;
            for (var i = 0; i < count; i++)
            {
                var cardId = _match.Hand[i];
                var selected = cardId == _selectedCardId;
                var x = (i - (count - 1) * 0.5f) * 176f;
                var y = selected ? 20f : -Mathf.Abs(i - (count - 1) * 0.5f) * 4f;
                var card = CreateCardFace(_handRoot, cardId, new Vector2(158, 216), true, () => SelectCard(cardId));
                card.anchoredPosition = new Vector2(x, y);
                card.localRotation = Quaternion.Euler(0, 0, (i - (count - 1) * 0.5f) * -1.8f);
                card.gameObject.AddComponent<DemoHoverScale>().Configure(1.08f, 16f);
                if (selected)
                {
                    card.SetAsLastSibling();
                }
            }
        }

        private void RefreshPlayerSlots()
        {
            foreach (var view in _playerUnitSlots) RefreshSlot(view, _match.UnitSlots[view.Index]);
            foreach (var view in _playerBuildingSlots) RefreshSlot(view, _match.BuildingSlots[view.Index]);
        }

        private void RefreshSlot(SlotView view, string cardId)
        {
            ClearChildrenExcept(view.Content, view.EmptyLabel.gameObject);
            var empty = string.IsNullOrEmpty(cardId);
            view.EmptyLabel.gameObject.SetActive(empty);
            var valid = empty && IsSelectedValidFor(view.Kind);
            view.Image.color = Color.clear;
            view.EmptyLabel.color = Color.clear;
            _battlefield.SetSlotState(true, view.Kind, view.Index, valid, !empty);

            if (!empty)
            {
                if (view.Kind == DemoSlotKind.Building && view.Index > 0 && _match.BuildingSlots[view.Index - 1] == cardId)
                    CreateText(view.Content, "Occupied", Vector2.zero, view.Content.sizeDelta, "结构占用", 14, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
                else
                    CreateWorldPieceLabel(view.Content, cardId, view.Content.sizeDelta, false);
            }
        }

        private void RefreshInspector()
        {
            ClearChildren(_inspectorRoot);
            CreateText(_inspectorRoot, "Header", new Vector2(0, 315), new Vector2(250, 38), "卡牌详情", 20, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (string.IsNullOrEmpty(_selectedCardId) || !_registry.TryGetDefinition(_selectedCardId, out var definition))
            {
                CreateText(_inspectorRoot, "Empty", new Vector2(0, 40), new Vector2(230, 180), "手牌已打空。\n切换左侧群系可重新装填演示手牌。", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
                return;
            }

            var face = CreateCardFace(_inspectorRoot, _selectedCardId, new Vector2(238, 350), false, null);
            face.anchoredPosition = new Vector2(0, 100);
            var deployType = definition.cardType == "UNIT" || definition.cardType == "BUILDING" || definition.cardType == "STRUCTURE";
            if (deployType)
            {
                var target = definition.cardType == "UNIT" ? "单位格" : $"建筑格（占 {Mathf.Max(1, definition.buildingSlots)} 格）";
                CreateText(_inspectorRoot, "DeployHint", new Vector2(0, -118), new Vector2(246, 64), $"选择发光的{target}完成部署", 15, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            else
            {
                var cast = CreateButton(_inspectorRoot, "Cast", new Vector2(0, -118), new Vector2(235, 60), Hex("#6A4C22"), Ember, "释放（效果占位）", 17);
                cast.onClick.AddListener(CastSelectedCard);
                cast.gameObject.AddComponent<DemoHoverScale>().Configure(1.04f, 16f);
            }
            CreateText(_inspectorRoot, "Implementation", new Vector2(0, -190), new Vector2(250, 62), $"规则状态：{definition.effectImplementationStatus}\n稳定效果槽：{string.Join(", ", definition.effectIds ?? Array.Empty<string>())}", 12, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        private bool IsSelectedValidFor(DemoSlotKind kind)
        {
            if (string.IsNullOrEmpty(_selectedCardId) || !_registry.TryGetDefinition(_selectedCardId, out var definition)) return false;
            return kind == DemoSlotKind.Unit
                ? definition.cardType == "UNIT"
                : definition.cardType == "BUILDING" || definition.cardType == "STRUCTURE";
        }

        private void SelectCard(string cardId)
        {
            _selectedCardId = cardId;
            RefreshAll();
            if (_registry.TryGetText(cardId, out var text)) ShowStatus($"已选择：{text.name}", false);
        }

        private void OnSlotClicked(DemoSlotKind kind, int index)
        {
            if (string.IsNullOrEmpty(_selectedCardId) || !_registry.TryGetDefinition(_selectedCardId, out var definition))
            {
                ShowStatus("请先选择一张手牌。", true);
                return;
            }
            var success = _match.TryDeploy(definition, kind, index, out var message);
            if (success) _selectedCardId = _match.Hand.FirstOrDefault();
            ShowStatus(message, !success);
            RefreshAll();
        }

        private void CastSelectedCard()
        {
            if (string.IsNullOrEmpty(_selectedCardId) || !_registry.TryGetDefinition(_selectedCardId, out var definition)) return;
            var success = _match.TryCast(definition, out var message);
            if (success) _selectedCardId = _match.Hand.FirstOrDefault();
            ShowStatus(message, !success);
            RefreshAll();
        }

        private void OnEndTurn()
        {
            if (!_match.IsPlayerTurn) return;
            _match.EndPlayerTurn();
            RefreshAll();
            StartCoroutine(SimulateOpponentTurn());
        }

        private IEnumerator SimulateOpponentTurn()
        {
            yield return ShowTurnBanner("对手回合", Ember);
            yield return new WaitForSecondsRealtime(0.55f);
            _match.BeginNextPlayerTurn();
            RefreshAll();
            ShowStatus("新回合开始：红石能量已补满并提高上限。", false);
            yield return ShowTurnBanner("你的回合", Cyan);
        }

        private IEnumerator ShowTurnBanner(string value, Color color)
        {
            _turnBannerText.text = value;
            _turnBannerText.color = color;
            for (var time = 0f; time < 0.18f; time += Time.unscaledDeltaTime)
            {
                _turnBanner.alpha = Mathf.Clamp01(time / 0.18f);
                yield return null;
            }
            _turnBanner.alpha = 1f;
            yield return new WaitForSecondsRealtime(0.32f);
            for (var time = 0f; time < 0.2f; time += Time.unscaledDeltaTime)
            {
                _turnBanner.alpha = 1f - Mathf.Clamp01(time / 0.2f);
                yield return null;
            }
            _turnBanner.alpha = 0f;
        }

        private IEnumerator CaptureDemo(string path)
        {
            yield return null;
            yield return null;
            if (HasCommandLineFlag("-previewGroundHover")) yield return new WaitForSecondsRealtime(0.2f);
            var canvas = GetComponentInChildren<Canvas>();
            var camera = _battlefield.BoardCamera;
            if (camera == null) throw new InvalidOperationException("2.5D battlefield camera is not available.");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var originalMode = canvas.renderMode;
            var originalCamera = canvas.worldCamera;
            var originalTarget = camera.targetTexture;
            var renderTexture = new RenderTexture(1920, 1080, 24, RenderTextureFormat.ARGB32);
            renderTexture.Create();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            camera.targetTexture = renderTexture;
            Canvas.ForceUpdateCanvases();
            camera.Render();

            var previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(1920, 1080, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, 1920, 1080), 0, 0, false);
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());

            RenderTexture.active = previousActive;
            camera.targetTexture = originalTarget;
            canvas.renderMode = originalMode;
            canvas.worldCamera = originalCamera;
            renderTexture.Release();
            Destroy(renderTexture);
            Destroy(texture);
            Debug.Log("Demo screenshot saved: " + path);
            Application.Quit(0);
        }

        private void ShowStatus(string value, bool error)
        {
            if (_statusText == null) return;
            _statusText.text = value;
            _statusText.color = error ? Danger : Pale;
        }

        private RectTransform CreateCardFace(Transform parent, string cardId, Vector2 size, bool compact, Action onClick)
        {
            if (!_registry.TryGetDefinition(cardId, out var definition) || !_registry.TryGetText(cardId, out var text))
                throw new InvalidOperationException("Card content is not registered: " + cardId);
            _registry.TryGetTheme(definition.themeId, out var theme);

            var shell = Color.Lerp(theme.FrameDark, Ink, 0.28f);
            var frame = Color.Lerp(theme.FrameBase, PanelRaised, 0.18f);
            var cardEdge = Color.Lerp(theme.Accent, OakEdge, 0.34f);
            var frameSprite = DemoCardFrameProvider.Load(definition.themeId);
            var usesStudyFrame = frameSprite != null;
            RectTransform root;
            Image image;
            if (usesStudyFrame)
            {
                root = CreateRect(parent, "Card_" + cardId, Vector2.zero, size);
                image = root.gameObject.AddComponent<Image>();
                image.sprite = frameSprite;
                image.type = Image.Type.Simple;
                image.preserveAspect = false;
                image.color = Color.white;
            }
            else
            {
                root = CreateFramedPanel(parent, "Card_" + cardId, Vector2.zero, size, shell, cardEdge);
                image = root.GetComponent<Image>();
            }
            if (onClick != null)
            {
                var button = root.gameObject.AddComponent<Button>();
                button.targetGraphic = image;
                ConfigureButtonColors(button, usesStudyFrame ? Color.white : shell, theme.Accent);
                button.onClick.AddListener(() => onClick());
            }

            if (!usesStudyFrame)
            {
                var inner = CreatePanel(root, "Frame", Vector2.zero, size - new Vector2(10, 10), frame);
                inner.raycastTarget = false;
            }
            var h = size.y;
            var w = size.x;
            var titleHeight = compact ? 31f : 39f;
            var titleY = h * 0.5f - titleHeight * 0.72f;
            if (!usesStudyFrame) CreatePanel(root, "TitleBand", new Vector2(0, titleY), new Vector2(w - 14, titleHeight), shell).raycastTarget = false;

            var costSize = compact ? 34f : 43f;
            var socketVisualSize = usesStudyFrame ? costSize * 1.55f : costSize;
            var costPosition = new Vector2(-w * 0.5f + socketVisualSize * 0.52f, h * 0.5f - socketVisualSize * 0.52f);
            if (usesStudyFrame)
            {
                var costFrame = CreatePanel(root, "CostSocketFrame", costPosition, new Vector2(socketVisualSize, socketVisualSize), Color.white);
                costFrame.sprite = DemoCardFrameProvider.LoadCostSocket(definition.themeId);
                costFrame.preserveAspect = true;
                costFrame.raycastTarget = false;
            }
            else CreatePanel(root, "CostSocket", costPosition, new Vector2(costSize, costSize), theme.Accent).raycastTarget = false;
            CreateText(root, "Name", new Vector2(usesStudyFrame ? 12f : 7f, titleY), new Vector2(w - (usesStudyFrame ? 64f : 50f), titleHeight - 4), text.name, compact ? 15 : 20, theme.TitleText, TextAnchor.MiddleCenter, FontStyle.Bold);
            CreateText(root, "Cost", costPosition, new Vector2(costSize, costSize), definition.cost.ToString(), compact ? 18 : 23, usesStudyFrame ? Pale : Ink, TextAnchor.MiddleCenter, FontStyle.Bold);

            var artHeight = compact ? h * 0.36f : h * 0.39f;
            var artY = compact ? h * 0.105f : h * 0.11f;
            var artWell = Color.Lerp(theme.FrameDark, Ink, 0.40f);
            artWell.a = 0.94f;
            if (!usesStudyFrame) CreatePanel(root, "ArtWell", new Vector2(0, artY), new Vector2(w - 18, artHeight), artWell).raycastTarget = false;
            var sprite = DemoCardArtProvider.Load(cardId);
            if (sprite != null)
            {
                var art = CreatePanel(root, "Art", new Vector2(0, artY), new Vector2(Mathf.Min(w * 0.54f, artHeight * 0.78f), artHeight * 0.78f), Color.white);
                art.sprite = sprite;
                art.preserveAspect = true;
                art.raycastTarget = false;
            }
            else
            {
                CreateText(root, "ArtFallback", new Vector2(0, artY), new Vector2(w - 28, artHeight - 10), "◆", compact ? 34 : 52, theme.Accent, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            var rulesHeight = compact ? h * 0.31f : h * 0.29f;
            var rulesY = -h * 0.235f;
            if (!usesStudyFrame) CreatePanel(root, "RulesSurface", new Vector2(0, rulesY), new Vector2(w - 18, rulesHeight), theme.RulesSurface).raycastTarget = false;
            var rules = CreateText(root, "Rules", new Vector2(0, rulesY + 2), new Vector2(w - (usesStudyFrame ? 38 : 30), rulesHeight - (usesStudyFrame ? 15 : 8)), text.rulesText, compact ? 11 : 14, theme.BodyText, TextAnchor.MiddleCenter, FontStyle.Normal);
            rules.resizeTextForBestFit = compact;
            rules.resizeTextMinSize = 9;
            rules.resizeTextMaxSize = compact ? 11 : 14;

            var typeY = -h * 0.5f + (compact ? 20f : 24f);
            CreateText(root, "Type", new Vector2(0, typeY), new Vector2(w - 54, compact ? 20 : 24), text.typeLabel, compact ? 10 : 12, theme.TitleText, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (definition.hasAttack) CreateStat(root, new Vector2(-w * 0.5f + 22, -h * 0.5f + 21), definition.attack.ToString(), Hex("#E6A641"), compact ? 28 : 36, !usesStudyFrame);
            if (definition.hasHealth) CreateStat(root, new Vector2(w * 0.5f - 22, -h * 0.5f + 21), definition.health.ToString(), Hex("#C94D42"), compact ? 28 : 36, !usesStudyFrame);
            if (definition.hasDurability) CreateStat(root, new Vector2(w * 0.5f - 22, -h * 0.5f + 21), definition.durability.ToString(), Hex("#74A9B8"), compact ? 28 : 36, !usesStudyFrame);
            return root;
        }

        private void CreateWorldPieceLabel(Transform parent, string cardId, Vector2 size, bool enemy)
        {
            if (!_registry.TryGetDefinition(cardId, out var definition) || !_registry.TryGetText(cardId, out var text)) return;
            _registry.TryGetTheme(definition.themeId, out var theme);
            var accent = enemy ? Ember : theme.Accent;
            var stats = definition.hasAttack && definition.hasHealth
                ? $"{text.name}   {definition.attack}/{definition.health}"
                : definition.hasHealth
                    ? $"{text.name}   ❤ {definition.health}"
                    : text.name;
            var labelY = -size.y * 0.34f;
            var plate = CreatePanel(parent, "WorldLabel", new Vector2(0, labelY), new Vector2(size.x - 8, 30), new Color(Ink.r, Ink.g, Ink.b, 0.84f));
            plate.raycastTarget = false;
            CreatePanel(parent, "WorldLabelAccent", new Vector2(0, labelY + 14), new Vector2(size.x - 8, 2), new Color(accent.r, accent.g, accent.b, 0.82f)).raycastTarget = false;
            CreateText(parent, "WorldLabelText", new Vector2(0, labelY), new Vector2(size.x - 14, 26), stats, 12, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void CreateStat(Transform parent, Vector2 position, string value, Color color, float size, bool drawSocket)
        {
            if (drawSocket) CreatePanel(parent, "StatSocket", position, new Vector2(size, size), color).raycastTarget = false;
            CreateText(parent, "Stat", position, new Vector2(size, size), value, Mathf.RoundToInt(size * 0.54f), Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void CreateTintBand(string name, Vector2 position, Vector2 size, Color color) =>
            CreatePanel(_canvasRoot, name, position, size, color).raycastTarget = false;

        private RectTransform CreateFramedPanel(Transform parent, string name, Vector2 position, Vector2 size, Color fill, Color edge)
        {
            var root = CreateRect(parent, name, position, size);
            var image = root.gameObject.AddComponent<Image>();
            image.color = fill;
            var shadow = root.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.46f);
            shadow.effectDistance = new Vector2(5, -5);
            if (_hudMaterialFactory == null)
                _hudMaterialFactory = new DemoHudMaterialFactory(StoneEdge, OakEdge, Ember, Cyan, Pale);
            _hudMaterialFactory.Decorate(root, size, fill, edge);
            return root;
        }

        private Image CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var root = CreateRect(parent, name, position, size);
            var image = root.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private Button CreateButton(Transform parent, string name, Vector2 position, Vector2 size, Color fill, Color accent, string label, int fontSize)
        {
            var root = CreateFramedPanel(parent, name, position, size, fill, accent);
            var button = root.gameObject.AddComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();
            ConfigureButtonColors(button, fill, accent);
            CreateText(root, "Label", Vector2.zero, size - new Vector2(12, 8), label, fontSize, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
            return button;
        }

        private static void ConfigureButtonColors(Button button, Color normal, Color accent)
        {
            var colors = button.colors;
            colors.normalColor = normal;
            colors.highlightedColor = Color.Lerp(normal, accent, 0.38f);
            colors.pressedColor = Color.Lerp(normal, accent, 0.62f);
            colors.selectedColor = colors.highlightedColor;
            colors.disabledColor = new Color(normal.r, normal.g, normal.b, 0.42f);
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0.08f;
            button.colors = colors;
        }

        private static void ConfigureInvisibleButton(Button button)
        {
            var colors = button.colors;
            colors.normalColor = Color.clear;
            colors.highlightedColor = Color.clear;
            colors.pressedColor = Color.clear;
            colors.selectedColor = Color.clear;
            colors.disabledColor = Color.clear;
            colors.colorMultiplier = 1f;
            colors.fadeDuration = 0f;
            button.colors = colors;
        }

        private Text CreateText(Transform parent, string name, Vector2 position, Vector2 size, string value, int fontSize, Color color, TextAnchor alignment, FontStyle style)
        {
            var root = CreateRect(parent, name, position, size);
            var text = root.gameObject.AddComponent<Text>();
            text.font = UiFont;
            text.text = value;
            text.fontSize = fontSize;
            text.fontStyle = style;
            text.color = color;
            text.alignment = alignment;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
            return text;
        }

        private RectTransform CreateRect(Transform parent, string name, Vector2 position, Vector2 size)
        {
            var go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            var rect = go.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0.5f, 0.5f);
            rect.anchorMax = new Vector2(0.5f, 0.5f);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = size;
            rect.anchoredPosition = position;
            return rect;
        }

        private Font UiFont
        {
            get
            {
                if (_font != null) return _font;
                _font = Font.CreateDynamicFontFromOSFont(new[] { "Microsoft YaHei UI", "Microsoft YaHei", "SimHei", "Arial" }, 20);
                if (_font == null) _font = Resources.GetBuiltinResource<Font>("LegacyRuntime.ttf");
                return _font;
            }
        }

        private void EnsureEventSystem()
        {
            if (EventSystem.current != null) return;
            var go = new GameObject("EventSystem", typeof(EventSystem), typeof(StandaloneInputModule));
            go.transform.SetParent(transform, false);
        }

        private static void ClearChildren(Transform parent)
        {
            for (var i = parent.childCount - 1; i >= 0; i--) DestroyUiObject(parent.GetChild(i).gameObject);
        }

        private static void ClearChildrenExcept(Transform parent, GameObject preserved)
        {
            for (var i = parent.childCount - 1; i >= 0; i--)
            {
                var child = parent.GetChild(i).gameObject;
                if (child != preserved) DestroyUiObject(child);
            }
        }

        private static void DestroyUiObject(GameObject target)
        {
            if (Application.isPlaying) Destroy(target);
            else DestroyImmediate(target);
        }

        private static Color Hex(string value)
        {
            if (ColorUtility.TryParseHtmlString(value, out var color)) return color;
            throw new FormatException("Invalid demo color: " + value);
        }

        private static string GetCommandLineValue(string key)
        {
            var args = Environment.GetCommandLineArgs();
            for (var i = 0; i < args.Length - 1; i++)
                if (string.Equals(args[i], key, StringComparison.Ordinal)) return args[i + 1];
            return string.Empty;
        }

        private static bool HasCommandLineFlag(string key) =>
            Environment.GetCommandLineArgs().Any(value => string.Equals(value, key, StringComparison.Ordinal));

        private sealed class SlotView
        {
            public readonly DemoSlotKind Kind;
            public readonly int Index;
            public readonly Image Image;
            public readonly Button Button;
            public readonly RectTransform Content;
            public readonly Text EmptyLabel;

            public SlotView(DemoSlotKind kind, int index, Image image, Button button, RectTransform content, Text emptyLabel)
            {
                Kind = kind;
                Index = index;
                Image = image;
                Button = button;
                Content = content;
                EmptyLabel = emptyLabel;
            }
        }

        private sealed class FactionButtonView
        {
            public readonly string Id;
            public readonly Button Button;
            public readonly Image Image;
            public readonly CardThemePalette Theme;

            public FactionButtonView(string id, Button button, Image image, CardThemePalette theme)
            {
                Id = id;
                Button = button;
                Image = image;
                Theme = theme;
            }
        }

        private sealed class FactionSpec
        {
            public readonly string Id;
            public readonly string Label;
            public readonly string Prefix;

            public FactionSpec(string id, string label, string prefix)
            {
                Id = id;
                Label = label;
                Prefix = prefix;
            }
        }
    }
}
