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
        private static readonly Color Pale = Hex("#F1E6CB");
        private static readonly Color Muted = Hex("#B2AA96");
        private static readonly Color Cyan = Hex("#5AAE9F");
        private static readonly Color Ember = Hex("#D98545");
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
        private CardDetailsView _cardDetailsView;
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
            var battlefieldPointer = GetComponent<DemoBattlefieldPointerController>();
            if (battlefieldPointer == null) battlefieldPointer = gameObject.AddComponent<DemoBattlefieldPointerController>();
            battlefieldPointer.Configure(_battlefield, OnSlotClicked);

            var canvasObject = new GameObject("DemoCanvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
            canvasObject.transform.SetParent(transform, false);
            var canvas = canvasObject.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.pixelPerfect = true;
            var scaler = canvasObject.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(ReferenceWidth, ReferenceHeight);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.MatchWidthOrHeight;
            scaler.matchWidthOrHeight = 0.5f;
            scaler.referencePixelsPerUnit = DemoUiMetrics.PixelsPerUnit;
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
            var titlePlate = CreateBasePanel(_canvasRoot, "TitlePlate", new Vector2(0, 502), new Vector2(510, 58));
            CreateText(titlePlate, "Title", Vector2.zero, new Vector2(480, 44), "群系竞逐  ·  本地战场演示", 24, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);

            var opponentHud = CreateBasePanel(_canvasRoot, "OpponentHUD", new Vector2(-760, 455), new Vector2(315, 104));
            CreatePanel(opponentHud, "Avatar", new Vector2(-112, 0), new Vector2(70, 70), Hex("#5B2020"));
            CreateText(opponentHud, "AvatarGlyph", new Vector2(-112, 1), new Vector2(60, 60), "▣", 36, Ember, TextAnchor.MiddleCenter, FontStyle.Bold);
            CreateText(opponentHud, "Name", new Vector2(34, 22), new Vector2(190, 32), "下界远征队", 20, Pale, TextAnchor.MiddleLeft, FontStyle.Bold);
            CreateText(opponentHud, "Health", new Vector2(34, -19), new Vector2(190, 30), "❤ 20     ◆ 4/6", 17, Hex("#F4C18A"), TextAnchor.MiddleLeft, FontStyle.Normal);

            var cardBackRoot = CreateRect(_canvasRoot, "OpponentHand", new Vector2(0, 410), new Vector2(480, 130));
            for (var i = 0; i < 5; i++)
            {
                var x = (i - 2) * 72f;
                var back = CreateBasePanel(cardBackRoot, "CardBack", new Vector2(x, Mathf.Abs(i - 2) * -4f), new Vector2(78, 112));
                back.localRotation = Quaternion.Euler(0, 0, (i - 2) * -2.5f);
                CreateText(back, "Rune", Vector2.zero, new Vector2(58, 80), "◇", 32, Ember, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            _playerHud = CreateBasePanel(_canvasRoot, "PlayerHUD", new Vector2(-782, -454), new Vector2(270, 105));
            CreatePanel(_playerHud, "Avatar", new Vector2(-92, 0), new Vector2(70, 70), Hex("#274C2D"));
            CreateText(_playerHud, "AvatarGlyph", new Vector2(-92, 1), new Vector2(60, 60), "▦", 34, Hex("#D3C35B"), TextAnchor.MiddleCenter, FontStyle.Bold);
            CreateText(_playerHud, "Name", new Vector2(35, 22), new Vector2(150, 30), "林地守护者", 18, Pale, TextAnchor.MiddleLeft, FontStyle.Bold);
            CreateText(_playerHud, "Health", new Vector2(35, -17), new Vector2(150, 30), "❤ 20", 18, Hex("#B8E5A9"), TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private void BuildFactionRail()
        {
            var rail = CreateBasePanel(_canvasRoot, "FactionRail", new Vector2(-879, 45), new Vector2(150, 590));
            CreateText(rail, "Header", new Vector2(0, 252), new Vector2(120, 48), "群 系", 18, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);

            for (var i = 0; i < Factions.Length; i++)
            {
                var spec = Factions[i];
                _registry.TryGetTheme(spec.Id, out var theme);
                var button = CreateSecondaryButton(rail, "Faction_" + spec.Id, new Vector2(0, 188 - i * 67), new Vector2(116, 51), spec.Label, 16);
                var selectionAccent = CreatePanel(button.transform, "SelectionAccent", new Vector2(-54, 0), new Vector2(3, 39), theme.Accent);
                selectionAccent.raycastTarget = false;
                var captured = spec.Id;
                button.onClick.AddListener(() => SelectFaction(captured));
                _factionButtons.Add(new FactionButtonView(spec.Id, button, button.targetGraphic as Image, selectionAccent));
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
            var content = CreateRect(root, "Content", Vector2.zero, size - new Vector2(12, 12));
            var label = CreateText(content, "EmptyLabel", Vector2.zero, size - new Vector2(20, 20), kind == DemoSlotKind.Unit ? $"单位格 {index + 1}" : $"建筑格 {index + 1}", 13, Color.clear, TextAnchor.MiddleCenter, FontStyle.Bold);
            return new SlotView(kind, index, content, label);
        }

        private void CreateOpponentSlot(DemoSlotKind kind, int index, Vector2 position, Vector2 size, string cardId)
        {
            var root = CreateRect(_canvasRoot, $"Opponent{kind}Slot{index}", position, size);
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
            var handPlate = CreateBasePanel(_canvasRoot, "HandPlate", new Vector2(35, -418), new Vector2(1360, 232));
            _handRoot = CreateRect(handPlate, "HandCards", new Vector2(-20, 14), new Vector2(1250, 225));
            CreateText(_canvasRoot, "HandLabel", new Vector2(-584, -508), new Vector2(130, 25), "手牌  5/7", 13, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private void BuildInspector()
        {
            var panel = CreateBasePanel(_canvasRoot, "CardDetailsPanel", new Vector2(785, 60), new Vector2(300, 715));
            var inset = DemoUiMetrics.PanelContentInset * 2f;
            _inspectorRoot = CreateRect(panel, "InspectorContent", Vector2.zero, new Vector2(300f - inset, 715f - inset));
            _cardDetailsView = _inspectorRoot.gameObject.AddComponent<CardDetailsView>();
            _cardDetailsView.Configure(_registry, UiFont);
        }

        private void BuildTurnControls()
        {
            var energyPlate = CreateBasePanel(_canvasRoot, "EnergyPlate", new Vector2(-570, -454), new Vector2(150, 72));
            _energyText = CreateText(energyPlate, "Energy", Vector2.zero, new Vector2(130, 54), "◆ 6/6", 22, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);

            var roundPlate = CreateBasePanel(_canvasRoot, "RoundPlate", new Vector2(710, 472), new Vector2(155, 54));
            _roundText = CreateText(roundPlate, "Round", Vector2.zero, new Vector2(135, 38), "第 1 回合", 17, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);

            _endTurnButton = CreatePrimaryActionButton(_canvasRoot, "EndTurnButton", new Vector2(786, -355), new Vector2(230, 86), "结束回合", 23);
            _endTurnLabel = _endTurnButton.GetComponentInChildren<Text>();
            _endTurnButton.onClick.AddListener(OnEndTurn);
            _endTurnButton.gameObject.AddComponent<DemoHoverScale>().Configure(1.04f, 16f);

            var statusPlate = CreateBasePanel(_canvasRoot, "StatusPlate", new Vector2(780, -462), new Vector2(315, 94));
            _statusText = CreateText(statusPlate, "Status", Vector2.zero, new Vector2(285, 70), string.Empty, 14, Pale, TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        private void BuildBanner()
        {
            var banner = CreateBasePanel(_canvasRoot, "TurnBanner", new Vector2(0, 8), new Vector2(570, 112));
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
                view.Image.color = DemoUiStyleCatalog.GetRootFill(DemoUiStyleClass.SecondaryButton);
                view.SelectionAccent.gameObject.SetActive(active);
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
                var card = DemoCardUiFactory.Create(_handRoot, _registry, cardId, new Vector2(158, 216), true, UiFont, () => SelectCard(cardId));
                card.RectTransform.anchoredPosition = new Vector2(x, y);
                card.RectTransform.localRotation = Quaternion.Euler(0, 0, (i - (count - 1) * 0.5f) * -1.8f);
                card.gameObject.AddComponent<DemoHoverScale>().Configure(1.08f, 16f);
                if (selected)
                {
                    card.RectTransform.SetAsLastSibling();
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
                _cardDetailsView.Clear();
                CreateText(_inspectorRoot, "Empty", new Vector2(0, 40), new Vector2(230, 180), "手牌已打空。\n切换左侧群系可重新装填演示手牌。", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
                return;
            }

            _cardDetailsView.ShowCard(_selectedCardId, new Vector2(238, 350), new Vector2(0, 100));
            var deployType = definition.cardType == "UNIT" || definition.cardType == "BUILDING" || definition.cardType == "STRUCTURE";
            if (deployType)
            {
                var target = definition.cardType == "UNIT" ? "单位格" : $"建筑格（占 {Mathf.Max(1, definition.buildingSlots)} 格）";
                CreateText(_inspectorRoot, "DeployHint", new Vector2(0, -118), new Vector2(246, 64), $"选择发光的{target}完成部署", 15, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            else
            {
                var cast = CreateSecondaryButton(_inspectorRoot, "Cast", new Vector2(0, -118), new Vector2(235, 60), "释放（效果占位）", 17);
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
            var command = _match.CreateDeployCommand(_selectedCardId, kind, index);
            var result = _match.ApplyDeploy(definition, command);
            if (result.Accepted) _selectedCardId = _match.Hand.FirstOrDefault();
            ShowStatus(result.Accepted ? $"{result.Message} · 状态 r{result.Revision}" : result.Message, !result.Accepted);
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

        private void CreateTintBand(string name, Vector2 position, Vector2 size, Color color) =>
            CreatePanel(_canvasRoot, name, position, size, color).raycastTarget = false;

        private RectTransform CreateBasePanel(Transform parent, string name, Vector2 position, Vector2 size) =>
            CreateStyledPanel(parent, name, position, size, DemoUiStyleClass.BasePanel);

        private RectTransform CreateStyledPanel(Transform parent, string name, Vector2 position, Vector2 size, DemoUiStyleClass styleClass)
        {
            var prefab = DemoUiPrefabProvider.Load(styleClass);
            var root = prefab != null
                ? Instantiate(prefab, parent, false).GetComponent<RectTransform>()
                : CreateRect(parent, name, position, size);
            root.name = name;
            root.anchorMin = new Vector2(0.5f, 0.5f);
            root.anchorMax = new Vector2(0.5f, 0.5f);
            root.pivot = new Vector2(0.5f, 0.5f);
            root.sizeDelta = size;
            root.anchoredPosition = position;
            var image = root.GetComponent<Image>() ?? root.gameObject.AddComponent<Image>();
            image.color = DemoUiStyleCatalog.GetRootFill(styleClass);
            AttachStyleComponent(root.gameObject, styleClass);
            var shadow = root.GetComponent<Shadow>() ?? root.gameObject.AddComponent<Shadow>();
            shadow.effectColor = new Color(0, 0, 0, 0.46f);
            shadow.effectDistance = new Vector2(5, -5);
            if (_hudMaterialFactory == null)
                _hudMaterialFactory = new DemoHudMaterialFactory(Pale);
            _hudMaterialFactory.Decorate(root, size, styleClass);
            return root;
        }

        private static void AttachStyleComponent(GameObject gameObject, DemoUiStyleClass styleClass)
        {
            if (gameObject.GetComponent<DemoUiStyleComponent>() != null) return;
            switch (styleClass)
            {
                case DemoUiStyleClass.SecondaryButton:
                    gameObject.AddComponent<SecondaryButton>();
                    break;
                case DemoUiStyleClass.PrimaryActionButton:
                    gameObject.AddComponent<PrimaryActionButton>();
                    break;
                default:
                    gameObject.AddComponent<BasePanel>();
                    break;
            }
        }

        private Image CreatePanel(Transform parent, string name, Vector2 position, Vector2 size, Color color)
        {
            var root = CreateRect(parent, name, position, size);
            var image = root.gameObject.AddComponent<Image>();
            image.color = color;
            return image;
        }

        private Button CreateSecondaryButton(Transform parent, string name, Vector2 position, Vector2 size, string label, int fontSize) =>
            CreateStyledButton(parent, name, position, size, label, fontSize, DemoUiStyleClass.SecondaryButton);

        private Button CreatePrimaryActionButton(Transform parent, string name, Vector2 position, Vector2 size, string label, int fontSize) =>
            CreateStyledButton(parent, name, position, size, label, fontSize, DemoUiStyleClass.PrimaryActionButton);

        private Button CreateStyledButton(Transform parent, string name, Vector2 position, Vector2 size, string label, int fontSize, DemoUiStyleClass styleClass)
        {
            var root = CreateStyledPanel(parent, name, position, size, styleClass);
            var button = root.GetComponent<Button>() ?? root.gameObject.AddComponent<Button>();
            button.targetGraphic = root.GetComponent<Image>();
            ConfigureButtonColors(button, DemoUiStyleCatalog.GetRootFill(styleClass), DemoUiStyleCatalog.GetFrameTint(styleClass));
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
            public readonly RectTransform Content;
            public readonly Text EmptyLabel;

            public SlotView(DemoSlotKind kind, int index, RectTransform content, Text emptyLabel)
            {
                Kind = kind;
                Index = index;
                Content = content;
                EmptyLabel = emptyLabel;
            }
        }

        private sealed class FactionButtonView
        {
            public readonly string Id;
            public readonly Button Button;
            public readonly Image Image;
            public readonly Image SelectionAccent;

            public FactionButtonView(string id, Button button, Image image, Image selectionAccent)
            {
                Id = id;
                Button = button;
                Image = image;
                SelectionAccent = selectionAccent;
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
