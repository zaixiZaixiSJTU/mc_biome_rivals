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
        private static readonly Color Gold = Hex("#E4B95F");
        private static readonly Color Ember = Hex("#D98545");
        private static readonly Color Danger = Hex("#E05A47");

        private readonly DemoLocalMatch _match = new DemoLocalMatch();
        private readonly List<SlotView> _playerUnitSlots = new List<SlotView>();
        private readonly List<SlotView> _playerBuildingSlots = new List<SlotView>();
        private readonly List<SlotView> _opponentUnitSlots = new List<SlotView>();
        private readonly List<SlotView> _opponentBuildingSlots = new List<SlotView>();
        private readonly List<FactionButtonView> _factionButtons = new List<FactionButtonView>();
        private readonly List<IMatchEventPresenter> _eventPresenters = new List<IMatchEventPresenter>();
        private readonly HashSet<int> _mulliganSelectedIndices = new HashSet<int>();

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
        private Button _opponentHeroTargetButton;
        private Button _playerHeroButton;
        private Text _playerEquipmentText;
        private Text _opponentEquipmentText;
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
        private RectTransform _mulliganOverlay;
        private RectTransform _mulliganCardsRoot;
        private Text _mulliganStatusText;
        private Text _mulliganConfirmLabel;
        private Button _mulliganConfirmButton;
        private RectTransform _choiceOverlay;
        private RectTransform _choiceCardsRoot;
        private Text _choiceRuleText;
        private Text _choiceStatusText;
        private Text _choiceConfirmLabel;
        private Button _choiceConfirmButton;
        private IMatchGateway _onlineGateway;
        private DemoOnlineMatchSession _onlineSession;
        private Image _opponentTint;
        private Image _playerTint;
        private string _selectedCardId;
        private string _selectedPaymentMethod = MatchPaymentMethods.Redstone;
        private string _selectedAttackerInstanceId;
        private string _pendingTargetCardId;
        private string _selectedDeploymentTargetInstanceId;
        private string _activeFaction = "plains_forest";
        private string _opponentFaction = "nether";
        private bool _built;
        private bool _previewMulligan;
        private int _selectedChoiceOptionIndex = -1;
        private string _renderedChoiceId;
        private Font _font;
        private DemoHudMaterialFactory _hudMaterialFactory;

        private bool IsOnlineBoard => _onlineSession?.HasAuthoritativeState == true;
        private bool IsFactionSelectionLocked => MatchView.PendingChoice != null || (_onlineGateway != null &&
            _onlineGateway.CurrentStatus.Phase != MatchConnectionPhase.Offline &&
            _onlineGateway.CurrentStatus.Phase != MatchConnectionPhase.Failed);
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
            if (HasCommandLineFlag("-previewMulligan"))
            {
                _previewMulligan = true;
                _mulliganSelectedIndices.Add(1);
                RefreshAll();
            }
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
            if (HasCommandLineFlag("-previewSummon")) SetupSummonPreview();
            else if (HasCommandLineFlag("-previewDeathrattle")) SetupDeathrattlePreview();
            else if (HasCommandLineFlag("-previewTaunt")) SetupTauntPreview();
            else if (HasCommandLineFlag("-previewStructurePlacementInvalid")) SetupStructurePlacementPreview(2);
            else if (HasCommandLineFlag("-previewStructurePlacement")) SetupStructurePlacementPreview(0);
            else if (HasCommandLineFlag("-previewStructureDeployed")) SetupStructureDeployedPreview();
            else if (HasCommandLineFlag("-previewCraftingMissing")) SetupCraftingPreview(false);
            else if (HasCommandLineFlag("-previewCrafting")) SetupCraftingPreview(true);
            else if (HasCommandLineFlag("-previewRaiderDiscount")) SetupRaiderDiscountPreview();
            else if (HasCommandLineFlag("-previewArchaeology")) SetupArchaeologyPreview();
            else if (HasCommandLineFlag("-previewLoot")) SetupLootPreview();
            else if (HasCommandLineFlag("-previewDungeonSkeleton")) SetupDungeonSkeletonPreview();
            else if (HasCommandLineFlag("-previewStray")) SetupStrayPreview();
            else if (HasCommandLineFlag("-previewEquipment")) SetupEquipmentPreview();
            else if (HasCommandLineFlag("-previewGuardianReaction")) SetupGuardianReactionPreview();
            else if (HasCommandLineFlag("-previewDrownedAdjacency")) SetupDrownedAdjacencyPreview();
            else if (HasCommandLineFlag("-previewDolphinCurrent")) SetupDolphinCurrentPreview();
            else if (HasCommandLineFlag("-previewWaterCurrent")) SetupWaterCurrentPreview();
            else if (HasCommandLineFlag("-previewSlow")) SetupSlowPreview();
            else if (HasCommandLineFlag("-previewCombat")) OnEndTurn();
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
            battlefieldPointer.Configure(_battlefield, OnSlotClicked, OnSlotHovered, OnSlotPressed);

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
            BuildMulliganOverlay();
            BuildChoiceOverlay();
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
            _opponentHeroTargetButton = opponentHeroTarget;
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

            var opponentEquipment = CreateBasePanel(_canvasRoot, "OpponentEquipment", new Vector2(-520, 455), new Vector2(170, 76));
            _opponentEquipmentText = CreateText(opponentEquipment, "Label", Vector2.zero, new Vector2(158, 64), "装备槽 · 未装备", 12, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);

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
            _playerHeroButton = _playerHud.gameObject.AddComponent<Button>();
            _playerHeroButton.targetGraphic = _playerHud.GetComponent<Image>();
            _playerHeroButton.transition = Selectable.Transition.ColorTint;
            _playerHeroButton.colors = new ColorBlock
            {
                normalColor = Color.white, highlightedColor = new Color(0.78f, 1f, 0.82f, 1f),
                pressedColor = new Color(0.58f, 0.82f, 0.63f, 1f), selectedColor = Color.white,
                disabledColor = new Color(0.55f, 0.55f, 0.55f, 1f), colorMultiplier = 1f, fadeDuration = 0.08f
            };
            _playerHeroButton.onClick.AddListener(SelectHeroAttacker);
            var playerEquipment = CreateBasePanel(_canvasRoot, "PlayerEquipment", new Vector2(-782, -365), new Vector2(270, 58));
            _playerEquipmentText = CreateText(playerEquipment, "Label", Vector2.zero, new Vector2(252, 48), "装备槽 · 未装备", 13, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
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
                _onlineGateway = compositionRoot.RegisterDefaultOnlineTransport(_activeFaction);
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
                    _onlineStatusText.text = "寻找对手中 · " + Factions.First(item => item.Id == _activeFaction).Label;
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
                if (_selectedAttackerInstanceId != MatchAttackerIds.Hero && FindSelectedAttacker() == null) _selectedAttackerInstanceId = null;
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
                MatchEventTypes.MaterialsConsumed, MatchEventTypes.CardDeployed, MatchEventTypes.ObjectSummoned, MatchEventTypes.CardPlayed,
                MatchEventTypes.CardEquipped, MatchEventTypes.EquipmentDurabilityChanged, MatchEventTypes.EquipmentDestroyed,
                MatchEventTypes.CardBuried, MatchEventTypes.ChoiceOffered, MatchEventTypes.ChoiceResolved,
                MatchEventTypes.CardExcavated, MatchEventTypes.CardDrawn,
                MatchEventTypes.CardBurned, MatchEventTypes.CardGenerated, MatchEventTypes.FatigueDamage, MatchEventTypes.HeroDamaged,
                MatchEventTypes.HeroHealed, MatchEventTypes.ArmorGained, MatchEventTypes.ObjectStatsChanged,
                MatchEventTypes.ObjectStatusApplied, MatchEventTypes.ObjectStatusRemoved, MatchEventTypes.ObjectMoved,
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
                case MatchEventTypes.MaterialsConsumed: {
                    var materialNames = (matchEvent.payload?.materials ?? Array.Empty<CraftingMaterialDto>())
                        .Select(material => $"{GetCardName(material.cardId)}×{material.count}");
                    var craftingViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var ownCrafting = matchEvent.payload?.playerId == craftingViewerId;
                    ShowStatus(ownCrafting
                        ? $"合成台消耗：{string.Join(" + ", materialNames)}。"
                        : $"对手完成合成：{string.Join(" + ", materialNames)}。", false);
                    yield return ShowTurnBanner("合成", Cyan);
                    break;
                }
                case MatchEventTypes.CardDeployed: {
                    if (matchEvent.payload?.cardId == "pf_001")
                    {
                        var deployViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var healing = matchEvent.payload.healing;
                        ShowStatus(matchEvent.payload.playerId == deployViewerId
                            ? $"蜜蜂落地：战吼为你恢复 {healing} 点生命。"
                            : $"敌方蜜蜂落地：战吼为对手恢复 {healing} 点生命。", false);
                    }
                    if (matchEvent.payload?.paymentMethod == MatchPaymentMethods.Crafting)
                        yield return PulseBattlefieldObject(matchEvent.payload.instanceId);
                    else yield return null;
                    break;
                }
                case MatchEventTypes.ObjectSummoned: {
                    var summonViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var ownSummon = matchEvent.payload?.playerId == summonViewerId;
                    var summonSourceName = string.IsNullOrEmpty(matchEvent.payload?.sourceCardId)
                        ? "卡牌"
                        : GetCardName(matchEvent.payload.sourceCardId);
                    var summonedName = string.IsNullOrEmpty(matchEvent.payload?.cardId)
                        ? "一个单位"
                        : GetCardName(matchEvent.payload.cardId);
                    var summonTriggerName = matchEvent.payload?.effectId == "effect.nt_001.01" ? "亡语" : "效果";
                    ShowStatus(ownSummon
                        ? $"{summonSourceName}{summonTriggerName}：{summonedName}已在单位格 {matchEvent.payload.slotIndex + 1} 召唤。"
                        : $"敌方{summonSourceName}{summonTriggerName}：{summonedName}已在单位格 {matchEvent.payload.slotIndex + 1} 召唤。", false);
                    yield return PulseBattlefieldObject(matchEvent.payload?.instanceId);
                    break;
                }
                case MatchEventTypes.CardPlayed: {
                    var effectViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var ownEffect = matchEvent.payload?.playerId == effectViewerId;
                    switch (matchEvent.payload?.effectId)
                    {
                        case "effect.db_006.01":
                            ShowStatus("沙尘暴席卷战场：所有生物受到 2 点伤害。", false);
                            yield return ShowTurnBanner("沙尘暴", Ember);
                            break;
                        case "effect.tk_009.01":
                            ShowStatus(ownEffect ? "骨头已生效：己方目标本回合获得 +1 攻击力。" : "对手使用骨头强化了一个生物。", false);
                            break;
                        case "effect.tk_010.01":
                            ShowStatus(ownEffect ? "圆石已生效：己方建筑恢复至新的生命值。" : "对手使用圆石修复了一个建筑。", false);
                            break;
                    }
                    yield return null;
                    break;
                }
                case MatchEventTypes.CardEquipped: {
                    var equipmentViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var ownEquipment = matchEvent.payload?.playerId == equipmentViewerId;
                    ShowStatus(ownEquipment
                        ? $"已装备 {GetCardName(matchEvent.payload.cardId)}：英雄现在可以攻击。"
                        : $"对手装备了 {GetCardName(matchEvent.payload.cardId)}。", false);
                    yield return ShowTurnBanner("装备", ownEquipment ? Cyan : Ember);
                    break;
                }
                case MatchEventTypes.EquipmentDurabilityChanged:
                    ShowStatus($"{GetCardName(matchEvent.payload.cardId)} 剩余 {matchEvent.payload.durability}/{matchEvent.payload.maxDurability} 耐久。", false);
                    yield return null;
                    break;
                case MatchEventTypes.EquipmentDestroyed:
                    ShowStatus($"{GetCardName(matchEvent.payload.cardId)} 已因{(matchEvent.payload.reason == "REPLACED" ? "替换" : "耐久耗尽")}进入弃牌堆。", false);
                    yield return null;
                    break;
                case MatchEventTypes.CardBuried: {
                    var burialViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var ownBurial = matchEvent.payload?.playerId == burialViewerId;
                    ShowStatus(ownBurial
                        ? $"已将 {GetCardName(matchEvent.payload.cardId)} 埋入牌库；当前有 {matchEvent.payload.buriedCount} 张掩埋牌。"
                        : $"对手埋入了 {GetCardName(matchEvent.payload.cardId)}。", false);
                    yield return ShowTurnBanner("掩埋", Ember);
                    break;
                }
                case MatchEventTypes.ChoiceOffered: {
                    var choiceViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var ownChoice = matchEvent.payload?.playerId == choiceViewerId;
                    if (matchEvent.payload?.kind == "MOVE_UNIT")
                    {
                        var salmonCurrent = matchEvent.payload.effectId == "effect.or_001.01";
                        ShowStatus(ownChoice
                            ? salmonCurrent ? "鲑鱼群水流：选择己方相邻发光地块，或保持原位。" : "激流三叉戟：选择目标旁的发光地块，或保持原位。"
                            : salmonCurrent ? "对手正在决定鲑鱼群的位置。" : "对手正在决定三叉戟目标的位置。", false);
                        yield return ShowTurnBanner(salmonCurrent ? "水流" : "激流位移", ownChoice ? Gold : Ember);
                        break;
                    }
                    ShowStatus(ownChoice
                        ? "沙漠考古学家发现了牌库顶三张牌，请选择可出土的掩埋牌。"
                        : "对手的沙漠考古学家正在查看牌库。", false);
                    yield return ShowTurnBanner("沙漠考古", ownChoice ? Gold : Ember);
                    break;
                }
                case MatchEventTypes.ChoiceResolved:
                    yield return null;
                    break;
                case MatchEventTypes.ObjectMoved:
                    ShowStatus($"{GetCardName(matchEvent.payload.cardId)} 已移动至单位格 {matchEvent.payload.toSlotIndex + 1}。", false);
                    yield return PulseBattlefieldObject(matchEvent.payload.instanceId);
                    break;
                case MatchEventTypes.CardExcavated: {
                    var excavationViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var ownExcavation = matchEvent.payload?.playerId == excavationViewerId;
                    var destination = matchEvent.payload?.destination == "HAND" ? "进入手牌" : "因满手进入弃牌堆";
                    ShowStatus(ownExcavation
                        ? $"出土：{GetCardName(matchEvent.payload.cardId)} {destination}，随后继续正常抽牌。"
                        : $"对手出土了 {GetCardName(matchEvent.payload.cardId)}。", false);
                    yield return ShowTurnBanner("出土", Gold);
                    break;
                }
                case MatchEventTypes.CardGenerated: {
                    var generatedViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var ownGeneration = matchEvent.payload?.playerId == generatedViewerId;
                    var generatedToHand = matchEvent.payload?.destination == "HAND";
                    var sourceName = string.IsNullOrEmpty(matchEvent.payload?.sourceCardId)
                        ? "卡牌"
                        : GetCardName(matchEvent.payload.sourceCardId);
                    var generatedName = string.IsNullOrEmpty(matchEvent.payload?.cardId)
                        ? "一张牌"
                        : GetCardName(matchEvent.payload.cardId);
                    var isLoot = matchEvent.payload?.effectId == "effect.db_001.01" ||
                        matchEvent.payload?.effectId == "effect.pf_002.01" ||
                        matchEvent.payload?.effectId == "effect.cd_003.01" ||
                        matchEvent.payload?.effectId == "effect.si_003.01" ||
                        matchEvent.payload?.effectId == "effect.or_004.01";
                    var triggerName = isLoot ? "掉落" : matchEvent.payload?.effectId == "effect.ed_004.01" ? "亡语" : "效果";
                    if (generatedToHand)
                    {
                        ShowStatus(ownGeneration
                            ? $"{sourceName}{triggerName}：{generatedName}已置入你的手牌。"
                            : $"敌方{sourceName}{triggerName}：对手获得一张牌。", false);
                    }
                    else
                    {
                        ShowStatus(ownGeneration
                            ? $"{sourceName}{triggerName}：手牌已满，{generatedName}进入弃牌堆。"
                            : $"敌方{sourceName}{triggerName}：对手手牌已满，{generatedName}进入弃牌堆。", false);
                    }
                    if (isLoot) yield return ShowTurnBanner("战利品", Gold);
                    if (ownGeneration) yield return PulsePlayerHud(generatedToHand ? Gold : Ember);
                    else yield return null;
                    break;
                }
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
                case MatchEventTypes.ObjectStatsChanged:
                    if (matchEvent.payload?.effectId == "effect.cd_003.01")
                    {
                        var damageViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var hitFriendly = matchEvent.payload?.playerId == damageViewerId;
                        ShowStatus(hitFriendly
                            ? "敌方地牢骷髅亡语：随机命中一个己方生物并造成 1 点伤害。"
                            : "地牢骷髅亡语：随机命中一个敌方生物并造成 1 点伤害。", false);
                        yield return ShowTurnBanner("亡语", Ember);
                    }
                    else if (matchEvent.payload?.effectId == "effect.or_002.01")
                    {
                        var guideViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var guidedFriendly = matchEvent.payload?.playerId == guideViewerId;
                        ShowStatus(guidedFriendly
                            ? "海豚向导：本回合第一次移动的其他己方生物额外获得 +1 攻击力。"
                            : "敌方海豚向导强化了本回合第一次移动的友军。", false);
                        yield return ShowTurnBanner("海豚引航", guidedFriendly ? Cyan : Ember);
                    }
                    else if (matchEvent.payload?.effectId == "effect.or_003.01")
                    {
                        ShowStatus("溺尸战吼：相邻水生友军激活效果，对选定的敌方生物造成 1 点伤害。", false);
                        yield return ShowTurnBanner("潮汐伏击", Cyan);
                    }
                    else if (matchEvent.payload?.effectId == "effect.or_004.01")
                    {
                        ShowStatus("守卫者射线：敌方本回合第一次实际移动的生物受到 1 点伤害。", false);
                        yield return PulseBattlefieldObject(matchEvent.payload?.sourceInstanceId);
                        yield return ShowTurnBanner("守卫射线", Ember);
                    }
                    yield return PulseBattlefieldObject(matchEvent.payload?.instanceId);
                    break;
                case MatchEventTypes.ObjectStatusApplied:
                    ShowStatus($"粉雪覆盖目标：缓慢 {matchEvent.payload?.remainingDuration}，期间不能普通攻击。", false);
                    yield return ShowTurnBanner("缓慢", Cyan);
                    yield return PulseBattlefieldObject(matchEvent.payload?.instanceId);
                    break;
                case MatchEventTypes.ObjectStatusRemoved:
                    ShowStatus("目标控制者的结束阶段已结算，缓慢与绑定的攻击修正已移除。", false);
                    yield return PulseBattlefieldObject(matchEvent.payload?.instanceId);
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

        private void BuildMulliganOverlay()
        {
            _mulliganOverlay = CreateRect(_canvasRoot, "MulliganOverlay", Vector2.zero, new Vector2(ReferenceWidth, ReferenceHeight));
            var dim = _mulliganOverlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0.025f, 0.03f, 0.035f, 0.82f);
            dim.raycastTarget = true;

            var panel = CreateBasePanel(_mulliganOverlay, "MulliganPanel", new Vector2(0, 12), new Vector2(1120, 690));
            CreateText(panel, "Title", new Vector2(0, 292), new Vector2(970, 54), "选择起手牌", 30, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
            CreateText(panel, "Rule", new Vector2(0, 245), new Vector2(940, 42), "点击任意卡牌标记替换；新牌抽出后，换出的牌才会洗回牌库", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            _mulliganCardsRoot = CreateRect(panel, "OpeningHand", new Vector2(0, 25), new Vector2(960, 410));
            _mulliganStatusText = CreateText(panel, "ReadyStatus", new Vector2(0, -220), new Vector2(760, 44), string.Empty, 16, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            _mulliganConfirmButton = CreatePrimaryActionButton(panel, "ConfirmMulligan", new Vector2(0, -282), new Vector2(310, 66), "保留全部", 20);
            _mulliganConfirmLabel = _mulliganConfirmButton.GetComponentInChildren<Text>();
            _mulliganConfirmButton.onClick.AddListener(ConfirmMulligan);
            _mulliganConfirmButton.gameObject.AddComponent<DemoHoverScale>().Configure(1.035f, 16f);
            _mulliganOverlay.gameObject.SetActive(false);
        }

        private void BuildChoiceOverlay()
        {
            _choiceOverlay = CreateRect(_canvasRoot, "ChoiceOverlay", Vector2.zero, new Vector2(ReferenceWidth, ReferenceHeight));
            var dim = _choiceOverlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0.025f, 0.03f, 0.035f, 0.84f);
            dim.raycastTarget = true;

            var panel = CreateBasePanel(_choiceOverlay, "ArchaeologyPanel", new Vector2(0, 10), new Vector2(1080, 670));
            CreateText(panel, "Title", new Vector2(0, 282), new Vector2(940, 54), "沙漠考古", 30, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            _choiceRuleText = CreateText(panel, "Rule", new Vector2(0, 235), new Vector2(920, 44), string.Empty, 16, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            _choiceCardsRoot = CreateRect(panel, "InspectedCards", new Vector2(0, 20), new Vector2(780, 390));
            _choiceStatusText = CreateText(panel, "ChoiceStatus", new Vector2(0, -218), new Vector2(780, 44), string.Empty, 16, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            _choiceConfirmButton = CreatePrimaryActionButton(panel, "ConfirmChoice", new Vector2(0, -280), new Vector2(310, 66), "确认", 20);
            _choiceConfirmLabel = _choiceConfirmButton.GetComponentInChildren<Text>();
            _choiceConfirmButton.onClick.AddListener(ConfirmChoice);
            _choiceConfirmButton.gameObject.AddComponent<DemoHoverScale>().Configure(1.035f, 16f);
            _choiceOverlay.gameObject.SetActive(false);
        }

        private void SelectFaction(string factionId)
        {
            if (IsFactionSelectionLocked)
            {
                ShowStatus("匹配请求已经锁定群系；取消联机后可重新选择。", true);
                return;
            }
            _pendingTargetCardId = null;
            _selectedDeploymentTargetInstanceId = null;
            _activeFaction = factionId;
            _match.SetPlayerFaction(factionId);
            var spec = Factions.First(item => item.Id == factionId);
            ApplyPlayerFactionVisuals(spec);
            var handNumbers = spec.Prefix == "nt" ? new[] { 1, 2, 3, 4, 6 } : Enumerable.Range(1, 5).ToArray();
            var ids = spec.Prefix == "db"
                ? new[] { "db_001", "db_002", "db_004", "db_006", "db_007", "tk_006", "db_002" }
                : handNumbers.Select(index => $"{spec.Prefix}_{index:000}").ToArray();
            var deck = Enumerable.Range(0, 25).Select(index => $"{spec.Prefix}_{(index % 8) + 1:000}").ToArray();
            _match.ResetDeckAndHand(ids, deck);
            _selectedCardId = _match.Hand.FirstOrDefault();
            _selectedPaymentMethod = MatchPaymentMethods.Redstone;
            _battlefield.SetBattlefieldThemes(_activeFaction, _opponentFaction);
            RefreshAll();
            ShowStatus($"已切换到{spec.Label}牌组；可打出已接入规则的卡牌。", false);
        }

        private void SetupTauntPreview()
        {
            SelectFaction("plains_forest");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("pf_003", out var attackerDefinition) ||
                !_registry.TryGetDefinition("pf_008", out var tauntDefinition) ||
                !_registry.TryGetDefinition("pf_001", out var normalDefinition)) return;
            _match.ResetHand(new[] { attackerDefinition.id });
            _match.TryDeploy(attackerDefinition, DemoSlotKind.Unit, 0, out _);
            _match.ResetOpponent(new[] { tauntDefinition, normalDefinition });
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _match.ApplyEnterCombat(_match.CreateEnterCombatCommand());
            var attacker = _match.GetObject(true, DemoSlotKind.Unit, 0);
            _selectedAttackerInstanceId = attacker?.InstanceId;
            if (attacker != null) _battlefield.SetSlotPressed(true, DemoSlotKind.Unit, attacker.SlotIndex, true);
            RefreshAll();
            ShowStatus("嘲讽生效：只能攻击带金色地表高亮的铁傀儡。", false);
        }

        private void SetupDeathrattlePreview()
        {
            SelectFaction("end");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("ed_004", out var shulkerDefinition) ||
                !_registry.TryGetDefinition("pf_008", out var ironGolemDefinition)) return;
            _match.ResetDeckAndHand(new[] { shulkerDefinition.id }, Array.Empty<string>());
            _match.ResetOpponent(new[] { ironGolemDefinition });
            if (!_match.TryDeploy(shulkerDefinition, DemoSlotKind.Unit, 0, out _)) return;
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _match.ApplyEnterCombat(_match.CreateEnterCombatCommand());
            var attacker = _match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = _match.GetObject(false, DemoSlotKind.Unit, 0);
            if (attacker == null || target == null) return;
            var result = _match.ApplyAttack(_match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _selectedAttackerInstanceId = null;
            _selectedCardId = "tk_016";
            RefreshAll();
            ShowStatus(result.Message, !result.Accepted);
        }

        private void SetupSummonPreview()
        {
            SelectFaction("nether");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("nt_001", out var magmaCubeDefinition) ||
                !_registry.TryGetDefinition("pf_008", out var ironGolemDefinition)) return;
            _match.ResetDeckAndHand(new[] { magmaCubeDefinition.id }, Array.Empty<string>());
            _match.ResetOpponent(new[] { ironGolemDefinition });
            if (!_match.TryDeploy(magmaCubeDefinition, DemoSlotKind.Unit, 1, out _)) return;
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _match.ApplyEnterCombat(_match.CreateEnterCombatCommand());
            var attacker = _match.GetObject(true, DemoSlotKind.Unit, 1);
            var target = _match.GetObject(false, DemoSlotKind.Unit, 0);
            if (attacker == null || target == null) return;
            var result = _match.ApplyAttack(_match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));
            _selectedAttackerInstanceId = null;
            _selectedCardId = null;
            RefreshAll();
            ShowStatus(result.Message, !result.Accepted);
            var summoned = _match.GetObject(true, DemoSlotKind.Unit, 1);
            if (summoned?.CardId == "tk_014") StartCoroutine(PulseBattlefieldObject(summoned.InstanceId));
        }

        private void SetupStructurePlacementPreview(int startIndex)
        {
            SelectFaction("desert_badlands");
            if (!_registry.TryGetDefinition("db_007", out var templeDefinition)) return;
            _match.ResetHand(new[] { templeDefinition.id });
            _selectedCardId = templeDefinition.id;
            RefreshAll();
            var preview = DemoDeploymentRules.Evaluate(_match, templeDefinition, DemoSlotKind.Building, startIndex);
            _battlefield.SetSlotRangeHovered(true, DemoSlotKind.Building, startIndex, preview.OccupiedSlots, true, !preview.IsLegal);
            ShowStatus(preview.Message, !preview.IsLegal);
        }

        private void SetupRaiderDiscountPreview()
        {
            SelectFaction("desert_badlands");
            _match.ResetDeckAndHand(new[] { "db_005" }, new[] { "db_001", "tk_006" }, new[] { "tk_006" });
            _match.EndPlayerTurn();
            var draw = _match.BeginNextPlayerTurn();
            _selectedCardId = "db_005";
            RefreshAll();
            ShowStatus(draw.ExcavatedCardIds.Contains("tk_006")
                ? "本回合已发掘埋藏牌：恶地劫掠者费用由 3 降为 2。"
                : "考古预览初始化失败。", !draw.ExcavatedCardIds.Contains("tk_006"));
        }

        private void SetupLootPreview()
        {
            SelectFaction("plains_forest");
            SelectOpponentFaction("desert_badlands");
            if (!_registry.TryGetDefinition("pf_008", out var ironGolemDefinition) ||
                !_registry.TryGetDefinition("db_001", out var huskDefinition)) return;
            _match.ResetDeckAndHand(new[] { ironGolemDefinition.id }, Array.Empty<string>());
            _match.ResetOpponent(new[] { huskDefinition });
            if (!_match.TryDeploy(ironGolemDefinition, DemoSlotKind.Unit, 0, out _)) return;
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            if (!_match.ApplyEnterCombat(_match.CreateEnterCombatCommand()).Accepted) return;
            var attacker = _match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = _match.GetObject(false, DemoSlotKind.Unit, 0);
            if (attacker == null || target == null) return;
            var result = _match.ApplyAttack(_match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));
            if (result.Accepted)
            {
                _match.EndPlayerTurn();
                _match.BeginNextPlayerTurn();
            }
            _selectedAttackerInstanceId = null;
            _selectedCardId = result.Accepted && _match.Hand.Contains("tk_005") ? "tk_005" : null;
            RefreshAll();
            ShowStatus(result.Message, !result.Accepted);
            if (result.Accepted) StartCoroutine(PulseBattlefieldObject(attacker.InstanceId));
        }

        private void SetupDungeonSkeletonPreview()
        {
            SelectFaction("plains_forest");
            SelectOpponentFaction("cave_dark_forest");
            if (!_registry.TryGetDefinition("pf_008", out var ironGolemDefinition) ||
                !_registry.TryGetDefinition("cd_003", out var skeletonDefinition)) return;
            _match.ResetDeckAndHand(new[] { ironGolemDefinition.id }, Array.Empty<string>());
            _match.ResetOpponent(new[] { skeletonDefinition });
            if (!_match.TryDeploy(ironGolemDefinition, DemoSlotKind.Unit, 0, out _)) return;
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            if (!_match.ApplyEnterCombat(_match.CreateEnterCombatCommand()).Accepted) return;
            var attacker = _match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = _match.GetObject(false, DemoSlotKind.Unit, 0);
            if (attacker == null || target == null) return;
            var result = _match.ApplyAttack(_match.CreateAttackCommand(attacker.InstanceId, "UNIT", target.InstanceId));
            if (result.Accepted)
            {
                _match.EndPlayerTurn();
                _match.BeginNextPlayerTurn();
            }
            _selectedAttackerInstanceId = null;
            _selectedCardId = result.Accepted && _match.Hand.Contains("tk_009") ? "tk_009" : null;
            RefreshAll();
            ShowStatus(result.Message, !result.Accepted);
            if (result.Accepted) StartCoroutine(PulseBattlefieldObject(attacker.InstanceId));
        }

        private void SetupSlowPreview()
        {
            SelectFaction("snow_ice");
            SelectOpponentFaction("nether");
            if (!_registry.TryGetDefinition("si_006", out var powderSnowDefinition) ||
                !_registry.TryGetDefinition("nt_003", out var blazeDefinition)) return;
            _match.ResetDeckAndHand(new[] { powderSnowDefinition.id, powderSnowDefinition.id }, Array.Empty<string>());
            _match.ResetOpponent(new[] { blazeDefinition });
            var target = _match.GetObject(false, DemoSlotKind.Unit, 0);
            if (target == null) return;
            var result = _match.ApplyPlayCard(powderSnowDefinition,
                _match.CreatePlayCardCommand(powderSnowDefinition.id, "UNIT", target.InstanceId));
            _selectedCardId = result.Accepted ? powderSnowDefinition.id : null;
            RefreshAll();
            ShowStatus(result.Message, !result.Accepted);
            if (result.Accepted) StartCoroutine(PulseBattlefieldObject(target.InstanceId));
        }

        private void SetupStrayPreview()
        {
            SelectFaction("snow_ice");
            SelectOpponentFaction("nether");
            if (!_registry.TryGetDefinition("si_003", out var strayDefinition) ||
                !_registry.TryGetDefinition("nt_003", out var blazeDefinition)) return;
            _match.ResetDeckAndHand(new[] { strayDefinition.id, strayDefinition.id }, Array.Empty<string>());
            _match.ResetOpponent(new[] { blazeDefinition });
            var target = _match.GetObject(false, DemoSlotKind.Unit, 0);
            if (target == null) return;
            var result = _match.ApplyDeploy(
                strayDefinition,
                _match.CreateDeployCommand(
                    strayDefinition.id, DemoSlotKind.Unit, 0, MatchPaymentMethods.Redstone, "UNIT", target.InstanceId));
            _selectedCardId = result.Accepted ? strayDefinition.id : null;
            _selectedDeploymentTargetInstanceId = result.Accepted ? target.InstanceId : null;
            RefreshAll();
            ShowStatus(result.Accepted
                ? "已锁定战吼目标：烈焰人。选择一个发光单位格部署下一张流浪者。"
                : result.Message, !result.Accepted);
            if (result.Accepted) StartCoroutine(PulseBattlefieldObject(target.InstanceId));
        }

        private void SetupEquipmentPreview()
        {
            SelectFaction("ocean_river");
            SelectOpponentFaction("snow_ice");
            if (!_registry.TryGetDefinition("or_006", out var tridentDefinition) ||
                !_registry.TryGetDefinition("si_005", out var polarBearDefinition)) return;
            _match.ResetDeckAndHand(new[] { tridentDefinition.id }, Array.Empty<string>());
            _match.ResetOpponent(new[] { polarBearDefinition, polarBearDefinition });
            var equipped = _match.ApplyPlayCard(tridentDefinition, _match.CreatePlayCardCommand(tridentDefinition.id));
            if (!equipped.Accepted || !_match.ApplyEnterCombat(_match.CreateEnterCombatCommand()).Accepted) return;
            var target = _match.GetObject(false, DemoSlotKind.Unit, 2);
            if (target == null) return;
            var attacked = _match.ApplyAttack(_match.CreateAttackCommand(MatchAttackerIds.Hero, "UNIT", target.InstanceId));
            _selectedCardId = null;
            _selectedAttackerInstanceId = null;
            RefreshAll();
            ShowStatus(attacked.Accepted
                ? "激流三叉戟已命中：点击目标两侧发光地块完成位移，或保持原位。"
                : attacked.Message, !attacked.Accepted);
            if (attacked.Accepted) StartCoroutine(PulseBattlefieldObject(target.InstanceId));
        }

        private void SetupWaterCurrentPreview()
        {
            SelectFaction("ocean_river");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("or_001", out var salmonDefinition)) return;
            _match.ResetDeckAndHand(new[] { salmonDefinition.id }, Array.Empty<string>());
            var deployed = _match.ApplyDeploy(salmonDefinition,
                _match.CreateDeployCommand(salmonDefinition.id, DemoSlotKind.Unit, 1));
            _selectedCardId = null;
            RefreshAll();
            ShowStatus(deployed.Accepted
                ? "鲑鱼群触发水流：点击左右任一发光地块移动，成功后本回合获得 +1 攻击。"
                : deployed.Message, !deployed.Accepted);
            var salmon = _match.GetObject(true, DemoSlotKind.Unit, 1);
            if (deployed.Accepted && salmon != null) StartCoroutine(PulseBattlefieldObject(salmon.InstanceId));
        }

        private void SetupDolphinCurrentPreview()
        {
            SelectFaction("ocean_river");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("or_002", out var dolphinDefinition) ||
                !_registry.TryGetDefinition("or_001", out var salmonDefinition)) return;
            _match.ResetDeckAndHand(new[] { dolphinDefinition.id, salmonDefinition.id }, Array.Empty<string>());
            var dolphinDeployed = _match.ApplyDeploy(dolphinDefinition,
                _match.CreateDeployCommand(dolphinDefinition.id, DemoSlotKind.Unit, 0));
            var salmonDeployed = _match.ApplyDeploy(salmonDefinition,
                _match.CreateDeployCommand(salmonDefinition.id, DemoSlotKind.Unit, 2));
            _selectedCardId = null;
            RefreshAll();
            ShowStatus(dolphinDeployed.Accepted && salmonDeployed.Accepted
                ? "海豚向导已就位：移动鲑鱼群会同时触发水流 +1 与本回合首次引航 +1。"
                : !dolphinDeployed.Accepted ? dolphinDeployed.Message : salmonDeployed.Message,
                !dolphinDeployed.Accepted || !salmonDeployed.Accepted);
            var dolphin = _match.GetObject(true, DemoSlotKind.Unit, 0);
            var salmon = _match.GetObject(true, DemoSlotKind.Unit, 2);
            if (dolphin == null || salmon == null) ShowStatus("海洋单位模型初始化失败。", true);
        }

        private void SetupDrownedAdjacencyPreview()
        {
            SelectFaction("ocean_river");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("or_001", out var salmonDefinition) ||
                !_registry.TryGetDefinition("or_003", out var drownedDefinition) ||
                !_registry.TryGetDefinition("pf_002", out var sheepDefinition)) return;
            _match.ResetDeckAndHand(new[] { salmonDefinition.id, drownedDefinition.id, drownedDefinition.id }, Array.Empty<string>());
            _match.ResetOpponent(new[] { sheepDefinition });
            var salmonDeployed = _match.ApplyDeploy(salmonDefinition,
                _match.CreateDeployCommand(salmonDefinition.id, DemoSlotKind.Unit, 1));
            if (salmonDeployed.Accepted && _match.PendingChoice != null)
                _match.ApplyResolveChoice(_match.CreateResolveChoiceCommand(_match.PendingChoice.choiceId, -1));
            var target = _match.GetObject(false, DemoSlotKind.Unit, 0);
            var drownedDeployed = target == null
                ? DemoCommandResult.Reject(DemoCommandRejectionCode.InvalidTarget, "缺少溺尸战吼目标。", _match.Revision)
                : _match.ApplyDeploy(drownedDefinition, _match.CreateDeployCommand(
                    drownedDefinition.id, DemoSlotKind.Unit, 2, MatchPaymentMethods.Redstone, "UNIT", target.InstanceId));
            _selectedCardId = drownedDefinition.id;
            _selectedDeploymentTargetInstanceId = target?.InstanceId;
            RefreshAll();
            ShowStatus(salmonDeployed.Accepted && drownedDeployed.Accepted
                ? "溺尸已借相邻鲑鱼触发战吼；金色地表格表示下一张溺尸可再次激活相邻条件。"
                : !salmonDeployed.Accepted ? salmonDeployed.Message : drownedDeployed.Message,
                !salmonDeployed.Accepted || !drownedDeployed.Accepted);
        }

        private void SetupGuardianReactionPreview()
        {
            SelectFaction("ocean_river");
            SelectOpponentFaction("ocean_river");
            if (!_registry.TryGetDefinition("or_001", out var salmonDefinition) ||
                !_registry.TryGetDefinition("or_004", out var guardianDefinition)) return;
            _match.ResetDeckAndHand(new[] { salmonDefinition.id }, Array.Empty<string>());
            _match.ResetOpponent(new[] { guardianDefinition });
            var deployed = _match.ApplyDeploy(salmonDefinition,
                _match.CreateDeployCommand(salmonDefinition.id, DemoSlotKind.Unit, 1));
            _selectedCardId = null;
            RefreshAll();
            ShowStatus(deployed.Accepted
                ? "敌方守卫者尚未触发：移动鲑鱼群会受到 1 点射线伤害；保持原位不会消耗其反应。"
                : deployed.Message, !deployed.Accepted);
            var salmon = _match.GetObject(true, DemoSlotKind.Unit, 1);
            var guardian = _match.GetObject(false, DemoSlotKind.Unit, 0);
            if (salmon != null) StartCoroutine(PulseBattlefieldObject(salmon.InstanceId));
            if (guardian != null) StartCoroutine(PulseBattlefieldObject(guardian.InstanceId));
        }

        private void SetupStructureDeployedPreview()
        {
            SelectFaction("desert_badlands");
            if (!_registry.TryGetDefinition("db_007", out var templeDefinition)) return;
            _match.ResetHand(new[] { templeDefinition.id });
            var deployed = _match.TryDeploy(templeDefinition, DemoSlotKind.Building, 0, out var message);
            _selectedCardId = null;
            RefreshAll();
            ShowStatus(deployed ? "沙漠神殿作为一个对象横跨建筑格 1—2；任意格受击都会共享同一生命值。" : message, !deployed);
        }

        private void SetupCraftingPreview(bool includeAllMaterials)
        {
            SelectFaction("desert_badlands");
            if (!_registry.TryGetDefinition("db_007", out var templeDefinition)) return;
            _match.ResetHand(includeAllMaterials
                ? new[] { templeDefinition.id, "db_002", "tk_006" }
                : new[] { templeDefinition.id, "tk_006" });
            _selectedCardId = templeDefinition.id;
            _selectedPaymentMethod = MatchPaymentMethods.Crafting;
            RefreshAll();
            var preview = DemoDeploymentRules.Evaluate(
                _match, templeDefinition, DemoSlotKind.Building, 0, _selectedPaymentMethod);
            _battlefield.SetSlotRangeHovered(true, DemoSlotKind.Building, 0, preview.OccupiedSlots, true, !preview.IsLegal);
            if (includeAllMaterials)
                ShowStatus("合成支付已就绪：选择连续建筑格 1—2，材料会公开进入弃牌堆。", false);
            else
                ShowStatus(ReplaceCardIdsWithNames(preview.Message, templeDefinition), true);
        }

        private void SetupArchaeologyPreview()
        {
            SelectFaction("desert_badlands");
            if (!_registry.TryGetDefinition("db_003", out var archaeologistDefinition)) return;
            _match.ResetDeckAndHand(
                new[] { archaeologistDefinition.id },
                new[] { "db_004", "tk_006", "db_001" },
                new[] { "tk_006" });
            _selectedCardId = archaeologistDefinition.id;
            var result = _match.ApplyDeploy(
                archaeologistDefinition,
                _match.CreateDeployCommand(archaeologistDefinition.id, DemoSlotKind.Unit, 0));
            _selectedCardId = _match.Hand.FirstOrDefault();
            RefreshAll();
            SelectChoiceOption(1);
            ShowStatus(result.Accepted ? "考古学家正在查看牌库顶三张牌；只有金色标记的掩埋牌可以出土。" : result.Message, !result.Accepted);
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
            if (IsFactionSelectionLocked)
            {
                ShowStatus("在线对局的敌方阵营由权威房间决定。", true);
                return;
            }
            var spec = Factions.First(item => item.Id == factionId);
            _opponentFaction = factionId;
            _match.SetOpponentFaction(factionId);
            _pendingTargetCardId = null;
            _selectedDeploymentTargetInstanceId = null;
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
            var playerFaction = Factions.FirstOrDefault(value => value.Id == MatchView.PlayerFactionId) ?? Factions[0];
            var opponentFaction = Factions.FirstOrDefault(value => value.Id == MatchView.OpponentFactionId) ?? Factions[5];
            ApplyPlayerFactionVisuals(playerFaction);
            ApplyOpponentFactionVisuals(opponentFaction);
            _battlefield.SetBattlefieldThemes(_activeFaction, _opponentFaction);
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
            RefreshMulligan();
            RefreshPendingChoice();
            RefreshFactionButtons();
            RefreshHand();
            RefreshOpponentHand(match.OpponentHandCount);
            RefreshBattlefieldSlots();
            RefreshOpponentHeroTarget();
            _battlefield.SyncPieces(match.PlayerBattlefield, match.OpponentBattlefield, _registry);
            RefreshInspector();
            _energyText.text = $"◆ {match.Energy}/{match.MaxEnergy}";
            _titleText.text = IsOnlineBoard ? "群系竞逐  ·  权威联机对局" : "群系竞逐  ·  本地战场演示";
            _roundText.text = match.IsMulligan ? "开局 · 起手调度" : $"第 {match.Round} 回合 · {(match.Phase == DemoTurnPhase.Main ? "主行动" : "战斗")}";
            _opponentHealthText.text = $"❤ {match.OpponentLife}";
            _playerHealthText.text = match.PlayerArmor > 0 ? $"❤ {match.PlayerLife}  ◈ {match.PlayerArmor}" : $"❤ {match.PlayerLife}";
            _playerEquipmentText.text = FormatEquipment(match.PlayerEquipment);
            _playerEquipmentText.color = match.PlayerEquipment == null ? Muted : Cyan;
            _opponentEquipmentText.text = FormatEquipment(match.OpponentEquipment);
            _opponentEquipmentText.color = match.OpponentEquipment == null ? Muted : Ember;
            _playerHeroButton.interactable = match.Phase != DemoTurnPhase.Combat || match.CanAttackWithHero(out _);
            _handLabel.text = match.BuriedCount > 0
                ? $"手牌 {match.Hand.Count}/7 · 牌库 {match.DeckCount}（掩埋 {match.BuriedCount}）· 弃牌 {match.DiscardCount}"
                : $"手牌 {match.Hand.Count}/7 · 牌库 {match.DeckCount} · 弃牌 {match.DiscardCount}";
            var canUseHand = !match.IsMulligan && match.PendingChoice == null && match.Phase == DemoTurnPhase.Main && match.IsPlayerTurn && (!IsOnlineBoard || _onlineSession.CanIssueCommand);
            _handCanvasGroup.alpha = canUseHand ? 1f : 0.52f;
            _handCanvasGroup.interactable = canUseHand;
            _handCanvasGroup.blocksRaycasts = canUseHand;
            _endTurnButton.interactable = !match.IsMulligan && match.PendingChoice == null && match.IsPlayerTurn && !match.IsFinished && (!IsOnlineBoard || _onlineSession.CanIssueCommand);
            _endTurnLabel.text = IsOnlineBoard && _onlineSession.HasPendingCommand
                ? "等待服务器"
                : match.IsMulligan ? "等待起手确认"
                : match.PendingChoice != null ? match.PendingChoice.kind == "MOVE_UNIT"
                    ? match.IsChoiceOwner ? "选择移动地块" : "对手正在移动"
                    : match.IsChoiceOwner ? "完成考古选择" : "对手正在选择"
                : !match.IsPlayerTurn ? "对手行动中" : match.IsFinished ? "对局结束" : match.Phase == DemoTurnPhase.Main ? "进入战斗" : "结束回合";
        }

        private void RefreshPendingChoice()
        {
            if (_choiceOverlay == null) return;
            var match = MatchView;
            var choice = match.PendingChoice;
            var visible = choice != null && choice.kind != "MOVE_UNIT";
            _choiceOverlay.gameObject.SetActive(visible);
            if (!visible)
            {
                _selectedChoiceOptionIndex = -1;
                _renderedChoiceId = choice?.choiceId;
                return;
            }

            if (_renderedChoiceId != choice.choiceId)
            {
                _renderedChoiceId = choice.choiceId;
                _selectedChoiceOptionIndex = -1;
            }
            _choiceOverlay.SetAsLastSibling();
            ClearChildren(_choiceCardsRoot);

            if (!match.IsChoiceOwner)
            {
                _choiceRuleText.text = "对手正在查看自己的牌库顶三张牌；牌面信息对你保密";
                _choiceStatusText.text = "等待对手完成考古选择…";
                _choiceConfirmButton.gameObject.SetActive(false);
                var hiddenCount = choice.options?.Length ?? 0;
                for (var index = 0; index < hiddenCount; index++) CreateHiddenChoiceCard(index, hiddenCount);
                return;
            }

            _choiceConfirmButton.gameObject.SetActive(true);
            _choiceRuleText.text = "查看牌库顶 3 张；选择一张带“掩埋”标记的牌立即出土，然后正常抽一张牌";
            var options = (choice.options ?? Array.Empty<PendingChoiceOptionDto>()).Where(option => option != null).ToArray();
            var hasSelectable = options.Any(option => option.selectable);
            foreach (var option in options) CreateChoiceCard(option, options.Length);
            if (hasSelectable)
            {
                _choiceStatusText.text = _selectedChoiceOptionIndex < 0 ? "请选择一张金色标记的掩埋牌" : "已选择出土目标";
                _choiceConfirmLabel.text = _selectedChoiceOptionIndex < 0 ? "选择一张掩埋牌" : "确认出土";
            }
            else
            {
                _choiceStatusText.text = "这三张牌中没有掩埋牌，将保持原顺序放回";
                _choiceConfirmLabel.text = "确认未发现";
            }
            _choiceConfirmButton.interactable = (!IsOnlineBoard || _onlineSession.CanIssueCommand) && (!hasSelectable || _selectedChoiceOptionIndex >= 0);
        }

        private void CreateChoiceCard(PendingChoiceOptionDto option, int optionCount)
        {
            var selected = option.optionIndex == _selectedChoiceOptionIndex;
            var x = (option.optionIndex - (optionCount - 1) * 0.5f) * 244f;
            var slot = CreateBasePanel(_choiceCardsRoot, "ChoiceSlot" + option.optionIndex, new Vector2(x, selected ? 18f : 0f), new Vector2(220, 350));
            var accent = option.selectable ? Gold : Muted;
            slot.GetComponent<Image>().color = selected
                ? Color.Lerp(DemoUiStyleCatalog.GetRootFill(DemoUiStyleClass.BasePanel), Gold, 0.46f)
                : DemoUiStyleCatalog.GetRootFill(DemoUiStyleClass.BasePanel);
            var materialFill = slot.Find("MaterialFill")?.GetComponent<Image>();
            if (materialFill != null && (option.selectable || selected))
                materialFill.color = Color.Lerp(materialFill.color, accent, selected ? 0.42f : 0.18f);
            var optionIndex = option.optionIndex;
            var card = DemoCardUiFactory.Create(
                slot, _registry, option.cardId, new Vector2(188, 270), true, UiFont,
                option.selectable ? (Action)(() => SelectChoiceOption(optionIndex)) : null);
            card.RectTransform.anchoredPosition = new Vector2(0, 18);
            if (option.selectable) card.gameObject.AddComponent<DemoHoverScale>().Configure(1.045f, 16f);
            CreateText(slot, "ChoiceLabel", new Vector2(0, -151), new Vector2(188, 30),
                option.selectable ? selected ? "◆ 已选中" : "◆ 可出土" : "保持牌库顺序",
                14, accent, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void CreateHiddenChoiceCard(int index, int optionCount)
        {
            var x = (index - (optionCount - 1) * 0.5f) * 244f;
            var slot = CreateBasePanel(_choiceCardsRoot, "HiddenChoiceSlot" + index, new Vector2(x, 0), new Vector2(220, 350));
            CreateText(slot, "HiddenGlyph", new Vector2(0, 22), new Vector2(160, 210), "◇\n?", 46, Ember, TextAnchor.MiddleCenter, FontStyle.Bold);
            CreateText(slot, "HiddenLabel", new Vector2(0, -144), new Vector2(180, 30), "牌库信息保密", 14, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void SelectChoiceOption(int optionIndex)
        {
            var choice = MatchView.PendingChoice;
            if (choice == null || !MatchView.IsChoiceOwner || (IsOnlineBoard && !_onlineSession.CanIssueCommand)) return;
            var option = (choice.options ?? Array.Empty<PendingChoiceOptionDto>())
                .FirstOrDefault(value => value != null && value.optionIndex == optionIndex && value.selectable);
            if (option == null) return;
            _selectedChoiceOptionIndex = optionIndex;
            RefreshPendingChoice();
        }

        private async void ConfirmChoice()
        {
            var choice = MatchView.PendingChoice;
            if (choice == null || !MatchView.IsChoiceOwner) return;
            var options = choice.options ?? Array.Empty<PendingChoiceOptionDto>();
            var hasSelectable = options.Any(option => option != null && option.selectable);
            if (hasSelectable && _selectedChoiceOptionIndex < 0) return;
            var selectedOptionIndex = hasSelectable ? _selectedChoiceOptionIndex : -1;
            if (IsOnlineBoard)
            {
                if (!_onlineSession.CanIssueCommand) return;
                await SendOnline(() => _onlineSession.ResolveChoiceAsync(choice.choiceId, selectedOptionIndex));
                RefreshAll();
                return;
            }

            var result = _match.ApplyResolveChoice(_match.CreateResolveChoiceCommand(choice.choiceId, selectedOptionIndex));
            var message = result.Message;
            foreach (var option in options)
                if (option != null && !string.IsNullOrEmpty(option.cardId)) message = message.Replace(option.cardId, GetCardName(option.cardId));
            if (_match.LastDrawResult != null && !string.IsNullOrEmpty(_match.LastDrawResult.CardId))
                message = message.Replace(_match.LastDrawResult.CardId, GetCardName(_match.LastDrawResult.CardId));
            ShowStatus(result.Accepted ? $"{message} · 状态 r{result.Revision}" : message, !result.Accepted);
            if (result.Accepted) StartCoroutine(ShowTurnBanner(hasSelectable ? "出土" : "未发现", hasSelectable ? Gold : Muted));
            RefreshAll();
        }

        private async void ResolveMovementChoice(int optionIndex)
        {
            var choice = MatchView.PendingChoice;
            if (choice == null || choice.kind != "MOVE_UNIT" || !MatchView.IsChoiceOwner) return;
            if (IsOnlineBoard)
            {
                if (!_onlineSession.CanIssueCommand) return;
                await SendOnline(() => _onlineSession.ResolveChoiceAsync(choice.choiceId, optionIndex));
                RefreshAll();
                return;
            }
            var waterCurrent = choice.effectId == "effect.or_001.01";
            var result = _match.ApplyResolveChoice(_match.CreateResolveChoiceCommand(choice.choiceId, optionIndex));
            ShowStatus(result.Accepted ? $"{result.Message} · 状态 r{result.Revision}" : result.Message, !result.Accepted);
            if (result.Accepted) StartCoroutine(ShowTurnBanner(optionIndex < 0 ? "保持原位" : waterCurrent ? "水流移动" : "激流位移", Gold));
            RefreshAll();
        }

        private void RefreshMulligan()
        {
            if (_mulliganOverlay == null) return;
            var match = MatchView;
            var preview = _previewMulligan && !IsOnlineBoard;
            var visible = preview || (IsOnlineBoard && match.IsMulligan);
            _mulliganOverlay.gameObject.SetActive(visible);
            if (!visible)
            {
                _mulliganSelectedIndices.Clear();
                return;
            }

            _mulliganOverlay.SetAsLastSibling();
            var openingHand = preview ? match.Hand.Take(4).ToArray() : match.Hand.ToArray();
            _mulliganSelectedIndices.RemoveWhere(index => index < 0 || index >= openingHand.Length);
            ClearChildren(_mulliganCardsRoot);
            var count = openingHand.Length;
            for (var index = 0; index < count; index++)
            {
                var selectedIndex = index;
                var selected = _mulliganSelectedIndices.Contains(index);
                var x = (index - (count - 1) * 0.5f) * 224f;
                var slot = CreateBasePanel(_mulliganCardsRoot, "MulliganSlot" + index, new Vector2(x, selected ? 18f : 0f), new Vector2(206, 330));
                slot.GetComponent<Image>().color = selected
                    ? Color.Lerp(DemoUiStyleCatalog.GetRootFill(DemoUiStyleClass.BasePanel), Cyan, 0.42f)
                    : DemoUiStyleCatalog.GetRootFill(DemoUiStyleClass.BasePanel);
                var materialFill = slot.Find("MaterialFill")?.GetComponent<Image>();
                if (selected && materialFill != null) materialFill.color = Color.Lerp(materialFill.color, Cyan, 0.34f);
                var frameSlice = slot.Find("FrameSlice")?.GetComponent<Image>();
                if (selected && frameSlice != null) frameSlice.color = Color.Lerp(frameSlice.color, Cyan, 0.28f);
                var card = DemoCardUiFactory.Create(slot, _registry, openingHand[index], new Vector2(184, 262), true, UiFont, () => ToggleMulliganCard(selectedIndex));
                card.RectTransform.anchoredPosition = new Vector2(0, 16);
                card.gameObject.AddComponent<DemoHoverScale>().Configure(1.045f, 16f);
                CreateText(slot, "Choice", new Vector2(0, -142), new Vector2(178, 30), selected ? "将替换" : "保留", 15, selected ? Cyan : Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            }

            if (!preview && match.PlayerMulliganCompleted)
            {
                _mulliganStatusText.text = match.OpponentMulliganCompleted ? "双方已确认，正在进入第一回合…" : "起手已锁定 · 等待对手确认";
                _mulliganConfirmLabel.text = "已确认";
                _mulliganConfirmButton.interactable = false;
            }
            else
            {
                _mulliganStatusText.text = preview ? "预览模式 · 点击卡牌检查替换反馈" : match.OpponentMulliganCompleted ? "对手已确认 · 请选择你的起手牌" : "双方正在选择起手牌";
                _mulliganConfirmLabel.text = _mulliganSelectedIndices.Count == 0 ? "保留全部" : $"替换 {_mulliganSelectedIndices.Count} 张";
                _mulliganConfirmButton.interactable = !preview && _onlineSession?.CanIssueCommand == true;
            }
        }

        private void ToggleMulliganCard(int index)
        {
            if (_previewMulligan && !IsOnlineBoard)
            {
                if (!_mulliganSelectedIndices.Add(index)) _mulliganSelectedIndices.Remove(index);
                RefreshMulligan();
                return;
            }
            if (!IsOnlineBoard || !MatchView.IsMulligan || MatchView.PlayerMulliganCompleted || _onlineSession?.CanIssueCommand != true) return;
            if (!_mulliganSelectedIndices.Add(index)) _mulliganSelectedIndices.Remove(index);
            RefreshMulligan();
        }

        private async void ConfirmMulligan()
        {
            if (!IsOnlineBoard || !MatchView.IsMulligan || MatchView.PlayerMulliganCompleted || _onlineSession?.CanIssueCommand != true) return;
            var selected = _mulliganSelectedIndices.OrderBy(index => index).ToArray();
            var result = await SendOnline(() => _onlineSession.MulliganAsync(selected));
            if (result?.Outcome == MatchCommandOutcome.Accepted) _mulliganSelectedIndices.Clear();
            RefreshAll();
        }

        private void RefreshFactionButtons()
        {
            foreach (var view in _factionButtons)
            {
                var active = view.Id == _activeFaction;
                view.Image.color = DemoUiStyleCatalog.GetRootFill(DemoUiStyleClass.SecondaryButton);
                view.SelectionAccent.gameObject.SetActive(active);
                view.Button.interactable = !IsFactionSelectionLocked;
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
                if (!_registry.TryGetDefinition(cardId, out var definition)) continue;
                var selected = cardId == _selectedCardId;
                var x = (i - (count - 1) * 0.5f) * 176f;
                var y = selected ? 20f : -Mathf.Abs(i - (count - 1) * 0.5f) * 4f;
                var card = DemoCardUiFactory.Create(_handRoot, _registry, cardId, new Vector2(158, 216), true, UiFont, () => SelectCard(cardId), match.GetEffectiveCost(definition));
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

        private void RefreshOpponentHeroTarget()
        {
            if (_opponentHeroTargetButton == null) return;
            var match = MatchView;
            var attacker = FindSelectedAttacker();
            var hasSelectedAttacker = attacker != null || _selectedAttackerInstanceId == MatchAttackerIds.Hero;
            _opponentHeroTargetButton.interactable = match.Phase != DemoTurnPhase.Combat || !hasSelectedAttacker ||
                match.CanAttackTarget(null, "HERO", out _);
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
            var valid = false;
            var movementChoice = match.PendingChoice != null && match.PendingChoice.kind == "MOVE_UNIT";
            var movementTargetIsPlayer = movementChoice && match.PlayerBattlefield.Any(value =>
                value != null && value.InstanceId == match.PendingChoice.targetInstanceId);
            if (canInteract && movementChoice)
                valid = player == movementTargetIsPlayer && view.Kind == DemoSlotKind.Unit && empty && match.IsChoiceOwner &&
                    (match.PendingChoice.options ?? Array.Empty<PendingChoiceOptionDto>()).Any(option =>
                        option != null && option.selectable && option.slotIndex == view.Index);
            else if (canInteract && selectingCardTarget)
                valid = IsValidPendingCardTarget(player, view.Kind, battlefieldObject);
            else if (canInteract && player)
                valid = match.Phase == DemoTurnPhase.Main
                    ? EvaluateSelectedDeployment(view.Kind, view.Index).IsLegal
                    : !empty && view.Kind == DemoSlotKind.Unit && match.CanAttackWith(battlefieldObject, out _);
            else if (canInteract && match.Phase == DemoTurnPhase.Combat && !string.IsNullOrEmpty(_selectedAttackerInstanceId) && !empty)
                valid = match.CanAttackTarget(battlefieldObject, view.Kind == DemoSlotKind.Unit ? "UNIT" : "BUILDING", out _);
            view.EmptyLabel.color = Color.clear;
            var activatesDrowned = valid && player && empty && view.Kind == DemoSlotKind.Unit &&
                !string.IsNullOrEmpty(_selectedCardId) &&
                _registry.TryGetDefinition(_selectedCardId, out var selectedDefinition) && IsDrowned(selectedDefinition) &&
                DrownedBattlecryActivatesAt(view.Index);
            var priorityTarget = movementChoice && battlefieldObject?.InstanceId == match.PendingChoice.targetInstanceId ||
                valid && !player && battlefieldObject?.HasKeyword("TAUNT") == true || activatesDrowned;
            _battlefield.SetSlotState(player, view.Kind, view.Index, valid, !empty, priorityTarget);

            if (!empty)
            {
                if (view.Kind == DemoSlotKind.Building && battlefieldObject != null && battlefieldObject.SlotIndex != view.Index)
                    CreateText(view.Content, "Occupied", Vector2.zero, view.Content.sizeDelta, "结构占用", 14, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
                else
                    CreateWorldPieceLabel(view.Content, cardId, view.Content.sizeDelta, !player, battlefieldObject);
            }
        }

        private void RefreshInspector()
        {
            var match = MatchView;
            ClearChildren(_inspectorRoot);
            var moving = match.PendingChoice != null && match.PendingChoice.kind == "MOVE_UNIT";
            CreateText(_inspectorRoot, "Header", new Vector2(0, 315), new Vector2(250, 38), moving ? "位移指令" : match.Phase == DemoTurnPhase.Main ? "卡牌详情" : "战斗指令", 20, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (moving)
            {
                _cardDetailsView.Clear();
                var waterCurrent = match.PendingChoice.effectId == "effect.or_001.01";
                var guideReady = waterCurrent && match.PlayerBattlefield.Any(value => value != null &&
                    value.CardId == "or_002" && value.InstanceId != match.PendingChoice.targetInstanceId &&
                    !match.HasTriggeredEffect(true, value.InstanceId, "effect.or_002.01"));
                var movementTargetIsPlayer = match.PlayerBattlefield.Any(value => value != null &&
                    value.InstanceId == match.PendingChoice.targetInstanceId);
                var guardianOwnerIsPlayer = !movementTargetIsPlayer;
                var guardianBattlefield = guardianOwnerIsPlayer ? match.PlayerBattlefield : match.OpponentBattlefield;
                var guardianThreats = guardianBattlefield.Count(value => value != null && value.Health > 0 &&
                    value.CardId == "or_004" && !match.HasTriggeredEffect(
                        guardianOwnerIsPlayer, value.InstanceId, "effect.or_004.01"));
                CreateText(_inspectorRoot, "CombatTitle", new Vector2(0, 105), new Vector2(245, 120),
                    waterCurrent ? "鲑鱼群 · 水流\n选择发光的相邻地块" : "激流三叉戟\n选择发光的相邻地块", 19, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
                var movementHint = "直接点击目标旁的发光地块，\n或保持目标原位。";
                if (guideReady) movementHint = "移动后：海豚向导追加 +1 攻击。";
                if (guardianThreats > 0) movementHint += $"\n警告：移动后承受 {guardianThreats} 点守卫者伤害；保持原位不触发。";
                CreateText(_inspectorRoot, "CombatHint", new Vector2(0, -5), new Vector2(245, 100),
                    match.IsChoiceOwner ? movementHint : "等待对手决定目标单位的位置。",
                    guardianThreats > 0 ? 14 : 15, guardianThreats > 0 ? Gold : Pale, TextAnchor.MiddleCenter, FontStyle.Normal);
                if (match.IsChoiceOwner)
                {
                    var stay = CreateSecondaryButton(_inspectorRoot, "KeepPosition", new Vector2(0, -105), new Vector2(220, 52), "保持原位", 16);
                    stay.interactable = !IsOnlineBoard || _onlineSession.CanIssueCommand;
                    stay.onClick.AddListener(() => ResolveMovementChoice(-1));
                }
                return;
            }
            if (match.Phase == DemoTurnPhase.Combat)
            {
                _cardDetailsView.Clear();
                var attacker = FindSelectedAttacker();
                var heroSelected = _selectedAttackerInstanceId == MatchAttackerIds.Hero;
                var title = heroSelected && match.PlayerEquipment != null
                    ? $"英雄 · {GetCardName(match.PlayerEquipment.CardId)}\n{match.PlayerEquipment.Attack} 攻 / {match.PlayerEquipment.Durability} 耐久"
                    : attacker == null ? "选择发光的己方生物\n或点击左下角英雄" : $"攻击者：{GetCardName(attacker.CardId)}\n{attacker.Attack}/{attacker.Health}";
                CreateText(_inspectorRoot, "CombatTitle", new Vector2(0, 100), new Vector2(245, 130), title, 19, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
                var combatHint = "再点击敌方生物、建筑，\n或左上角敌方英雄面板。\n单位会同步反击，建筑不会反击。";
                if ((attacker != null || heroSelected) && !match.CanAttackTarget(null, "HERO", out var tauntMessage))
                    combatHint = tauntMessage + "\n只有金色地表目标可被攻击。";
                CreateText(_inspectorRoot, "CombatHint", new Vector2(0, -25), new Vector2(245, 120), combatHint, 15, Pale, TextAnchor.MiddleCenter, FontStyle.Normal);
                return;
            }
            if (string.IsNullOrEmpty(_selectedCardId) || !_registry.TryGetDefinition(_selectedCardId, out var definition))
            {
                _cardDetailsView.Clear();
                CreateText(_inspectorRoot, "Empty", new Vector2(0, 40), new Vector2(230, 180), "手牌已打空。\n切换左侧群系可重新装填演示手牌。", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
                return;
            }

            var effectiveCost = match.GetEffectiveCost(definition);
            var isDiscounted = effectiveCost < definition.cost;
            _cardDetailsView.ShowCard(_selectedCardId, new Vector2(238, 350), new Vector2(0, 100), effectiveCost);
            var deployType = definition.cardType == "UNIT" || definition.cardType == "BUILDING" || definition.cardType == "STRUCTURE";
            if (deployType)
            {
                var target = definition.cardType == "UNIT" ? "单位格" : $"建筑格（占 {Mathf.Max(1, definition.buildingSlots)} 格）";
                var requiresBattlecryTarget = DemoCardTargeting.TryGetRule(definition, out var battlecryTargetRule);
                var selectedBattlecryTarget = requiresBattlecryTarget ? FindSelectedDeploymentTarget() : null;
                var conditionalDrowned = IsDrowned(definition);
                var drownedCanActivate = conditionalDrowned && HasPotentialDrownedBattlecrySlot();
                var drownedHasTarget = conditionalDrowned && DemoCardTargeting.HasLegalTarget(match, battlecryTargetRule);
                if (requiresBattlecryTarget && (!conditionalDrowned || drownedCanActivate && drownedHasTarget))
                {
                    var targeting = _pendingTargetCardId == definition.id;
                    var hasLegalTarget = DemoCardTargeting.HasLegalTarget(match, battlecryTargetRule);
                    var actionLabel = targeting
                        ? "取消目标选择"
                        : selectedBattlecryTarget != null
                            ? $"战吼目标：{GetCardName(selectedBattlecryTarget.CardId)}"
                            : hasLegalTarget ? battlecryTargetRule.ActionLabel : "没有合法目标";
                    var targetButton = CreateSecondaryButton(_inspectorRoot, "BattlecryTarget", new Vector2(0, -118), new Vector2(235, 54), actionLabel, 15);
                    targetButton.interactable = targeting || (hasLegalTarget && match.IsPlayerTurn && match.Hand.Contains(definition.id) &&
                        (!IsOnlineBoard || _onlineSession.CanIssueCommand));
                    targetButton.onClick.AddListener(targeting ? (UnityEngine.Events.UnityAction)CancelTargetSelection : CastSelectedCard);
                    targetButton.gameObject.AddComponent<DemoHoverScale>().Configure(1.04f, 16f);
                    var targetHint = selectedBattlecryTarget == null
                        ? "先锁定敌方战吼目标，再选择己方单位格。"
                        : $"已锁定 {GetCardName(selectedBattlecryTarget.CardId)} · 选择发光的{target}部署";
                    CreateText(_inspectorRoot, "DeployHint", new Vector2(0, -174), new Vector2(246, 48), targetHint, 13,
                        selectedBattlecryTarget == null ? Gold : Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
                else if (conditionalDrowned)
                {
                    CreateText(_inspectorRoot, "DeployHint", new Vector2(0, -126), new Vector2(246, 82),
                        drownedCanActivate
                            ? "金色地表格可激活战吼，\n但当前没有敌方生物可选。"
                            : "当前没有可相邻的水生友军；\n仍可部署，但不会造成战吼伤害。",
                        14, drownedCanActivate ? Gold : Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
                else if (definition.hasCraftingRecipe)
                {
                    var canInteract = match.IsPlayerTurn && match.Phase == DemoTurnPhase.Main && match.Hand.Contains(definition.id) &&
                                      (!IsOnlineBoard || _onlineSession.CanIssueCommand);
                    var redstoneSelected = _selectedPaymentMethod == MatchPaymentMethods.Redstone;
                    var craftingSelected = _selectedPaymentMethod == MatchPaymentMethods.Crafting;
                    var redstoneLabel = $"{(redstoneSelected ? "◆ " : string.Empty)}红石 {effectiveCost}";
                    var redstoneButton = CreateSecondaryButton(_inspectorRoot, "PayRedstone", new Vector2(-61, -112), new Vector2(116, 52), redstoneLabel, 14);
                    redstoneButton.interactable = canInteract;
                    redstoneButton.onClick.AddListener(() => SelectPaymentMethod(MatchPaymentMethods.Redstone));
                    ConfigureButtonColors(redstoneButton, redstoneSelected ? Color.Lerp(Panel, Ember, 0.45f) : Panel, Ember);

                    var recipeLabel = string.Join(" + ", definition.craftingRecipe.Select(value => $"{GetCardName(value.cardId)}×{value.count}"));
                    var hasMaterials = DemoDeploymentRules.CanPayWithCrafting(match, definition, out var materialMessage);
                    var craftingLabel = $"{(craftingSelected ? "◆ " : string.Empty)}合成\n{recipeLabel}";
                    var craftingButton = CreateSecondaryButton(_inspectorRoot, "PayCrafting", new Vector2(61, -112), new Vector2(116, 52), craftingLabel, 11);
                    craftingButton.interactable = canInteract;
                    craftingButton.onClick.AddListener(() => SelectPaymentMethod(MatchPaymentMethods.Crafting));
                    ConfigureButtonColors(craftingButton, craftingSelected ? Color.Lerp(Panel, Cyan, 0.48f) : Panel, Cyan);

                    var paymentHint = craftingSelected && !hasMaterials
                        ? ReplaceCardIdsWithNames(materialMessage, definition)
                        : $"{(craftingSelected ? "合成 " + GetCraftingBonusLabel(definition) : $"消耗 {effectiveCost} 红石")} · 选择发光的{target}";
                    CreateText(_inspectorRoot, "DeployHint", new Vector2(0, -174), new Vector2(246, 48), paymentHint, 13,
                        craftingSelected && !hasMaterials ? Danger : Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
                else
                    CreateText(_inspectorRoot, "DeployHint", new Vector2(0, -118), new Vector2(246, 64), isDiscounted
                        ? $"考古触发 · 费用 {definition.cost} → {effectiveCost}\n选择发光的{target}完成部署"
                        : $"选择发光的{target}完成部署", 15, isDiscounted ? Gold : Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            else
            {
                var implemented = definition.effectImplementationStatus == "IMPLEMENTED";
                var targeting = _pendingTargetCardId == definition.id;
                var requiresTarget = DemoCardTargeting.TryGetRule(definition, out var targetRule);
                var hasLegalTarget = !requiresTarget || DemoCardTargeting.HasLegalTarget(match, targetRule);
                var actionLabel = !implemented ? "效果尚未接入" : targeting ? "取消目标选择" : !hasLegalTarget ? "没有合法目标" : requiresTarget ? targetRule.ActionLabel : definition.cardType == "EQUIPMENT" ? "装备武器" : "释放卡牌";
                var cast = CreateSecondaryButton(_inspectorRoot, "Cast", new Vector2(0, -118), new Vector2(235, 60), actionLabel, 17);
                var canPlay = match.IsPlayerTurn && match.Phase == DemoTurnPhase.Main && match.Hand.Contains(definition.id) && effectiveCost <= match.Energy;
                cast.interactable = targeting || (implemented && hasLegalTarget && canPlay && (!IsOnlineBoard || _onlineSession.CanIssueCommand));
                cast.onClick.AddListener(targeting ? (UnityEngine.Events.UnityAction)CancelTargetSelection : CastSelectedCard);
                cast.gameObject.AddComponent<DemoHoverScale>().Configure(1.04f, 16f);
            }
            var implementationLabel = definition.effectImplementationStatus == "IMPLEMENTED" ? "已接入" : definition.effectImplementationStatus == "NONE" ? "无额外效果" : "待接入";
            var targetedDeployment = deployType && DemoCardTargeting.TryGetRule(definition, out _);
            var implementationY = deployType && (definition.hasCraftingRecipe || targetedDeployment) ? -236 : -190;
            var implementationText = isDiscounted
                ? $"考古联动：本回合费用 -1（{definition.cost} → {effectiveCost}）\n稳定效果槽：{string.Join(", ", definition.effectIds ?? Array.Empty<string>())}"
                : definition.hasCraftingRecipe
                ? $"配方：已接入 · {definition.recipeId}\n卡牌效果：{implementationLabel}\n{string.Join(", ", definition.effectIds ?? Array.Empty<string>())}"
                : $"规则状态：{implementationLabel}\n稳定效果槽：{string.Join(", ", definition.effectIds ?? Array.Empty<string>())}";
            CreateText(_inspectorRoot, "Implementation", new Vector2(0, implementationY), new Vector2(250, 68), implementationText,
                definition.hasCraftingRecipe ? 11 : 12, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
        }

        private DemoDeploymentPreview EvaluateSelectedDeployment(DemoSlotKind kind, int index)
        {
            if (string.IsNullOrEmpty(_selectedCardId) || !_registry.TryGetDefinition(_selectedCardId, out var definition))
                return new DemoDeploymentPreview(false, 1, "请先选择一张战场部署牌。");
            return DemoDeploymentRules.Evaluate(MatchView, definition, kind, index, _selectedPaymentMethod);
        }

        private bool IsPreviewingDeployment(bool player, out int occupiedSlots)
        {
            occupiedSlots = 1;
            if (!player || !string.IsNullOrEmpty(_pendingTargetCardId) || MatchView.Phase != DemoTurnPhase.Main ||
                string.IsNullOrEmpty(_selectedCardId) || !_registry.TryGetDefinition(_selectedCardId, out var definition)) return false;
            if (definition.cardType != "UNIT" && definition.cardType != "BUILDING" && definition.cardType != "STRUCTURE") return false;
            if (DemoCardTargeting.TryGetRule(definition, out var targetRule) && FindSelectedDeploymentTarget() == null &&
                (!IsDrowned(definition) || HasPotentialDrownedBattlecrySlot() && DemoCardTargeting.HasLegalTarget(MatchView, targetRule))) return false;
            occupiedSlots = definition.cardType == "UNIT" ? 1 : Mathf.Max(1, definition.buildingSlots);
            return true;
        }

        private void OnSlotHovered(bool player, DemoSlotKind kind, int index, bool hovered)
        {
            if (MatchView.PendingChoice != null) return;
            var preview = EvaluateSelectedDeployment(kind, index);
            var isDeployment = IsPreviewingDeployment(player, out var occupiedSlots);
            var rejected = isDeployment && !preview.IsLegal;
            _battlefield.SetSlotRangeHovered(player, kind, index, isDeployment ? occupiedSlots : 1, hovered, rejected);
            if (hovered && isDeployment)
            {
                if (!rejected && _registry.TryGetDefinition(_selectedCardId, out var definition) && IsDrowned(definition))
                {
                    var selectedTarget = FindSelectedDeploymentTarget();
                    ShowStatus(DrownedBattlecryActivatesAt(index)
                        ? selectedTarget == null
                            ? "金色地表：此位置相邻水生友军，部署前需要选择敌方战吼目标。"
                            : $"金色地表：部署后将对 {GetCardName(selectedTarget.CardId)} 造成 1 点伤害。"
                        : "普通地表：可部署溺尸，但此位置没有相邻水生友军，战吼不会触发。", false);
                }
                else ShowStatus(preview.Message, rejected);
            }
        }

        private void OnSlotPressed(bool player, DemoSlotKind kind, int index, bool pressed)
        {
            if (MatchView.PendingChoice != null) return;
            var preview = EvaluateSelectedDeployment(kind, index);
            var isDeployment = IsPreviewingDeployment(player, out var occupiedSlots);
            _battlefield.SetSlotRangePressed(player, kind, index, isDeployment ? occupiedSlots : 1, pressed, isDeployment && !preview.IsLegal);
        }

        private void SelectCard(string cardId)
        {
            if (MatchView.PendingChoice != null) return;
            _pendingTargetCardId = null;
            _selectedDeploymentTargetInstanceId = null;
            _selectedCardId = cardId;
            _selectedPaymentMethod = MatchPaymentMethods.Redstone;
            RefreshAll();
            if (_registry.TryGetText(cardId, out var text)) ShowStatus($"已选择：{text.name}", false);
        }

        private async void OnSlotClicked(bool player, DemoSlotKind kind, int index)
        {
            if (MatchView.PendingChoice != null)
            {
                var choice = MatchView.PendingChoice;
                if (choice.kind == "MOVE_UNIT" && MatchView.IsChoiceOwner && kind == DemoSlotKind.Unit)
                {
                    var targetIsPlayer = MatchView.PlayerBattlefield.Any(value => value != null && value.InstanceId == choice.targetInstanceId);
                    var option = (choice.options ?? Array.Empty<PendingChoiceOptionDto>()).FirstOrDefault(value =>
                        value != null && value.selectable && value.slotIndex == index && player == targetIsPlayer);
                    if (option != null) ResolveMovementChoice(option.optionIndex);
                }
                return;
            }
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
            if (DemoCardTargeting.TryGetRule(definition, out var deploymentTargetRule) && FindSelectedDeploymentTarget() == null &&
                (!IsDrowned(definition) || DrownedBattlecryActivatesAt(index) && DemoCardTargeting.HasLegalTarget(MatchView, deploymentTargetRule)))
            {
                CastSelectedCard();
                return;
            }
            var deploymentPreview = DemoDeploymentRules.Evaluate(MatchView, definition, kind, index, _selectedPaymentMethod);
            if (!deploymentPreview.IsLegal)
            {
                ShowStatus(deploymentPreview.Message, true);
                RefreshAll();
                return;
            }
            if (IsOnlineBoard)
            {
                var onlineResult = await SendOnline(() => _onlineSession.DeployAsync(
                    _selectedCardId, kind, index, _selectedPaymentMethod,
                    deploymentTargetRule?.TargetType ?? string.Empty,
                    _selectedDeploymentTargetInstanceId ?? string.Empty));
                if (onlineResult?.Outcome == MatchCommandOutcome.Accepted)
                {
                    _selectedCardId = MatchView.Hand.FirstOrDefault();
                    _selectedPaymentMethod = MatchPaymentMethods.Redstone;
                    _selectedDeploymentTargetInstanceId = null;
                }
                RefreshAll();
                return;
            }
            var crafted = _selectedPaymentMethod == MatchPaymentMethods.Crafting;
            var command = _match.CreateDeployCommand(
                _selectedCardId, kind, index, _selectedPaymentMethod,
                deploymentTargetRule?.TargetType ?? string.Empty,
                _selectedDeploymentTargetInstanceId ?? string.Empty);
            var result = _match.ApplyDeploy(definition, command);
            if (result.Accepted)
            {
                _selectedCardId = MatchView.Hand.FirstOrDefault();
                _selectedPaymentMethod = MatchPaymentMethods.Redstone;
                _selectedDeploymentTargetInstanceId = null;
                if (crafted) StartCoroutine(ShowTurnBanner("合成完成", Cyan));
            }
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
            var heroSelected = _selectedAttackerInstanceId == MatchAttackerIds.Hero;
            if (selected == null && !heroSelected)
            {
                ShowStatus("请先选择一个发光的己方生物，或点击左下角英雄。", true);
                return;
            }
            var target = match.GetObject(false, kind, index);
            if (target == null)
            {
                ShowStatus("该敌方格为空。", true);
                return;
            }
            var targetType = kind == DemoSlotKind.Unit ? "UNIT" : "BUILDING";
            if (!match.CanAttackTarget(target, targetType, out var targetMessage))
            {
                ShowStatus(targetMessage, true);
                return;
            }
            ResolveAttack(heroSelected ? MatchAttackerIds.Hero : selected.InstanceId, selected, targetType, target.InstanceId);
        }

        private void SelectHeroAttacker()
        {
            if (MatchView.Phase != DemoTurnPhase.Combat) return;
            if (!MatchView.CanAttackWithHero(out var message))
            {
                ShowStatus(message, true);
                return;
            }
            ClearSelectedAttackerHighlight();
            _selectedAttackerInstanceId = MatchAttackerIds.Hero;
            ShowStatus($"已选择英雄：{GetCardName(MatchView.PlayerEquipment.CardId)}（{MatchView.PlayerEquipment.Attack} 攻击）", false);
            RefreshAll();
        }

        private void AttackOpponentHero()
        {
            if (MatchView.PendingChoice != null) return;
            if (MatchView.Phase != DemoTurnPhase.Combat) return;
            var selected = FindSelectedAttacker();
            var heroSelected = _selectedAttackerInstanceId == MatchAttackerIds.Hero;
            if (selected == null && !heroSelected)
            {
                ShowStatus("请先选择一个发光的己方生物，或点击左下角英雄。", true);
                return;
            }
            if (!MatchView.CanAttackTarget(null, "HERO", out var targetMessage))
            {
                ShowStatus(targetMessage, true);
                return;
            }
            ResolveAttack(heroSelected ? MatchAttackerIds.Hero : selected.InstanceId, selected, "HERO", string.Empty);
        }

        private async void ResolveAttack(string attackerInstanceId, DemoBattlefieldObject attacker, string targetType, string targetInstanceId)
        {
            if (IsOnlineBoard)
            {
                var onlineResult = await SendOnline(() => _onlineSession.AttackAsync(attackerInstanceId, targetType, targetInstanceId));
                if (onlineResult?.Outcome == MatchCommandOutcome.Accepted)
                {
                    if (attacker != null) _battlefield.SetSlotPressed(true, DemoSlotKind.Unit, attacker.SlotIndex, false);
                    _selectedAttackerInstanceId = null;
                }
                RefreshAll();
                return;
            }
            var knownInstanceIds = new HashSet<string>(_match.PlayerBattlefield.Concat(_match.OpponentBattlefield)
                .Where(value => value != null)
                .Select(value => value.InstanceId));
            var command = _match.CreateAttackCommand(attackerInstanceId, targetType, targetInstanceId);
            var result = _match.ApplyAttack(command);
            if (result.Accepted)
            {
                if (attacker != null) _battlefield.SetSlotPressed(true, DemoSlotKind.Unit, attacker.SlotIndex, false);
                _selectedAttackerInstanceId = null;
            }
            ShowStatus(result.Accepted ? $"{result.Message} · 状态 r{result.Revision}" : result.Message, !result.Accepted);
            RefreshAll();
            if (result.Accepted)
            {
                var summoned = _match.PlayerBattlefield.Concat(_match.OpponentBattlefield)
                    .FirstOrDefault(value => value != null && !knownInstanceIds.Contains(value.InstanceId));
                if (summoned != null) StartCoroutine(PulseBattlefieldObject(summoned.InstanceId));
            }
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

        private string FormatEquipment(DemoEquipment equipment) => equipment == null
            ? "装备槽 · 未装备"
            : $"{GetCardName(equipment.CardId)}  ·  ⚔ {equipment.Attack}  ◆ {equipment.Durability}/{equipment.MaxDurability}";

        private string ReplaceCardIdsWithNames(string message, CardDefinitionEntry definition)
        {
            var result = message ?? string.Empty;
            foreach (var ingredient in definition?.craftingRecipe ?? Array.Empty<CraftingIngredientEntry>())
                result = result.Replace(ingredient.cardId, GetCardName(ingredient.cardId));
            return result;
        }

        private static string GetCraftingBonusLabel(CardDefinitionEntry definition)
        {
            var bonuses = new List<string>();
            if (definition.craftedAttackBonus > 0) bonuses.Add($"+{definition.craftedAttackBonus} 攻击");
            if (definition.craftedHealthBonus > 0) bonuses.Add($"+{definition.craftedHealthBonus} 最大生命");
            if (definition.craftedDurabilityBonus > 0) bonuses.Add($"+{definition.craftedDurabilityBonus} 耐久");
            return bonuses.Count > 0 ? string.Join(" / ", bonuses) : "无额外属性";
        }

        private void SelectPaymentMethod(string paymentMethod)
        {
            if (MatchView.PendingChoice != null) return;
            _selectedPaymentMethod = paymentMethod;
            if (_registry.TryGetDefinition(_selectedCardId, out var definition) && paymentMethod == MatchPaymentMethods.Crafting)
            {
                var ready = DemoDeploymentRules.CanPayWithCrafting(MatchView, definition, out var message);
                ShowStatus(ready ? "合成支付已选择：材料充足。" : ReplaceCardIdsWithNames(message, definition), !ready);
            }
            else ShowStatus("红石支付已选择。", false);
            RefreshAll();
        }

        private async void CastSelectedCard()
        {
            if (MatchView.PendingChoice != null) return;
            if (string.IsNullOrEmpty(_selectedCardId) || !_registry.TryGetDefinition(_selectedCardId, out var definition)) return;
            if (DemoCardTargeting.TryGetRule(definition, out var targetRule))
            {
                if (!DemoCardTargeting.HasLegalTarget(MatchView, targetRule))
                {
                    ShowStatus(targetRule.MissingTargetMessage, true);
                    return;
                }
                _pendingTargetCardId = definition.id;
                ShowStatus(targetRule.SelectionPrompt, false);
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
            if (!_registry.TryGetDefinition(_pendingTargetCardId, out var definition) ||
                !DemoCardTargeting.TryGetRule(definition, out var targetRule))
            {
                CancelTargetSelection();
                return;
            }
            var target = MatchView.GetObject(player, kind, index);
            if (!targetRule.IsLegal(player, kind, target))
            {
                ShowStatus(targetRule.SelectionPrompt, true);
                return;
            }
            var deployType = definition.cardType == "UNIT" || definition.cardType == "BUILDING" || definition.cardType == "STRUCTURE";
            if (deployType)
            {
                _selectedDeploymentTargetInstanceId = target.InstanceId;
                _pendingTargetCardId = null;
                ShowStatus($"战吼目标已锁定：{GetCardName(target.CardId)}。现在选择一个发光的己方部署格。", false);
                RefreshAll();
                return;
            }
            if (IsOnlineBoard)
            {
                var onlineResult = await SendOnline(() => _onlineSession.PlayCardAsync(definition.id, targetRule.TargetType, target.InstanceId));
                if (onlineResult?.Outcome == MatchCommandOutcome.Accepted)
                {
                    _pendingTargetCardId = null;
                    _selectedCardId = MatchView.Hand.FirstOrDefault();
                }
                RefreshAll();
                return;
            }
            var command = _match.CreatePlayCardCommand(definition.id, targetRule.TargetType, target.InstanceId);
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

        private bool IsValidPendingCardTarget(bool player, DemoSlotKind kind, DemoBattlefieldObject target)
        {
            if (!_registry.TryGetDefinition(_pendingTargetCardId, out var definition) ||
                !DemoCardTargeting.TryGetRule(definition, out var targetRule)) return false;
            return targetRule.IsLegal(player, kind, target);
        }

        private DemoBattlefieldObject FindSelectedDeploymentTarget()
        {
            if (string.IsNullOrEmpty(_selectedDeploymentTargetInstanceId)) return null;
            return MatchView.PlayerBattlefield.Concat(MatchView.OpponentBattlefield)
                .FirstOrDefault(value => value != null && value.InstanceId == _selectedDeploymentTargetInstanceId && value.Health > 0);
        }

        private static bool IsDrowned(CardDefinitionEntry definition) =>
            definition?.effectIds?.Contains("effect.or_003.01") == true;

        private bool DrownedBattlecryActivatesAt(int slotIndex)
        {
            return MatchView.PlayerBattlefield.Any(value => value != null && value.Health > 0 &&
                value.SlotKind == DemoSlotKind.Unit && Mathf.Abs(value.SlotIndex - slotIndex) == 1 &&
                _registry.TryGetDefinition(value.CardId, out var adjacentDefinition) &&
                (adjacentDefinition.tags ?? Array.Empty<string>()).Contains("aquatic"));
        }

        private bool HasPotentialDrownedBattlecrySlot()
        {
            for (var slotIndex = 0; slotIndex < MatchView.UnitSlots.Length; slotIndex++)
                if (string.IsNullOrEmpty(MatchView.UnitSlots[slotIndex]) && DrownedBattlecryActivatesAt(slotIndex)) return true;
            return false;
        }

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

                if (MatchView.IsMulligan && !MatchView.PlayerMulliganCompleted)
                {
                    var mulliganResult = await SendOnline(() => _onlineSession.MulliganAsync(Array.Empty<int>()));
                    if (mulliganResult?.Outcome != MatchCommandOutcome.Accepted)
                        throw new InvalidOperationException("The Unity client did not receive an accepted MULLIGAN acknowledgement.");
                }
                while (MatchView.IsMulligan && Time.realtimeSinceStartup < deadline) await Task.Yield();
                if (MatchView.IsMulligan) throw new TimeoutException("Both Unity clients did not finish opening hand selection.");

                var actionStartRevision = MatchView.Revision;
                if (performAction && MatchView.IsPlayerTurn && MatchView.Phase == DemoTurnPhase.Main)
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
                    var expectedActionRevision = actionStartRevision + 2;
                    while (MatchView.Revision < expectedActionRevision && Time.realtimeSinceStartup < deadline) await Task.Yield();
                    if (MatchView.Revision < expectedActionRevision)
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
                        matchStatus = GameCompositionRoot.Instance.MatchStateStore.Current.status,
                        playerMulliganCompleted = MatchView.PlayerMulliganCompleted,
                        opponentMulliganCompleted = MatchView.OpponentMulliganCompleted,
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

        private IEnumerator PulseBattlefieldObject(string instanceId)
        {
            if (string.IsNullOrEmpty(instanceId) || _battlefield == null) yield break;
            var target = MatchView.PlayerBattlefield.Concat(MatchView.OpponentBattlefield)
                .FirstOrDefault(value => value != null && value.InstanceId == instanceId);
            if (target == null) yield break;
            for (var index = target.SlotIndex; index < target.SlotIndex + target.OccupiedSlots; index++)
            {
                _battlefield.SetSlotState(target.Player, target.SlotKind, index, true, true);
                _battlefield.SetSlotPressed(target.Player, target.SlotKind, index, true);
            }
            yield return new WaitForSecondsRealtime(0.2f);
            for (var index = target.SlotIndex; index < target.SlotIndex + target.OccupiedSlots; index++)
                _battlefield.SetSlotPressed(target.Player, target.SlotKind, index, false);
            RefreshAll();
        }

        private async void OnEndTurn()
        {
            var match = MatchView;
            if (match.PendingChoice != null) return;
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
            if (battlefieldObject?.HasKeyword("TAUNT") == true) stats = $"◆ 嘲讽   {stats}";
            else if (battlefieldObject?.HasKeyword("CHARGE") == true) stats = $"➤ 冲锋   {stats}";
            var slow = (battlefieldObject?.Statuses ?? Array.Empty<BattlefieldStatusStateDto>())
                .FirstOrDefault(value => value != null && value.statusId == "SLOW");
            if (slow != null)
            {
                stats = $"缓慢 {slow.remainingDuration} · {stats}";
                accent = Cyan;
            }
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
            public string matchStatus;
            public bool playerMulliganCompleted;
            public bool opponentMulliganCompleted;
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
