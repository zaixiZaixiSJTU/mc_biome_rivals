using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using BiomeRivals.Bootstrap;
using BiomeRivals.Content;
using BiomeRivals.Core;
using BiomeRivals.Networking;
using BiomeRivals.Presentation;
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
        private readonly List<SlotView> _opponentUnitSlots = new List<SlotView>();
        private readonly List<SlotView> _opponentBuildingSlots = new List<SlotView>();
        private readonly List<FactionButtonView> _factionButtons = new List<FactionButtonView>();
        private readonly List<IMatchEventPresenter> _eventPresenters = new List<IMatchEventPresenter>();

        private CardContentRegistry _registry;
        private DemoBattlefield3D _battlefield;
        private RectTransform _canvasRoot;
        private RectTransform _handRoot;
        private RectTransform _opponentHandRoot;
        private CanvasGroup _handCanvasGroup;
        private RectTransform _inspectorRoot;
        private CardDetailsView _cardDetailsView;
        private RectTransform _playerHud;
        private Text _energyText;
        private Text _handLabel;
        private Text _opponentHealthText;
        private Image _opponentAvatarImage;
        private Text _opponentAvatarGlyph;
        private Text _opponentNameText;
        private Text _opponentFactionLabel;
        private Text _playerHealthText;
        private Image _playerAvatarImage;
        private Text _playerAvatarGlyph;
        private Text _playerNameText;
        private CanvasGroup _playerEffectFlash;
        private Text _roundText;
        private Text _titleText;
        private Text _statusText;
        private Text _endTurnLabel;
        private Button _endTurnButton;
        private CanvasGroup _turnBanner;
        private Text _turnBannerText;
        private Text _onlineStatusText;
        private Text _onlineActionLabel;
        private Button _onlineActionButton;
        private IMatchGateway _onlineGateway;
        private DemoOnlineMatchSession _onlineSession;
        private Image _opponentTint;
        private Image _playerTint;
        private string _selectedCardId;
        private string _selectedAttackerInstanceId;
        private string _pendingTargetCardId;
        private string _activeFaction = "plains_forest";
        private string _opponentFaction = "nether";
        private bool _built;
        private Font _font;
        private DemoHudMaterialFactory _hudMaterialFactory;

        private bool IsOnlineBoard => _onlineSession?.HasAuthoritativeState == true;
        private IDemoMatchView MatchView => IsOnlineBoard ? _onlineSession.View : _match;

        private static readonly FactionSpec[] Factions =
        {
            new FactionSpec("plains_forest", "平原", "pf", "林地守护者"),
            new FactionSpec("desert_badlands", "沙漠", "db", "荒漠考古队"),
            new FactionSpec("snow_ice", "冰原", "si", "雪原巡守者"),
            new FactionSpec("cave_dark_forest", "深暗", "cd", "幽匿勘探队"),
            new FactionSpec("ocean_river", "海洋", "or", "潮汐守卫"),
            new FactionSpec("nether", "下界", "nt", "熔岩统御者"),
            new FactionSpec("end", "末地", "ed", "虚空行者")
        };

        private void Start()
        {
            Application.runInBackground = true;
            if (HasCommandLineFlag("-disableLocalCardArt")) DemoCardArtProvider.LocalArtEnabled = false;
            if (HasCommandLineFlag("-disableLocalWorldAssets")) DemoWorldAssetProvider.LocalAssetsEnabled = false;
            BuildNow();
            var previewPlayerFaction = GetCommandLineValue("-previewPlayerFaction");
            if (Factions.Any(item => item.Id == previewPlayerFaction)) SelectFaction(previewPlayerFaction);
            var previewOpponentFaction = GetCommandLineValue("-previewOpponentFaction");
            if (Factions.Any(item => item.Id == previewOpponentFaction)) SelectOpponentFaction(previewOpponentFaction);
            if (HasCommandLineFlag("-previewEffect"))
            {
                SelectFaction("nether");
                SelectCard("nt_006");
                CastSelectedCard();
            }
            if (HasCommandLineFlag("-previewTargeting"))
            {
                SelectFaction("snow_ice");
                SelectCard("si_001");
                CastSelectedCard();
            }
            if (HasCommandLineFlag("-previewCombat")) OnEndTurn();
            if (HasCommandLineFlag("-previewGroundHover")) _battlefield.SetSlotHovered(true, DemoSlotKind.Unit, 0, true);
            var capturePath = GetCommandLineValue("-captureDemo");
            if (!string.IsNullOrWhiteSpace(capturePath)) StartCoroutine(CaptureDemo(capturePath));
            var onlineProbePath = GetCommandLineValue("-onlineProbe");
            var captureOnlinePath = GetCommandLineValue("-captureOnline");
            if (HasCommandLineFlag("-autoOnline") || !string.IsNullOrWhiteSpace(onlineProbePath) || !string.IsNullOrWhiteSpace(captureOnlinePath))
            {
                ToggleOnlineConnection();
                if (!string.IsNullOrWhiteSpace(onlineProbePath) || !string.IsNullOrWhiteSpace(captureOnlinePath))
                    RunOnlineProbe(onlineProbePath, captureOnlinePath, HasCommandLineFlag("-autoOnlineAction"));
            }
        }

        private void OnDestroy()
        {
            if (_onlineGateway != null) _onlineGateway.ConnectionStateChanged -= HandleOnlineConnectionState;
            DisposeOnlineSession();
            UnregisterOnlineEventPresenters();
            if (_hudMaterialFactory != null)
            {
                _hudMaterialFactory.Dispose();
                _hudMaterialFactory = null;
            }
        }

        private void Update()
        {
            if (string.IsNullOrEmpty(_pendingTargetCardId)) return;
            if (Input.GetKeyDown(KeyCode.Escape) || Input.GetMouseButtonDown(1)) CancelTargetSelection();
        }

        public void BuildNow()
        {
            if (_built) return;
            _built = true;
            _registry = CardContentLoader.Current;
            _match.ResetOpponent(GetOpponentDefinitions(_opponentFaction));
            BuildInterface();
            RegisterOnlineEventPresenters();
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
            _opponentTint = CreateTintBand("OpponentTint", new Vector2(0, 270), new Vector2(ReferenceWidth, 540), new Color(0.16f, 0.035f, 0.025f, 0.10f));
            _playerTint = CreateTintBand("PlayerTint", new Vector2(0, -270), new Vector2(ReferenceWidth, 540), new Color(0.035f, 0.10f, 0.045f, 0.08f));
            CreatePanel(_canvasRoot, "CenterRiverBed", new Vector2(0, 0), new Vector2(1540, 6), new Color(Ink.r, Ink.g, Ink.b, 0.58f)).raycastTarget = false;
            CreatePanel(_canvasRoot, "CenterRiverGlow", new Vector2(0, 0), new Vector2(1540, 2), new Color(Cyan.r, Cyan.g, Cyan.b, 0.56f)).raycastTarget = false;

            BuildTopChrome();
            BuildOnlineStatus();
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
            _titleText = CreateText(titlePlate, "Title", Vector2.zero, new Vector2(480, 44), "群系竞逐  ·  本地战场演示", 24, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);

            var opponentHud = CreateBasePanel(_canvasRoot, "OpponentHUD", new Vector2(-760, 455), new Vector2(315, 104));
            _opponentAvatarImage = CreatePanel(opponentHud, "Avatar", new Vector2(-112, 0), new Vector2(70, 70), Hex("#5B2020"));
            _opponentAvatarGlyph = CreateText(opponentHud, "AvatarGlyph", new Vector2(-112, 1), new Vector2(60, 60), "▣", 36, Ember, TextAnchor.MiddleCenter, FontStyle.Bold);
            _opponentNameText = CreateText(opponentHud, "Name", new Vector2(34, 22), new Vector2(190, 32), "熔岩统御者", 20, Pale, TextAnchor.MiddleLeft, FontStyle.Bold);
            _opponentHealthText = CreateText(opponentHud, "Health", new Vector2(34, -19), new Vector2(190, 30), "❤ 30", 17, Hex("#F4C18A"), TextAnchor.MiddleLeft, FontStyle.Bold);
            var opponentHeroTarget = opponentHud.gameObject.AddComponent<Button>();
            opponentHeroTarget.targetGraphic = opponentHud.GetComponent<Image>();
            opponentHeroTarget.transition = Selectable.Transition.ColorTint;
            opponentHeroTarget.colors = new ColorBlock
            {
                normalColor = Color.white,
                highlightedColor = new Color(1f, 0.82f, 0.65f, 1f),
                pressedColor = new Color(0.82f, 0.55f, 0.42f, 1f),
                selectedColor = Color.white,
                disabledColor = new Color(0.55f, 0.55f, 0.55f, 1f),
                colorMultiplier = 1f,
                fadeDuration = 0.08f
            };
            opponentHeroTarget.onClick.AddListener(AttackOpponentHero);

            var opponentSelector = CreateBasePanel(_canvasRoot, "OpponentFactionSelector", new Vector2(-760, 380), new Vector2(315, 38));
            var previousOpponent = CreateSecondaryButton(opponentSelector, "PreviousOpponentFaction", new Vector2(-128, 0), new Vector2(42, 30), "◀", 15);
            _opponentFactionLabel = CreateText(opponentSelector, "FactionLabel", Vector2.zero, new Vector2(190, 28), "敌方 · 下界", 14, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
            var nextOpponent = CreateSecondaryButton(opponentSelector, "NextOpponentFaction", new Vector2(128, 0), new Vector2(42, 30), "▶", 15);
            previousOpponent.onClick.AddListener(() => CycleOpponentFaction(-1));
            nextOpponent.onClick.AddListener(() => CycleOpponentFaction(1));

            _opponentHandRoot = CreateRect(_canvasRoot, "OpponentHand", new Vector2(0, 410), new Vector2(480, 130));
            RefreshOpponentHand(5);
            BuildPlayerChrome();
        }

        private void RefreshOpponentHand(int count)
        {
            ClearChildren(_opponentHandRoot);
            count = Mathf.Clamp(count, 0, 7);
            for (var i = 0; i < count; i++)
            {
                var center = (count - 1) * 0.5f;
                var x = (i - center) * 72f;
                var back = CreateBasePanel(_opponentHandRoot, "CardBack", new Vector2(x, Mathf.Abs(i - center) * -4f), new Vector2(78, 112));
                back.localRotation = Quaternion.Euler(0, 0, (i - center) * -2.5f);
                CreateText(back, "Rune", Vector2.zero, new Vector2(58, 80), "◇", 32, Ember, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
        }

        private void BuildPlayerChrome()
        {
            _playerHud = CreateBasePanel(_canvasRoot, "PlayerHUD", new Vector2(-782, -454), new Vector2(270, 105));
            _playerAvatarImage = CreatePanel(_playerHud, "Avatar", new Vector2(-92, 0), new Vector2(70, 70), Hex("#274C2D"));
            _playerAvatarGlyph = CreateText(_playerHud, "AvatarGlyph", new Vector2(-92, 1), new Vector2(60, 60), "▦", 34, Hex("#D3C35B"), TextAnchor.MiddleCenter, FontStyle.Bold);
            _playerNameText = CreateText(_playerHud, "Name", new Vector2(35, 22), new Vector2(150, 30), "林地守护者", 18, Pale, TextAnchor.MiddleLeft, FontStyle.Bold);
            _playerHealthText = CreateText(_playerHud, "Health", new Vector2(35, -17), new Vector2(150, 30), "❤ 30", 18, Hex("#B8E5A9"), TextAnchor.MiddleLeft, FontStyle.Bold);
            var effectFlash = CreatePanel(_playerHud, "EffectFlash", Vector2.zero, new Vector2(258, 93), Color.white);
            effectFlash.raycastTarget = false;
            _playerEffectFlash = effectFlash.gameObject.AddComponent<CanvasGroup>();
            _playerEffectFlash.alpha = 0f;
            _playerEffectFlash.blocksRaycasts = false;
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

        private void BuildOnlineStatus()
        {
            var panel = CreateBasePanel(_canvasRoot, "OnlineStatusPanel", new Vector2(410, 472), new Vector2(250, 54));
            _onlineStatusText = CreateText(panel, "Status", new Vector2(-43, 0), new Vector2(138, 36), "本地模式", 13, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            _onlineActionButton = CreateSecondaryButton(panel, "OnlineAction", new Vector2(78, 0), new Vector2(78, 38), "联机", 14);
            _onlineActionLabel = _onlineActionButton.GetComponentInChildren<Text>();
            _onlineActionButton.onClick.AddListener(ToggleOnlineConnection);
        }

        private async void ToggleOnlineConnection()
        {
            try
            {
                if (_onlineGateway != null && _onlineGateway.CurrentStatus.Phase != MatchConnectionPhase.Offline &&
                    _onlineGateway.CurrentStatus.Phase != MatchConnectionPhase.Failed)
                {
                    await _onlineGateway.DisconnectAsync();
                    return;
                }

                var compositionRoot = GameCompositionRoot.Instance;
                if (compositionRoot == null)
                {
                    _onlineStatusText.text = "联机底座未启动";
                    _onlineStatusText.color = Danger;
                    return;
                }
                if (_onlineGateway != null) _onlineGateway.ConnectionStateChanged -= HandleOnlineConnectionState;
                DisposeOnlineSession();
                _onlineGateway = compositionRoot.RegisterDefaultOnlineTransport();
                _onlineSession = new DemoOnlineMatchSession(_onlineGateway, compositionRoot.MatchStateStore);
                _onlineSession.StateChanged += HandleOnlineBoardChanged;
                _onlineSession.CommandPending += HandleOnlineCommandPending;
                _onlineSession.CommandCompleted += HandleOnlineCommandCompleted;
                _onlineGateway.ConnectionStateChanged += HandleOnlineConnectionState;
                HandleOnlineConnectionState(_onlineGateway.CurrentStatus);
                await _onlineGateway.ConnectAsync();
            }
            catch (Exception exception)
            {
                if (_onlineStatusText != null)
                {
                    _onlineStatusText.text = "连接失败";
                    _onlineStatusText.color = Danger;
                }
                Debug.LogWarning("Online connection failed: " + exception.Message, this);
            }
        }

        private void HandleOnlineConnectionState(MatchConnectionStatus status)
        {
            if (_onlineStatusText == null || _onlineActionLabel == null) return;
            switch (status.Phase)
            {
                case MatchConnectionPhase.Authenticating:
                    _onlineStatusText.text = "身份认证中";
                    break;
                case MatchConnectionPhase.Connecting:
                    _onlineStatusText.text = "连接服务器";
                    break;
                case MatchConnectionPhase.Matchmaking:
                    _onlineStatusText.text = "寻找对手中";
                    break;
                case MatchConnectionPhase.Joining:
                    _onlineStatusText.text = "进入权威对局";
                    break;
                case MatchConnectionPhase.Ready:
                    _onlineStatusText.text = "权威对局已连接";
                    break;
                case MatchConnectionPhase.Reconnecting:
                    _onlineStatusText.text = $"正在重连 · 第 {status.Attempt} 次";
                    break;
                case MatchConnectionPhase.Failed:
                    _onlineStatusText.text = "连接失败";
                    break;
                case MatchConnectionPhase.Disconnecting:
                    _onlineStatusText.text = "正在断开";
                    break;
                default:
                    _onlineStatusText.text = "本地模式";
                    break;
            }
            var ready = status.Phase == MatchConnectionPhase.Ready;
            var idle = status.Phase == MatchConnectionPhase.Offline || status.Phase == MatchConnectionPhase.Failed;
            _onlineActionLabel.text = ready ? "断开" : idle ? "联机" : "取消";
            _onlineStatusText.color = status.Phase == MatchConnectionPhase.Failed ? Danger : ready ? Cyan : Muted;
            RefreshAll();
        }

        private void DisposeOnlineSession()
        {
            if (_onlineSession == null) return;
            _onlineSession.StateChanged -= HandleOnlineBoardChanged;
            _onlineSession.CommandPending -= HandleOnlineCommandPending;
            _onlineSession.CommandCompleted -= HandleOnlineCommandCompleted;
            _onlineSession.Dispose();
            _onlineSession = null;
        }

        private void HandleOnlineBoardChanged()
        {
            if (IsOnlineBoard)
            {
                ApplyAuthoritativeFactionVisuals();
                if (!MatchView.Hand.Contains(_selectedCardId)) _selectedCardId = MatchView.Hand.FirstOrDefault();
                if (FindSelectedAttacker() == null) _selectedAttackerInstanceId = null;
            }
            RefreshAll();
        }

        private void HandleOnlineCommandPending(string commandId)
        {
            ShowStatus("命令已发送，等待服务器确认…", false);
            RefreshAll();
        }

        private void HandleOnlineCommandCompleted(MatchCommandDispatchResult result)
        {
            if (result.Outcome == MatchCommandOutcome.Accepted)
            {
                ShowStatus($"服务器已确认 · 状态 r{result.Revision}", false);
            }
            else
            {
                var detail = string.IsNullOrEmpty(result.Message) ? result.Code : $"{result.Code} · {result.Message}";
                ShowStatus($"命令未生效：{detail}", true);
            }
            RefreshAll();
        }

        private void RegisterOnlineEventPresenters()
        {
            var queue = GameCompositionRoot.Instance?.PresentationQueue;
            if (queue == null || _eventPresenters.Count > 0) return;
            var eventTypes = new[]
            {
                MatchEventTypes.CardDeployed, MatchEventTypes.CardPlayed, MatchEventTypes.CardDrawn,
                MatchEventTypes.CardBurned, MatchEventTypes.FatigueDamage, MatchEventTypes.HeroDamaged,
                MatchEventTypes.HeroHealed, MatchEventTypes.ArmorGained, MatchEventTypes.ObjectStatsChanged,
                MatchEventTypes.PhaseChanged, MatchEventTypes.AttackResolved, MatchEventTypes.ObjectDied,
                MatchEventTypes.TurnEnded, MatchEventTypes.TurnStarted, MatchEventTypes.PlayerConceded,
                MatchEventTypes.MatchEnded
            };
            foreach (var eventType in eventTypes)
            {
                var presenter = new DemoMatchEventPresenter(eventType, PresentOnlineEvent);
                queue.Registry.Register(presenter);
                _eventPresenters.Add(presenter);
            }
        }

        private void UnregisterOnlineEventPresenters()
        {
            var queue = GameCompositionRoot.Instance?.PresentationQueue;
            if (queue != null)
                foreach (var presenter in _eventPresenters) queue.Registry.Unregister(presenter);
            _eventPresenters.Clear();
        }

        private IEnumerator PresentOnlineEvent(MatchEventDto matchEvent)
        {
            if (!IsOnlineBoard || matchEvent == null) yield break;
            switch (matchEvent.type)
            {
                case MatchEventTypes.PhaseChanged:
                    yield return ShowTurnBanner("进入战斗阶段", Cyan);
                    break;
                case MatchEventTypes.TurnStarted:
                    var viewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var ownTurn = matchEvent.payload?.playerId == viewerId;
                    yield return ShowTurnBanner(ownTurn ? "你的回合" : "对手回合", ownTurn ? Cyan : Ember);
                    break;
                case MatchEventTypes.MatchEnded:
                    var winnerId = matchEvent.payload?.winnerPlayerId;
                    var playerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    yield return ShowTurnBanner(winnerId == playerId ? "胜利" : "战败", winnerId == playerId ? Cyan : Danger);
                    break;
                case MatchEventTypes.HeroDamaged:
                case MatchEventTypes.FatigueDamage:
                    if (matchEvent.payload?.playerId == GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId)
                        yield return PulsePlayerHud(Danger);
                    break;
                case MatchEventTypes.HeroHealed:
                case MatchEventTypes.ArmorGained:
                    if (matchEvent.payload?.playerId == GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId)
                        yield return PulsePlayerHud(Cyan);
                    break;
                default:
                    yield return null;
                    break;
            }
        }

        private void BuildSlots()
        {
            for (var i = 0; i < 3; i++)
                _opponentBuildingSlots.Add(CreateOpponentSlot(DemoSlotKind.Building, i, _battlefield.GetSlotReferencePosition(false, DemoSlotKind.Building, i), new Vector2(190, 92)));
            for (var i = 0; i < 4; i++)
                _opponentUnitSlots.Add(CreateOpponentSlot(DemoSlotKind.Unit, i, _battlefield.GetSlotReferencePosition(false, DemoSlotKind.Unit, i), new Vector2(150, 118)));

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

        private SlotView CreateOpponentSlot(DemoSlotKind kind, int index, Vector2 position, Vector2 size)
        {
            var root = CreateRect(_canvasRoot, $"Opponent{kind}Slot{index}", position, size);
            var content = CreateRect(root, "Content", Vector2.zero, size - new Vector2(10, 10));
            var empty = CreateText(content, "EmptyLabel", Vector2.zero, size - new Vector2(16, 16), string.Empty, 12, Color.clear, TextAnchor.MiddleCenter, FontStyle.Bold);
            return new SlotView(kind, index, content, empty);
        }

        private void BuildHandArea()
        {
            var handPlate = CreateBasePanel(_canvasRoot, "HandPlate", new Vector2(35, -418), new Vector2(1360, 232));
            _handRoot = CreateRect(handPlate, "HandCards", new Vector2(-20, 14), new Vector2(1250, 225));
            _handCanvasGroup = _handRoot.gameObject.AddComponent<CanvasGroup>();
            _handLabel = CreateText(_canvasRoot, "HandLabel", new Vector2(-484, -508), new Vector2(330, 25), "手牌 5/7 · 牌库 25 · 弃牌 0", 13, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
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

            var roundPlate = CreateBasePanel(_canvasRoot, "RoundPlate", new Vector2(675, 472), new Vector2(230, 54));
            _roundText = CreateText(roundPlate, "Round", Vector2.zero, new Vector2(208, 38), "第 1 回合 · 主行动", 17, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);

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
            if (IsOnlineBoard)
            {
                ShowStatus("在线对局的己方牌组由权威房间决定。", true);
                return;
            }
            _pendingTargetCardId = null;
            _activeFaction = factionId;
            var spec = Factions.First(item => item.Id == factionId);
            ApplyPlayerFactionVisuals(spec);
            var handNumbers = spec.Prefix == "nt" ? new[] { 1, 2, 3, 4, 6 } : Enumerable.Range(1, 5).ToArray();
            var ids = handNumbers.Select(index => $"{spec.Prefix}_{index:000}").ToArray();
            var deck = Enumerable.Range(0, 25).Select(index => $"{spec.Prefix}_{(index % 8) + 1:000}").ToArray();
            _match.ResetDeckAndHand(ids, deck);
            _selectedCardId = _match.Hand.FirstOrDefault();
            _battlefield.SetBattlefieldThemes(_activeFaction, _opponentFaction);
            RefreshAll();
            ShowStatus($"已切换到{spec.Label}牌组；可打出已接入规则的卡牌。", false);
        }

        private void ApplyPlayerFactionVisuals(FactionSpec spec)
        {
            _activeFaction = spec.Id;
            if (_registry.TryGetTheme(spec.Id, out var selectedTheme))
            {
                _playerAvatarImage.color = Color.Lerp(selectedTheme.FrameDark, selectedTheme.Accent, 0.24f);
                _playerAvatarGlyph.color = selectedTheme.Accent;
            }
            var battlefieldTheme = DemoBattlefieldThemeCatalog.Get(spec.Id);
            _playerTint.color = new Color(battlefieldTheme.UiTint.r, battlefieldTheme.UiTint.g, battlefieldTheme.UiTint.b, 0.075f);
            _playerNameText.text = spec.PlayerTitle;
        }

        private void CycleOpponentFaction(int offset)
        {
            var current = Array.FindIndex(Factions, item => item.Id == _opponentFaction);
            var next = (current + offset + Factions.Length) % Factions.Length;
            SelectOpponentFaction(Factions[next].Id);
        }

        private void SelectOpponentFaction(string factionId)
        {
            if (IsOnlineBoard)
            {
                ShowStatus("在线对局的敌方阵营由权威房间决定。", true);
                return;
            }
            var spec = Factions.First(item => item.Id == factionId);
            _opponentFaction = factionId;
            _pendingTargetCardId = null;
            _selectedAttackerInstanceId = null;
            _match.ResetOpponent(GetOpponentDefinitions(factionId));
            ApplyOpponentFactionVisuals(spec);
            _battlefield.SetBattlefieldThemes(_activeFaction, _opponentFaction);
            RefreshAll();
            ShowStatus($"敌方阵营已切换为{spec.Label}；远端半场与生物同步更新。", false);
        }

        private void ApplyOpponentFactionVisuals(FactionSpec spec)
        {
            _opponentFaction = spec.Id;
            if (_registry.TryGetTheme(spec.Id, out var selectedTheme))
            {
                _opponentAvatarImage.color = Color.Lerp(selectedTheme.FrameDark, selectedTheme.Accent, 0.24f);
                _opponentAvatarGlyph.color = selectedTheme.Accent;
            }
            var battlefieldTheme = DemoBattlefieldThemeCatalog.Get(spec.Id);
            _opponentTint.color = new Color(battlefieldTheme.UiTint.r, battlefieldTheme.UiTint.g, battlefieldTheme.UiTint.b, 0.085f);
            _opponentNameText.text = spec.PlayerTitle;
            _opponentFactionLabel.text = "敌方 · " + spec.Label;
        }

        private void ApplyAuthoritativeFactionVisuals()
        {
            var playerFaction = DetectFaction(MatchView.Hand.Concat(MatchView.PlayerBattlefield.Select(value => value.CardId)));
            if (playerFaction == null) playerFaction = MatchView.ViewerIndex == 0 ? Factions[0] : Factions[5];
            var opponentFaction = DetectFaction(MatchView.OpponentBattlefield.Select(value => value.CardId));
            if (opponentFaction == null) opponentFaction = MatchView.ViewerIndex == 0 ? Factions[5] : Factions[0];
            ApplyPlayerFactionVisuals(playerFaction);
            ApplyOpponentFactionVisuals(opponentFaction);
            _battlefield.SetBattlefieldThemes(_activeFaction, _opponentFaction);
        }

        private static FactionSpec DetectFaction(IEnumerable<string> cardIds)
        {
            foreach (var cardId in cardIds ?? Enumerable.Empty<string>())
            {
                if (string.IsNullOrEmpty(cardId)) continue;
                var separator = cardId.IndexOf('_');
                var prefix = separator > 0 ? cardId.Substring(0, separator) : cardId;
                var faction = Factions.FirstOrDefault(value => value.Prefix == prefix);
                if (faction != null) return faction;
            }
            return null;
        }

        private IEnumerable<CardDefinitionEntry> GetOpponentDefinitions(string factionId)
        {
            var spec = Factions.First(item => item.Id == factionId);
            var units = new List<CardDefinitionEntry>();
            CardDefinitionEntry building = null;
            for (var index = 1; index <= 8; index++)
            {
                if (!_registry.TryGetDefinition($"{spec.Prefix}_{index:000}", out var definition)) continue;
                if (definition.cardType == "UNIT" && units.Count < 2) units.Add(definition);
                else if (building == null && (definition.cardType == "BUILDING" || definition.cardType == "STRUCTURE")) building = definition;
            }
            if (building != null) units.Add(building);
            return units;
        }

        private void RefreshAll()
        {
            if (!_built || _battlefield == null) return;
            var match = MatchView;
            RefreshFactionButtons();
            RefreshHand();
            RefreshOpponentHand(match.OpponentHandCount);
            RefreshBattlefieldSlots();
            _battlefield.SyncPieces(match.UnitSlots, match.BuildingSlots, match.OpponentUnitSlots, match.OpponentBuildingSlots, _registry);
            RefreshInspector();
            _energyText.text = $"◆ {match.Energy}/{match.MaxEnergy}";
            _titleText.text = IsOnlineBoard ? "群系竞逐  ·  权威联机对局" : "群系竞逐  ·  本地战场演示";
            _roundText.text = $"第 {match.Round} 回合 · {(match.Phase == DemoTurnPhase.Main ? "主行动" : "战斗")}";
            _opponentHealthText.text = $"❤ {match.OpponentLife}";
            _playerHealthText.text = match.PlayerArmor > 0 ? $"❤ {match.PlayerLife}  ◈ {match.PlayerArmor}" : $"❤ {match.PlayerLife}";
            _handLabel.text = $"手牌 {match.Hand.Count}/7 · 牌库 {match.DeckCount} · 弃牌 {match.DiscardCount}";
            var canUseHand = match.Phase == DemoTurnPhase.Main && match.IsPlayerTurn && (!IsOnlineBoard || _onlineSession.CanIssueCommand);
            _handCanvasGroup.alpha = canUseHand ? 1f : 0.52f;
            _handCanvasGroup.interactable = canUseHand;
            _handCanvasGroup.blocksRaycasts = canUseHand;
            _endTurnButton.interactable = match.IsPlayerTurn && !match.IsFinished && (!IsOnlineBoard || _onlineSession.CanIssueCommand);
            _endTurnLabel.text = IsOnlineBoard && _onlineSession.HasPendingCommand
                ? "等待服务器"
                : !match.IsPlayerTurn ? "对手行动中" : match.IsFinished ? "对局结束" : match.Phase == DemoTurnPhase.Main ? "进入战斗" : "结束回合";
        }

        private void RefreshFactionButtons()
        {
            foreach (var view in _factionButtons)
            {
                var active = view.Id == _activeFaction;
                view.Image.color = DemoUiStyleCatalog.GetRootFill(DemoUiStyleClass.SecondaryButton);
                view.SelectionAccent.gameObject.SetActive(active);
                view.Button.interactable = !IsOnlineBoard;
            }
        }

        private void RefreshHand()
        {
            ClearChildren(_handRoot);
            var match = MatchView;
            var count = match.Hand.Count;
            for (var i = 0; i < count; i++)
            {
                var cardId = match.Hand[i];
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

        private void RefreshBattlefieldSlots()
        {
            var match = MatchView;
            foreach (var view in _playerUnitSlots) RefreshSlot(true, view, match.UnitSlots[view.Index]);
            foreach (var view in _playerBuildingSlots) RefreshSlot(true, view, match.BuildingSlots[view.Index]);
            foreach (var view in _opponentUnitSlots) RefreshSlot(false, view, match.OpponentUnitSlots[view.Index]);
            foreach (var view in _opponentBuildingSlots) RefreshSlot(false, view, match.OpponentBuildingSlots[view.Index]);
        }

        private void RefreshSlot(bool player, SlotView view, string cardId)
        {
            ClearChildrenExcept(view.Content, view.EmptyLabel.gameObject);
            var empty = string.IsNullOrEmpty(cardId);
            view.EmptyLabel.gameObject.SetActive(empty);
            var match = MatchView;
            var battlefieldObject = match.GetObject(player, view.Kind, view.Index);
            var selectingCardTarget = !string.IsNullOrEmpty(_pendingTargetCardId);
            var canInteract = !IsOnlineBoard || _onlineSession.CanIssueCommand;
            var valid = canInteract && (selectingCardTarget
                ? !player && view.Kind == DemoSlotKind.Unit && !empty
                : player
                    ? match.Phase == DemoTurnPhase.Main
                        ? empty && IsSelectedValidFor(view.Kind)
                        : !empty && view.Kind == DemoSlotKind.Unit && match.CanAttackWith(battlefieldObject, out _)
                    : match.Phase == DemoTurnPhase.Combat && !string.IsNullOrEmpty(_selectedAttackerInstanceId) && !empty);
            view.EmptyLabel.color = Color.clear;
            _battlefield.SetSlotState(player, view.Kind, view.Index, valid, !empty);

            if (!empty)
            {
                var slots = player
                    ? view.Kind == DemoSlotKind.Unit ? match.UnitSlots : match.BuildingSlots
                    : view.Kind == DemoSlotKind.Unit ? match.OpponentUnitSlots : match.OpponentBuildingSlots;
                if (view.Kind == DemoSlotKind.Building && view.Index > 0 && slots[view.Index - 1] == cardId)
                    CreateText(view.Content, "Occupied", Vector2.zero, view.Content.sizeDelta, "结构占用", 14, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
                else
                    CreateWorldPieceLabel(view.Content, cardId, view.Content.sizeDelta, !player, battlefieldObject);
            }
        }

        private void RefreshInspector()
        {
            var match = MatchView;
            ClearChildren(_inspectorRoot);
            CreateText(_inspectorRoot, "Header", new Vector2(0, 315), new Vector2(250, 38), match.Phase == DemoTurnPhase.Main ? "卡牌详情" : "战斗指令", 20, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (match.Phase == DemoTurnPhase.Combat)
            {
                _cardDetailsView.Clear();
                var attacker = FindSelectedAttacker();
                var title = attacker == null ? "选择一个发光的己方生物" : $"攻击者：{GetCardName(attacker.CardId)}\n{attacker.Attack}/{attacker.Health}";
                CreateText(_inspectorRoot, "CombatTitle", new Vector2(0, 100), new Vector2(245, 130), title, 19, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
                CreateText(_inspectorRoot, "CombatHint", new Vector2(0, -25), new Vector2(245, 120), "再点击敌方生物、建筑，\n或左上角敌方英雄面板。\n单位会同步反击，建筑不会反击。", 15, Pale, TextAnchor.MiddleCenter, FontStyle.Normal);
                return;
            }
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
                var implemented = definition.effectImplementationStatus == "IMPLEMENTED";
                var targeting = _pendingTargetCardId == definition.id;
                var requiresTarget = RequiresEnemyUnitTarget(definition);
                var hasLegalTarget = !requiresTarget || HasEnemyUnitTarget();
                var actionLabel = !implemented ? "效果尚未接入" : targeting ? "取消目标选择" : !hasLegalTarget ? "没有合法目标" : requiresTarget ? "选择敌方目标" : "释放卡牌";
                var cast = CreateSecondaryButton(_inspectorRoot, "Cast", new Vector2(0, -118), new Vector2(235, 60), actionLabel, 17);
                var canPlay = match.IsPlayerTurn && match.Phase == DemoTurnPhase.Main && match.Hand.Contains(definition.id) && definition.cost <= match.Energy;
                cast.interactable = targeting || (implemented && hasLegalTarget && canPlay && (!IsOnlineBoard || _onlineSession.CanIssueCommand));
                cast.onClick.AddListener(targeting ? (UnityEngine.Events.UnityAction)CancelTargetSelection : CastSelectedCard);
                cast.gameObject.AddComponent<DemoHoverScale>().Configure(1.04f, 16f);
            }
            var implementationLabel = definition.effectImplementationStatus == "IMPLEMENTED" ? "已接入" : definition.effectImplementationStatus == "NONE" ? "无额外效果" : "待接入";
            CreateText(_inspectorRoot, "Implementation", new Vector2(0, -190), new Vector2(250, 62), $"规则状态：{implementationLabel}\n稳定效果槽：{string.Join(", ", definition.effectIds ?? Array.Empty<string>())}", 12, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        private bool IsSelectedValidFor(DemoSlotKind kind)
        {
            if (string.IsNullOrEmpty(_selectedCardId) || !_registry.TryGetDefinition(_selectedCardId, out var definition)) return false;
            var match = MatchView;
            if (!match.IsPlayerTurn || match.Phase != DemoTurnPhase.Main || definition.cost > match.Energy || !match.Hand.Contains(definition.id)) return false;
            return kind == DemoSlotKind.Unit
                ? definition.cardType == "UNIT"
                : definition.cardType == "BUILDING" || definition.cardType == "STRUCTURE";
        }

        private void SelectCard(string cardId)
        {
            _pendingTargetCardId = null;
            _selectedCardId = cardId;
            RefreshAll();
            if (_registry.TryGetText(cardId, out var text)) ShowStatus($"已选择：{text.name}", false);
        }

        private async void OnSlotClicked(bool player, DemoSlotKind kind, int index)
        {
            if (!string.IsNullOrEmpty(_pendingTargetCardId))
            {
                ResolveTargetedCard(player, kind, index);
                return;
            }
            if (MatchView.Phase == DemoTurnPhase.Combat)
            {
                HandleCombatSlotClick(player, kind, index);
                return;
            }
            if (!player)
            {
                ShowStatus("主行动阶段不能攻击；请点击右下角进入战斗。", true);
                return;
            }
            if (string.IsNullOrEmpty(_selectedCardId) || !_registry.TryGetDefinition(_selectedCardId, out var definition))
            {
                ShowStatus("请先选择一张手牌。", true);
                return;
            }
            if (IsOnlineBoard)
            {
                var onlineResult = await SendOnline(() => _onlineSession.DeployAsync(_selectedCardId, kind, index));
                if (onlineResult?.Outcome == MatchCommandOutcome.Accepted) _selectedCardId = MatchView.Hand.FirstOrDefault();
                RefreshAll();
                return;
            }
            var command = _match.CreateDeployCommand(_selectedCardId, kind, index);
            var result = _match.ApplyDeploy(definition, command);
            if (result.Accepted) _selectedCardId = MatchView.Hand.FirstOrDefault();
            ShowStatus(result.Accepted ? $"{result.Message} · 状态 r{result.Revision}" : result.Message, !result.Accepted);
            RefreshAll();
        }

        private void HandleCombatSlotClick(bool player, DemoSlotKind kind, int index)
        {
            var match = MatchView;
            if (IsOnlineBoard && !_onlineSession.CanIssueCommand)
            {
                ShowStatus("请等待服务器确认上一条命令。", true);
                return;
            }
            if (player)
            {
                var attacker = match.GetObject(true, kind, index);
                if (!match.CanAttackWith(attacker, out var message))
                {
                    ShowStatus(message, true);
                    return;
                }
                ClearSelectedAttackerHighlight();
                _selectedAttackerInstanceId = attacker.InstanceId;
                _battlefield.SetSlotPressed(true, DemoSlotKind.Unit, attacker.SlotIndex, true);
                ShowStatus($"已选择攻击者：{GetCardName(attacker.CardId)}（{attacker.Attack}/{attacker.Health}）", false);
                RefreshAll();
                return;
            }

            var selected = FindSelectedAttacker();
            if (selected == null)
            {
                ShowStatus("请先选择一个发光的己方生物。", true);
                return;
            }
            var target = match.GetObject(false, kind, index);
            if (target == null)
            {
                ShowStatus("该敌方格为空。", true);
                return;
            }
            ResolveAttack(selected, kind == DemoSlotKind.Unit ? "UNIT" : "BUILDING", target.InstanceId);
        }

        private void AttackOpponentHero()
        {
            if (MatchView.Phase != DemoTurnPhase.Combat) return;
            var selected = FindSelectedAttacker();
            if (selected == null)
            {
                ShowStatus("请先选择一个发光的己方生物。", true);
                return;
            }
            ResolveAttack(selected, "HERO", string.Empty);
        }

        private async void ResolveAttack(DemoBattlefieldObject attacker, string targetType, string targetInstanceId)
        {
            if (IsOnlineBoard)
            {
                var onlineResult = await SendOnline(() => _onlineSession.AttackAsync(attacker.InstanceId, targetType, targetInstanceId));
                if (onlineResult?.Outcome == MatchCommandOutcome.Accepted)
                {
                    _battlefield.SetSlotPressed(true, DemoSlotKind.Unit, attacker.SlotIndex, false);
                    _selectedAttackerInstanceId = null;
                }
                RefreshAll();
                return;
            }
            var command = _match.CreateAttackCommand(attacker.InstanceId, targetType, targetInstanceId);
            var result = _match.ApplyAttack(command);
            if (result.Accepted)
            {
                _battlefield.SetSlotPressed(true, DemoSlotKind.Unit, attacker.SlotIndex, false);
                _selectedAttackerInstanceId = null;
            }
            ShowStatus(result.Accepted ? $"{result.Message} · 状态 r{result.Revision}" : result.Message, !result.Accepted);
            RefreshAll();
        }

        private DemoBattlefieldObject FindSelectedAttacker() =>
            string.IsNullOrEmpty(_selectedAttackerInstanceId)
                ? null
                : MatchView.PlayerBattlefield.FirstOrDefault(value => value.InstanceId == _selectedAttackerInstanceId);

        private void ClearSelectedAttackerHighlight()
        {
            var selected = FindSelectedAttacker();
            if (selected != null) _battlefield.SetSlotPressed(true, DemoSlotKind.Unit, selected.SlotIndex, false);
        }

        private string GetCardName(string cardId) =>
            _registry.TryGetText(cardId, out var text) ? text.name : cardId;

        private async void CastSelectedCard()
        {
            if (string.IsNullOrEmpty(_selectedCardId) || !_registry.TryGetDefinition(_selectedCardId, out var definition)) return;
            if (RequiresEnemyUnitTarget(definition))
            {
                if (!HasEnemyUnitTarget())
                {
                    ShowStatus("当前没有可选择的敌方生物。", true);
                    return;
                }
                _pendingTargetCardId = definition.id;
                ShowStatus("请选择一个发光的敌方生物；右键或 Esc 取消。", false);
                RefreshAll();
                return;
            }
            if (IsOnlineBoard)
            {
                var onlineResult = await SendOnline(() => _onlineSession.PlayCardAsync(definition.id));
                if (onlineResult?.Outcome == MatchCommandOutcome.Accepted) _selectedCardId = MatchView.Hand.FirstOrDefault();
                RefreshAll();
                return;
            }
            var lifeBefore = MatchView.PlayerLife;
            var armorBefore = MatchView.PlayerArmor;
            var success = _match.TryCast(definition, out var message);
            if (success && _match.LastDrawResult != null && !string.IsNullOrEmpty(_match.LastDrawResult.CardId))
                message = message.Replace(_match.LastDrawResult.CardId, GetCardName(_match.LastDrawResult.CardId));
            if (success) _selectedCardId = _match.Hand.FirstOrDefault();
            ShowStatus(message, !success);
            RefreshAll();
            if (success)
            {
                var color = MatchView.PlayerArmor > armorBefore ? Cyan : MatchView.PlayerLife < lifeBefore ? Danger : Hex("#B8E5A9");
                StartCoroutine(PulsePlayerHud(color));
            }
        }

        private async void ResolveTargetedCard(bool player, DemoSlotKind kind, int index)
        {
            if (player || kind != DemoSlotKind.Unit)
            {
                ShowStatus("该卡牌只能选择发光的敌方生物。", true);
                return;
            }
            var target = MatchView.GetObject(false, kind, index);
            if (target == null || !_registry.TryGetDefinition(_pendingTargetCardId, out var definition))
            {
                ShowStatus("目标已经离场，请重新选择。", true);
                return;
            }
            if (IsOnlineBoard)
            {
                var onlineResult = await SendOnline(() => _onlineSession.PlayCardAsync(definition.id, "UNIT", target.InstanceId));
                if (onlineResult?.Outcome == MatchCommandOutcome.Accepted)
                {
                    _pendingTargetCardId = null;
                    _selectedCardId = MatchView.Hand.FirstOrDefault();
                }
                RefreshAll();
                return;
            }
            var command = _match.CreatePlayCardCommand(definition.id, "UNIT", target.InstanceId);
            var result = _match.ApplyPlayCard(definition, command);
            if (result.Accepted)
            {
                _pendingTargetCardId = null;
                _selectedCardId = _match.Hand.FirstOrDefault();
            }
            var message = result.Message.Replace(target.CardId, GetCardName(target.CardId));
            ShowStatus(result.Accepted ? $"{message} · 状态 r{result.Revision}" : message, !result.Accepted);
            RefreshAll();
        }

        private void CancelTargetSelection()
        {
            if (string.IsNullOrEmpty(_pendingTargetCardId)) return;
            _pendingTargetCardId = null;
            ShowStatus("已取消目标选择。", false);
            RefreshAll();
        }

        private static bool RequiresEnemyUnitTarget(CardDefinitionEntry definition) =>
            definition != null && definition.effectIds != null && definition.effectIds.Contains("effect.si_001.01");

        private bool HasEnemyUnitTarget() =>
            MatchView.OpponentBattlefield.Any(value => value != null && value.SlotKind == DemoSlotKind.Unit && value.Health > 0);

        private async Task<MatchCommandDispatchResult?> SendOnline(Func<Task<MatchCommandDispatchResult>> send)
        {
            try
            {
                if (_onlineSession == null || !_onlineSession.CanIssueCommand)
                {
                    ShowStatus("权威对局尚未就绪，或上一条命令仍在等待确认。", true);
                    return null;
                }
                return await send();
            }
            catch (Exception exception)
            {
                ShowStatus("联机命令失败：" + exception.Message, true);
                Debug.LogWarning("Online command failed: " + exception, this);
                RefreshAll();
                return null;
            }
        }

        private async void RunOnlineProbe(string reportPath, string capturePath, bool performAction)
        {
            try
            {
                var deadline = Time.realtimeSinceStartup + 45f;
                while ((!IsOnlineBoard || _onlineGateway.CurrentStatus.Phase != MatchConnectionPhase.Ready) && Time.realtimeSinceStartup < deadline)
                    await Task.Yield();
                if (!IsOnlineBoard || _onlineGateway.CurrentStatus.Phase != MatchConnectionPhase.Ready)
                    throw new TimeoutException("Unity client did not receive an authoritative snapshot within 45 seconds.");

                if (performAction && MatchView.Revision == 0 && MatchView.IsPlayerTurn && MatchView.Phase == DemoTurnPhase.Main)
                {
                    var combatResult = await SendOnline(() => _onlineSession.EnterCombatAsync());
                    if (combatResult?.Outcome != MatchCommandOutcome.Accepted)
                        throw new InvalidOperationException("The active Unity client did not receive an accepted ENTER_COMBAT acknowledgement.");
                    var turnResult = await SendOnline(() => _onlineSession.EndTurnAsync());
                    if (turnResult?.Outcome != MatchCommandOutcome.Accepted)
                        throw new InvalidOperationException("The active Unity client did not receive an accepted END_TURN acknowledgement.");
                }
                if (performAction)
                {
                    while (MatchView.Revision < 2 && Time.realtimeSinceStartup < deadline) await Task.Yield();
                    if (MatchView.Revision < 2)
                        throw new TimeoutException("The Unity clients did not observe the authoritative action revision.");
                    var presentationQueue = GameCompositionRoot.Instance?.PresentationQueue;
                    while (presentationQueue != null && presentationQueue.IsPlaying && Time.realtimeSinceStartup < deadline) await Task.Yield();
                }

                if (!string.IsNullOrWhiteSpace(reportPath))
                {
                    var directory = Path.GetDirectoryName(reportPath);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                    File.WriteAllText(reportPath, JsonUtility.ToJson(new OnlineProbeReport
                    {
                        ok = true,
                        matchId = GameCompositionRoot.Instance.MatchStateStore.Current.matchId,
                        viewerPlayerId = GameCompositionRoot.Instance.MatchStateStore.Current.viewerPlayerId,
                        revision = MatchView.Revision,
                        phase = MatchView.Phase == DemoTurnPhase.Main ? "MAIN" : "COMBAT",
                        isPlayerTurn = MatchView.IsPlayerTurn,
                        hand = MatchView.Hand.ToArray(),
                        energy = MatchView.Energy,
                        playerLife = MatchView.PlayerLife,
                        opponentLife = MatchView.OpponentLife,
                        playerFaction = _activeFaction,
                        opponentFaction = _opponentFaction
                    }, true));
                }

                if (!string.IsNullOrWhiteSpace(capturePath)) StartCoroutine(CaptureDemo(capturePath));
                else if (HasCommandLineFlag("-quitAfterOnlineProbe")) Application.Quit(0);
            }
            catch (Exception exception)
            {
                Debug.LogError("Online probe failed: " + exception, this);
                if (!string.IsNullOrWhiteSpace(reportPath))
                {
                    var directory = Path.GetDirectoryName(reportPath);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                    File.WriteAllText(reportPath, JsonUtility.ToJson(new OnlineProbeReport { ok = false, error = exception.Message }, true));
                }
                Application.Quit(1);
            }
        }

        private IEnumerator PulsePlayerHud(Color color)
        {
            if (_playerEffectFlash == null || _playerHud == null) yield break;
            _playerEffectFlash.GetComponent<Image>().color = new Color(color.r, color.g, color.b, 0.55f);
            for (var time = 0f; time < 0.14f; time += Time.unscaledDeltaTime)
            {
                var progress = Mathf.Clamp01(time / 0.14f);
                _playerEffectFlash.alpha = progress;
                _playerHud.localScale = Vector3.one * Mathf.Lerp(1f, 1.035f, progress);
                yield return null;
            }
            for (var time = 0f; time < 0.28f; time += Time.unscaledDeltaTime)
            {
                var progress = Mathf.Clamp01(time / 0.28f);
                _playerEffectFlash.alpha = 1f - progress;
                _playerHud.localScale = Vector3.one * Mathf.Lerp(1.035f, 1f, progress);
                yield return null;
            }
            _playerEffectFlash.alpha = 0f;
            _playerHud.localScale = Vector3.one;
        }

        private async void OnEndTurn()
        {
            var match = MatchView;
            if (!match.IsPlayerTurn) return;
            _pendingTargetCardId = null;
            if (IsOnlineBoard)
            {
                var onlineResult = match.Phase == DemoTurnPhase.Main
                    ? await SendOnline(() => _onlineSession.EnterCombatAsync())
                    : await SendOnline(() => _onlineSession.EndTurnAsync());
                if (onlineResult?.Outcome == MatchCommandOutcome.Accepted)
                {
                    ClearSelectedAttackerHighlight();
                    _selectedAttackerInstanceId = null;
                }
                RefreshAll();
                return;
            }
            if (match.Phase == DemoTurnPhase.Main)
            {
                var result = _match.ApplyEnterCombat(_match.CreateEnterCombatCommand());
                ClearSelectedAttackerHighlight();
                _selectedAttackerInstanceId = null;
                ShowStatus(result.Message, !result.Accepted);
                RefreshAll();
                return;
            }
            _match.EndPlayerTurn();
            ClearSelectedAttackerHighlight();
            _selectedAttackerInstanceId = null;
            RefreshAll();
            StartCoroutine(SimulateOpponentTurn());
        }

        private IEnumerator SimulateOpponentTurn()
        {
            yield return ShowTurnBanner("对手回合", Ember);
            yield return new WaitForSecondsRealtime(0.55f);
            var draw = _match.BeginNextPlayerTurn();
            RefreshAll();
            if (draw.Outcome == DemoDrawOutcome.Drawn)
                ShowStatus($"新回合：能量已补满，抽到 {GetCardName(draw.CardId)}。", false);
            else if (draw.Outcome == DemoDrawOutcome.Burned)
                ShowStatus($"手牌已满，{GetCardName(draw.CardId)} 被公开并置入弃牌堆。", true);
            else
                ShowStatus($"牌库为空，受到 {draw.FatigueDamage} 点疲劳伤害。", true);
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
            if (HasCommandLineFlag("-previewEffect")) yield return new WaitForSecondsRealtime(0.1f);
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

        private void CreateWorldPieceLabel(Transform parent, string cardId, Vector2 size, bool enemy, DemoBattlefieldObject battlefieldObject = null)
        {
            if (!_registry.TryGetDefinition(cardId, out var definition) || !_registry.TryGetText(cardId, out var text)) return;
            _registry.TryGetTheme(definition.themeId, out var theme);
            var accent = enemy ? Ember : theme.Accent;
            var attack = battlefieldObject?.Attack ?? definition.attack;
            var health = battlefieldObject?.Health ?? definition.health;
            var stats = definition.hasAttack && definition.hasHealth
                ? $"{text.name}   {attack}/{health}"
                : definition.hasHealth
                    ? $"{text.name}   ❤ {health}"
                    : text.name;
            var labelY = -size.y * 0.34f;
            var plate = CreatePanel(parent, "WorldLabel", new Vector2(0, labelY), new Vector2(size.x - 8, 30), new Color(Ink.r, Ink.g, Ink.b, 0.84f));
            plate.raycastTarget = false;
            CreatePanel(parent, "WorldLabelAccent", new Vector2(0, labelY + 14), new Vector2(size.x - 8, 2), new Color(accent.r, accent.g, accent.b, 0.82f)).raycastTarget = false;
            CreateText(parent, "WorldLabelText", new Vector2(0, labelY), new Vector2(size.x - 14, 26), stats, 12, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private Image CreateTintBand(string name, Vector2 position, Vector2 size, Color color)
        {
            var image = CreatePanel(_canvasRoot, name, position, size, color);
            image.raycastTarget = false;
            return image;
        }

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
            public readonly string PlayerTitle;

            public FactionSpec(string id, string label, string prefix, string playerTitle)
            {
                Id = id;
                Label = label;
                Prefix = prefix;
                PlayerTitle = playerTitle;
            }
        }

        [Serializable]
        private sealed class OnlineProbeReport
        {
            public bool ok;
            public string error;
            public string matchId;
            public string viewerPlayerId;
            public int revision;
            public string phase;
            public bool isPlayerTurn;
            public string[] hand;
            public int energy;
            public int playerLife;
            public int opponentLife;
            public string playerFaction;
            public string opponentFaction;
        }
    }
}
