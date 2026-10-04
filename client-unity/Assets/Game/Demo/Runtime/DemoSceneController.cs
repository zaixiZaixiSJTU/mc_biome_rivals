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
    public sealed partial class DemoSceneController : MonoBehaviour
    {
        private enum MatchEndOutcome
        {
            Draw,
            PlayerWon,
            PlayerLost
        }

        private const float ReferenceWidth = 1920f;
        private const float ReferenceHeight = 1080f;
        private const float ChoiceOverlayEntranceDuration = 0.18f;
        private const float InactiveHeroHudAlpha = 0.78f;
        private const float FullHandLayoutCoordinateTolerance = 0.5f;
        private const int WorldLabelFontSize = 15;
        private const int WorldLabelMinimumFontSize = 12;

        private static readonly Color Ink = Hex("#0E100E");
        private static readonly Color Panel = Hex("#20231F");
        private static readonly Color Pale = Hex("#F1E6CB");
        private static readonly Color Muted = Hex("#B2AA96");
        private static readonly Color Cyan = Hex("#5AAE9F");
        private static readonly Color Leaf = Hex("#91C25A");
        private static readonly Color Gold = Hex("#E4B95F");
        private static readonly Color Ember = Hex("#D98545");
        private static readonly Color Danger = Hex("#E05A47");

        private DemoLocalMatch _match = new DemoLocalMatch();
        private readonly List<SlotView> _playerUnitSlots = new List<SlotView>();
        private readonly List<SlotView> _playerBuildingSlots = new List<SlotView>();
        private readonly List<SlotView> _opponentUnitSlots = new List<SlotView>();
        private readonly List<SlotView> _opponentBuildingSlots = new List<SlotView>();
        private readonly List<FactionButtonView> _factionButtons = new List<FactionButtonView>();
        private readonly List<IMatchEventPresenter> _eventPresenters = new List<IMatchEventPresenter>();
        private readonly HashSet<int> _mulliganSelectedIndices = new HashSet<int>();
        private readonly HashSet<string> _renderedHandCardInstanceIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly List<string> _selectedCardTargetInstanceIds = new List<string>();
        private readonly Dictionary<string, int> _pendingOnlineDamagePopups = new Dictionary<string, int>(StringComparer.Ordinal);

        private CardContentRegistry _registry;
        private DemoBattlefield3D _battlefield;
        private RectTransform _canvasRoot;
        private RectTransform _handRoot;
        private RectTransform _opponentHandRoot;
        private CanvasGroup _handCanvasGroup;
        private RectTransform _inspectorRoot;
        private CardDetailsView _cardDetailsView;
        private RectTransform _opponentHud;
        private RectTransform _playerHud;
        private Text _energyText;
        private Text _opponentEnergyText;
        private Text _handLabel;
        private Text _opponentHealthText;
        private Button _opponentHeroTargetButton;
        private Button _playerHeroButton;
        private Text _previousOpponentFactionLabelText;
        private Text _nextOpponentFactionLabelText;
        private Text _playerEquipmentText;
        private Text _opponentEquipmentText;
        private Image _opponentAvatarImage;
        private Image _opponentAvatarIcon;
        private Text _opponentAvatarGlyph;
        private Text _opponentNameText;
        private Text _opponentFactionLabel;
        private CanvasGroup _opponentEffectFlash;
        private CanvasGroup _opponentHeroControlAlpha;
        private Text _playerHealthText;
        private Image _playerAvatarImage;
        private Image _playerAvatarIcon;
        private Text _playerAvatarGlyph;
        private Text _playerNameText;
        private CanvasGroup _playerEffectFlash;
        private CanvasGroup _playerHeroControlAlpha;
        private Text _roundText;
        private Text _titleText;
        private Text _statusText;
        private Text _endTurnLabel;
        private Button _endTurnButton;
        private CanvasGroup _turnBanner;
        private Text _turnBannerText;
        private int _turnBannerSequence;
        private Vector2 _turnBannerRestingPosition;
        private bool _hasTurnBannerRestingPosition;
        private Text _onlineStatusText;
        private Text _accountStatusText;
        private Text _deckStatusText;
        private Text _onlineActionLabel;
        private Button _onlineActionButton;
        private Button _previousOpponentFactionButton;
        private Button _nextOpponentFactionButton;
        private RectTransform _mulliganOverlay;
        private RectTransform _mulliganCardsRoot;
        private Text _mulliganStatusText;
        private Text _mulliganConfirmLabel;
        private Button _mulliganConfirmButton;
        private RectTransform _choiceOverlay;
        private RectTransform _choicePanel;
        private CanvasGroup _choiceOverlayCanvasGroup;
        private bool _choiceOverlayEntranceActive;
        private float _choiceOverlayEntranceElapsed;
        private RectTransform _choiceCardsRoot;
        private Text _choiceTitleText;
        private Text _choiceRuleText;
        private Text _choiceStatusText;
        private Text _choiceConfirmLabel;
        private Button _choiceConfirmButton;
        private IMatchGateway _onlineGateway;
        private IPlayerAccountService _accountService;
        private DemoOnlineMatchSession _onlineSession;
        private Image _opponentTint;
        private Image _playerTint;
        private string _selectedCardId;
        private string _selectedHandCardInstanceId;
        private string _selectedPaymentMethod = MatchPaymentMethods.Redstone;
        private string _selectedAttackerInstanceId;
        private string _pendingTargetCardId;
        private string _pendingOnlineAttackTargetInstanceId;
        private string _pendingOnlineAttackAttackerInstanceId;
        private string _selectedDeploymentTargetInstanceId;
        private string _activeFaction = "plains_forest";
        private string _opponentFaction = "nether";
        private bool _built;
        private bool _previewMulligan;
        private bool _previewOnlineStatus;
        private bool _showRuleDiagnostics;
        private bool _returnPreviewUiBlocked;
        private bool _returnPreviewCompleted;
        private bool _combatPreviewCompleted;
        private bool _spiderPreviewCompleted;
        private bool _guardianPosePreviewCompleted;
        private bool _babySheepPreviewCompleted;
        private bool _blazePosePreviewCompleted;
        private bool _craftingInteractionPreviewCompleted;
        private bool _matchOutcomePreviewCompleted;
        private bool _structureDragPreviewCompleted;
        private bool _cardArrivalPreviewCompleted;
        private bool _choiceInteractionPreviewCompleted;
        private bool _unaffordableCardSelectionPreviewCompleted;
        private bool _hasRenderedHandSnapshot;
        private int _selectedChoiceOptionIndex = -1;
        private string _renderedChoiceId;
        private Font _font;
        private DemoHudMaterialFactory _hudMaterialFactory;

        private bool IsOnlineBoard => _onlineSession?.HasAuthoritativeState == true;
        private bool HasPendingOnlineCommand => _onlineSession?.HasPendingCommand == true;
        private bool IsFactionSelectionLocked => _previewOnlineStatus || MatchView.IsFinished || MatchView.PendingChoice != null || (_onlineGateway != null &&
            _onlineGateway.CurrentStatus.Phase != MatchConnectionPhase.Offline &&
            _onlineGateway.CurrentStatus.Phase != MatchConnectionPhase.Failed);
        private IDemoMatchView MatchView => IsOnlineBoard ? _onlineSession.View : _match;

        private static readonly FactionSpec[] Factions =
        {
            new FactionSpec("plains_forest", "平原", "pf", "林地守护者", "pf_008"),
            new FactionSpec("desert_badlands", "沙漠", "db", "荒漠考古队", "db_003"),
            new FactionSpec("snow_ice", "冰原", "si", "雪原巡守者", "si_005"),
            new FactionSpec("cave_dark_forest", "深暗", "cd", "幽匿勘探队", "cd_005"),
            new FactionSpec("ocean_river", "海洋", "or", "潮汐守卫", "or_004"),
            new FactionSpec("nether", "下界", "nt", "熔岩统御者", "nt_003"),
            new FactionSpec("end", "末地", "ed", "虚空行者", "ed_003")
        };

        private void Start()
        {
            Application.runInBackground = true;
            if (HasCommandLineFlag("-disableLocalCardArt")) DemoCardArtProvider.LocalArtEnabled = false;
            if (HasCommandLineFlag("-disableLocalWorldAssets")) DemoWorldAssetProvider.LocalAssetsEnabled = false;
            if (HasCommandLineFlag("-captureEntityCatalogue"))
            {
                StartEntityCatalogueCapture();
                return;
            }
            _showRuleDiagnostics = HasCommandLineFlag("-showRuleDiagnostics");
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
            if (HasCommandLineFlag("-previewFullHand")) SetupFullHandPreview();
            if (HasCommandLineFlag("-previewHandState")) SetupHandReadabilityStatePreview(GetCommandLineValue("-previewHandState"));
            else if (HasCommandLineFlag("-previewTargeting"))
            {
                SelectFaction("snow_ice");
                SelectCard("si_001");
                CastSelectedCard();
            }
            if (HasCommandLineFlag("-previewOpponentEnergy")) SetupOpponentEnergyPreview();
            if (HasCommandLineFlag("-previewSummon")) SetupSummonPreview();
            else if (HasCommandLineFlag("-previewDeathrattle")) SetupDeathrattlePreview();
            else if (HasCommandLineFlag("-previewTaunt")) SetupTauntPreview();
            else if (HasCommandLineFlag("-previewStructurePlacementInvalid")) SetupStructurePlacementPreview(2);
            else if (HasCommandLineFlag("-previewStructurePlacement")) SetupStructurePlacementPreview(0);
            else if (HasCommandLineFlag("-previewStructureDeployed")) SetupStructureDeployedPreview();
            else if (HasCommandLineFlag("-previewCraftingInteraction")) StartCoroutine(SetupCraftingInteractionPreview());
            else if (HasCommandLineFlag("-previewMatchOutcome")) SetupMatchOutcomePreview();
            else if (HasCommandLineFlag("-previewTerminalWorld")) SetupTerminalWorldPreview();
            else if (HasCommandLineFlag("-previewTntTrapOwnerWins")) SetupTntTrapOwnerWinsPreview();
            else if (HasCommandLineFlag("-previewCraftingMissing")) SetupCraftingPreview(false);
            else if (HasCommandLineFlag("-previewCrafting")) SetupCraftingPreview(true);
            else if (HasCommandLineFlag("-previewRaiderDiscount")) SetupRaiderDiscountPreview();
            else if (HasCommandLineFlag("-previewArchaeology")) StartCoroutine(SetupArchaeologyPreview());
            else if (HasCommandLineFlag("-previewChoiceInteraction")) StartCoroutine(SetupChoiceInteractionPreview());
            else if (HasCommandLineFlag("-previewBatScry")) SetupBatScryPreview();
            else if (HasCommandLineFlag("-previewCaveSpiderPoison")) StartCoroutine(SetupCaveSpiderPoisonPreview());
            else if (HasCommandLineFlag("-previewBlazeFire")) SetupBlazeFirePreview();
            else if (HasCommandLineFlag("-previewDarkness")) SetupDarknessPreview();
            else if (HasCommandLineFlag("-previewAbandonedMine")) SetupAbandonedMinePreview();
            else if (HasCommandLineFlag("-previewWoodlandMansion")) SetupWoodlandMansionPreview();
            else if (HasCommandLineFlag("-previewSummonReadiness")) SetupSummonReadinessPreview();
            else if (HasCommandLineFlag("-previewLoot")) SetupLootPreview();
            else if (HasCommandLineFlag("-previewTamedWolf")) SetupTamedWolfPreview();
            else if (HasCommandLineFlag("-previewVillagerFarmer")) SetupVillagerFarmerPreview();
            else if (HasCommandLineFlag("-previewWoodlandNursery")) SetupWoodlandNurseryPreview();
            else if (HasCommandLineFlag("-previewBreedingSeason")) SetupBreedingSeasonPreview();
            else if (HasCommandLineFlag("-previewWoodlandRally")) SetupWoodlandRallyPreview();
            else if (HasCommandLineFlag("-previewIronGolem")) SetupIronGolemPreview();
            else if (HasCommandLineFlag("-previewPolarBearWool") || HasCommandLineFlag("-previewPolarBearPose")) SetupPolarBearWoolPreview();
            else if (HasCommandLineFlag("-previewVindicator")) SetupVindicatorPreview();
            else if (HasCommandLineFlag("-previewCactusFence")) SetupCactusFencePreview();
            else if (HasCommandLineFlag("-previewDesertTemple")) SetupDesertTemplePreview();
            else if (HasCommandLineFlag("-previewDungeonSkeleton")) SetupDungeonSkeletonPreview();
            else if (HasCommandLineFlag("-previewStray")) SetupStrayPreview();
            else if (HasCommandLineFlag("-previewSnowGolem")) SetupSnowGolemPreview();
            else if (HasCommandLineFlag("-previewEquipment")) SetupEquipmentPreview();
            else if (HasCommandLineFlag("-previewPrismarineShard")) SetupPrismarineShardPreview();
            else if (HasCommandLineFlag("-previewTurtlePose")) SetupTurtlePosePreview();
            else if (HasCommandLineFlag("-previewGuardianPose")) StartCoroutine(SetupGuardianPosePreview());
            else if (HasCommandLineFlag("-previewBabySheepPose")) StartCoroutine(SetupBabySheepPosePreview());
            else if (HasCommandLineFlag("-previewBlazePose")) StartCoroutine(SetupBlazePosePreview());
            else if (HasCommandLineFlag("-previewDesertVillagerSurface")) SetupDesertVillagerSurfacePreview();
            else if (HasCommandLineFlag("-previewTurtleAura")) SetupTurtleAuraPreview();
            else if (HasCommandLineFlag("-previewCoralReef")) SetupCoralReefPreview();
            else if (HasCommandLineFlag("-previewOceanMonument")) SetupOceanMonumentPreview();
            else if (HasCommandLineFlag("-previewGuardianReaction")) SetupGuardianReactionPreview();
            else if (HasCommandLineFlag("-previewDrownedAdjacency")) SetupDrownedAdjacencyPreview();
            else if (HasCommandLineFlag("-previewDolphinCurrent")) SetupDolphinCurrentPreview();
            else if (HasCommandLineFlag("-previewWaterCurrent")) SetupWaterCurrentPreview();
            else if (HasCommandLineFlag("-previewSlow")) SetupSlowPreview();
            else if (HasCommandLineFlag("-previewIceSpire")) SetupIceSpirePreview();
            else if (HasCommandLineFlag("-previewGoat")) SetupGoatPreview();
            else if (HasCommandLineFlag("-previewSnowHut")) SetupSnowHutPreview();
            else if (HasCommandLineFlag("-previewEndCrystal")) SetupEndCrystalPreview();
            else if (HasCommandLineFlag("-previewNetherStatusSummon")) SetupNetherStatusSummonPreview();
            else if (HasCommandLineFlag("-previewNetherTriggerLifecycle")) SetupNetherTriggerLifecyclePreview();
            else if (HasCommandLineFlag("-previewEndReturnInteraction")) StartCoroutine(SetupEndReturnInteractionPreview());
            else if (HasCommandLineFlag("-previewCombatInteraction")) StartCoroutine(SetupCombatInteractionPreview());
            else if (HasCommandLineFlag("-previewStructureDragDeployment")) StartCoroutine(SetupStructureDragDeploymentPreview());
            else if (HasCommandLineFlag("-previewCardArrival")) StartCoroutine(SetupCardArrivalPreview());
            else if (HasCommandLineFlag("-previewRespawnAnchor")) SetupRespawnAnchorPreview();
            else if (HasCommandLineFlag("-previewPiglinMagma")) SetupPiglinMagmaPreview();
            else if (HasCommandLineFlag("-previewAttackFeedback")) SetupAttackFeedbackPreview();
            else if (HasCommandLineFlag("-previewButtonFeedback")) SetupButtonFeedbackPreview();
            else if (HasCommandLineFlag("-previewCombat")) OnEndTurn();
            if (HasCommandLineFlag("-previewGroundHover")) _battlefield.SetSlotHovered(true, DemoSlotKind.Unit, 0, true);
            if (HasCommandLineFlag("-previewGroundReturnPulse"))
            {
                var emptyUnitSlot = DemoDeploymentRules.FindFirstEmptyUnitSlot(
                    MatchView.PlayerBattlefield, _battlefield.GetSlotCount(DemoSlotKind.Unit));
                var pulseSlot = emptyUnitSlot >= 0 ? emptyUnitSlot : 0;
                _battlefield.PulseSlotRange(true, DemoSlotKind.Unit, pulseSlot, 1, Hex("#FFE27A"), 10f);
                ShowStatus($"回手反馈预览：己方单位格 {pulseSlot + 1} 呈现末影地表脉冲。", false);
                Debug.Log($"Ground return pulse preview settled: {emptyUnitSlot >= 0}");
            }
            if (HasCommandLineFlag("-previewHandHover")) StartCoroutine(PreviewHandCardHoverAfterUiSettles());
            if (HasCommandLineFlag("-previewUnaffordableCardSelection"))
                StartCoroutine(PreviewUnaffordableCardSelectionAfterUiSettles());
            if (HasCommandLineFlag("-previewOnlineStatus"))
            {
                _previewOnlineStatus = true;
                HandleOnlineConnectionState(new MatchConnectionStatus(
                    MatchConnectionPhase.Reconnecting, "UI preview", "preview-match", 999));
                var factionSelectorsLocked = _factionButtons.All(value => !value.Button.interactable);
                var opponentSelectorsLocked = !_previousOpponentFactionButton.interactable && !_nextOpponentFactionButton.interactable;
                var labelsMuted = _factionButtons.All(value => value.Label.color == Muted) &&
                    _previousOpponentFactionLabelText.color == Muted && _nextOpponentFactionLabelText.color == Muted;
                Debug.Log($"Online status preview settled: reconnect attempt 999; factionSelectorsLocked={factionSelectorsLocked}; opponentSelectorsLocked={opponentSelectorsLocked}; labelsMuted={labelsMuted}");
            }
            if (HasCommandLineFlag("-previewTurnBanner")) StartCoroutine(SetupTurnBannerPreview());
            var capturePath = GetCommandLineValue("-captureDemo");
            if (!string.IsNullOrWhiteSpace(capturePath)) StartCoroutine(CaptureDemo(capturePath));
            var onlineProbePath = GetCommandLineValue("-onlineProbe");
            var captureOnlinePath = GetCommandLineValue("-captureOnline");
            if (HasCommandLineFlag("-aiPlayer")) SetAgentPolicy(new BasicMatchAgentPolicy(_registry));
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
            if (_accountService != null) _accountService.StateChanged -= HandleAccountStateChanged;
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
            TickAgent();
            AdvanceChoiceOverlayEntrance(Time.unscaledDeltaTime);
            if (!Input.GetKeyDown(KeyCode.Escape) && !Input.GetMouseButtonDown(1)) return;
            if (_statusInspectionOpen) { CloseStatusInspection(); return; }
            if (IsHandInspectionOpen) { CloseHandInspection(); return; }
            if (MatchView.IsFinished || MatchView.PendingChoice != null) return;
            if (IsOnlineBoard && !_onlineSession.CanIssueCommand) return;
            CancelCurrentInteraction();
        }

        public void BuildNow()
        {
            if (_built) return;
            _built = true;
            ConfigurePreviewArenaFromCommandLine();
            _registry = CardContentLoader.Current;
            _match.ResetOpponent(GetOpponentDefinitions(_opponentFaction));
            BuildInterface();
            RegisterOnlineEventPresenters();
            ApplyOpponentFactionVisuals(Factions.First(item => item.Id == _opponentFaction));
            SelectFaction(_activeFaction);
            ShowStatus("单位与建筑可拖到发光的战场格，也可先点选再点格部署；法术和材料从右侧释放。", false);
        }

        private void ConfigurePreviewArenaFromCommandLine()
        {
            var arguments = Environment.GetCommandLineArgs();
            for (var index = 0; index < arguments.Length - 1; index++)
            {
                if (!string.Equals(arguments[index], "-previewArena", StringComparison.Ordinal)) continue;
                var arenaId = arguments[index + 1];
                if (!ArenaLayouts.TryGet(arenaId, out _))
                    throw new ArgumentOutOfRangeException(nameof(arenaId), arenaId, "Preview arena is not registered.");
                _match = new DemoLocalMatch(arenaId);
                return;
            }
        }

        private void BuildInterface()
        {
            EnsureEventSystem();
            _battlefield = GetComponent<DemoBattlefield3D>();
            if (_battlefield == null) _battlefield = gameObject.AddComponent<DemoBattlefield3D>();
            _battlefield.ConfigureArena(_match.ArenaId);
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
            // Fit the complete authored canvas, matching the battlefield camera's centered 16:9 viewport.
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
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
            BuildStatusInspection();
        }

        private void BuildTopChrome()
        {
            var titlePlate = CreateBasePanel(_canvasRoot, "TitlePlate", new Vector2(0, 502), new Vector2(510, 58));
            _titleText = CreateText(titlePlate, "Title", Vector2.zero, new Vector2(480, 44), "群系竞逐  ·  本地战场演示", 24, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);

            _opponentHud = CreateBasePanel(_canvasRoot, "OpponentHUD", new Vector2(-760, 455), new Vector2(315, 104));
            _opponentHeroControlAlpha = _opponentHud.gameObject.AddComponent<CanvasGroup>();
            _opponentAvatarImage = CreatePanel(_opponentHud, "Avatar", new Vector2(-112, 0), new Vector2(70, 70), Hex("#5B2020"));
            _opponentAvatarGlyph = CreateText(_opponentHud, "AvatarGlyph", new Vector2(-112, 1), new Vector2(60, 60), "▣", 36, Ember, TextAnchor.MiddleCenter, FontStyle.Bold);
            _opponentAvatarIcon = CreatePanel(_opponentHud, "AvatarIcon", new Vector2(-112, 0), new Vector2(48, 48), Color.white);
            _opponentAvatarIcon.preserveAspect = true;
            _opponentAvatarIcon.raycastTarget = false;
            _opponentAvatarIcon.gameObject.SetActive(false);
            _opponentNameText = CreateText(_opponentHud, "Name", new Vector2(34, 22), new Vector2(190, 32), "熔岩统御者", 20, Pale, TextAnchor.MiddleLeft, FontStyle.Bold);
            _opponentHealthText = CreateText(_opponentHud, "Health", new Vector2(34, -19), new Vector2(190, 30), "❤ 30", 17, Hex("#F4C18A"), TextAnchor.MiddleLeft, FontStyle.Bold);
            var opponentFlash = CreatePanel(_opponentHud, "EffectFlash", Vector2.zero, new Vector2(303, 92), Color.white);
            opponentFlash.raycastTarget = false;
            _opponentEffectFlash = opponentFlash.gameObject.AddComponent<CanvasGroup>();
            _opponentEffectFlash.alpha = 0f;
            _opponentEffectFlash.blocksRaycasts = false;
            var opponentHeroTarget = _opponentHud.gameObject.AddComponent<Button>();
            _opponentHeroTargetButton = opponentHeroTarget;
            opponentHeroTarget.targetGraphic = _opponentHud.GetComponent<Image>();
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
            _previousOpponentFactionButton = CreateSecondaryButton(opponentSelector, "PreviousOpponentFaction", new Vector2(-128, 0), new Vector2(42, 30), "◀", 15);
            _previousOpponentFactionLabelText = _previousOpponentFactionButton.GetComponentInChildren<Text>();
            _opponentFactionLabel = CreateText(opponentSelector, "FactionLabel", Vector2.zero, new Vector2(190, 28), "敌方 · 下界", 14, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
            _nextOpponentFactionButton = CreateSecondaryButton(opponentSelector, "NextOpponentFaction", new Vector2(128, 0), new Vector2(42, 30), "▶", 15);
            _nextOpponentFactionLabelText = _nextOpponentFactionButton.GetComponentInChildren<Text>();
            _previousOpponentFactionButton.onClick.AddListener(() => CycleOpponentFaction(-1));
            _nextOpponentFactionButton.onClick.AddListener(() => CycleOpponentFaction(1));

            var opponentEnergyPlate = CreateBasePanel(_canvasRoot, "OpponentEnergyPlate", new Vector2(-520, 380), new Vector2(170, 54));
            _opponentEnergyText = CreateText(opponentEnergyPlate, "Resource", Vector2.zero, new Vector2(154, 46), "敌方红石 · 下回合 1/1", 13, Hex("#D96A50"), TextAnchor.MiddleCenter, FontStyle.Bold);
            _opponentEnergyText.raycastTarget = false;
            opponentEnergyPlate.GetComponent<Image>().raycastTarget = false;

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
            _playerHeroControlAlpha = _playerHud.gameObject.AddComponent<CanvasGroup>();
            _playerAvatarImage = CreatePanel(_playerHud, "Avatar", new Vector2(-92, 0), new Vector2(70, 70), Hex("#274C2D"));
            _playerAvatarGlyph = CreateText(_playerHud, "AvatarGlyph", new Vector2(-92, 1), new Vector2(60, 60), "▦", 34, Hex("#D3C35B"), TextAnchor.MiddleCenter, FontStyle.Bold);
            _playerAvatarIcon = CreatePanel(_playerHud, "AvatarIcon", new Vector2(-92, 0), new Vector2(48, 48), Color.white);
            _playerAvatarIcon.preserveAspect = true;
            _playerAvatarIcon.raycastTarget = false;
            _playerAvatarIcon.gameObject.SetActive(false);
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
            CreateText(rail, "Header", new Vector2(0, 252), new Vector2(120, 48), "卡 组", 18, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);

            for (var i = 0; i < Factions.Length; i++)
            {
                var spec = Factions[i];
                _registry.TryGetTheme(spec.Id, out var theme);
                var button = CreateSecondaryButton(rail, "Faction_" + spec.Id, new Vector2(0, 188 - i * 67), new Vector2(116, 51), spec.Label, 16);
                var label = button.GetComponentInChildren<Text>();
                var selectionAccent = CreatePanel(button.transform, "SelectionAccent", new Vector2(-54, 0), new Vector2(3, 39), theme.Accent);
                selectionAccent.raycastTarget = false;
                var captured = spec.Id;
                button.onClick.AddListener(() => SelectFaction(captured));
                _factionButtons.Add(new FactionButtonView(spec.Id, button, button.targetGraphic as Image, label, selectionAccent));
            }
        }

        private void BuildOnlineStatus()
        {
            var panel = CreateBasePanel(_canvasRoot, "OnlineStatusPanel", new Vector2(444, 456), new Vector2(358, 84));
            _accountStatusText = CreateText(panel, "Account", new Vector2(-100, 18), new Vector2(150, 26), "游客 · 未登录", 15, Pale, TextAnchor.MiddleLeft, FontStyle.Bold);
            _deckStatusText = CreateText(panel, "Deck", new Vector2(-100, -13), new Vector2(150, 26), "卡组 · 平原", 14, Muted, TextAnchor.MiddleLeft, FontStyle.Normal);
            _onlineStatusText = CreateText(panel, "Status", new Vector2(43, 0), new Vector2(120, 58), "本地模式", 15, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            _onlineStatusText.gameObject.AddComponent<DemoHudTypography>().Configure(15);
            // The narrow middle column uses deliberate phase/detail lines, never an isolated overflow glyph.
            _onlineStatusText.resizeTextForBestFit = false;
            _onlineActionButton = CreateSecondaryButton(panel, "OnlineAction", new Vector2(145, 0), new Vector2(52, 46), "匹配", 14);
            _onlineActionLabel = _onlineActionButton.GetComponentInChildren<Text>();
            _onlineActionButton.onClick.AddListener(ToggleOnlineConnection);
            _accountService = GameCompositionRoot.Instance?.PlayerAccountService;
            if (_accountService != null)
            {
                _accountService.StateChanged += HandleAccountStateChanged;
                HandleAccountStateChanged(_accountService.CurrentStatus);
            }
        }

        private void HandleAccountStateChanged(PlayerAccountStatus status)
        {
            if (_accountStatusText == null) return;
            switch (status.Phase)
            {
                case PlayerAccountPhase.Authenticating:
                    _accountStatusText.text = "游客 · 认证中";
                    break;
                case PlayerAccountPhase.Ready:
                case PlayerAccountPhase.Updating:
                    _accountStatusText.text = "游客 · " + CompactPlayerName(status.Profile?.DisplayName);
                    break;
                case PlayerAccountPhase.SigningOut:
                    _accountStatusText.text = "游客 · 退出中";
                    break;
                case PlayerAccountPhase.Failed:
                    _accountStatusText.text = "账户异常·重试";
                    break;
                default:
                    _accountStatusText.text = "游客 · 未登录";
                    break;
            }
            _accountStatusText.color = status.Phase == PlayerAccountPhase.Failed ? Danger :
                status.Phase == PlayerAccountPhase.Ready ? Cyan : Pale;
            RefreshDeckShell();
        }

        private static string CompactPlayerName(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) return "未命名";
            value = value.Trim();
            return value.Length <= 4 ? value : value.Substring(0, 3) + "…";
        }

        private void RefreshDeckShell()
        {
            if (_deckStatusText == null) return;
            var faction = Factions.FirstOrDefault(item => item.Id == _activeFaction);
            _deckStatusText.text = "卡组 · " + (faction?.Label ?? _activeFaction);
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
                    _onlineStatusText.text = "联机底座\n未启动";
                    _onlineStatusText.color = Danger;
                    ShowStatus("联机系统尚未启动。请重新打开游戏，再尝试匹配。", true);
                    return;
                }
                if (_onlineGateway != null) _onlineGateway.ConnectionStateChanged -= HandleOnlineConnectionState;
                DisposeOnlineSession();
                if (_registry == null)
                    throw new InvalidOperationException("Card content must be loaded before online matchmaking.");
                _onlineGateway = compositionRoot.RegisterDefaultOnlineTransport(
                    _activeFaction,
                    _registry.ContentVersion,
                    _registry.ImplementedEffectRegistryVersion);
                _onlineSession = new DemoOnlineMatchSession(_onlineGateway, compositionRoot.MatchStateStore);
                SetAgentPolicy(_agentPolicy);
                _onlineSession.StateChanged += HandleOnlineBoardChanged;
                _onlineSession.CommandPending += HandleOnlineCommandPending;
                _onlineSession.CommandCompleted += HandleOnlineCommandCompleted;
                _onlineGateway.ConnectionStateChanged += HandleOnlineConnectionState;
                _onlineGateway.SnapshotReceived += HandleOnlinePresentationSnapshot;
                HandleOnlineConnectionState(_onlineGateway.CurrentStatus);
                await _onlineGateway.ConnectAsync();
            }
            catch (Exception exception)
            {
                if (_onlineStatusText != null && _onlineGateway?.CurrentStatus.Phase != MatchConnectionPhase.Failed)
                {
                    _onlineStatusText.text = "连接失败";
                    _onlineStatusText.color = Danger;
                }
                ShowOnlineException(exception, connecting: true);
                Debug.LogWarning($"Online connection failed ({exception.GetType().Name}).", this);
            }
        }

        private void HandleOnlineConnectionState(MatchConnectionStatus status)
        {
            if (_onlineStatusText == null || _onlineActionLabel == null) return;
            switch (status.Phase)
            {
                case MatchConnectionPhase.Authenticating:
                    _onlineStatusText.text = status.Detail.IndexOf("Checking server gameplay", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "校验客户端\n版本"
                        : "身份认证中";
                    break;
                case MatchConnectionPhase.Connecting:
                    _onlineStatusText.text = "连接\n服务器";
                    break;
                case MatchConnectionPhase.Matchmaking:
                    _onlineStatusText.text = "寻找对手中\n" + Factions.First(item => item.Id == _activeFaction).Label;
                    break;
                case MatchConnectionPhase.Joining:
                    _onlineStatusText.text = "进入\n权威对局";
                    break;
                case MatchConnectionPhase.Ready:
                    _onlineStatusText.text = "权威对局\n已连接";
                    break;
                case MatchConnectionPhase.Reconnecting:
                    _onlineStatusText.text = $"正在重连\n第 {status.Attempt} 次";
                    break;
                case MatchConnectionPhase.Failed:
                    _onlineStatusText.text = status.Detail.IndexOf("version mismatch", StringComparison.OrdinalIgnoreCase) >= 0
                        ? "版本不兼容\n检查两端"
                        : "连接失败";
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
            _onlineActionLabel.text = ready ? "断开" : idle ? "匹配" : "取消";
            _onlineStatusText.color = status.Phase == MatchConnectionPhase.Failed ? Danger : ready ? Cyan : Muted;
            RefreshAll();
            if (status.Phase == MatchConnectionPhase.Failed)
            {
                if (status.CompatibilityFailure != null) ShowCompatibilityFailure(status.CompatibilityFailure);
                else ShowStatus(DemoOnlineFeedback.ConnectionFailureText, true);
            }
            else if (!MatchView.IsFinished && !MatchView.IsMulligan && MatchView.PendingChoice == null && !HasPendingOnlineCommand)
                ShowStatus(DemoOnlineFeedback.FormatConnectionPhase(status), status.Phase == MatchConnectionPhase.Reconnecting);
        }

        private void DisposeOnlineSession()
        {
            _agentRunner?.Dispose();
            _agentRunner = null;
            if (_onlineGateway != null) _onlineGateway.SnapshotReceived -= HandleOnlinePresentationSnapshot;
            if (_onlineSession == null) return;
            _onlineSession.StateChanged -= HandleOnlineBoardChanged;
            _onlineSession.CommandPending -= HandleOnlineCommandPending;
            _onlineSession.CommandCompleted -= HandleOnlineCommandCompleted;
            _onlineSession.Dispose();
            _onlineSession = null;
            GetComponent<DemoBattlefieldPointerController>()?.SetInputEnabled(!IsReadOnlyOverlayOpen && !MatchView.IsFinished);
        }

        private void HandleOnlinePresentationSnapshot(MatchStateDto snapshot)
        {
            // CompositionRoot has stopped the old presentation queue. Its nested banner
            // coroutine may have been interrupted between fade-in and fade-out.
            // Invalidate it and restore the resting pose; do not change gameplay/input state.
            ++_turnBannerSequence;
            if (_turnBanner != null)
            {
                _turnBanner.alpha = 0f;
                if (_hasTurnBannerRestingPosition && _turnBanner.transform is RectTransform rect)
                    rect.anchoredPosition = _turnBannerRestingPosition;
            }
        }

        private void HandleOnlineBoardChanged()
        {
            if (IsOnlineBoard)
            {
                ApplyAuthoritativeFactionVisuals();
                var selectedHandCard = MatchView.HandCards.FirstOrDefault(value => value != null &&
                    value.handCardInstanceId == _selectedHandCardInstanceId && value.cardId == _selectedCardId);
                if (selectedHandCard == null) SelectFirstHandCard();
                if (_selectedAttackerInstanceId != MatchAttackerIds.Hero && FindSelectedAttacker() == null) _selectedAttackerInstanceId = null;
            }
            RefreshAll();
        }

        private void HandleOnlineCommandPending(string commandId)
        {
            GetComponent<DemoBattlefieldPointerController>()?.SetInputEnabled(false);
            ShowStatus("命令已发送，等待服务器确认…", false);
            RefreshAll();
        }

        private void HandleOnlineCommandCompleted(MatchCommandDispatchResult result)
        {
            GetComponent<DemoBattlefieldPointerController>()?.SetInputEnabled(!HasPendingOnlineCommand && !IsReadOnlyOverlayOpen && !MatchView.IsFinished);
            ShowStatus(DemoOnlineFeedback.FormatCommand(result), result.Outcome != MatchCommandOutcome.Accepted);
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
                MatchEventTypes.ObjectStatusApplied, MatchEventTypes.ObjectStatusTicked, MatchEventTypes.ObjectStatusRemoved, MatchEventTypes.ObjectMoved,
                MatchEventTypes.PlayerStatusApplied, MatchEventTypes.PlayerStatusTicked, MatchEventTypes.PlayerStatusRemoved,
                MatchEventTypes.PhaseChanged, MatchEventTypes.AttackResolved, MatchEventTypes.ObjectDied,
                MatchEventTypes.ObjectReturned, MatchEventTypes.HandCardCostModifierExpired,
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
                    var summonTriggerName = matchEvent.payload?.effectId == "effect.nt_001.01" ? "亡语" :
                        matchEvent.payload?.effectId == "effect.pf_006.01" ? "繁殖" :
                        matchEvent.payload?.effectId == "effect.pf_007.01" ? "集结" :
                        matchEvent.payload?.effectId == "effect.cd_008.01" ? "府邸增援" :
                        matchEvent.payload?.effectId == "effect.nt_008.01" ? "岩浆增援" : "效果";
                    ShowStatus(ownSummon
                        ? $"{summonSourceName}{summonTriggerName}：{summonedName}已在单位格 {matchEvent.payload.slotIndex + 1} 召唤。"
                        : $"敌方{summonSourceName}{summonTriggerName}：{summonedName}已在单位格 {matchEvent.payload.slotIndex + 1} 召唤。", false);
                    if (matchEvent.payload?.effectId == "effect.cd_008.01")
                        yield return ShowTurnBanner("府邸增援", ownSummon ? Leaf : Ember);
                    else if (matchEvent.payload?.effectId == "effect.nt_008.01")
                        yield return ShowTurnBanner("要塞增援", ownSummon ? Gold : Ember);
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
                        case "effect.tk_002.01":
                            ShowStatus(ownEffect ? "小麦已喂给己方生物；若目标是动物，它本回合还会获得 +1 攻击。" : "对手使用小麦喂食了一个生物。", false);
                            yield return ShowTurnBanner("喂食", ownEffect ? Gold : Ember);
                            break;
                        case "effect.tk_001.01":
                            ShowStatus(ownEffect ? "羊毛已附着：己方目标本回合获得 +1 当前与最大生命。" : "对手用羊毛临时保护了一个生物。", false);
                            yield return ShowTurnBanner("羊毛护持", ownEffect ? Pale : Ember);
                            break;
                        case "effect.tk_009.01":
                            ShowStatus(ownEffect ? "骨头已生效：己方目标本回合获得 +1 攻击力。" : "对手使用骨头强化了一个生物。", false);
                            break;
                        case "effect.tk_010.01":
                            ShowStatus(ownEffect ? "圆石已生效：己方建筑恢复至新的生命值。" : "对手使用圆石修复了一个建筑。", false);
                            break;
                        case "effect.pf_007.01":
                            ShowStatus(ownEffect
                                ? "林间集结开始：每个步骤会召唤林地伙伴；单位格不足时改为抽牌。"
                                : "敌方正在进行林间集结。", false);
                            yield return ShowTurnBanner("林间集结", ownEffect ? Leaf : Ember);
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
                        : "对手将一张未知卡牌埋入了隐藏牌库。", false);
                    yield return ShowTurnBanner("掩埋", Ember);
                    break;
                }
                case MatchEventTypes.ChoiceOffered: {
                    var choiceViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var ownChoice = matchEvent.payload?.playerId == choiceViewerId;
                    if (matchEvent.payload?.kind == "MOVE_UNIT")
                    {
                        var salmonCurrent = matchEvent.payload.effectId == "effect.or_001.01";
                        var prismarineShard = matchEvent.payload.effectId == "effect.tk_012.01";
                        var ownMessage = prismarineShard
                            ? "海晶碎片：必须将选中的水生生物移动到一个相邻发光地块；移动后恢复 1 点生命。"
                            : salmonCurrent ? "鲑鱼群水流：选择己方相邻发光地块，或保持原位。" : "激流三叉戟：选择目标旁的发光地块，或保持原位。";
                        var opponentMessage = prismarineShard
                            ? "对手正在用海晶碎片移动一个水生生物。"
                            : salmonCurrent ? "对手正在决定鲑鱼群的位置。" : "对手正在决定三叉戟目标的位置。";
                        ShowStatus(ownChoice ? ownMessage : opponentMessage, false);
                        yield return ShowTurnBanner(prismarineShard ? "碎片涌流" : salmonCurrent ? "水流" : "激流位移", ownChoice ? Gold : Ember);
                        break;
                    }
                    if (matchEvent.payload?.kind == "HEAL_UNIT")
                    {
                        ShowStatus(ownChoice
                            ? "雪屋：多个友军并列为受伤最重，请直接点击场内发光单位。"
                            : "对手正在决定雪屋的治疗目标。", false);
                        yield return ShowTurnBanner("雪屋疗愈", ownChoice ? Cyan : Ember);
                        break;
                    }
                    if (matchEvent.payload?.kind == "TOP_CARD_SCRY")
                    {
                        ShowStatus(ownChoice
                            ? "洞穴蝙蝠带回了牌库顶的回声：可保留，或将它置于牌库底。"
                            : "对手的洞穴蝙蝠正在窥视一张牌库顶牌。", false);
                        yield return ShowTurnBanner("洞穴回声", ownChoice ? Cyan : Ember);
                        break;
                    }
                    ShowStatus(ownChoice
                        ? "沙漠考古学家发现了牌库顶三张牌，请选择可出土的掩埋牌。"
                        : "对手的沙漠考古学家正在查看牌库。", false);
                    yield return ShowTurnBanner("沙漠考古", ownChoice ? Gold : Ember);
                    break;
                }
                case MatchEventTypes.ChoiceResolved:
                    if (matchEvent.payload?.kind == "TOP_CARD_SCRY")
                    {
                        var scryViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var ownScry = matchEvent.payload?.playerId == scryViewerId;
                        var movedToBottom = matchEvent.payload?.selectedOptionIndex == 0;
                        var revealedName = ownScry && movedToBottom && !string.IsNullOrEmpty(matchEvent.payload?.selectedCardId)
                            ? GetCardName(matchEvent.payload.selectedCardId)
                            : "该牌";
                        ShowStatus(movedToBottom
                            ? ownScry ? $"洞穴回声：已将 {revealedName} 置于牌库底。" : "对手将窥视到的牌置于了牌库底。"
                            : ownScry ? "洞穴回声：牌库顶保持不变。" : "对手保留了牌库顶牌。", false);
                        yield return ShowTurnBanner(movedToBottom ? "沉入牌底" : "保留牌顶", ownScry ? Cyan : Ember);
                    }
                    yield return null;
                    break;
                case MatchEventTypes.ObjectMoved:
                    if (matchEvent.payload?.effectId == "effect.si_004.01")
                    {
                        ShowStatus($"山羊越位：{GetCardName(matchEvent.payload.cardId)} 从单位格 {matchEvent.payload.fromSlotIndex + 1} 移动至 {matchEvent.payload.toSlotIndex + 1}。", false);
                        yield return ShowTurnBanner("山羊越位", Cyan);
                        yield return PulseBattlefieldObject(matchEvent.payload.sourceInstanceId);
                    }
                    else ShowStatus($"{GetCardName(matchEvent.payload.cardId)} 已移动至单位格 {matchEvent.payload.toSlotIndex + 1}。", false);
                    yield return PulseBattlefieldObject(matchEvent.payload.instanceId);
                    break;
                case MatchEventTypes.AttackResolved: {
                    var payload = matchEvent.payload;
                    _pendingOnlineAttackTargetInstanceId = null;
                    _pendingOnlineAttackAttackerInstanceId = null;
                    if (payload == null)
                    {
                        yield return null;
                        break;
                    }

                    var attackViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var attackerIsHero = string.Equals(payload.attackerInstanceId, MatchAttackerIds.Hero, StringComparison.Ordinal);
                    var targetIsHero = string.Equals(payload.targetType, "HERO", StringComparison.OrdinalIgnoreCase);
                    var attacker = attackerIsHero ? null : FindBattlefieldObject(payload.attackerInstanceId);
                    var target = targetIsHero ? null : FindBattlefieldObject(payload.targetInstanceId);
                    var attackerName = attackerIsHero ? "英雄" : attacker == null ? "单位" : GetCardName(attacker.CardId);
                    var targetName = targetIsHero
                        ? "英雄"
                        : target == null ? (string.Equals(payload.targetType, "BUILDING", StringComparison.OrdinalIgnoreCase) ? "建筑" : "单位")
                        : GetCardName(target.CardId);
                    ShowStatus(FormatAttackResolvedStatus(payload, attackViewerId, attackerName, targetName), false);

                    if (!attackerIsHero && payload.damageToAttacker > 0)
                    {
                        if (payload.attackerHealth <= 0)
                            _pendingOnlineDamagePopups[payload.attackerInstanceId] = payload.damageToAttacker;
                        else if (attacker != null) _battlefield.ShowCombatDamageNumber(attacker.Player, attacker.SlotKind,
                            attacker.SlotIndex, attacker.OccupiedSlots, payload.damageToAttacker);
                    }
                    if (!targetIsHero && payload.damageToTarget > 0)
                    {
                        if (payload.targetHealth <= 0)
                            _pendingOnlineDamagePopups[payload.targetInstanceId] = payload.damageToTarget;
                        else if (target != null) _battlefield.ShowCombatDamageNumber(target.Player, target.SlotKind,
                            target.SlotIndex, target.OccupiedSlots, payload.damageToTarget);
                    }

                    if (!attackerIsHero && target != null)
                        yield return AnimateAttackLunge(payload.attackerInstanceId, target.Player, target.SlotKind, target.SlotIndex);
                    else if (!attackerIsHero && !targetIsHero && attacker != null && !string.IsNullOrEmpty(payload.targetInstanceId))
                    {
                        _pendingOnlineAttackTargetInstanceId = payload.targetInstanceId;
                        _pendingOnlineAttackAttackerInstanceId = payload.attackerInstanceId;
                    }

                    if (attackerIsHero && payload.damageToAttacker > 0)
                        yield return PulseHeroHudForPlayer(payload.attackerPlayerId, attackViewerId, Danger);
                    else if (attacker != null)
                        yield return PulseBattlefieldObject(attacker.InstanceId);

                    if (targetIsHero && payload.damageToTarget > 0)
                        yield return PulseHeroHudForPlayer(payload.targetPlayerId, attackViewerId, Danger);
                    else if (target != null)
                        yield return PulseBattlefieldObject(target.InstanceId);
                    else yield return null;
                    break;
                }
                case MatchEventTypes.ObjectDied: {
                    var payload = matchEvent.payload;
                    if (payload == null)
                    {
                        yield return null;
                        break;
                    }
                    var deathViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var ownDeath = payload.playerId == deathViewerId;
                    var slotKind = string.Equals(payload.slotKind, "BUILDING", StringComparison.OrdinalIgnoreCase)
                        ? DemoSlotKind.Building
                        : DemoSlotKind.Unit;
                    if (_pendingOnlineDamagePopups.TryGetValue(payload.instanceId, out var deathDamage))
                    {
                        _battlefield.ShowCombatDamageNumber(ownDeath, slotKind, payload.slotIndex,
                            payload.occupiedSlots, deathDamage);
                        _pendingOnlineDamagePopups.Remove(payload.instanceId);
                    }
                    if (payload.instanceId == _pendingOnlineAttackTargetInstanceId &&
                        !string.IsNullOrEmpty(_pendingOnlineAttackAttackerInstanceId))
                    {
                        yield return AnimateAttackLunge(_pendingOnlineAttackAttackerInstanceId,
                            ownDeath, slotKind, payload.slotIndex);
                        _pendingOnlineAttackTargetInstanceId = null;
                        _pendingOnlineAttackAttackerInstanceId = null;
                    }
                    var kindName = slotKind == DemoSlotKind.Building ? "建筑" : "单位";
                    ShowStatus(ownDeath
                        ? $"己方{kindName}{GetCardName(payload.cardId)}阵亡，已进入弃牌堆。"
                        : $"敌方{kindName}{GetCardName(payload.cardId)}阵亡，已进入弃牌堆。", false);
                    yield return PulseBattlefieldSlot(ownDeath, slotKind, payload.slotIndex, payload.occupiedSlots, Danger);
                    break;
                }
                case MatchEventTypes.ObjectReturned: {
                    var payload = matchEvent.payload;
                    var returnViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var cardName = string.IsNullOrEmpty(payload?.cardId) ? string.Empty : GetCardName(payload.cardId);
                    var sourceCardName = string.IsNullOrEmpty(payload?.sourceCardId) ? string.Empty : GetCardName(payload.sourceCardId);
                    ShowStatus(FormatObjectReturnedStatus(payload, returnViewerId, cardName, sourceCardName), false);
                    if (payload != null)
                    {
                        var returnedSlotKind = string.Equals(payload.fromSlotKind, "BUILDING", StringComparison.OrdinalIgnoreCase)
                            ? DemoSlotKind.Building
                            : DemoSlotKind.Unit;
                        yield return PulseBattlefieldSlot(
                            payload.controllerPlayerId == returnViewerId,
                            returnedSlotKind,
                            payload.fromSlotIndex,
                            Math.Max(1, payload.occupiedSlots),
                            Hex("#FFE27A"));
                    }
                    yield return ShowTurnBanner("末影回响", Hex("#B95CFF"));
                    if (payload?.destination == "DISCARD")
                    {
                        yield return PulseHeroHudForPlayer(payload.ownerPlayerId, returnViewerId, Ember);
                    }
                    else if (payload?.ownerPlayerId == returnViewerId)
                    {
                        yield return PulsePlayerHud(Gold);
                    }
                    else yield return null;
                    break;
                }
                case MatchEventTypes.HandCardCostModifierExpired: {
                    var payload = matchEvent.payload;
                    var expirationViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var expiryStatus = FormatHandCardCostModifierExpiredStatus(
                        payload, expirationViewerId,
                        string.IsNullOrEmpty(payload?.cardId) ? string.Empty : GetCardName(payload.cardId));
                    if (!string.IsNullOrEmpty(expiryStatus))
                    {
                        ShowStatus(expiryStatus, false);
                        yield return ShowTurnBanner("费用恢复", Gold);
                        yield return PulsePlayerHud(Gold);
                    }
                    else yield return null;
                    break;
                }
                case MatchEventTypes.CardExcavated: {
                    var excavationViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var ownExcavation = matchEvent.payload?.playerId == excavationViewerId;
                    var destination = matchEvent.payload?.destination == "HAND" ? "进入手牌" : "因满手进入弃牌堆";
                    var excavationName = GetCardName(matchEvent.payload.cardId);
                    var excavationDetail = matchEvent.payload?.effectId == "effect.tk_007.01"
                        ? "藏宝图将生成一张绿宝石。"
                        : matchEvent.payload?.effectId == "effect.tk_008.01"
                            ? "炸药机关即将对双方英雄结算伤害。"
                            : "随后继续正常抽牌。";
                    ShowStatus(ownExcavation
                        ? $"出土：{excavationName} {destination}；{excavationDetail}"
                        : $"对手出土了 {excavationName}；{excavationDetail}", false);
                    yield return ShowTurnBanner(matchEvent.payload?.effectId == "effect.tk_008.01" ? "炸药机关" : "出土", Gold);
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
                    var isFarmerBattlecry = matchEvent.payload?.effectId == "effect.pf_004.01";
                    var isSnowGolemBattlecry = matchEvent.payload?.effectId == "effect.si_002.01";
                    var isMineProduction = matchEvent.payload?.effectId == "effect.cd_007.01";
                    var triggerName = isLoot ? "掉落" : matchEvent.payload?.effectId == "effect.ed_004.01" ? "亡语" :
                        isFarmerBattlecry || isSnowGolemBattlecry ? "战吼" :
                        isMineProduction ? "产出" : "效果";
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
                    else if (isFarmerBattlecry) yield return ShowTurnBanner("收获小麦", Gold);
                    else if (isSnowGolemBattlecry) yield return ShowTurnBanner("凝聚雪球", Cyan);
                    else if (isMineProduction) yield return ShowTurnBanner("矿井产出", Gold);
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
                    var outcome = ResolveMatchEndOutcome(winnerId, playerId);
                    yield return ShowTurnBanner(GetMatchEndBannerText(outcome), GetMatchEndBannerColor(outcome));
                    break;
                case MatchEventTypes.HeroDamaged:
                case MatchEventTypes.FatigueDamage:
                    var damagedViewer = matchEvent.payload?.playerId == GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    if (matchEvent.payload?.effectId == "effect.tk_008.01")
                    {
                        var damagedSide = damagedViewer ? "己方" : "敌方";
                        var damageType = matchEvent.payload.damageType == "TRUE" ? "真实" : "普通";
                        ShowStatus($"炸药机关：{damagedSide}英雄受到 {matchEvent.payload.damage} 点{damageType}伤害。", false);
                        if (matchEvent.payload.damageType == "NORMAL")
                            yield return PulseBothHeroHuds(Danger);
                        else
                            yield return null;
                    }
                    else if (matchEvent.payload?.effectId == "effect.nt_002.01")
                    {
                        var damagedSide = damagedViewer ? "己方" : "敌方";
                        ShowStatus($"僵尸猪灵岩浆：消耗 1 点红石，{damagedSide}英雄受到 1 点普通伤害。", false);
                        if (!string.IsNullOrEmpty(matchEvent.payload.sourceInstanceId))
                            yield return PulseBattlefieldObject(matchEvent.payload.sourceInstanceId);
                        yield return ShowTurnBanner("岩浆喷射", Hex("#FF6A1A"));
                        yield return damagedViewer ? PulsePlayerHud(Danger) : PulseOpponentHud(Danger);
                    }
                    else if (matchEvent.payload?.effectId == "effect.ed_007.01")
                    {
                        var backlash = matchEvent.payload.damageType == "TRUE";
                        var damagedSide = damagedViewer ? "己方" : "敌方";
                        ShowStatus(backlash
                            ? $"末影水晶亡语反噬：{damagedSide}英雄受到 {matchEvent.payload.damage} 点真实伤害。"
                            : $"末影水晶结束阶段脉冲：{damagedSide}英雄受到 {matchEvent.payload.damage} 点普通伤害。", false);
                        if (!string.IsNullOrEmpty(matchEvent.payload.sourceInstanceId))
                            yield return PulseBattlefieldObject(matchEvent.payload.sourceInstanceId);
                        yield return ShowTurnBanner(backlash ? "水晶反噬" : "末影脉冲", backlash ? Danger : Hex("#D28BFF"));
                        yield return damagedViewer ? PulsePlayerHud(Danger) : PulseOpponentHud(Danger);
                    }
                    else yield return damagedViewer ? PulsePlayerHud(Danger) : PulseOpponentHud(Danger);
                    break;
                case MatchEventTypes.HeroHealed:
                    var healedViewer = matchEvent.payload?.playerId == GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var healedSide = healedViewer ? "己方" : "敌方";
                    var healedCardName = string.IsNullOrEmpty(matchEvent.payload?.sourceCardId)
                        ? "卡牌效果"
                        : GetCardName(matchEvent.payload.sourceCardId);
                    var healedAmount = Math.Max(0, matchEvent.payload?.healing ?? 0);
                    var currentLife = Math.Max(0, matchEvent.payload?.life ?? 0);
                    ShowStatus(healedAmount > 0
                        ? $"{healedCardName}：{healedSide}英雄恢复 {healedAmount} 点生命，当前 {currentLife} 点。"
                        : $"{healedCardName}：{healedSide}英雄生命已满，本次没有恢复。", false);
                    if (healedAmount > 0)
                        yield return healedViewer ? PulsePlayerHud(Cyan) : PulseOpponentHud(Cyan);
                    else yield return null;
                    break;
                case MatchEventTypes.ArmorGained:
                    var armorViewer = matchEvent.payload?.playerId == GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    yield return armorViewer ? PulsePlayerHud(Cyan) : PulseOpponentHud(Cyan);
                    break;
                case MatchEventTypes.RedstoneChanged:
                    if (matchEvent.payload?.effectId == "effect.nt_007.01" &&
                        matchEvent.payload.reason == "TEMPORARY_GRANTED")
                    {
                        var grantedViewer = matchEvent.payload.playerId ==
                            GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        ShowStatus($"重生锚：{(grantedViewer ? "己方" : "敌方")}获得 1 点本回合临时红石能量。", false);
                        if (!string.IsNullOrEmpty(matchEvent.payload.sourceInstanceId))
                            yield return PulseBattlefieldObject(matchEvent.payload.sourceInstanceId);
                        yield return ShowTurnBanner("重生锚充能", Hex("#B95CFF"));
                        yield return grantedViewer ? PulsePlayerHud(Cyan) : PulseOpponentHud(Cyan);
                    }
                    else if (matchEvent.payload?.effectId == "effect.nt_008.01" &&
                             matchEvent.payload.reason == "AUTOMATIC_PAYMENT")
                    {
                        var paidViewer = matchEvent.payload.playerId ==
                            GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        ShowStatus($"下界要塞：{(paidViewer ? "己方" : "敌方")}消耗 1 点红石，正在召集要塞凋灵骷髅。", false);
                        if (!string.IsNullOrEmpty(matchEvent.payload.sourceInstanceId))
                            yield return PulseBattlefieldObject(matchEvent.payload.sourceInstanceId);
                        yield return paidViewer ? PulsePlayerHud(Gold) : PulseOpponentHud(Ember);
                    }
                    break;
                case MatchEventTypes.ObjectStatsChanged:
                    if (matchEvent.payload?.effectId == "effect.tk_001.01" && matchEvent.payload?.reason == "TEMPORARY_HEALTH_MODIFIER")
                    {
                        var woolFriendly = matchEvent.payload.playerId == GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        ShowStatus(woolFriendly
                            ? $"羊毛附着：目标当前生命 {matchEvent.payload.health} / 最大生命 {matchEvent.payload.maxHealth}，本回合临时生命 +{matchEvent.payload.temporaryHealthModifier}。"
                            : "对手的羊毛附着在一个生物上，临时提高其当前与最大生命。", false);
                    }
                    else if (matchEvent.payload?.reason == "TEMPORARY_EXPIRED" && matchEvent.payload?.sourceCardId == null)
                    {
                        ShowStatus($"本回合临时修正已清除：当前生命 {matchEvent.payload.health} / 最大生命 {matchEvent.payload.maxHealth}。", false);
                    }
                    if (matchEvent.payload?.effectId == "effect.nt_005.01" && matchEvent.payload?.reason == "DAMAGE")
                    {
                        var witherViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var witheredFriendly = matchEvent.payload?.playerId == witherViewerId;
                        ShowStatus(witheredFriendly
                            ? "己方生物的凋零发作，受到 1 点真实伤害。"
                            : "敌方生物的凋零发作，受到 1 点真实伤害。", false);
                        yield return ShowTurnBanner("凋零发作", Hex("#8E61C7"));
                    }
                    else if ((matchEvent.payload?.effectId == "effect.nt_003.01" || matchEvent.payload?.effectId == "effect.tk_013.01") &&
                        matchEvent.payload?.reason == "DAMAGE")
                    {
                        var fireViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var burningFriendly = matchEvent.payload?.playerId == fireViewerId;
                        var fireTick = matchEvent.payload?.damageType == "TRUE";
                        ShowStatus(fireTick
                            ? burningFriendly
                                ? "己方对象的烈焰发作，受到 1 点真实伤害。"
                                : "敌方对象的烈焰发作，受到 1 点真实伤害。"
                            : burningFriendly
                                ? "敌方烈焰棒命中己方生物，造成 1 点伤害。"
                                : "烈焰棒命中敌方生物，造成 1 点伤害。", false);
                        yield return ShowTurnBanner(fireTick ? "烈焰发作" : "烈焰棒", Hex("#FF8A2A"));
                    }
                    else if (matchEvent.payload?.effectId == "effect.cd_002.01" && matchEvent.payload?.reason == "DAMAGE")
                    {
                        var poisonViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var poisonedFriendly = matchEvent.payload?.playerId == poisonViewerId;
                        ShowStatus(poisonedFriendly
                            ? "己方生物的毒素发作，受到 1 点普通伤害。"
                            : "敌方生物的毒素发作，受到 1 点普通伤害。", false);
                        yield return ShowTurnBanner("毒素发作", Hex("#A6F04D"));
                    }
                    else if (matchEvent.payload?.effectId == "effect.cd_003.01")
                    {
                        var damageViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var hitFriendly = matchEvent.payload?.playerId == damageViewerId;
                        ShowStatus(hitFriendly
                            ? "敌方地牢骷髅亡语：随机命中一个己方生物并造成 1 点伤害。"
                            : "地牢骷髅亡语：随机命中一个敌方生物并造成 1 点伤害。", false);
                        yield return ShowTurnBanner("亡语", Ember);
                    }
                    else if (matchEvent.payload?.effectId == "effect.pf_003.01")
                    {
                        var wolfViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var wolfFriendly = matchEvent.payload?.playerId == wolfViewerId;
                        ShowStatus(wolfFriendly
                            ? "驯服的狼落位时与另一个己方动物相邻，永久获得 +1 当前与最大生命。"
                            : "敌方驯服的狼借助相邻动物获得了永久生命成长。", false);
                        yield return ShowTurnBanner("忠诚成长", wolfFriendly ? Gold : Ember);
                    }
                    else if (matchEvent.payload?.effectId == "effect.pf_006.01")
                    {
                        var breedingViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var breedingFriendly = matchEvent.payload?.playerId == breedingViewerId;
                        ShowStatus(breedingFriendly
                            ? "繁殖季节：两个选定的己方动物永久获得 +1 当前与最大生命。"
                            : "敌方繁殖季节强化了两个动物。", false);
                        yield return ShowTurnBanner("繁殖成长", breedingFriendly ? Leaf : Ember);
                    }
                    else if (matchEvent.payload?.effectId == "effect.pf_008.01")
                    {
                        var golemViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var golemFriendly = matchEvent.payload?.playerId == golemViewerId;
                        ShowStatus(golemFriendly
                            ? "铁傀儡响应己方建筑，永久获得 +1 攻击、+1 当前与最大生命。"
                            : "敌方铁傀儡响应建筑，永久获得了 +1/+1。", false);
                        yield return ShowTurnBanner("建筑共鸣", golemFriendly ? Gold : Ember);
                    }
                    else if (matchEvent.payload?.effectId == "effect.si_007.01" && matchEvent.payload?.reason == "HEAL")
                    {
                        var hutViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var hutFriendly = matchEvent.payload?.playerId == hutViewerId;
                        ShowStatus(hutFriendly
                            ? "雪屋在起始阶段为最重伤的己方生物恢复了 1 点生命。"
                            : "敌方雪屋为一个最重伤生物恢复了 1 点生命。", false);
                        yield return PulseBattlefieldObject(matchEvent.payload?.sourceInstanceId);
                        yield return PulseBattlefieldObject(matchEvent.payload?.instanceId);
                        yield return ShowTurnBanner("雪屋疗愈", hutFriendly ? Cyan : Ember);
                    }
                    else if (matchEvent.payload?.effectId == "effect.cd_005.01")
                    {
                        var vindicatorViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var vindicatorFriendly = matchEvent.payload?.playerId == vindicatorViewerId;
                        ShowStatus(vindicatorFriendly
                            ? "林地卫道士借助己方建筑发动伏击，本回合获得 +2 攻击力。"
                            : "敌方林地卫道士借助建筑，本回合获得了 +2 攻击力。", false);
                        yield return ShowTurnBanner("建筑伏击", vindicatorFriendly ? Cyan : Ember);
                    }
                    else if (matchEvent.payload?.effectId == "effect.db_004.01")
                    {
                        var cactusViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var hitFriendly = matchEvent.payload?.playerId == cactusViewerId;
                        ShowStatus(hitFriendly
                            ? "敌方仙人掌围栏在英雄受击后反伤，使己方攻击生物受到 1 点伤害。"
                            : "仙人掌围栏反击了攻击己方英雄的敌方生物，造成 1 点伤害。", false);
                        yield return PulseBattlefieldObject(matchEvent.payload?.sourceInstanceId);
                        yield return ShowTurnBanner("尖刺反击", hitFriendly ? Danger : Gold);
                    }
                    else if (matchEvent.payload?.effectId == "effect.db_007.01")
                    {
                        ShowStatus($"沙漠神殿响应掩埋牌出土；当前生命为 {matchEvent.payload.health}。", false);
                        yield return PulseBattlefieldObject(matchEvent.payload?.sourceInstanceId);
                        yield return ShowTurnBanner("遗迹修复", Gold);
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
                    else if (matchEvent.payload?.effectId == "effect.tk_012.01")
                    {
                        ShowStatus("海晶碎片：目标在移动后恢复 1 点生命值。", false);
                        yield return ShowTurnBanner("海晶愈合", Cyan);
                    }
                    else if (matchEvent.payload?.effectId == "effect.or_005.01")
                    {
                        var gainedAura = matchEvent.payload?.adjacencyHealthModifier > 0;
                        ShowStatus(gainedAura
                            ? $"海龟光环：相邻单位当前获得 +{matchEvent.payload.adjacencyHealthModifier} 最大生命。"
                            : "海龟光环已离开该单位；当前生命与最大生命同步调整。", false);
                        yield return ShowTurnBanner("潮甲光环", gainedAura ? Cyan : Ember);
                    }
                    else if (matchEvent.payload?.effectId == "effect.pf_005.01")
                    {
                        var nurseryViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var nurseryFriendly = matchEvent.payload?.playerId == nurseryViewerId;
                        ShowStatus(nurseryFriendly
                            ? "苗圃培育：本回合首次入场的动物永久获得 +1 当前与最大生命。"
                            : "敌方林地苗圃培育了本回合首次入场的动物。", false);
                        yield return PulseBattlefieldObject(matchEvent.payload?.sourceInstanceId);
                        yield return ShowTurnBanner("苗圃培育", nurseryFriendly ? Leaf : Ember);
                    }
                    else if (matchEvent.payload?.effectId == "effect.or_007.01")
                    {
                        var coralViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var coralFriendly = matchEvent.payload?.playerId == coralViewerId;
                        ShowStatus(coralFriendly
                            ? "珊瑚滋养：本回合首次入场的水生生物获得 +1 当前与最大生命。"
                            : "敌方珊瑚礁滋养了本回合首次入场的水生生物。", false);
                        yield return PulseBattlefieldObject(matchEvent.payload?.sourceInstanceId);
                        yield return ShowTurnBanner("珊瑚滋养", coralFriendly ? Cyan : Ember);
                    }
                    else if (matchEvent.payload?.effectId == "effect.or_008.01")
                    {
                        var monumentViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                        var hitFriendly = matchEvent.payload?.playerId == monumentViewerId;
                        ShowStatus(hitFriendly
                            ? "敌方海底神殿锁定了没有相邻友军的己方生物，并在结束阶段造成 1 点伤害。"
                            : "海底神殿对没有相邻友军的敌方生物造成 1 点结束阶段伤害。", false);
                        yield return PulseBattlefieldObject(matchEvent.payload?.sourceInstanceId);
                        yield return ShowTurnBanner("神殿脉冲", hitFriendly ? Danger : Gold);
                    }
                    yield return PulseBattlefieldObject(matchEvent.payload?.instanceId);
                    break;
                case MatchEventTypes.ObjectStatusApplied:
                    var poisonApplied = matchEvent.payload?.statusId == "POISON";
                    var fireApplied = matchEvent.payload?.statusId == "FIRE";
                    var witherApplied = matchEvent.payload?.statusId == "WITHER";
                    var iceSpireApplied = matchEvent.payload?.effectId == "effect.si_008.01";
                    ShowStatus(witherApplied
                        ? $"凋零侵蚀目标：剩余 {matchEvent.payload?.remainingDuration} 次；目标控制者每次结束阶段受到 1 点真实伤害。"
                        : fireApplied
                        ? $"烈焰附着目标：着火 {matchEvent.payload?.remainingDuration}；目标控制者每次结束阶段受到 1 点真实伤害。"
                        : poisonApplied
                        ? $"洞穴蜘蛛的毒素附着目标：中毒 {matchEvent.payload?.remainingDuration}；目标控制者每次结束阶段受到 1 点普通伤害。"
                        : iceSpireApplied
                            ? $"冰刺之巅截获边缘召唤：目标获得缓慢 {matchEvent.payload?.remainingDuration}，期间不能普通攻击。"
                            : $"粉雪覆盖目标：缓慢 {matchEvent.payload?.remainingDuration}，期间不能普通攻击。", false);
                    yield return ShowTurnBanner(witherApplied ? "凋零" : fireApplied ? "着火" : poisonApplied ? "中毒" : iceSpireApplied ? "冰刺封锁" : "缓慢",
                        witherApplied ? Hex("#8E61C7") : fireApplied ? Hex("#FF8A2A") : poisonApplied ? Hex("#A6F04D") : Cyan);
                    if (iceSpireApplied) yield return PulseBattlefieldObject(matchEvent.payload?.sourceInstanceId);
                    yield return PulseBattlefieldObject(matchEvent.payload?.instanceId);
                    break;
                case MatchEventTypes.ObjectStatusRemoved:
                    var poisonRemoved = matchEvent.payload?.statusId == "POISON";
                    var fireRemoved = matchEvent.payload?.statusId == "FIRE";
                    var witherRemoved = matchEvent.payload?.statusId == "WITHER";
                    ShowStatus(witherRemoved
                        ? "第二次凋零真实伤害已经结算，凋零状态移除。"
                        : fireRemoved
                        ? "第二次真实火焰伤害已经结算，着火状态移除。"
                        : poisonRemoved
                        ? "第三次毒伤已经结算，中毒状态移除。"
                        : "目标控制者的结束阶段已结算，缓慢与绑定的攻击修正已移除。", false);
                    yield return PulseBattlefieldObject(matchEvent.payload?.instanceId);
                    break;
                case MatchEventTypes.ObjectStatusTicked:
                    yield return null;
                    break;
                case MatchEventTypes.PlayerStatusApplied:
                    var darkViewerId = GameCompositionRoot.Instance?.MatchStateStore.Current?.viewerPlayerId;
                    var darkFriendly = matchEvent.payload?.playerId == darkViewerId;
                    ShowStatus(darkFriendly
                        ? "黑暗笼罩：本回合第一次指定敌方战场对象时，只能选择各排最外侧的发光目标。"
                        : "敌方陷入黑暗：其第一次主动指定将受到边缘目标限制。", false);
                    yield return darkFriendly ? PulsePlayerHud(Hex("#25D7C6")) : PulseOpponentHud(Hex("#25D7C6"));
                    yield return ShowTurnBanner("黑暗", Hex("#25D7C6"));
                    break;
                case MatchEventTypes.PlayerStatusRemoved:
                    ShowStatus("黑暗持续时间结束，目标限制已解除。", false);
                    yield return null;
                    break;
                case MatchEventTypes.PlayerStatusTicked:
                    yield return null;
                    break;
                default:
                    yield return null;
                    break;
            }
        }

        private void BuildSlots()
        {
            for (var i = 0; i < _battlefield.GetSlotCount(DemoSlotKind.Building); i++)
                _opponentBuildingSlots.Add(CreateOpponentSlot(DemoSlotKind.Building, i, _battlefield.GetSlotReferencePosition(false, DemoSlotKind.Building, i), new Vector2(190, 92)));
            for (var i = 0; i < _battlefield.GetSlotCount(DemoSlotKind.Unit); i++)
                _opponentUnitSlots.Add(CreateOpponentSlot(DemoSlotKind.Unit, i, _battlefield.GetSlotReferencePosition(false, DemoSlotKind.Unit, i), new Vector2(150, 118)));

            for (var i = 0; i < _battlefield.GetSlotCount(DemoSlotKind.Unit); i++)
                _playerUnitSlots.Add(CreatePlayerSlot(DemoSlotKind.Unit, i, _battlefield.GetSlotReferencePosition(true, DemoSlotKind.Unit, i), new Vector2(158, 118)));
            for (var i = 0; i < _battlefield.GetSlotCount(DemoSlotKind.Building); i++)
                _playerBuildingSlots.Add(CreatePlayerSlot(DemoSlotKind.Building, i, _battlefield.GetSlotReferencePosition(true, DemoSlotKind.Building, i), new Vector2(195, 84)));

            if (_canvasRoot.Find("OpponentLaneLabel") == null)
                CreateText(_canvasRoot, "OpponentLaneLabel", new Vector2(-687, 68), new Vector2(175, 34), "敌方单位排", 13, new Color(Pale.r, Pale.g, Pale.b, 0.72f), TextAnchor.MiddleLeft, FontStyle.Bold);
            if (_canvasRoot.Find("PlayerLaneLabel") == null)
                CreateText(_canvasRoot, "PlayerLaneLabel", new Vector2(-687, -66), new Vector2(175, 34), "己方单位排", 13, new Color(Pale.r, Pale.g, Pale.b, 0.72f), TextAnchor.MiddleLeft, FontStyle.Bold);
        }

        private void SynchronizeArenaTopology(string arenaId)
        {
            if (_battlefield.ArenaId == arenaId) return;
            GetComponent<DemoBattlefieldPointerController>()?.SetInputEnabled(false);
            _selectedAttackerInstanceId = null;
            _selectedDeploymentTargetInstanceId = null;
            _pendingTargetCardId = null;
            _selectedCardTargetInstanceIds.Clear();
            _battlefield.ApplyAuthoritativeArena(arenaId);
            var rows = new[] { _opponentBuildingSlots, _opponentUnitSlots, _playerUnitSlots, _playerBuildingSlots };
            var firstSlotSibling = rows.SelectMany(row => row).Min(slot => slot.Content.parent.GetSiblingIndex());
            foreach (var row in rows)
            {
                foreach (var slot in row)
                {
                    var root = slot.Content.parent.gameObject;
                    root.SetActive(false);
                    root.name = "Retired_" + root.name;
                    if (Application.isPlaying) Destroy(root); else DestroyImmediate(root);
                }
                row.Clear();
            }
            BuildSlots();
            // Insert the replacement cells where the old cells lived; never reorder HUD/hand/controls.
            foreach (var slot in rows.SelectMany(row => row)) slot.Content.parent.SetSiblingIndex(firstSlotSibling++);
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
            _handRoot = CreateRect(handPlate, "HandCards", new Vector2(-20, 28), new Vector2(1250, 225));
            _handCanvasGroup = _handRoot.gameObject.AddComponent<CanvasGroup>();
            _handLabel = CreateText(_canvasRoot, "HandLabel", new Vector2(-135, -508), new Vector2(700, 28), "手牌 5/7 · 牌库 25 · 弃牌 0", 14, Muted, TextAnchor.MiddleLeft, FontStyle.Bold);
            BuildHandInspection();
        }

        private void BuildInspector()
        {
            const float panelWidth = 286f;
            var panel = CreateBasePanel(_canvasRoot, "CardDetailsPanel", new Vector2(803, 60), new Vector2(panelWidth, 715));
            var inset = DemoUiMetrics.PanelContentInset * 2f;
            _inspectorRoot = CreateRect(panel, "InspectorContent", Vector2.zero, new Vector2(panelWidth - inset, 715f - inset));
            _cardDetailsView = _inspectorRoot.gameObject.AddComponent<CardDetailsView>();
            _cardDetailsView.Configure(_registry, UiFont);
            _cardNotesButton = CreateSecondaryButton(panel, "ReadCardNotes", new Vector2(0, -325), new Vector2(248, 36), "查看完整说明", 15);
            _cardNotesButton.onClick.AddListener(OpenCardNotes);
        }

        private void BuildTurnControls()
        {
            var energyPlate = CreateBasePanel(_canvasRoot, "EnergyPlate", new Vector2(-782, -289), new Vector2(270, 64));
            _energyText = CreateText(energyPlate, "Energy", Vector2.zero, new Vector2(248, 54), "红石 ◆ 1/1", 18, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);

            var roundPlate = CreateBasePanel(_canvasRoot, "RoundPlate", new Vector2(760, 472), new Vector2(250, 54));
            _roundText = CreateText(roundPlate, "Round", Vector2.zero, new Vector2(228, 38), "第 1 回合 · 主行动", 17, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);

            _endTurnButton = CreatePrimaryActionButton(_canvasRoot, "EndTurnButton", new Vector2(786, -355), new Vector2(230, 86), "结束回合", 23);
            _endTurnLabel = _endTurnButton.GetComponentInChildren<Text>();
            _endTurnButton.onClick.AddListener(OnEndTurn);
            ConfigureHoverScale(_endTurnButton.gameObject, 1.04f, 16f);

            var statusPlate = CreateBasePanel(_canvasRoot, "StatusPlate", new Vector2(810, -458), new Vector2(260, 86));
            _statusText = CreateText(statusPlate, "Status", Vector2.zero, new Vector2(230, 70), string.Empty, 14, Pale, TextAnchor.MiddleCenter, FontStyle.Normal);
            _statusSummary = _statusText.gameObject.AddComponent<DemoReadableSummary>();
            _statusSummary.Configure(18);
            _statusText.lineSpacing = 0.92f;
            _statusText.supportRichText = false;
        }

        private void BuildBanner()
        {
            var banner = CreateBasePanel(_canvasRoot, "TurnBanner", new Vector2(0, 8), new Vector2(570, 112));
            _turnBannerRestingPosition = banner.anchoredPosition;
            _hasTurnBannerRestingPosition = true;
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
            ConfigureHoverScale(_mulliganConfirmButton.gameObject, 1.035f, 16f);
            _mulliganOverlay.gameObject.SetActive(false);
        }

        private void BuildChoiceOverlay()
        {
            _choiceOverlay = CreateRect(_canvasRoot, "ChoiceOverlay", Vector2.zero, new Vector2(ReferenceWidth, ReferenceHeight));
            _choiceOverlayCanvasGroup = _choiceOverlay.gameObject.AddComponent<CanvasGroup>();
            var dim = _choiceOverlay.gameObject.AddComponent<Image>();
            dim.color = new Color(0.025f, 0.03f, 0.035f, 0.84f);
            dim.raycastTarget = true;

            _choicePanel = CreateBasePanel(_choiceOverlay, "ChoicePanel", new Vector2(0, 10), new Vector2(1080, 670));
            _choiceTitleText = CreateText(_choicePanel, "Title", new Vector2(0, 282), new Vector2(940, 54), "牌库选择", 30, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            _choiceRuleText = CreateText(_choicePanel, "Rule", new Vector2(0, 235), new Vector2(920, 44), string.Empty, 16, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            _choiceCardsRoot = CreateRect(_choicePanel, "InspectedCards", new Vector2(0, 20), new Vector2(780, 390));
            _choiceStatusText = CreateText(_choicePanel, "ChoiceStatus", new Vector2(0, -218), new Vector2(780, 44), string.Empty, 16, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            _choiceConfirmButton = CreatePrimaryActionButton(_choicePanel, "ConfirmChoice", new Vector2(0, -280), new Vector2(310, 66), "确认", 20);
            _choiceConfirmLabel = _choiceConfirmButton.GetComponentInChildren<Text>();
            _choiceConfirmButton.onClick.AddListener(ConfirmChoice);
            ConfigureHoverScale(_choiceConfirmButton.gameObject, 1.035f, 16f);
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
            _selectedCardTargetInstanceIds.Clear();
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
            SelectFirstHandCard();
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

        private void SetupTamedWolfPreview()
        {
            SelectFaction("plains_forest");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("pf_002", out var sheepDefinition) ||
                !_registry.TryGetDefinition("pf_003", out var wolfDefinition)) return;
            _match.ResetHand(new[] { sheepDefinition.id, wolfDefinition.id });
            var sheepDeployed = _match.ApplyDeploy(sheepDefinition,
                _match.CreateDeployCommand(sheepDefinition.id, DemoSlotKind.Unit, 0));
            var wolfDeployed = _match.ApplyDeploy(wolfDefinition,
                _match.CreateDeployCommand(wolfDefinition.id, DemoSlotKind.Unit, 1));
            _match.ResetHand(new[] { wolfDefinition.id });
            _selectedCardId = wolfDefinition.id;
            RefreshAll();
            var wolf = _match.GetObject(true, DemoSlotKind.Unit, 1);
            ShowStatus(sheepDeployed.Accepted && wolfDeployed.Accepted && wolf?.MaxHealth == 3
                ? "已部署的狼因相邻绵羊永久成长；金色地表会触发忠诚战吼，远端青绿色空格只会普通部署。"
                : !sheepDeployed.Accepted ? sheepDeployed.Message : wolfDeployed.Message,
                !sheepDeployed.Accepted || !wolfDeployed.Accepted || wolf?.MaxHealth != 3);
            _battlefield.SetSlotHovered(true, DemoSlotKind.Unit, 2, true);
        }

        private void SetupVillagerFarmerPreview()
        {
            SelectFaction("plains_forest");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("pf_004", out var farmerDefinition) ||
                !_registry.TryGetDefinition("tk_002", out var wheatDefinition) ||
                !_registry.TryGetDefinition("pf_002", out var sheepDefinition)) return;
            _match.ResetHand(new[] { sheepDefinition.id, farmerDefinition.id });
            var sheepDeployed = _match.ApplyDeploy(sheepDefinition,
                _match.CreateDeployCommand(sheepDefinition.id, DemoSlotKind.Unit, 0));
            var sheep = _match.GetObject(true, DemoSlotKind.Unit, 0);
            if (sheep != null) sheep.Health = Math.Max(1, sheep.Health - 1);
            var deployed = _match.ApplyDeploy(farmerDefinition,
                _match.CreateDeployCommand(farmerDefinition.id, DemoSlotKind.Unit, 1));
            RefreshAll();
            if (deployed.Accepted && _match.Hand.Contains(wheatDefinition.id))
            {
                SelectCard(wheatDefinition.id);
                CastSelectedCard();
            }
            ShowStatus(sheepDeployed.Accepted && deployed.Accepted && _match.Hand.Contains(wheatDefinition.id)
                ? "小麦已进入手牌并等待目标：选择受伤绵羊会恢复 1 点生命，并让动物本回合获得 +1 攻击。"
                : !sheepDeployed.Accepted ? sheepDeployed.Message : deployed.Message,
                !sheepDeployed.Accepted || !deployed.Accepted || !_match.Hand.Contains(wheatDefinition.id));
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

        private IEnumerator SetupEndReturnInteractionPreview()
        {
            SelectFaction("end");
            if (!_registry.TryGetDefinition("ed_003", out var endermanDefinition) ||
                !_registry.TryGetDefinition("ed_002", out var chorusFruitDefinition))
            {
                Debug.LogError("End return interaction preview could not resolve its registered cards.");
                yield break;
            }

            // This interaction preview exercises a three-cost unit followed by a one-cost spell.
            // Keep its fixture independent from the live match's 1/1 opening economy.
            ConfigureEndReturnInteractionPreview(endermanDefinition, chorusFruitDefinition);
            RefreshAll();
            yield return null;
            if (!ClickHandCardThroughEventSystem(endermanDefinition.id) || _selectedCardId != endermanDefinition.id)
            {
                Debug.LogError("End return interaction preview could not select the unit through the hand-card UI event system.");
                yield break;
            }
            if (!ClickBattlefieldSlotThroughPointer(true, DemoSlotKind.Unit, 1))
            {
                Debug.LogError("End return interaction preview could not raycast/click the intended deployment slot.");
                yield break;
            }

            var deployed = _match.GetObject(true, DemoSlotKind.Unit, 1);
            if (deployed == null || deployed.CardId != endermanDefinition.id)
            {
                Debug.LogError("End return interaction preview failed to deploy its unit through the battlefield click handler.");
                yield break;
            }

            yield return null;
            if (!ClickHandCardThroughEventSystem(chorusFruitDefinition.id) || _selectedCardId != chorusFruitDefinition.id)
            {
                Debug.LogError("End return interaction preview could not select the spell through the hand-card UI event system.");
                yield break;
            }
            yield return null;
            if (!ClickSelectedCardActionThroughEventSystem() || _pendingTargetCardId != chorusFruitDefinition.id)
            {
                Debug.LogError("End return interaction preview could not start spell targeting through the inspector UI button.");
                yield break;
            }
            if (!ClickBattlefieldSlotThroughPointer(true, DemoSlotKind.Unit, deployed.SlotIndex))
            {
                Debug.LogError("End return interaction preview could not raycast/click the intended return target.");
                yield break;
            }
            RefreshAll();
            StartCoroutine(ReportEndReturnInteractionPreviewAfterUiSettles());
        }

        private void ConfigureEndReturnInteractionPreview(
            CardDefinitionEntry endermanDefinition,
            CardDefinitionEntry chorusFruitDefinition)
        {
            if (endermanDefinition == null) throw new ArgumentNullException(nameof(endermanDefinition));
            if (chorusFruitDefinition == null) throw new ArgumentNullException(nameof(chorusFruitDefinition));
            // ED-003 costs 3 and ED-002 costs 1. Do not inherit the production 1/1 opening economy.
            _match.ResetPlayerRedstoneForScenario(4, 4);
            _match.ResetDeckAndHand(new[] { endermanDefinition.id, chorusFruitDefinition.id }, Array.Empty<string>());
        }

        private bool ClickHandCardThroughEventSystem(string cardId)
        {
            var handCard = MatchView.HandCards.FirstOrDefault(value => value != null && value.cardId == cardId);
            if (handCard == null) return false;
            var card = _handRoot.GetComponentsInChildren<CardUI>(true).FirstOrDefault(value =>
                value.CardId == cardId && value.HandCardInstanceId == handCard.handCardInstanceId);
            return ClickButtonThroughEventSystem(card?.GetComponent<Button>());
        }

        private bool DragHandCardToBattlefieldThroughEventSystem(
            string cardId, bool player, DemoSlotKind kind, int index)
        {
            var pointer = GetComponent<DemoBattlefieldPointerController>();
            var camera = _battlefield != null ? _battlefield.BoardCamera : null;
            if (pointer == null || camera == null) return false;
            var dropPosition3d = camera.WorldToScreenPoint(_battlefield.GetSlotInteractionWorldPosition(player, kind, index));
            var dropPosition = new Vector2(dropPosition3d.x, dropPosition3d.y);
            var blockedByUi = pointer.IsPointerOverUiAtPosition(dropPosition);
            var hitSlot = _battlefield.TryRaycastSlot(dropPosition, out var intendedTarget);
            if (blockedByUi || !hitSlot ||
                intendedTarget.Player != player || intendedTarget.Kind != kind || intendedTarget.Index != index)
            {
                Debug.LogError($"Hand drop did not resolve to its intended slot: blockedByUi={blockedByUi}, hitSlot={hitSlot}, target={(intendedTarget == null ? "<none>" : $"{intendedTarget.Player}/{intendedTarget.Kind}/{intendedTarget.Index}")}, expected={player}/{kind}/{index}.");
                return false;
            }

            if (!DragHandCardToScreenThroughEventSystem(cardId, dropPosition))
            {
                Debug.LogError($"Hand drag source did not resolve to its CardUI drag handler: card={cardId}.");
                return false;
            }
            var deployed = _match.GetObject(player, kind, index)?.CardId == cardId;
            if (!deployed) Debug.LogError($"Hand drag reached its target but did not deploy: card={cardId}, slot={player}/{kind}/{index}.");
            return deployed;
        }

        private bool DragHandCardToScreenThroughEventSystem(string cardId, Vector2 dropPosition)
        {
            var eventSystem = EventSystem.current;
            var card = _handRoot.GetComponentsInChildren<CardUI>(true).FirstOrDefault(value => value.CardId == cardId);
            var cardRect = card != null ? card.RectTransform : null;
            if (eventSystem == null || card == null || cardRect == null) return false;

            Canvas.ForceUpdateCanvases();
            var canvas = card.GetComponentInParent<Canvas>();
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var startPosition = RectTransformUtility.WorldToScreenPoint(eventCamera, cardRect.position);
            var pointerData = new PointerEventData(eventSystem)
            {
                position = startPosition,
                pressPosition = startPosition,
                button = PointerEventData.InputButton.Left,
                pointerDrag = card.gameObject,
                eligibleForClick = false
            };
            var raycastResults = new List<RaycastResult>();
            eventSystem.RaycastAll(pointerData, raycastResults);
            var topUiHit = raycastResults.FirstOrDefault(value => value.module is GraphicRaycaster);
            var dragHandler = topUiHit.gameObject != null
                ? ExecuteEvents.GetEventHandler<IBeginDragHandler>(topUiHit.gameObject)
                : null;
            if (dragHandler != card.gameObject)
            {
                Debug.LogError($"Hand drag source raycast mismatch: expected={card.gameObject.name}, top={topUiHit.gameObject?.name ?? "<none>"}, handler={dragHandler?.name ?? "<none>"}.");
                return false;
            }

            pointerData.pointerPressRaycast = topUiHit;
            ExecuteEvents.Execute(dragHandler, pointerData, ExecuteEvents.beginDragHandler);
            pointerData.position = dropPosition;
            pointerData.delta = dropPosition - startPosition;
            ExecuteEvents.Execute(dragHandler, pointerData, ExecuteEvents.dragHandler);
            ExecuteEvents.Execute(dragHandler, pointerData, ExecuteEvents.endDragHandler);
            return true;
        }

        private bool ClickSelectedCardActionThroughEventSystem() =>
            ClickButtonThroughEventSystem(FindSelectedCardActionButton());

        private Button FindSelectedCardActionButton()
        {
            var deployAction = _registry.TryGetDefinition(_selectedCardId, out var definition) &&
                (definition.cardType == "UNIT" || definition.cardType == "BUILDING" || definition.cardType == "STRUCTURE");
            var actionName = deployAction ? "BattlecryTarget" : "Cast";
            return _inspectorRoot.GetComponentsInChildren<Button>(true)
                .FirstOrDefault(value => value != null && value.gameObject.name == actionName);
        }

        private bool ClickButtonThroughEventSystem(Button button)
        {
            var eventSystem = EventSystem.current;
            var rect = button == null ? null : button.transform as RectTransform;
            if (eventSystem == null || rect == null || !button.isActiveAndEnabled || !button.interactable) return false;
            var buttonName = button.gameObject.name;

            Canvas.ForceUpdateCanvases();
            var canvas = button.GetComponentInParent<Canvas>();
            var eventCamera = canvas != null && canvas.renderMode != RenderMode.ScreenSpaceOverlay
                ? canvas.worldCamera
                : null;
            var screenPosition = RectTransformUtility.WorldToScreenPoint(eventCamera, rect.TransformPoint(rect.rect.center));
            var pointer = new PointerEventData(eventSystem)
            {
                position = screenPosition,
                button = PointerEventData.InputButton.Left,
                clickCount = 1,
                eligibleForClick = true
            };
            var raycastResults = new List<RaycastResult>();
            eventSystem.RaycastAll(pointer, raycastResults);
            var topUiHit = raycastResults.FirstOrDefault(value => value.module is GraphicRaycaster);
            if (topUiHit.gameObject == null ||
                ExecuteEvents.GetEventHandler<IPointerClickHandler>(topUiHit.gameObject) != button.gameObject)
            {
                var hitName = topUiHit.gameObject == null ? "<none>" : topUiHit.gameObject.name;
                var clickHandler = topUiHit.gameObject == null
                    ? null
                    : ExecuteEvents.GetEventHandler<IPointerClickHandler>(topUiHit.gameObject);
                var raycasterSummary = string.Join(";", Resources.FindObjectsOfTypeAll<GraphicRaycaster>()
                    .Where(value => value != null && value.gameObject.scene.IsValid())
                    .Select(value => $"{value.name}:enabled={value.isActiveAndEnabled}:mode={value.GetComponent<Canvas>()?.renderMode}"));
                var rootImage = button.GetComponent<Image>();
                Debug.Log($"UI click raycast missed {button.gameObject.name}: hits={raycastResults.Count}, top={hitName}, handler={clickHandler?.name ?? "<none>"}, screen={screenPosition}, screenSize={Screen.width}x{Screen.height}, rootRaycast={rootImage?.raycastTarget}, handBlocks={_handCanvasGroup?.blocksRaycasts}, raycasters={raycasterSummary}.");
                return false;
            }

            var pointerPress = ExecuteEvents.GetEventHandler<IPointerDownHandler>(topUiHit.gameObject) ?? button.gameObject;
            pointer.pointerPress = pointerPress;
            pointer.rawPointerPress = topUiHit.gameObject;
            ExecuteEvents.Execute(pointerPress, pointer, ExecuteEvents.pointerDownHandler);
            ExecuteEvents.Execute(pointerPress, pointer, ExecuteEvents.pointerUpHandler);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerClickHandler);
            Debug.Log($"UI click dispatched: {buttonName} at {screenPosition}.");
            return true;
        }

        private bool ClickBattlefieldSlotThroughPointer(bool player, DemoSlotKind kind, int index)
        {
            var pointer = GetComponent<DemoBattlefieldPointerController>();
            var camera = _battlefield?.BoardCamera;
            if (pointer == null || camera == null) return false;

            var screenPosition = camera.WorldToScreenPoint(_battlefield.GetSlotWorldPosition(player, kind, index));
            var pressedTarget = pointer.ProcessPointerFrame(screenPosition, true, false);
            var releasedTarget = pointer.ProcessPointerFrame(screenPosition, false, true);
            var clickedIntendedTarget = pressedTarget != null && pressedTarget == releasedTarget &&
                pressedTarget.Player == player && pressedTarget.Kind == kind && pressedTarget.Index == index;
            // A normal Unity Update supplies another frame at the real cursor position after a click.
            // Deterministic previews inject events directly, so explicitly leave the board hover state.
            pointer.ProcessPointerFrame(Vector2.zero, true, false, false);
            return clickedIntendedTarget;
        }

        private IEnumerator ReportEndReturnInteractionPreviewAfterUiSettles()
        {
            yield return null;
            yield return null;

            var pointer = GetComponent<DemoBattlefieldPointerController>();
            var endTurnButton = _endTurnButton.GetComponent<RectTransform>();
            if (pointer != null && endTurnButton != null)
            {
                var uiRaycastHit = pointer.IsPointerOverUiAtPosition(endTurnButton.position);
                var blockedUiDownTarget = pointer.ProcessPointerFrame(endTurnButton.position, true, false);
                var blockedUiUpTarget = pointer.ProcessPointerFrame(endTurnButton.position, false, true);
                _returnPreviewUiBlocked = uiRaycastHit && blockedUiDownTarget == null && blockedUiUpTarget == null;
                Debug.Log($"End-turn UI raycast hit: {uiRaycastHit}; board target blocked: {_returnPreviewUiBlocked}.");
            }

            var returnedUnit = _match.HandCards.FirstOrDefault(value => value != null && value.cardId == "ed_003");
            var settled = _match.GetObject(true, DemoSlotKind.Unit, 1) == null &&
                returnedUnit != null && returnedUnit.costModifier == -1 &&
                returnedUnit.expiresAtEndOfTurnPlayerId == "local-player" &&
                _match.DiscardPile.Contains("ed_002") && _returnPreviewUiBlocked;
            var returnedCost = returnedUnit != null && _registry.TryGetDefinition("ed_003", out var endermanDefinition)
                ? _match.GetEffectiveCost(endermanDefinition, returnedUnit.handCardInstanceId)
                : -1;
            if (settled)
            {
                _battlefield.PulseSlotRange(true, DemoSlotKind.Unit, 1, 1, Hex("#FFE27A"), 10f);
                ShowStatus("紫颂果已回手末影人：本回合费用 -1；单位格已清空。", false);
            }
            Debug.Log($"End return interaction preview settled: {settled} (3D slot raycast + pointer down/up + UI raycast gate + scene callbacks; returned discount {returnedCost}).");
            _returnPreviewCompleted = true;
        }

        private IEnumerator SetupCombatInteractionPreview()
        {
            SelectFaction("plains_forest");
            SelectOpponentFaction("desert_badlands");
            if (!_registry.TryGetDefinition("pf_001", out var beeDefinition) ||
                !_registry.TryGetDefinition("pf_002", out var attackerDefinition) ||
                !_registry.TryGetDefinition("db_001", out var huskDefinition))
            {
                Debug.LogError("Combat interaction preview could not resolve its registered cards.");
                yield break;
            }

            _match.ResetDeckAndHand(new[] { beeDefinition.id, attackerDefinition.id }, Array.Empty<string>());
            // This preview validates two real drag deployments (1-cost Bee + 2-cost Sheep)
            // before the first end turn and expects 7 energy after the turn cycle. Keep its
            // scenario economy explicit instead of depending on the live match's 1/1 opening.
            _match.ResetPlayerRedstoneForScenario(6, 6);
            _match.ResetOpponent(new[] { huskDefinition });
            RefreshAll();
            yield return null;
            if (!ClickHandCardThroughEventSystem(beeDefinition.id) || _selectedCardId != beeDefinition.id ||
                !ClickBattlefieldSlotThroughPointer(true, DemoSlotKind.Unit, 1) ||
                _match.GetObject(true, DemoSlotKind.Unit, 1)?.CardId != beeDefinition.id ||
                _match.HandCards.All(value => value == null || value.cardId != attackerDefinition.id))
            {
                Debug.LogError("Combat interaction preview regressed the click-to-select then click-slot deployment fallback.");
                yield break;
            }

            yield return null;
            var deploymentCard = _match.HandCards.SingleOrDefault(value => value != null && value.cardId == attackerDefinition.id);
            var energyBeforeInvalidDrop = _match.Energy;
            var invalidDropPosition = new Vector2(Screen.width * 0.5f, Screen.height * 0.5f);
            var invalidDropPointer = GetComponent<DemoBattlefieldPointerController>();
            if (deploymentCard == null || invalidDropPointer == null ||
                invalidDropPointer.IsPointerOverUiAtPosition(invalidDropPosition) ||
                _battlefield.TryRaycastSlot(invalidDropPosition, out _))
            {
                Debug.LogError("Combat interaction preview could not find an unblocked screen point outside every battlefield slot for the invalid-drop check.");
                yield break;
            }
            if (!DragHandCardToScreenThroughEventSystem(attackerDefinition.id, invalidDropPosition) ||
                !_match.HandCards.Any(value => value != null && value.handCardInstanceId == deploymentCard.handCardInstanceId) ||
                _match.Energy != energyBeforeInvalidDrop || _match.GetObject(true, DemoSlotKind.Unit, 0) != null)
            {
                Debug.LogError("Combat interaction preview consumed or lost the deployment card after a drop outside all battlefield slots.");
                yield break;
            }
            // Destroyed hand visuals are deferred until the frame ends in Player builds.
            // Let the replacement hand settle before the next synthetic UI raycast.
            yield return null;
            if (!DragHandCardToBattlefieldThroughEventSystem(attackerDefinition.id, true, DemoSlotKind.Unit, 0))
            {
                Debug.LogError("Combat interaction preview could not drag the hand unit through the UI event system and deploy it on the intended slot.");
                yield break;
            }

            var attacker = _match.GetObject(true, DemoSlotKind.Unit, 0);
            if (attacker == null || attacker.CardId != attackerDefinition.id)
            {
                Debug.LogError("Combat interaction preview failed to deploy its unit.");
                yield break;
            }

            yield return null;
            if (!ClickButtonThroughEventSystem(_endTurnButton) || _match.Phase != DemoTurnPhase.Combat)
            {
                Debug.LogError("Combat interaction preview could not enter combat through the turn button.");
                yield break;
            }
            if (!ClickButtonThroughEventSystem(_endTurnButton) || _match.IsPlayerTurn)
            {
                Debug.LogError("Combat interaction preview could not end its first player turn through the turn button.");
                yield break;
            }

            var timeout = 0f;
            while ((!_match.IsPlayerTurn || _match.Round < 2) && timeout < 5f)
            {
                timeout += Time.unscaledDeltaTime;
                yield return null;
            }
            if (!_match.IsPlayerTurn || _match.Round < 2)
            {
                Debug.LogError("Combat interaction preview did not return from the simulated opponent turn.");
                yield break;
            }
            yield return new WaitForSecondsRealtime(0.8f);
            if (!ClickButtonThroughEventSystem(_endTurnButton) || _match.Phase != DemoTurnPhase.Combat)
            {
                Debug.LogError("Combat interaction preview could not enter the next combat phase.");
                yield break;
            }
            if (!ClickBattlefieldSlotThroughPointer(true, DemoSlotKind.Unit, 0) ||
                _selectedAttackerInstanceId != attacker.InstanceId)
            {
                Debug.LogError("Combat interaction preview could not select the ready attacker through the battlefield pointer.");
                yield break;
            }
            if (!ClickBattlefieldSlotThroughPointer(false, DemoSlotKind.Unit, 0))
            {
                Debug.LogError("Combat interaction preview could not target the opposing unit through the battlefield pointer.");
                yield break;
            }

            // Let attack lunge, damage feedback, and the death pulse settle before capture reports completion.
            yield return new WaitForSecondsRealtime(0.9f);
            StartCoroutine(ReportCombatInteractionPreviewAfterUiSettles());
        }

        private IEnumerator ReportCombatInteractionPreviewAfterUiSettles()
        {
            yield return null;
            yield return null;

            var attacker = _match.GetObject(true, DemoSlotKind.Unit, 0);
            var settled = _match.Round == 2 && _match.IsPlayerTurn && _match.Phase == DemoTurnPhase.Combat &&
                attacker != null && attacker.CardId == "pf_002" && attacker.Health == 1 &&
                _match.GetObject(false, DemoSlotKind.Unit, 0) == null && _match.Energy == 7 &&
                string.IsNullOrEmpty(_selectedAttackerInstanceId);
            if (settled) ShowStatus("放牧绵羊完成首次攻击：敌方僵尸已击败；你的回合与战斗阶段仍在进行。", false);
            Debug.Log($"Combat interaction preview settled: {settled} (click-to-select fallback + invalid drop preserved hand/energy + hand drag-and-drop deploy + turn buttons + attacker/target raycasts; round {_match.Round}, energy {_match.Energy}, attacker health {attacker?.Health ?? 0}).");
            _combatPreviewCompleted = true;
        }

        private IEnumerator SetupStructureDragDeploymentPreview()
        {
            SelectFaction("desert_badlands");
            if (!_registry.TryGetDefinition("db_007", out var templeDefinition))
            {
                Debug.LogError("Structure drag preview could not resolve the registered Desert Temple.");
                yield break;
            }

            // The production opening turn now starts at 1/1; this deterministic preview needs the registered 5-cost structure.
            _match.ResetPlayerRedstoneForScenario(templeDefinition.cost, templeDefinition.cost);
            _match.ResetDeckAndHand(new[] { templeDefinition.id }, Array.Empty<string>());
            RefreshAll();
            yield return null;

            var energyBefore = _match.Energy;
            if (!DragHandCardToBattlefieldThroughEventSystem(templeDefinition.id, true, DemoSlotKind.Building, 1))
            {
                Debug.LogError("Structure drag preview could not drag the registered structure through the UI and onto building slot 2.");
                yield break;
            }

            var structure = _match.GetObject(true, DemoSlotKind.Building, 1);
            var settled = structure != null && structure.CardId == templeDefinition.id &&
                structure.SlotIndex == 1 && structure.OccupiedSlots == 2 &&
                string.IsNullOrEmpty(_match.BuildingSlots[0]) && _match.BuildingSlots[1] == templeDefinition.id &&
                _match.BuildingSlots[2] == templeDefinition.id && _match.Energy == energyBefore - templeDefinition.cost &&
                !_match.HandCards.Any(value => value != null && value.cardId == templeDefinition.id);
            if (settled) ShowStatus("沙漠神殿已拖放至建筑格 2–3，占用范围与费用结算正确。", false);
            Debug.Log($"Structure drag deployment preview settled: {settled} (building slots 2-3 occupied, slot 1 empty, energy {_match.Energy}).");
            _structureDragPreviewCompleted = true;
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

        private void SetupAttackFeedbackPreview()
        {
            SelectFaction("plains_forest");
            SelectOpponentFaction("desert_badlands");
            if (!_registry.TryGetDefinition("pf_008", out var ironGolemDefinition) ||
                !_registry.TryGetDefinition("db_001", out var huskDefinition)) return;
            _match.ResetDeckAndHand(new[] { ironGolemDefinition.id }, Array.Empty<string>());
            _match.ResetOpponent(new[] { huskDefinition });
            if (!_match.TryDeploy(ironGolemDefinition, DemoSlotKind.Unit, 0, out var deployMessage))
            {
                ShowStatus("攻击反馈预览初始化失败：" + deployMessage, true);
                return;
            }
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            if (!_match.ApplyEnterCombat(_match.CreateEnterCombatCommand()).Accepted)
            {
                ShowStatus("攻击反馈预览初始化失败：无法进入战斗阶段。", true);
                return;
            }
            var attacker = _match.GetObject(true, DemoSlotKind.Unit, 0);
            var target = _match.GetObject(false, DemoSlotKind.Unit, 0);
            if (attacker == null || target == null)
            {
                ShowStatus("攻击反馈预览初始化失败：攻击者或目标缺失。", true);
                return;
            }
            RefreshAll();
            ResolveAttack(attacker.InstanceId, attacker, "UNIT", target.InstanceId);
            var attackerAfter = _match.GetObject(true, DemoSlotKind.Unit, 0);
            var targetAfter = _match.GetObject(false, DemoSlotKind.Unit, 0);
            var statusShowsRevision = _statusText != null && _statusText.text.Contains("状态 r");
            var resolved = attackerAfter != null && attackerAfter.Health == 5 && targetAfter == null && _match.Phase == DemoTurnPhase.Combat;
            Debug.Log($"Attack feedback preview settled: {resolved} (golem health {attackerAfter?.Health ?? 0}, target alive {targetAfter != null}, phase {_match.Phase}).");
            Debug.Log($"Local action status hides internal revision: {!statusShowsRevision}.");
        }

        private void SetupButtonFeedbackPreview()
        {
            var button = _canvasRoot != null
                ? _canvasRoot.Find("FactionRail/Faction_desert_badlands")?.GetComponent<Button>()
                : null;
            var eventSystem = EventSystem.current;
            if (button == null || eventSystem == null || !button.IsInteractable())
            {
                Debug.LogError("Button feedback preview could not find an interactable desert faction button.");
                return;
            }

            var rect = button.transform as RectTransform;
            var pointer = new PointerEventData(eventSystem)
            {
                button = PointerEventData.InputButton.Left,
                position = rect != null
                    ? RectTransformUtility.WorldToScreenPoint(null, rect.position)
                    : Vector2.zero
            };
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerEnterHandler);
            var feedback = button.GetComponent<DemoHoverScale>();
            feedback?.PinHoverForPreview();
            eventSystem.SetSelectedGameObject(button.gameObject);
            ExecuteEvents.Execute(button.gameObject, pointer, ExecuteEvents.pointerDownHandler);
            var pressed = feedback != null && Mathf.Approximately(feedback.TargetScale, 0.97f);
            Debug.Log($"Button feedback preview pressed state: {pressed} (button {button.name}).");
        }

        private void SetupOpponentEnergyPreview()
        {
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _match.EndPlayerTurn();
            RefreshAll();
            var actual = _opponentEnergyText != null ? _opponentEnergyText.text : string.Empty;
            var settled = !_match.IsPlayerTurn && _match.Round == 2 &&
                _match.OpponentEnergy == 2 && _match.OpponentMaxEnergy == 2 && actual == "敌方红石 ◆ 2/2";
            Debug.Log($"Opponent energy preview settled: {settled} (round {_match.Round}; opponent energy {_match.OpponentEnergy}/{_match.OpponentMaxEnergy}; HUD '{actual}').");
        }

        private void SetupMatchOutcomePreview()
        {
            SetupHeroLethalPreview(false);
        }

        private void SetupTerminalWorldPreview()
        {
            SetupHeroLethalPreview(true);
        }

        private void SetupHeroLethalPreview(bool populateReactiveBuildings)
        {
            SelectFaction("ocean_river");
            SelectOpponentFaction(populateReactiveBuildings ? "snow_ice" : "desert_badlands");
            if (!_registry.TryGetDefinition("or_006", out var trident))
            {
                ShowStatus("终局预览初始化失败：未找到激流三叉戟。", true);
                return;
            }

            // The production first turn now starts at 1/1; this deterministic preview must pay for the registered 3-cost weapon.
            var activeCuesWereReady = true;
            if (populateReactiveBuildings)
            {
                if (!_registry.TryGetDefinition("or_007", out var coral) ||
                    !_registry.TryGetDefinition("si_008", out var iceSpire))
                    throw new InvalidOperationException("Terminal-world preview requires the registered reactive buildings.");
                var budget = trident.cost + coral.cost;
                _match.ResetPlayerRedstoneForScenario(budget, budget);
                _match.ResetHand(new[] { coral.id, trident.id });
                _match.ResetOpponent(new[] { iceSpire });
                var deployed = _match.ApplyDeploy(coral,
                    _match.CreateDeployCommand(coral.id, DemoSlotKind.Building, 0));
                if (!deployed.Accepted) throw new InvalidOperationException(deployed.Message);
                RefreshAll();
                activeCuesWereReady = _battlefield.HasActiveGameplayHighlights;
            }
            else
            {
                _match.ResetPlayerRedstoneForScenario(trident.cost, trident.cost);
                _match.ResetOpponent(Array.Empty<CardDefinitionEntry>());
                _match.ResetHand(new[] { trident.id });
            }
            _match.ResetOpponentLife(2);
            var equipped = _match.ApplyPlayCard(trident, _match.CreatePlayCardCommand(trident.id));
            var enteredCombat = equipped.Accepted
                ? _match.ApplyEnterCombat(_match.CreateEnterCombatCommand())
                : DemoCommandResult.Reject(DemoCommandRejectionCode.InvalidCommand, equipped.Message, _match.Revision);
            var attacked = enteredCombat.Accepted
                ? _match.ApplyAttack(_match.CreateAttackCommand(MatchAttackerIds.Hero, "HERO"))
                : DemoCommandResult.Reject(DemoCommandRejectionCode.InvalidCommand, enteredCombat.Message, _match.Revision);
            RefreshAll();

            var settled = equipped.Accepted && enteredCombat.Accepted && attacked.Accepted &&
                _match.IsFinished && _match.HasWinner && _match.IsPlayerWinner &&
                _match.PlayerLife == 30 && _match.OpponentLife == 0;
            if (!settled) ShowStatus("终局预览初始化失败：未能通过英雄攻击结束对局。", true);
            StartCoroutine(ShowTurnBanner(settled ? "胜利" : "预览失败", settled ? Cyan : Danger));
            _matchOutcomePreviewCompleted = settled;
            Debug.Log($"Match outcome preview settled: {settled} (result={(_match.IsPlayerWinner ? "win" : "unknown")}; player life {_match.PlayerLife}; opponent life {_match.OpponentLife}).");
            if (populateReactiveBuildings)
            {
                var terminalWorldSettled = settled && activeCuesWereReady && !_battlefield.HasActiveGameplayHighlights &&
                    GetComponent<DemoBattlefieldPointerController>()?.InputEnabled == false &&
                    _match.GetObject(true, DemoSlotKind.Building, 0)?.CardId == "or_007" &&
                    _match.GetObject(false, DemoSlotKind.Building, 0)?.CardId == "si_008";
                Debug.Log($"Terminal world preview settled: {terminalWorldSettled} (reactive buildings remained; gameplay highlights cleared; pointer disabled).");
                if (!terminalWorldSettled) Debug.LogError("Terminal world presentation did not settle after a legal lethal hero attack.");
            }
        }

        private void SetupTntTrapOwnerWinsPreview()
        {
            SelectFaction("desert_badlands");
            SelectOpponentFaction("ocean_river");
            _match.ResetPlayerLife(1);
            _match.ResetOpponentLife(3);
            _match.ResetDeckAndHand(Array.Empty<string>(), new[] { "pf_001", "tk_008" }, new[] { "tk_008" });
            _match.EndPlayerTurn();
            var draw = _match.BeginNextPlayerTurn();
            RefreshAll();
            var shown = TryShowLocalMatchOutcome();
            var settled = draw.Outcome == DemoDrawOutcome.MatchEnded && shown && _match.IsFinished &&
                _match.HasWinner && _match.IsPlayerWinner && _match.PlayerLife == 0 && _match.OpponentLife == 0;
            if (!settled) ShowStatus("炸药机关双杀预览失败：出土方未取得胜利。", true);
            Debug.Log($"DB-007 simultaneous lethal preview settled: {settled} (result={(_match.IsPlayerWinner ? "win" : !_match.HasWinner ? "draw" : "loss")}; player life {_match.PlayerLife}; opponent life {_match.OpponentLife}).");
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

        private void SetupIceSpirePreview()
        {
            SelectFaction("plains_forest");
            SelectOpponentFaction("snow_ice");
            if (!_registry.TryGetDefinition("pf_007", out var rallyDefinition) ||
                !_registry.TryGetDefinition("si_008", out var iceSpireDefinition)) return;
            _match.ResetDeckAndHand(new[] { rallyDefinition.id }, new[] { "pf_001" });
            _match.ResetOpponent(new[] { iceSpireDefinition });
            var result = _match.ApplyPlayCard(rallyDefinition,
                _match.CreatePlayCardCommand(rallyDefinition.id));
            var slowed = _match.PlayerBattlefield.Where(value => value.CardId == "tk_004" &&
                    value.HasStatus("SLOW"))
                .OrderBy(value => value.SlotIndex)
                .ToArray();
            _selectedCardId = iceSpireDefinition.id;
            RefreshAll();
            ShowStatus(result.Accepted && slowed.Length == 2
                ? "冰刺之巅按每次召唤前的空位边界截获林间集结：两个林地伙伴均在边缘落位并获得缓慢；普通手牌部署不会触发。"
                : result.Message, !result.Accepted || slowed.Length != 2);
            var iceSpire = _match.GetObject(false, DemoSlotKind.Building, 0);
            if (iceSpire != null) StartCoroutine(PulseBattlefieldObject(iceSpire.InstanceId));
            foreach (var target in slowed) StartCoroutine(PulseBattlefieldObject(target.InstanceId));
        }

        private void SetupGoatPreview()
        {
            SelectFaction("snow_ice");
            SelectOpponentFaction("ocean_river");
            if (!_registry.TryGetDefinition("si_004", out var goatDefinition) ||
                !_registry.TryGetDefinition("pf_002", out var sheepDefinition) ||
                !_registry.TryGetDefinition("or_002", out var dolphinDefinition) ||
                !_registry.TryGetDefinition("or_004", out var guardianDefinition)) return;
            _match.ResetHand(new[] { sheepDefinition.id, dolphinDefinition.id });
            var sheepDeployed = _match.ApplyDeploy(sheepDefinition,
                _match.CreateDeployCommand(sheepDefinition.id, DemoSlotKind.Unit, 0));
            var dolphinDeployed = _match.ApplyDeploy(dolphinDefinition,
                _match.CreateDeployCommand(dolphinDefinition.id, DemoSlotKind.Unit, 3));
            _match.ResetOpponent(new[] { guardianDefinition });
            var target = _match.GetObject(true, DemoSlotKind.Unit, 0);
            _match.ResetHand(new[] { goatDefinition.id });
            var deployed = target == null
                ? DemoCommandResult.Reject(DemoCommandRejectionCode.InvalidTarget, "缺少山羊越位目标。", _match.Revision)
                : _match.ApplyDeploy(goatDefinition, _match.CreateDeployCommand(
                    goatDefinition.id, DemoSlotKind.Unit, 1, MatchPaymentMethods.Redstone, "UNIT", target.InstanceId));
            _selectedCardId = goatDefinition.id;
            RefreshAll();
            var goat = _match.GetObject(true, DemoSlotKind.Unit, 1);
            ShowStatus(sheepDeployed.Accepted && dolphinDeployed.Accepted && deployed.Accepted && target?.SlotIndex == 2 && goat?.Attack == 4
                ? "山羊已落在中间格，把绵羊从 1 号格越位推到 3 号格：海豚先为移动单位加攻、守卫者随后射击，山羊自身本回合显示为 4 攻。"
                : !sheepDeployed.Accepted ? sheepDeployed.Message : !dolphinDeployed.Accepted ? dolphinDeployed.Message : deployed.Message,
                !sheepDeployed.Accepted || !dolphinDeployed.Accepted || !deployed.Accepted || target?.SlotIndex != 2 || goat?.Attack != 4);
            if (goat != null) StartCoroutine(PulseBattlefieldObject(goat.InstanceId));
            if (target != null) StartCoroutine(PulseBattlefieldObject(target.InstanceId));
            var guardian = _match.GetObject(false, DemoSlotKind.Unit, 0);
            if (guardian != null) StartCoroutine(PulseBattlefieldObject(guardian.InstanceId));
        }

        private void SetupSnowHutPreview()
        {
            SelectFaction("snow_ice");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("si_007", out var hutDefinition) ||
                !_registry.TryGetDefinition("pf_001", out var beeDefinition)) return;
            _match.ResetDeckAndHand(new[] { hutDefinition.id, beeDefinition.id, beeDefinition.id }, new[] { "si_001" });
            var hutResult = _match.ApplyDeploy(hutDefinition,
                _match.CreateDeployCommand(hutDefinition.id, DemoSlotKind.Building, 1));
            var leftResult = _match.ApplyDeploy(beeDefinition,
                _match.CreateDeployCommand(beeDefinition.id, DemoSlotKind.Unit, 0));
            var rightResult = _match.ApplyDeploy(beeDefinition,
                _match.CreateDeployCommand(beeDefinition.id, DemoSlotKind.Unit, 2));
            var left = _match.GetObject(true, DemoSlotKind.Unit, 0);
            var right = _match.GetObject(true, DemoSlotKind.Unit, 2);
            if (left != null) left.Health = 1;
            if (right != null) right.Health = 1;
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _selectedCardId = hutDefinition.id;
            RefreshAll();
            var ready = hutResult.Accepted && leftResult.Accepted && rightResult.Accepted &&
                _match.PendingChoice?.kind == "HEAL_UNIT";
            ShowStatus(ready
                ? "雪屋在起始阶段发现两个并列最重伤单位：直接点击场内发光的蜜蜂完成治疗，选择期间其他行动均被锁定。"
                : "雪屋起始阶段预览初始化失败。", !ready);
            var hut = _match.GetObject(true, DemoSlotKind.Building, 1);
            if (hut != null) StartCoroutine(PulseBattlefieldObject(hut.InstanceId));
        }

        private void SetupEndCrystalPreview()
        {
            SelectFaction("end");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("ed_007", out var crystalDefinition) ||
                !_registry.TryGetDefinition("pf_008", out var golemDefinition)) return;
            _match.ResetHand(Array.Empty<string>());
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _match.ResetHand(new[] { crystalDefinition.id });
            _match.ResetOpponent(new[] { golemDefinition });
            var result = _match.ApplyDeploy(crystalDefinition,
                _match.CreateDeployCommand(crystalDefinition.id, DemoSlotKind.Building, 1));
            _match.ResetHand(new[] { crystalDefinition.id });
            _selectedCardId = crystalDefinition.id;
            RefreshAll();
            ShowStatus(result.Accepted
                ? "末影水晶已蓄能：己方结束阶段对敌方英雄造成 2 点普通伤害；被摧毁时会反噬拥有者 2 点真实伤害。"
                : result.Message, !result.Accepted);
            var crystal = _match.GetObject(true, DemoSlotKind.Building, 1);
            if (crystal != null) StartCoroutine(PulseBattlefieldObject(crystal.InstanceId));
        }

        private void SetupPiglinMagmaPreview()
        {
            SelectFaction("nether");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("nt_002", out var piglinDefinition)) return;
            _match.ResetDeckAndHand(new[] { piglinDefinition.id, piglinDefinition.id }, new[] { "nt_001" });
            var left = _match.ApplyDeploy(piglinDefinition,
                _match.CreateDeployCommand(piglinDefinition.id, DemoSlotKind.Unit, 0));
            var right = _match.ApplyDeploy(piglinDefinition,
                _match.CreateDeployCommand(piglinDefinition.id, DemoSlotKind.Unit, 2));
            var ended = left.Accepted && right.Accepted
                ? _match.ApplyEndTurn(_match.CreateEndTurnCommand())
                : DemoCommandResult.Reject(DemoCommandRejectionCode.InvalidCommand,
                    !left.Accepted ? left.Message : right.Message, _match.Revision);
            _match.ResetHand(new[] { piglinDefinition.id });
            _selectedCardId = piglinDefinition.id;
            RefreshAll();
            var resolved = ended.Accepted && _match.OpponentLife == 28 && _match.Energy == 0;
            ShowStatus(resolved
                ? "两只僵尸猪灵按单位格顺序各支付 1 点红石，岩浆共造成 2 点普通伤害；当前能量为 0。"
                : ended.Message, !resolved);
            var firstPiglin = _match.GetObject(true, DemoSlotKind.Unit, 0);
            if (firstPiglin != null) StartCoroutine(PulseBattlefieldObject(firstPiglin.InstanceId));
        }

        private void SetupRespawnAnchorPreview()
        {
            SelectFaction("nether");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("nt_007", out var anchorDefinition) ||
                !_registry.TryGetDefinition("nt_006", out var sacrificeDefinition)) return;
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _match.ResetDeckAndHand(new[] { anchorDefinition.id, anchorDefinition.id, sacrificeDefinition.id },
                new[] { "nt_001" });
            var left = _match.ApplyDeploy(anchorDefinition,
                _match.CreateDeployCommand(anchorDefinition.id, DemoSlotKind.Building, 0));
            var right = _match.ApplyDeploy(anchorDefinition,
                _match.CreateDeployCommand(anchorDefinition.id, DemoSlotKind.Building, 2));
            var castMessage = !left.Accepted ? left.Message : right.Message;
            var cast = left.Accepted && right.Accepted && _match.TryCast(sacrificeDefinition, out castMessage);
            _match.ResetHand(new[] { anchorDefinition.id });
            _selectedCardId = anchorDefinition.id;
            RefreshAll();
            var resolved = cast && _match.TemporaryEnergy == 2 && _match.Energy == 2;
            ShowStatus(resolved
                ? "己方英雄首次实际掉血：两座重生锚按建筑格顺序各提供 1 点临时红石；当前 2 点均会在本回合结束时过期。"
                : castMessage, !resolved);
            var firstAnchor = _match.GetObject(true, DemoSlotKind.Building, 0);
            var secondAnchor = _match.GetObject(true, DemoSlotKind.Building, 2);
            if (firstAnchor != null) StartCoroutine(PulseBattlefieldObject(firstAnchor.InstanceId));
            if (secondAnchor != null) StartCoroutine(PulseBattlefieldObject(secondAnchor.InstanceId));
        }

        private void SetupNetherTriggerLifecyclePreview()
        {
            SelectFaction("nether");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("nt_002", out var piglinDefinition) ||
                !_registry.TryGetDefinition("nt_007", out var anchorDefinition)) return;

            _match.ResetDeckAndHand(Array.Empty<string>(), new[] { "nt_001", "nt_001", "nt_001" });
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _match.ResetPlayerRedstoneForScenario(8, 8);
              _match.ResetDeckAndHand(
                  new[] { piglinDefinition.id, anchorDefinition.id, anchorDefinition.id },
                  Array.Empty<string>());
              var anchorInstanceIds = _match.HandCards.Skip(1).Select(card => card.handCardInstanceId).ToArray();
              var piglinResult = _match.ApplyDeploy(piglinDefinition,
                  _match.CreateDeployCommand(piglinDefinition.id, DemoSlotKind.Unit, 0));
              var leftAnchorResult = _match.ApplyDeploy(anchorDefinition,
                  _match.CreateDeployCommand(anchorDefinition.id, DemoSlotKind.Building, 0,
                      handCardInstanceId: anchorInstanceIds[0]));
              var rightAnchorResult = _match.ApplyDeploy(anchorDefinition,
                  _match.CreateDeployCommand(anchorDefinition.id, DemoSlotKind.Building, 2,
                      handCardInstanceId: anchorInstanceIds[1]));
            var setupReady = piglinResult.Accepted && leftAnchorResult.Accepted && rightAnchorResult.Accepted;
            if (setupReady)
            {
                _match.EndPlayerTurn();
                _match.BeginNextPlayerTurn();
            }
            var piglin = _match.GetObject(true, DemoSlotKind.Unit, 0);
            var triggered = setupReady && piglin != null && piglin.Attack == 3 && piglin.Health == 3 &&
                _match.PlayerLife == 29 && _match.TemporaryEnergy == 2 && _match.Energy == 11 && _match.MaxEnergy == 9;
            var ended = triggered
                ? _match.ApplyEndTurn(_match.CreateEndTurnCommand())
                : DemoCommandResult.Reject(DemoCommandRejectionCode.InvalidCommand,
                    "下界触发生命周期预览初始化失败。", _match.Revision);

            _match.ResetHand(new[] { piglinDefinition.id });
            SelectCard(piglinDefinition.id);
            var resolved = ended.Accepted && piglin != null && piglin.Attack == 3 && piglin.Health == 3 &&
                _match.OpponentLife == 29 && _match.TemporaryEnergy == 0 && _match.Energy == 9 && _match.MaxEnergy == 9;
            ShowStatus(resolved
                ? "完整链路：疲劳首次掉血 → 猪灵成长 3/3 → 两座锚获得临时红石 +2 → 岩浆消耗 1 并造成 1 伤害 → 剩余 1 点过期；基础红石保持 9/9。"
                : ended.Message, !resolved);
            if (piglin != null) StartCoroutine(PulseBattlefieldObject(piglin.InstanceId));
            var firstAnchor = _match.GetObject(true, DemoSlotKind.Building, 0);
            if (firstAnchor != null) StartCoroutine(PulseBattlefieldObject(firstAnchor.InstanceId));
        }

        private void SetupNetherStatusSummonPreview()
        {
            SelectFaction("nether");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("nt_004", out var striderDefinition) ||
                !_registry.TryGetDefinition("nt_005", out var witherSkeletonDefinition) ||
                !_registry.TryGetDefinition("nt_008", out var fortressDefinition) ||
                !_registry.TryGetDefinition("pf_002", out var sheepDefinition) ||
                !_registry.TryGetDefinition("or_005", out var turtleDefinition)) return;

            _match.ResetPlayerRedstoneForScenario(10, 10);
            _match.ResetDeckAndHand(Array.Empty<string>(), new[] { "nt_001", "nt_001", "nt_001", "nt_001" });
            _match.ResetOpponent(new[] { turtleDefinition }, new[] { 2 });
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();

            _match.ResetHand(new[] { fortressDefinition.id });
            var fortressResult = _match.ApplyDeploy(fortressDefinition,
                _match.CreateDeployCommand(fortressDefinition.id, DemoSlotKind.Building, 0));
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _match.ResetPlayerRedstoneForScenario(10, 10);

            _match.ResetHand(new[] { sheepDefinition.id, witherSkeletonDefinition.id, striderDefinition.id });
            var sheepResult = _match.ApplyDeploy(sheepDefinition,
                _match.CreateDeployCommand(sheepDefinition.id, DemoSlotKind.Unit, 3));
            var sheep = _match.GetObject(true, DemoSlotKind.Unit, 3);
            if (sheep != null)
            {
                sheep.Health = Mathf.Max(1, sheep.Health - 1);
                sheep.Statuses = new[]
                {
                    new BattlefieldStatusStateDto
                    {
                        statusId = "FIRE", remainingDuration = 2, sourcePlayerId = "opponent",
                        sourceCardId = "nt_003", sourceInstanceId = "preview-fire-source", effectId = "effect.nt_003.01"
                    }
                };
            }
            var skeletonResult = _match.ApplyDeploy(witherSkeletonDefinition,
                _match.CreateDeployCommand(witherSkeletonDefinition.id, DemoSlotKind.Unit, 1));
            var striderResult = sheep == null
                ? DemoCommandResult.Reject(DemoCommandRejectionCode.InvalidTarget,
                    "净火目标初始化失败。", _match.Revision)
                : _match.ApplyDeploy(striderDefinition, _match.CreateDeployCommand(
                    striderDefinition.id, DemoSlotKind.Unit, 2, MatchPaymentMethods.Redstone, "UNIT", sheep.InstanceId));

            _match.ResetPlayerRedstoneForScenario(2, 10);
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            var enteredCombat = _match.ApplyEnterCombat(_match.CreateEnterCombatCommand());
            var skeleton = _match.GetObject(true, DemoSlotKind.Unit, 1);
            var turtle = _match.GetObject(false, DemoSlotKind.Unit, 2);
            var attackResult = skeleton == null || turtle == null
                ? DemoCommandResult.Reject(DemoCommandRejectionCode.InvalidTarget,
                    "凋零目标初始化失败。", _match.Revision)
                : _match.ApplyAttack(_match.CreateAttackCommand(skeleton.InstanceId, "UNIT", turtle.InstanceId));
            var endResult = attackResult.Accepted
                ? _match.ApplyEndTurn(_match.CreateEndTurnCommand())
                : DemoCommandResult.Reject(DemoCommandRejectionCode.InvalidCommand, attackResult.Message, _match.Revision);
            if (endResult.Accepted) _match.BeginNextPlayerTurn();

            _match.ResetHand(new[] { striderDefinition.id, witherSkeletonDefinition.id, fortressDefinition.id });
            _selectedCardId = fortressDefinition.id;
            RefreshAll();

            var fortress = _match.GetObject(true, DemoSlotKind.Building, 0);
            var summoned = _match.GetObject(true, DemoSlotKind.Unit, 0);
            var strider = _match.GetObject(true, DemoSlotKind.Unit, 2);
            turtle = _match.GetObject(false, DemoSlotKind.Unit, 2);
            var wither = turtle?.Statuses?.FirstOrDefault(value => value != null && value.statusId == "WITHER");
            var resolved = fortressResult.Accepted && sheepResult.Accepted && skeletonResult.Accepted && striderResult.Accepted &&
                enteredCombat.Accepted && attackResult.Accepted && endResult.Accepted && fortress?.CardId == fortressDefinition.id &&
                summoned?.CardId == "tk_015" && strider?.CardId == striderDefinition.id &&
                sheep != null && sheep.Health == sheep.MaxHealth && !sheep.HasStatus("FIRE") &&
                wither?.remainingDuration == 1 && turtle.Health == 1;
            ShowStatus(resolved
                ? "演示完成：炽足兽净火并治疗 +1；凋灵骷髅施加并触发凋零；下界要塞支付 1 点，在最左空格召唤 3/3 令牌。"
                : "下界状态与召唤预览初始化失败。", !resolved);
            if (fortress != null) StartCoroutine(PulseBattlefieldObject(fortress.InstanceId));
            if (summoned != null) StartCoroutine(PulseBattlefieldObject(summoned.InstanceId));
            if (strider != null) StartCoroutine(PulseBattlefieldObject(strider.InstanceId));
            if (turtle != null) StartCoroutine(PulseBattlefieldObject(turtle.InstanceId));
        }

        private void SetupSnowGolemPreview()
        {
            SelectFaction("snow_ice");
            SelectOpponentFaction("nether");
            if (!_registry.TryGetDefinition("si_002", out var snowGolemDefinition) ||
                !_registry.TryGetDefinition("nt_003", out var blazeDefinition)) return;
            _match.ResetHand(new[] { snowGolemDefinition.id });
            _match.ResetOpponent(new[] { blazeDefinition });
            var result = _match.ApplyDeploy(snowGolemDefinition,
                _match.CreateDeployCommand(snowGolemDefinition.id, DemoSlotKind.Unit, 1));
            RefreshAll();
            if (result.Accepted && _match.Hand.Contains("si_001")) SelectCard("si_001");
            ShowStatus(result.Accepted
                ? "雪傀儡战吼已凝聚雪球；选择右侧“释放卡牌”，再点击敌方单位所在的发光地表。"
                : result.Message, !result.Accepted);
            var snowGolem = _match.GetObject(true, DemoSlotKind.Unit, 1);
            if (result.Accepted && snowGolem != null) StartCoroutine(PulseBattlefieldObject(snowGolem.InstanceId));
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

        private void SetupPrismarineShardPreview()
        {
            SelectFaction("ocean_river");
            SelectOpponentFaction("ocean_river");
            if (!_registry.TryGetDefinition("or_001", out var salmonDefinition) ||
                !_registry.TryGetDefinition("or_004", out var guardianDefinition) ||
                !_registry.TryGetDefinition("tk_012", out var shardDefinition)) return;
            _match.ResetDeckAndHand(new[] { salmonDefinition.id }, Array.Empty<string>());
            _match.ResetOpponent(new[] { guardianDefinition });
            var deployed = _match.ApplyDeploy(salmonDefinition,
                _match.CreateDeployCommand(salmonDefinition.id, DemoSlotKind.Unit, 1));
            if (deployed.Accepted && _match.PendingChoice != null)
                _match.ApplyResolveChoice(_match.CreateResolveChoiceCommand(_match.PendingChoice.choiceId, -1));
            var salmon = _match.GetObject(true, DemoSlotKind.Unit, 1);
            if (salmon == null) return;
            salmon.Health = 1;
            _match.ResetHand(new[] { shardDefinition.id });
            var played = _match.ApplyPlayCard(shardDefinition,
                _match.CreatePlayCardCommand(shardDefinition.id, "UNIT", salmon.InstanceId));
            _selectedCardId = null;
            RefreshAll();
            ShowStatus(played.Accepted
                ? "海晶碎片已锁定受伤的鲑鱼群：必须点击相邻发光地块；落位后先治疗，再结算守卫者射线。"
                : played.Message, !played.Accepted);
            if (played.Accepted) StartCoroutine(PulseBattlefieldObject(salmon.InstanceId));
        }

        private void SetupDesertVillagerSurfacePreview()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SelectFaction("desert_badlands");
            SelectOpponentFaction("desert_badlands");
            if (!_registry.TryGetDefinition("db_003", out var villager))
                throw new InvalidOperationException("Registered desert villager definition is missing.");
            _match.ResetPlayerRedstoneForScenario(10, 10);
            _match.ResetOpponent(new[] { villager }, new[] { 1 });
            _match.ResetDeckAndHand(new[] { villager.id }, Array.Empty<string>());
            var deployed = _match.ApplyDeploy(villager, _match.CreateDeployCommand(villager.id, DemoSlotKind.Unit, 1));
            if (!deployed.Accepted) throw new InvalidOperationException(deployed.Message);
            if (_match.PendingChoice != null)
            {
                var resolved = _match.ApplyResolveChoice(_match.CreateResolveChoiceCommand(_match.PendingChoice.choiceId, -1));
                if (!resolved.Accepted) throw new InvalidOperationException(resolved.Message);
            }
            _selectedCardId = null;
            _selectedHandCardInstanceId = null;
            RefreshAll();
            ShowStatus("村民材质审查：基础皮肤与沙漠服装同网格合成。", false);
#else
            throw new NotSupportedException("Entity surface diagnostics require a Development build.");
#endif
        }

        private void AuditDesertVillagerSurfaceCapture()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Physics.SyncTransforms();
            foreach (var player in new[] { true, false })
            {
                var villager = _match.GetObject(player, DemoSlotKind.Unit, 1);
                var piece = _battlefield.FindPieceTransform(villager?.InstanceId);
                if (villager?.CardId != "db_003" || piece == null || piece.GetComponent<DemoEntityIdleAnimator>() == null)
                    throw new InvalidOperationException("Desert villager must retain real model and idle animation.");
                var renderers = piece.Find("Model")?.GetComponentsInChildren<MeshRenderer>();
                if (renderers == null || renderers.Length == 0) throw new InvalidOperationException("Empty villager model.");
                foreach (var renderer in renderers)
                {
                    var material = renderer.sharedMaterial;
                    if (material.shader.name != "BiomeRivals/Demo/Entity" || material.GetFloat("_UseSurfaceOverlay") != 1 ||
                        material.mainTexture != DemoWorldAssetProvider.LoadBlockTexture("entity_villager") ||
                        material.GetTexture("_SurfaceOverlayTex") != DemoWorldAssetProvider.LoadBlockTexture("entity_villager_desert"))
                        throw new InvalidOperationException("Villager skin/biome surface is missing or substituted.");
                }
                var point = _battlefield.BoardCamera.WorldToScreenPoint(_battlefield.GetSlotInteractionWorldPosition(player, DemoSlotKind.Unit, 1));
                if (point.z <= 0 || !_battlefield.TryRaycastSlot(point, out var slot) || slot.Player != player ||
                    slot.Kind != DemoSlotKind.Unit || slot.Index != 1)
                    throw new InvalidOperationException("Original villager deployment slot raycast failed.");
            }
            Debug.Log("Desert villager surface audit: True (both sides, skin plus biome, idle and original slot raycast).");
#endif
        }

        private void SetupTurtlePosePreview()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SelectFaction("ocean_river");
            SelectOpponentFaction("ocean_river");
            if (!_registry.TryGetDefinition("or_005", out var turtle))
                throw new InvalidOperationException("Registered turtle definition is missing.");
            _match.ResetPlayerRedstoneForScenario(10, 10);
            _match.ResetOpponent(new[] { turtle }, new[] { 1 });
            _match.ResetDeckAndHand(new[] { turtle.id }, Array.Empty<string>());
            var deployed = _match.ApplyDeploy(turtle, _match.CreateDeployCommand(turtle.id, DemoSlotKind.Unit, 1));
            if (!deployed.Accepted) throw new InvalidOperationException(deployed.Message);
            _selectedCardId = null;
            _selectedHandCardInstanceId = null;
            RefreshAll();
            ShowStatus("海龟模型审查：双侧原版纹理、水平龟壳与原格位射线。", false);
#else
            throw new NotSupportedException("Turtle pose diagnostics require a Development build.");
#endif
        }

        private IEnumerator SetupBlazePosePreview()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SelectFaction("nether"); SelectOpponentFaction("nether");
            if (!_registry.TryGetDefinition("nt_003", out var blaze) || !_registry.TryGetDefinition("nt_002", out var target))
                throw new InvalidOperationException("Registered blaze/target is missing.");
            _match.ResetPlayerRedstoneForScenario(10, 10);
            // Attack the separate 2-attack target so both displayed blazes remain alive.
            _match.ResetOpponent(new[] { target, blaze }, new[] { 0, 1 });
            _match.ResetDeckAndHand(new[] { blaze.id }, Array.Empty<string>());
            RefreshAll(); yield return null;
            if (!ClickHandCardThroughEventSystem(blaze.id) ||
                !ClickBattlefieldSlotThroughPointer(true, DemoSlotKind.Unit, 1)) yield break;
            var own = _match.GetObject(true, DemoSlotKind.Unit, 1);
            if (own?.CardId != blaze.id) yield break;
            _match.EndPlayerTurn(); _match.BeginNextPlayerTurn();
            _match.ApplyEnterCombat(_match.CreateEnterCombatCommand());
            RefreshAll(); yield return null;
            if (!ClickBattlefieldSlotThroughPointer(true, DemoSlotKind.Unit, 1) ||
                _selectedAttackerInstanceId != own.InstanceId ||
                !ClickBattlefieldSlotThroughPointer(false, DemoSlotKind.Unit, 0)) yield break;
            yield return new WaitForSecondsRealtime(0.9f);
            _blazePosePreviewCompleted = own.Health > 0 && own.HasAttacked &&
                _match.GetObject(false, DemoSlotKind.Unit, 1)?.CardId == blaze.id &&
                _match.GetObject(false, DemoSlotKind.Unit, 0) == null;
            ShowStatus("烈焰人审查：双侧三层棒环、真实手牌部署与原地块攻击。", !_blazePosePreviewCompleted);
            Debug.Log($"Blaze interaction preview settled: {_blazePosePreviewCompleted} (hand UI + original slot deployment/attacker/target raycasts).");
#else
            throw new NotSupportedException("Blaze pose diagnostics require a Development build.");
#endif
        }

        private void AuditBlazePoseCapture()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Physics.SyncTransforms();
            foreach (var player in new[] { true, false })
            {
                var value = _match.GetObject(player, DemoSlotKind.Unit, 1);
                var piece = _battlefield.FindPieceTransform(value?.InstanceId);
                var renderers = piece?.GetComponentsInChildren<MeshRenderer>();
                if (value?.CardId != "nt_003" || renderers?.Length != 13 ||
                    piece.GetComponent<DemoEntityIdleAnimator>()?.TrackCount != 13)
                    throw new InvalidOperationException("Blaze original cubes/animation contract failed.");
                for (var rod = 0; rod < 12; rod++)
                {
                    var bone = piece.Find("Model/Bone_upperBodyParts" + rod);
                    var expectedRadius = (rod < 4 ? 9f : rod < 8 ? 7f : 5f) / 16f;
                    if (bone == null || Mathf.Abs(new Vector2(bone.localPosition.x, bone.localPosition.z).magnitude - expectedRadius) > 0.001f)
                        throw new InvalidOperationException("Blaze source ring radius failed.");
                }
                foreach (var renderer in renderers)
                    if (renderer.sharedMaterial.shader.name != "BiomeRivals/Demo/Entity" ||
                        renderer.sharedMaterial.mainTexture?.name != "entity_blaze")
                        throw new InvalidOperationException("Blaze original material was replaced.");
                var point = _battlefield.BoardCamera.WorldToScreenPoint(_battlefield.GetSlotInteractionWorldPosition(player, DemoSlotKind.Unit, 1));
                if (point.z <= 0 || !_battlefield.TryRaycastSlot(point, out var slot) || slot.Player != player ||
                    slot.Kind != DemoSlotKind.Unit || slot.Index != 1)
                    throw new InvalidOperationException("Original blaze slot raycast failed.");
            }
            Debug.Log("Blaze pose audit: True (both sides, 13 original cubes, three moving source rings, material and original slots).");
#endif
        }

        private IEnumerator SetupBabySheepPosePreview()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SelectFaction("plains_forest"); SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("tk_003", out var baby) ||
                !_registry.TryGetDefinition("pf_001", out var target))
                throw new InvalidOperationException("Registered baby sheep/bee are missing.");
            _match.ResetPlayerRedstoneForScenario(10, 10);
            _match.ResetOpponent(new[] { target, baby }, new[] { 0, 1 });
            _match.ResetDeckAndHand(new[] { baby.id }, Array.Empty<string>());
            RefreshAll(); yield return null;
            if (!ClickHandCardThroughEventSystem(baby.id) ||
                !ClickBattlefieldSlotThroughPointer(true, DemoSlotKind.Unit, 1)) yield break;
            var own = _match.GetObject(true, DemoSlotKind.Unit, 1);
            if (own?.CardId != baby.id) yield break;
            _match.EndPlayerTurn(); _match.BeginNextPlayerTurn();
            _match.ApplyEnterCombat(_match.CreateEnterCombatCommand());
            RefreshAll(); yield return null;
            if (!ClickBattlefieldSlotThroughPointer(true, DemoSlotKind.Unit, 1) ||
                _selectedAttackerInstanceId != own.InstanceId ||
                !ClickBattlefieldSlotThroughPointer(false, DemoSlotKind.Unit, 0)) yield break;
            yield return new WaitForSecondsRealtime(.9f);
            _babySheepPreviewCompleted = own.Health > 0 && own.HasAttacked &&
                _match.GetObject(false, DemoSlotKind.Unit, 0) == null &&
                _match.GetObject(false, DemoSlotKind.Unit, 1)?.CardId == baby.id;
            ShowStatus("幼羊审查：独立原模型/皮肤、双侧比例、真实手牌部署与原地块攻击。", !_babySheepPreviewCompleted);
            Debug.Log($"Baby sheep interaction preview settled: {_babySheepPreviewCompleted} (hand UI + original slot deployment/attacker/target raycasts).");
#else
            throw new NotSupportedException("Baby sheep diagnostics require a Development build.");
#endif
        }

        private void AuditBabySheepPoseCapture()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Physics.SyncTransforms();
            foreach (var player in new[] { true, false })
            {
                var unit = _match.GetObject(player, DemoSlotKind.Unit, 1);
                var piece = _battlefield.FindPieceTransform(unit?.InstanceId);
                var skeleton = piece?.Find("Model/Bone_root");
                var head = skeleton?.Find("Bone_head"); var body = skeleton?.Find("Bone_body");
                var renderers = piece?.GetComponentsInChildren<MeshRenderer>();
                if (unit?.CardId != "tk_003" || head == null || body == null || renderers.Length != 6 ||
                    piece.GetComponent<DemoEntityIdleAnimator>()?.TrackCount != 1 ||
                    Vector3.Distance(head.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size, Vector3.one * 5 / 16f) > .001f ||
                    Vector3.Distance(body.GetComponentInChildren<MeshFilter>().sharedMesh.bounds.size, new Vector3(6,4,9) / 16f) > .001f)
                    throw new InvalidOperationException("Baby sheep source geometry/proportions/idle were substituted.");
                foreach(var renderer in renderers)
                    if(renderer.sharedMaterial.shader.name != "BiomeRivals/Demo/Entity" ||
                        renderer.sharedMaterial.mainTexture != DemoWorldAssetProvider.LoadBlockTexture("entity_sheep_baby") ||
                        renderer.sharedMaterial.GetFloat("_UseAlphaColorMask") < .5f)
                        throw new InvalidOperationException("Baby sheep source texture or dye-mask material was substituted.");
                var point = _battlefield.BoardCamera.WorldToScreenPoint(_battlefield.GetSlotInteractionWorldPosition(player, DemoSlotKind.Unit, 1));
                if(point.z <= 0 || !_battlefield.TryRaycastSlot(point, out var slot) || slot.Player != player ||
                    slot.Kind != DemoSlotKind.Unit || slot.Index != 1)
                    throw new InvalidOperationException("Original baby sheep slot raycast failed.");
            }
            Debug.Log("Baby sheep pose audit: True (both sides, 6 original cubes, source proportions/texture, dye mask, idle and original slots).");
#endif
        }

        private IEnumerator SetupGuardianPosePreview()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            SelectFaction("ocean_river"); SelectOpponentFaction("ocean_river");
            if (!_registry.TryGetDefinition("or_004", out var guardian))
                throw new InvalidOperationException("Registered guardian is missing.");
            _match.ResetPlayerRedstoneForScenario(10, 10);
            _match.ResetOpponent(new[] { guardian }, new[] { 1 });
            _match.ResetDeckAndHand(new[] { guardian.id }, Array.Empty<string>());
            RefreshAll(); yield return null;
            if (!ClickHandCardThroughEventSystem(guardian.id) ||
                !ClickBattlefieldSlotThroughPointer(true, DemoSlotKind.Unit, 1)) yield break;
            var own = _match.GetObject(true, DemoSlotKind.Unit, 1);
            if (own?.CardId != guardian.id) yield break;
            _match.EndPlayerTurn(); _match.BeginNextPlayerTurn();
            _match.ApplyEnterCombat(_match.CreateEnterCombatCommand());
            RefreshAll(); yield return null;
            if (!ClickBattlefieldSlotThroughPointer(true, DemoSlotKind.Unit, 1) ||
                _selectedAttackerInstanceId != own.InstanceId ||
                !ClickBattlefieldSlotThroughPointer(false, DemoSlotKind.Unit, 1)) yield break;
            yield return new WaitForSecondsRealtime(0.9f);
            var opponent = _match.GetObject(false, DemoSlotKind.Unit, 1);
            _guardianPosePreviewCompleted = own.Health > 0 && own.HasAttacked && opponent?.Health > 0;
            ShowStatus("守卫者审查：双侧连接姿态、真实手牌部署与原地块攻击选择。", !_guardianPosePreviewCompleted);
            Debug.Log($"Guardian interaction preview settled: {_guardianPosePreviewCompleted} (hand UI + original slot deployment/attacker/target raycasts).");
#else
            throw new NotSupportedException("Guardian pose diagnostics require a Development build.");
#endif
        }

        private void AuditGuardianPoseCapture()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Physics.SyncTransforms();
            foreach (var player in new[] { true, false })
            {
                var value = _match.GetObject(player, DemoSlotKind.Unit, 1);
                var piece = _battlefield.FindPieceTransform(value?.InstanceId);
                var head = piece?.Find("Model/Bone_head");
                var renderers = head?.GetComponentsInChildren<MeshRenderer>();
                if (value?.CardId != "or_004" || head == null || renderers?.Length != 22 ||
                    piece.GetComponent<DemoEntityIdleAnimator>()?.TrackCount != 4 ||
                    Mathf.Abs(head.Find("Bone_eye").localPosition.z + 8.25f / 16f) > 0.001f ||
                    Mathf.Abs(head.Find("Bone_tailpart0/Bone_tailpart1").localPosition.z - 14f / 16f) > 0.001f)
                    throw new InvalidOperationException("Guardian source eye/tail/cubes/idle contract failed.");
                for (var index = 0; index < 12; index++)
                    if (head.Find("Bone_spikepart" + index) == null)
                        throw new InvalidOperationException("Guardian source spike was dropped.");
                foreach (var renderer in renderers)
                    if (renderer.sharedMaterial.shader.name != "BiomeRivals/Demo/Entity" ||
                        renderer.sharedMaterial.mainTexture != DemoWorldAssetProvider.LoadBlockTexture("entity_guardian"))
                        throw new InvalidOperationException("Guardian source material was substituted.");
                var point = _battlefield.BoardCamera.WorldToScreenPoint(_battlefield.GetSlotInteractionWorldPosition(player, DemoSlotKind.Unit, 1));
                if (point.z <= 0 || !_battlefield.TryRaycastSlot(point, out var slot) || slot.Player != player ||
                    slot.Kind != DemoSlotKind.Unit || slot.Index != 1)
                    throw new InvalidOperationException("Original guardian slot raycast failed.");
            }
            Debug.Log("Guardian pose audit: True (both sides, 22 original cubes, eye/tail/spikes, idle/material and original slots).");
#endif
        }

        private void AuditTurtlePoseCapture()
        {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
            Physics.SyncTransforms();
            var camera = _battlefield.BoardCamera;
            foreach (var player in new[] { true, false })
            {
                var turtle = _match.GetObject(player, DemoSlotKind.Unit, 1);
                var piece = _battlefield.FindPieceTransform(turtle?.InstanceId);
                var body = piece?.Find("Model/Bone_body");
                var shell = body?.GetComponentsInChildren<MeshFilter>().FirstOrDefault(value => value.transform.parent == body);
                var point = camera.WorldToScreenPoint(_battlefield.GetSlotInteractionWorldPosition(player, DemoSlotKind.Unit, 1));
                if (turtle?.CardId != "or_005" || body == null || shell == null ||
                    Quaternion.Angle(body.localRotation, Quaternion.identity) > 0.001f ||
                    Mathf.Abs(shell.sharedMesh.bounds.size.y - 6f / 16f) > 0.001f ||
                    Mathf.Abs(shell.sharedMesh.bounds.size.z - 20f / 16f) > 0.001f ||
                    Quaternion.Angle(piece.localRotation, Quaternion.Euler(0, player ? 180f : 0, 0)) > 5.001f ||
                    point.z <= 0 || !_battlefield.TryRaycastSlot(point, out var slot) ||
                    slot.Player != player || slot.Kind != DemoSlotKind.Unit || slot.Index != 1)
                    throw new InvalidOperationException("Turtle mesh binding, facing or original slot raycast failed.");
                foreach (var name in new[] { "head", "leg0", "leg1", "leg2", "leg3", "eggbelly" })
                {
                    var bone = body.Find("Bone_" + name);
                    if (bone == null || Vector3.Dot(bone.up, Vector3.up) < 0.999f)
                        throw new InvalidOperationException("Turtle child bone inherited mesh binding rotation.");
                }
                if (piece.GetComponent<DemoEntityIdleAnimator>() == null)
                    throw new InvalidOperationException("Turtle idle animation was lost.");
            }
            Debug.Log("Turtle source binding audit: True (both Minecraft models, shell horizontal, children upright, original slots raycast).");
#else
            throw new NotSupportedException("Turtle pose diagnostics require a Development build.");
#endif
        }

        private void SetupTurtleAuraPreview()
        {
            SelectFaction("ocean_river");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("or_005", out var turtleDefinition) ||
                !_registry.TryGetDefinition("or_001", out var salmonDefinition)) return;
            _match.ResetDeckAndHand(new[] { turtleDefinition.id, salmonDefinition.id }, Array.Empty<string>());
            var turtleDeployed = _match.ApplyDeploy(turtleDefinition,
                _match.CreateDeployCommand(turtleDefinition.id, DemoSlotKind.Unit, 1));
            var salmonDeployed = _match.ApplyDeploy(salmonDefinition,
                _match.CreateDeployCommand(salmonDefinition.id, DemoSlotKind.Unit, 2));
            if (salmonDeployed.Accepted && _match.PendingChoice != null)
                _match.ApplyResolveChoice(_match.CreateResolveChoiceCommand(_match.PendingChoice.choiceId, -1));
            _match.ResetHand(new[] { turtleDefinition.id });
            _selectedCardId = turtleDefinition.id;
            RefreshAll();
            var salmon = _match.GetObject(true, DemoSlotKind.Unit, 2);
            ShowStatus(turtleDeployed.Accepted && salmonDeployed.Accepted && salmon?.AdjacencyHealthModifier == 1
                ? "海龟的潮甲光环正在贴地照亮左右相邻格；鲑鱼群已动态获得 +1 当前与最大生命，离开后会立即失去。"
                : !turtleDeployed.Accepted ? turtleDeployed.Message : salmonDeployed.Message,
                !turtleDeployed.Accepted || !salmonDeployed.Accepted || salmon?.AdjacencyHealthModifier != 1);
            if (salmon != null) StartCoroutine(PulseBattlefieldObject(salmon.InstanceId));
        }

        private void SetupCoralReefPreview()
        {
            SelectFaction("ocean_river");
            SelectOpponentFaction("nether");
            if (!_registry.TryGetDefinition("or_007", out var reefDefinition) ||
                !_registry.TryGetDefinition("or_003", out var drownedDefinition)) return;
            _match.ResetDeckAndHand(new[] { reefDefinition.id, drownedDefinition.id }, new[] { "or_001" });
            var reefDeployed = _match.ApplyDeploy(reefDefinition,
                _match.CreateDeployCommand(reefDefinition.id, DemoSlotKind.Building, 0));
            var drownedDeployed = _match.ApplyDeploy(drownedDefinition,
                _match.CreateDeployCommand(drownedDefinition.id, DemoSlotKind.Unit, 1));
            var reef = _match.GetObject(true, DemoSlotKind.Building, 0);
            var drowned = _match.GetObject(true, DemoSlotKind.Unit, 1);
            var grew = drownedDeployed.Accepted && drowned?.MaxHealth == drownedDefinition.health + 1;
            if (grew)
            {
                _match.EndPlayerTurn();
                _match.BeginNextPlayerTurn();
            }
            _match.ResetHand(new[] { reefDefinition.id });
            _selectedCardId = reefDefinition.id;
            RefreshAll();
            var ready = reef != null && !_match.HasTriggeredEffect(true, reef.InstanceId, "effect.or_007.01");
            ShowStatus(reefDeployed.Accepted && grew && ready
                ? "珊瑚礁上一回合已使溺尸永久获得 +1 生命；当前回合重新就绪，粉紫色建筑地表脉冲表示可再次滋养。"
                : !reefDeployed.Accepted ? reefDeployed.Message : drownedDeployed.Message,
                !reefDeployed.Accepted || !grew || !ready);
            if (drowned != null) StartCoroutine(PulseBattlefieldObject(drowned.InstanceId));
        }

        private void SetupWoodlandNurseryPreview()
        {
            SelectFaction("plains_forest");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("pf_005", out var nurseryDefinition) ||
                !_registry.TryGetDefinition("pf_002", out var sheepDefinition)) return;
            _match.ResetDeckAndHand(new[] { nurseryDefinition.id, sheepDefinition.id }, new[] { "pf_001" });
            var nurseryDeployed = _match.ApplyDeploy(nurseryDefinition,
                _match.CreateDeployCommand(nurseryDefinition.id, DemoSlotKind.Building, 0));
            var sheepDeployed = _match.ApplyDeploy(sheepDefinition,
                _match.CreateDeployCommand(sheepDefinition.id, DemoSlotKind.Unit, 1));
            var nursery = _match.GetObject(true, DemoSlotKind.Building, 0);
            var sheep = _match.GetObject(true, DemoSlotKind.Unit, 1);
            var grew = sheepDeployed.Accepted && sheep?.MaxHealth == sheepDefinition.health + 1;
            if (grew)
            {
                _match.EndPlayerTurn();
                _match.BeginNextPlayerTurn();
            }
            _match.ResetHand(new[] { nurseryDefinition.id });
            _selectedCardId = nurseryDefinition.id;
            RefreshAll();
            var ready = nursery != null &&
                !_match.HasTriggeredEffect(true, nursery.InstanceId, "effect.pf_005.01");
            ShowStatus(nurseryDeployed.Accepted && grew && ready
                ? "林地苗圃上一回合已使放牧绵羊永久获得 +1 生命；当前回合重新就绪，叶绿色地表脉冲表示可再次培育。"
                : !nurseryDeployed.Accepted ? nurseryDeployed.Message : sheepDeployed.Message,
                !nurseryDeployed.Accepted || !grew || !ready);
            if (sheep != null) StartCoroutine(PulseBattlefieldObject(sheep.InstanceId));
        }

        private void SetupBreedingSeasonPreview()
        {
            SelectFaction("plains_forest");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("pf_001", out var beeDefinition) ||
                !_registry.TryGetDefinition("pf_002", out var sheepDefinition) ||
                !_registry.TryGetDefinition("pf_006", out var breedingDefinition)) return;
            _match.ResetDeckAndHand(new[] { beeDefinition.id, sheepDefinition.id }, new[] { "pf_003" });
            var beeDeployed = _match.ApplyDeploy(beeDefinition,
                _match.CreateDeployCommand(beeDefinition.id, DemoSlotKind.Unit, 0));
            var sheepDeployed = _match.ApplyDeploy(sheepDefinition,
                _match.CreateDeployCommand(sheepDefinition.id, DemoSlotKind.Unit, 2));
            var bee = _match.GetObject(true, DemoSlotKind.Unit, 0);
            _match.ResetHand(new[] { breedingDefinition.id });
            _selectedCardId = breedingDefinition.id;
            _pendingTargetCardId = breedingDefinition.id;
            _selectedCardTargetInstanceIds.Clear();
            if (bee != null) _selectedCardTargetInstanceIds.Add(bee.InstanceId);
            RefreshAll();
            ShowStatus(beeDeployed.Accepted && sheepDeployed.Accepted
                ? "繁殖目标：已选择 1/2；金色蜜蜂为已选目标，绿色绵羊仍可选择，确认后才会结算。"
                : !beeDeployed.Accepted ? beeDeployed.Message : sheepDeployed.Message,
                !beeDeployed.Accepted || !sheepDeployed.Accepted);
        }

        private void SetupWoodlandRallyPreview()
        {
            SelectFaction("plains_forest");
            SelectOpponentFaction("desert_badlands");
            if (!_registry.TryGetDefinition("pf_001", out var beeDefinition) ||
                !_registry.TryGetDefinition("pf_002", out var sheepDefinition) ||
                !_registry.TryGetDefinition("pf_003", out var wolfDefinition) ||
                !_registry.TryGetDefinition("pf_007", out var rallyDefinition)) return;
            var unitSlotCount = _battlefield.GetSlotCount(DemoSlotKind.Unit);
            if (unitSlotCount < 3)
            {
                Debug.LogError($"Woodland rally preview requires at least three unit slots; arena has {unitSlotCount}.");
                return;
            }

            _match.ResetPlayerRedstoneForScenario(10, 10);
            _match.ResetDeckAndHand(Array.Empty<string>(), Array.Empty<string>());
            var openSlot = unitSlotCount / 2;
            var fillerUnits = new[] { beeDefinition, sheepDefinition, beeDefinition, wolfDefinition };
            for (var slot = 0; slot < unitSlotCount; slot++)
            {
                if (slot == openSlot) continue;
                var definition = fillerUnits[slot % fillerUnits.Length];
                _match.ResetHand(new[] { definition.id });
                var deployed = _match.ApplyDeploy(definition,
                    _match.CreateDeployCommand(definition.id, DemoSlotKind.Unit, slot));
                if (!deployed.Accepted)
                {
                    Debug.LogError($"Woodland rally preview could not fill unit slot {slot + 1}: {deployed.Message}");
                    return;
                }
            }

            _match.ResetDeckAndHand(new[] { rallyDefinition.id }, new[] { beeDefinition.id });
            var result = _match.ApplyPlayCard(rallyDefinition, _match.CreatePlayCardCommand(rallyDefinition.id));
            var companion = _match.GetObject(true, DemoSlotKind.Unit, openSlot);
            var drawnHandCard = MatchView.HandCards.FirstOrDefault(value => value != null &&
                value.cardId == beeDefinition.id);
            var settled = result.Accepted && companion?.CardId == "tk_004" &&
                _match.PlayerBattlefield.Count == unitSlotCount &&
                _match.Hand.SequenceEqual(new[] { beeDefinition.id }) && drawnHandCard != null;
            if (settled) SelectCard(drawnHandCard.cardId);
            ShowStatus(settled
                ? $"林间集结：填满 {unitSlotCount} 格前保留的唯一空位，召唤林地伙伴后因满场抽取一张牌。"
                : result.Message,
                !settled);
            var detailsFollowDrawnCopy = settled && _selectedHandCardInstanceId == drawnHandCard.handCardInstanceId &&
                _inspectorRoot.Find("StaleHandSelection") == null;
            Debug.Log($"Woodland rally preview settled: {settled && detailsFollowDrawnCopy} (unit slots={unitSlotCount}; summoned={companion?.CardId ?? "none"}; slot={openSlot + 1}; drew={_match.Hand.SingleOrDefault() ?? "none"}; drawn card details refreshed={detailsFollowDrawnCopy}).");
            if (companion != null) StartCoroutine(PulseBattlefieldObject(companion.InstanceId));
        }

        private void SetupIronGolemPreview()
        {
            SelectFaction("plains_forest");
            SelectOpponentFaction("nether");
            if (!_registry.TryGetDefinition("pf_005", out var nurseryDefinition) ||
                !_registry.TryGetDefinition("pf_008", out var golemDefinition)) return;
            _match.ResetDeckAndHand(new[] { nurseryDefinition.id }, Array.Empty<string>());
            var nurseryDeployed = _match.ApplyDeploy(nurseryDefinition,
                _match.CreateDeployCommand(nurseryDefinition.id, DemoSlotKind.Building, 0));
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _match.ResetHand(new[] { golemDefinition.id });
            var golemDeployed = _match.ApplyDeploy(golemDefinition,
                _match.CreateDeployCommand(golemDefinition.id, DemoSlotKind.Unit, 1));
            var golem = _match.GetObject(true, DemoSlotKind.Unit, 1);
            _match.ResetHand(new[] { golemDefinition.id });
            _selectedCardId = golemDefinition.id;
            RefreshAll();
            ShowStatus(nurseryDeployed.Accepted && golemDeployed.Accepted && golem?.Attack == 6 && golem.MaxHealth == 8
                ? "铁傀儡响应己方林地苗圃，战吼永久获得 +1/+1；场上模型与卡牌均使用 Minecraft 铁傀儡纹理。"
                : !nurseryDeployed.Accepted ? nurseryDeployed.Message : golemDeployed.Message,
                !nurseryDeployed.Accepted || !golemDeployed.Accepted || golem?.Attack != 6 || golem.MaxHealth != 8);
            if (golem != null) StartCoroutine(PulseBattlefieldObject(golem.InstanceId));
        }

        private void SetupPolarBearWoolPreview()
        {
            var posePreview = HasCommandLineFlag("-previewPolarBearPose");
            SelectFaction("snow_ice");
            SelectOpponentFaction(posePreview ? "snow_ice" : "plains_forest");
            if (!_registry.TryGetDefinition("si_005", out var polarBearDefinition) ||
                !_registry.TryGetDefinition("tk_001", out var woolDefinition)) return;
            if (posePreview) _match.ResetOpponent(new[] { polarBearDefinition }, new[] { 1 });

            _match.ResetPlayerRedstoneForScenario(10, 10);
            _match.ResetPlayerLife(15);
            _match.ResetDeckAndHand(new[] { polarBearDefinition.id }, new[] { "si_001" });
            var deployed = _match.ApplyDeploy(polarBearDefinition,
                _match.CreateDeployCommand(polarBearDefinition.id, DemoSlotKind.Unit, 1));
            var bear = _match.GetObject(true, DemoSlotKind.Unit, 1);
            if (!deployed.Accepted || bear == null)
            {
                ShowStatus(deployed.Message, true);
                return;
            }

            _match.ResetDeckAndHand(new[] { woolDefinition.id, woolDefinition.id }, new[] { "si_001" });
            var protectedBear = _match.ApplyPlayCard(woolDefinition,
                _match.CreatePlayCardCommand(woolDefinition.id, "UNIT", bear.InstanceId,
                    handCardInstanceId: _match.HandCards[0].handCardInstanceId));
            var remainingWool = _match.HandCards.FirstOrDefault(value => value != null && value.cardId == woolDefinition.id);
            _selectedCardId = remainingWool?.cardId;
            _selectedHandCardInstanceId = remainingWool?.handCardInstanceId;
            RefreshAll();
            var nameplateText = _playerUnitSlots[1].Content.GetComponentsInChildren<Text>(true)
                .FirstOrDefault(value => value.name == "WorldLabelText");
            var nameplateLineCount = nameplateText?.text.Split('\n').Length ?? 0;
            var nameplateHeight = nameplateText?.rectTransform.sizeDelta.y ?? 0f;
            var settled = protectedBear.Accepted && remainingWool != null &&
                _selectedHandCardInstanceId == remainingWool.handCardInstanceId &&
                bear.Attack == 4 && bear.MaxHealth == 7 &&
                bear.TemporaryHealthModifier == 1 && bear.HasKeyword("TAUNT") &&
                nameplateLineCount == 3 && nameplateText.resizeTextForBestFit &&
                nameplateText.resizeTextMinSize == WorldLabelMinimumFontSize && nameplateHeight == 54f;
            ShowStatus(settled
                    ? "英雄 15 血：北极熊 4/7\n攻+1 永久 · 血+1 临时 · 嘲讽\n另一张羊毛可继续选己方目标"
                    : protectedBear.Message, !settled);
            Debug.Log($"Polar bear wool preview settled: {settled} (attack {bear?.Attack}/health {bear?.Health}; remaining Wool selected; label {nameplateLineCount} lines, text height {nameplateHeight:0}).");
            if (posePreview)
                Debug.Log($"Polar bear pose preview settled: {settled && _match.GetObject(false, DemoSlotKind.Unit, 1)?.CardId == "si_005"} (both sides, Minecraft models, idle retained).");
            if (protectedBear.Accepted) StartCoroutine(PulseBattlefieldObject(bear.InstanceId));
        }

        private void SetupVindicatorPreview()
        {
            SelectFaction("cave_dark_forest");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("cd_004", out var sensorDefinition) ||
                !_registry.TryGetDefinition("cd_005", out var vindicatorDefinition)) return;
            _match.ResetDeckAndHand(new[] { sensorDefinition.id }, new[] { "pf_001" });
            var sensorDeployed = _match.ApplyDeploy(sensorDefinition,
                _match.CreateDeployCommand(sensorDefinition.id, DemoSlotKind.Building, 0));
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _match.ResetHand(new[] { vindicatorDefinition.id });
            var vindicatorDeployed = _match.ApplyDeploy(vindicatorDefinition,
                _match.CreateDeployCommand(vindicatorDefinition.id, DemoSlotKind.Unit, 1));
            var vindicator = _match.GetObject(true, DemoSlotKind.Unit, 1);
            _match.ResetHand(new[] { vindicatorDefinition.id });
            _selectedCardId = vindicatorDefinition.id;
            RefreshAll();
            ShowStatus(sensorDeployed.Accepted && vindicatorDeployed.Accepted && vindicator?.Attack == 6
                ? "林地卫道士响应己方幽匿感测体，战吼在当前行动回合获得 +2 攻击；回合结束后精确恢复为 4。"
                : !sensorDeployed.Accepted ? sensorDeployed.Message : vindicatorDeployed.Message,
                !sensorDeployed.Accepted || !vindicatorDeployed.Accepted || vindicator?.Attack != 6);
            if (vindicator != null) StartCoroutine(PulseBattlefieldObject(vindicator.InstanceId));
        }

        private void SetupDarknessPreview()
        {
            SelectFaction("plains_forest");
            SelectOpponentFaction("cave_dark_forest");
            if (!_registry.TryGetDefinition("cd_004", out var sensor) ||
                !_registry.TryGetDefinition("db_002", out var sand) ||
                !_registry.TryGetDefinition("si_001", out var snowball) ||
                !_registry.TryGetDefinition("pf_001", out var bee) ||
                !_registry.TryGetDefinition("pf_002", out var sheep) ||
                !_registry.TryGetDefinition("pf_003", out var wolf)) return;

            _match.ResetPlayerRedstoneForScenario(10, 10);
            _match.ResetDeckAndHand(new[] { sand.id, sand.id }, Array.Empty<string>());
            _match.ResetOpponent(new[] { bee, sheep, wolf, sensor }, new[] { 0, 1, 3 });
            var sandHandCardInstanceIds = _match.HandCards
                .Where(value => value != null && value.cardId == sand.id)
                .Select(value => value.handCardInstanceId)
                .ToArray();
            if (sandHandCardInstanceIds.Length != 2 || sandHandCardInstanceIds.Any(string.IsNullOrEmpty))
            {
                ShowStatus("黑暗预览初始化失败：疑砂手牌实例不完整。", true);
                return;
            }
            var first = _match.ApplyPlayCard(sand, _match.CreatePlayCardCommand(sand.id,
                handCardInstanceId: sandHandCardInstanceIds[0]));
            var second = _match.ApplyPlayCard(sand, _match.CreatePlayCardCommand(sand.id,
                handCardInstanceId: sandHandCardInstanceIds[1]));
            _match.ResetHand(new[] { snowball.id });
            var snowballHandCard = _match.HandCards.FirstOrDefault(value => value != null && value.cardId == snowball.id);
            _selectedCardId = snowballHandCard?.cardId;
            _selectedHandCardInstanceId = snowballHandCard?.handCardInstanceId;
            RefreshAll();
            CastSelectedCard();
            var darkActive = _match.HasPlayerStatus(true, "DARK");
            var settled = first.Accepted && second.Accepted && darkActive &&
                _pendingTargetCardId == snowball.id && snowballHandCard != null &&
                _selectedHandCardInstanceId == snowballHandCard.handCardInstanceId;
            ShowStatus(settled
                ? "幽匿感测体已在第二张牌结算后施加黑暗：本次只能点击敌方单位行左右边缘的发光地表；中间目标不可交互，英雄目标不受影响。"
                : !first.Accepted ? first.Message : second.Message,
                !settled);
            Debug.Log($"Darkness preview settled: {settled} (two exact Suspicious Sand instances consumed; DARK active; Snowball target selected).");
        }

        private void SetupBatScryPreview()
        {
            SelectFaction("cave_dark_forest");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("cd_001", out var batDefinition)) return;
            _match.ResetDeckAndHand(new[] { batDefinition.id }, new[] { "cd_002", "cd_005" });
            _selectedCardId = batDefinition.id;
            var deployed = _match.ApplyDeploy(batDefinition,
                _match.CreateDeployCommand(batDefinition.id, DemoSlotKind.Unit, 0));
            SelectFirstHandCard();
            RefreshAll();
            if (deployed.Accepted && _match.PendingChoice != null) SelectChoiceOption(0);
            ShowStatus(deployed.Accepted
                ? "洞穴蝙蝠已查看牌库顶牌；当前选中操作会将该牌置于牌库底，也可再次点击取消并保留牌库顶。"
                : deployed.Message, !deployed.Accepted);
            var bat = _match.GetObject(true, DemoSlotKind.Unit, 0);
            if (bat != null) StartCoroutine(PulseBattlefieldObject(bat.InstanceId));
        }

        private IEnumerator SetupChoiceInteractionPreview()
        {
            SelectFaction("cave_dark_forest");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("cd_001", out var batDefinition))
            {
                Debug.LogError("Choice interaction preview could not resolve the registered cave bat.");
                yield break;
            }

            _match.ResetDeckAndHand(new[] { batDefinition.id }, new[] { "cd_002", "cd_005" });
            var deployed = _match.ApplyDeploy(batDefinition,
                _match.CreateDeployCommand(batDefinition.id, DemoSlotKind.Unit, 0));
            RefreshAll();
            if (!deployed.Accepted || _match.PendingChoice?.kind != "TOP_CARD_SCRY" || !_choiceOverlay.gameObject.activeSelf)
            {
                Debug.LogError("Choice interaction preview could not open the bat's private top-card choice.");
                yield break;
            }

            var timeout = 0f;
            while (_choiceOverlayEntranceActive && timeout < 2f)
            {
                timeout += Time.unscaledDeltaTime;
                yield return null;
            }
            if (_choiceOverlayEntranceActive || !_choiceOverlayCanvasGroup.interactable)
            {
                Debug.LogError("Choice interaction preview did not finish the choice-overlay entrance before input.");
                yield break;
            }

            var choiceCard = _choiceCardsRoot.GetComponentsInChildren<CardUI>(true)
                .FirstOrDefault(value => value != null && value.GetComponent<Button>() != null);
            if (choiceCard == null || !ClickButtonThroughEventSystem(choiceCard.GetComponent<Button>()) ||
                _selectedChoiceOptionIndex != 0 || _match.PendingChoice == null)
            {
                Debug.LogError("Choice interaction preview could not select the inspected card through the UI event system.");
                yield break;
            }

            ShowStatus("已选中牌库顶牌；确认后将它置于牌库底。", false);
            Debug.Log("Choice interaction preview settled: True (GraphicRaycaster click selected the private top card; choice remains pending).");
            _choiceInteractionPreviewCompleted = true;
        }

        private IEnumerator SetupCaveSpiderPoisonPreview()
        {
            SelectFaction("cave_dark_forest");
            SelectOpponentFaction("snow_ice");
            if (!_registry.TryGetDefinition("cd_002", out var spiderDefinition) ||
                !_registry.TryGetDefinition("si_002", out var snowGolemDefinition)) yield break;
            _match.ResetPlayerRedstoneForScenario(10, 10);
            _match.ResetDeckAndHand(new[] { spiderDefinition.id }, new[] { "cd_001", "cd_003", "cd_005" });
            _match.ResetOpponent(new[] { snowGolemDefinition });
            RefreshAll();
            yield return null;
            if (!ClickHandCardThroughEventSystem(spiderDefinition.id) ||
                !ClickBattlefieldSlotThroughPointer(true, DemoSlotKind.Unit, 0)) yield break;
            var spider = _match.GetObject(true, DemoSlotKind.Unit, 0);
            if (spider == null || spider.CardId != spiderDefinition.id) yield break;
            yield return null;
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _match.ApplyEnterCombat(_match.CreateEnterCombatCommand());
            var target = _match.GetObject(false, DemoSlotKind.Unit, 0);
            RefreshAll();
            yield return null;
            if (target == null || !ClickBattlefieldSlotThroughPointer(true, DemoSlotKind.Unit, 0) ||
                _selectedAttackerInstanceId != spider.InstanceId ||
                !ClickBattlefieldSlotThroughPointer(false, DemoSlotKind.Unit, 0)) yield break;
            yield return new WaitForSecondsRealtime(0.9f);
            var settled = spider.Health > 0 && spider.HasAttacked && target.HasStatus("POISON");
            _selectedCardId = spiderDefinition.id;
            RefreshAll();
            ShowStatus(settled
                ? "洞穴蜘蛛造成普通攻击伤害后施加中毒；绿色发光直接作用于目标脚下的 3D 地表材质。"
                : "蜘蛛交互预览未通过。", !settled);
            Debug.Log($"Spider interaction preview settled: {settled} (hand UI + original 3D slot deployment/attacker/target raycasts + poison).");
            _spiderPreviewCompleted = settled;
            if (target != null) StartCoroutine(PulseBattlefieldObject(target.InstanceId));
        }

        private void SetupBlazeFirePreview()
        {
            SelectFaction("nether");
            SelectOpponentFaction("ocean_river");
            if (!_registry.TryGetDefinition("nt_003", out var blazeDefinition) ||
                !_registry.TryGetDefinition("tk_013", out var blazeRodDefinition) ||
                !_registry.TryGetDefinition("or_005", out var turtleDefinition)) return;
            _match.ResetDeckAndHand(new[] { blazeDefinition.id, blazeDefinition.id, blazeRodDefinition.id }, new[] { "nt_001", "nt_006" });
            _match.ResetOpponent(new[] { turtleDefinition });
            var firstDeployed = _match.ApplyDeploy(blazeDefinition,
                _match.CreateDeployCommand(blazeDefinition.id, DemoSlotKind.Unit, 1));
            var secondDeployed = _match.ApplyDeploy(blazeDefinition,
                _match.CreateDeployCommand(blazeDefinition.id, DemoSlotKind.Unit, 2));
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            var target = _match.GetObject(false, DemoSlotKind.Unit, 0);
            _match.ApplyEnterCombat(_match.CreateEnterCombatCommand());
            var blaze = _match.GetObject(true, DemoSlotKind.Unit, 1);
            var attacked = blaze == null || target == null
                ? DemoCommandResult.Reject(DemoCommandRejectionCode.InvalidTarget, "预览目标初始化失败。", _match.Revision)
                : _match.ApplyAttack(_match.CreateAttackCommand(blaze.InstanceId, "UNIT", target.InstanceId));
            if (attacked.Accepted)
            {
                _match.ApplyEndTurn(_match.CreateEndTurnCommand());
                _match.BeginNextPlayerTurn();
            }
            _selectedCardId = blazeRodDefinition.id;
            RefreshAll();
            ShowStatus(attacked.Accepted
                ? "烈焰人已令海龟着火：橙色像素火焰附着模型并直接照亮地表；当前已回到主行动阶段，可用手牌中的烈焰棒继续点燃目标。"
                : firstDeployed.Accepted && secondDeployed.Accepted ? attacked.Message : !firstDeployed.Accepted ? firstDeployed.Message : secondDeployed.Message,
                !firstDeployed.Accepted || !secondDeployed.Accepted || !attacked.Accepted);
            if (target != null) StartCoroutine(PulseBattlefieldObject(target.InstanceId));
        }

        private void SetupAbandonedMinePreview()
        {
            SelectFaction("cave_dark_forest");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("cd_007", out var mineDefinition)) return;
            _match.ResetDeckAndHand(new[] { mineDefinition.id }, new[] { "cd_001" });
            var deployed = _match.ApplyDeploy(mineDefinition,
                _match.CreateDeployCommand(mineDefinition.id, DemoSlotKind.Building, 0));
            var ended = deployed.Accepted
                ? _match.ApplyEndTurn(_match.CreateEndTurnCommand())
                : DemoCommandResult.Reject(DemoCommandRejectionCode.InvalidCommand, deployed.Message, _match.Revision);
            _selectedCardId = _match.Hand.Contains("tk_010") ? "tk_010" : mineDefinition.id;
            RefreshAll();
            var mine = _match.GetObject(true, DemoSlotKind.Building, 0);
            var resolved = deployed.Accepted && ended.Accepted && _match.Hand.Contains("tk_010");
            ShowStatus(resolved
                ? "废弃矿井横跨两个建筑格：本回合恰好打出矿井这一张牌，结束阶段已产出一张圆石。"
                : deployed.Accepted ? ended.Message : deployed.Message, !resolved);
            if (mine != null) StartCoroutine(PulseBattlefieldObject(mine.InstanceId));
        }

        private void SetupSummonReadinessPreview()
        {
            SelectFaction("cave_dark_forest");
            SelectOpponentFaction("nether");
            if (!_registry.TryGetDefinition("cd_008", out var mansion) ||
                !_registry.TryGetDefinition("nt_008", out var fortress)) return;
            _match.ResetPlayerRedstoneForScenario(mansion.cost, mansion.cost);
            _match.ResetDeckAndHand(new[] { mansion.id, "pf_001" }, new[] { "pf_001", "pf_001" });
            _match.ResetOpponent(new[] { fortress });
            var deployed = _match.ApplyDeploy(mansion,
                _match.CreateDeployCommand(mansion.id, DemoSlotKind.Building, 0));
            var ended = deployed.Accepted
                ? _match.ApplyEndTurn(_match.CreateEndTurnCommand())
                : DemoCommandResult.Reject(DemoCommandRejectionCode.InvalidCommand, deployed.Message, _match.Revision);
            SelectFirstHandCard();
            RefreshAll();
            var ownReady = TryGetEndPhaseSummonReadiness(true, false, out var ownStatus);
            var enemyReady = TryGetEndPhaseSummonReadiness(false, true, out var enemyStatus);
            var settled = deployed.Accepted && ended.Accepted && !ownReady && enemyReady &&
                ownStatus == "等待己方回合" && enemyStatus == "结束阶段就绪" &&
                _match.GetObject(true, DemoSlotKind.Unit, 0)?.CardId == "tk_011";
            ShowStatus(settled ? "府邸等待己方回合；敌方要塞当前满足召唤条件，地表已亮起。"
                : !deployed.Accepted ? deployed.Message : ended.Message, !settled);
            Debug.Log($"Summon readiness preview settled: {settled} (mansion={ownStatus}; fortress={enemyStatus}; opponent redstone={_match.OpponentEnergy}).");
        }

        private void SetupWoodlandMansionPreview()
        {
            SelectFaction("cave_dark_forest");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("cd_008", out var mansionDefinition)) return;
            _match.ResetDeckAndHand(Array.Empty<string>(), new[] { "cd_001" });
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _match.ResetHand(new[] { mansionDefinition.id });
            var deployed = _match.ApplyDeploy(mansionDefinition,
                _match.CreateDeployCommand(mansionDefinition.id, DemoSlotKind.Building, 0));
            var ended = deployed.Accepted
                ? _match.ApplyEndTurn(_match.CreateEndTurnCommand())
                : DemoCommandResult.Reject(DemoCommandRejectionCode.InvalidCommand, deployed.Message, _match.Revision);
            _selectedCardId = mansionDefinition.id;
            RefreshAll();
            var mansion = _match.GetObject(true, DemoSlotKind.Building, 0);
            var recruit = _match.GetObject(true, DemoSlotKind.Unit, 0);
            var resolved = deployed.Accepted && ended.Accepted && recruit?.CardId == "tk_011";
            ShowStatus(resolved
                ? "林地府邸横跨全部三个建筑格：结束阶段检测到空单位格，已在最左侧召唤 2/2 卫道士新兵。"
                : deployed.Accepted ? ended.Message : deployed.Message, !resolved);
            if (mansion != null) StartCoroutine(PulseBattlefieldObject(mansion.InstanceId));
            if (recruit != null) StartCoroutine(PulseBattlefieldObject(recruit.InstanceId));
        }

        private void SetupCactusFencePreview()
        {
            SelectFaction("plains_forest");
            SelectOpponentFaction("desert_badlands");
            if (!_registry.TryGetDefinition("pf_001", out var beeDefinition) ||
                !_registry.TryGetDefinition("pf_002", out var sheepDefinition) ||
                !_registry.TryGetDefinition("db_004", out var fenceDefinition)) return;
            _match.ResetDeckAndHand(new[] { beeDefinition.id }, new[] { sheepDefinition.id });
            _match.ResetOpponent(new[] { fenceDefinition });
            var deployed = _match.ApplyDeploy(beeDefinition,
                _match.CreateDeployCommand(beeDefinition.id, DemoSlotKind.Unit, 0));
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            var enteredCombat = _match.ApplyEnterCombat(_match.CreateEnterCombatCommand());
            var attacker = _match.GetObject(true, DemoSlotKind.Unit, 0);
            var attacked = attacker == null
                ? DemoCommandResult.Reject(DemoCommandRejectionCode.InvalidTarget, "攻击者不存在。", _match.Revision)
                : _match.ApplyAttack(_match.CreateAttackCommand(attacker.InstanceId, "HERO"));
            var fence = _match.GetObject(false, DemoSlotKind.Building, 0);
            _selectedCardId = fenceDefinition.id;
            _selectedAttackerInstanceId = null;
            RefreshAll();
            var triggered = fence != null && _match.HasTriggeredEffect(false, fence.InstanceId, "effect.db_004.01");
            ShowStatus(deployed.Accepted && enteredCombat.Accepted && attacked.Accepted && attacker?.Health == 1 && triggered
                ? "仙人掌围栏在英雄承受攻击后完成尖刺反击：蜜蜂受到 1 点伤害，围栏铭牌切换为本回合已触发。"
                : !deployed.Accepted ? deployed.Message : !enteredCombat.Accepted ? enteredCombat.Message : attacked.Message,
                !deployed.Accepted || !enteredCombat.Accepted || !attacked.Accepted || attacker?.Health != 1 || !triggered);
            if (fence != null) StartCoroutine(PulseBattlefieldObject(fence.InstanceId));
            if (attacker != null) StartCoroutine(PulseBattlefieldObject(attacker.InstanceId));
        }

        private void SetupDesertTemplePreview()
        {
            SelectFaction("desert_badlands");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("db_007", out var templeDefinition)) return;
            _match.ResetDeckAndHand(new[] { templeDefinition.id }, Array.Empty<string>());
            var deployed = _match.ApplyDeploy(templeDefinition,
                _match.CreateDeployCommand(templeDefinition.id, DemoSlotKind.Building, 0));
            var temple = _match.GetObject(true, DemoSlotKind.Building, 0);
            if (temple != null) temple.Health = 4;
            _match.ResetDeckAndHand(Array.Empty<string>(), new[] { "db_001", "tk_008" }, new[] { "tk_008" });
            _match.EndPlayerTurn();
            var draw = _match.BeginNextPlayerTurn();
            _selectedCardId = draw.ExcavatedCardIds.Contains("tk_008") ? "tk_008" : templeDefinition.id;
            RefreshAll();
            var resolved = deployed.Accepted && draw.ExcavatedCardIds.Contains("tk_008") && temple?.Health == 6 &&
                _match.PlayerLife == 29 && _match.OpponentLife == 27;
            ShowStatus(resolved
                ? "炸药机关出土：敌方英雄受到 3 点伤害，己方英雄受到 1 点真实伤害；沙漠神殿随后由 4/8 修复至 6/8。"
                : "沙漠神殿出土链预览初始化失败。", !resolved);
            StartCoroutine(PulseBothHeroHuds(Danger));
            if (temple != null) StartCoroutine(PulseBattlefieldObject(temple.InstanceId));
        }

        private void SetupOceanMonumentPreview()
        {
            SelectFaction("ocean_river");
            SelectOpponentFaction("plains_forest");
            if (!_registry.TryGetDefinition("or_008", out var monumentDefinition) ||
                !_registry.TryGetDefinition("pf_001", out var beeDefinition) ||
                !_registry.TryGetDefinition("pf_003", out var wolfDefinition)) return;

            _match.ResetDeckAndHand(Array.Empty<string>(), new[] { "or_001" });
            _match.EndPlayerTurn();
            _match.BeginNextPlayerTurn();
            _match.ResetHand(new[] { monumentDefinition.id });
            var deployed = _match.ApplyDeploy(monumentDefinition,
                _match.CreateDeployCommand(monumentDefinition.id, DemoSlotKind.Building, 0));
            _match.ResetOpponent(new[] { beeDefinition, beeDefinition, wolfDefinition }, new[] { 0, 1, 3 });
            _match.ResetHand(new[] { monumentDefinition.id });
            _selectedCardId = monumentDefinition.id;
            RefreshAll();
            var isolatedWolf = _match.GetObject(false, DemoSlotKind.Unit, 3);
            ShowStatus(deployed.Accepted
                ? "海底神殿横跨三个建筑格；相邻的两只蜜蜂互相掩护，孤立的狼已被结束阶段脉冲锁定。"
                : deployed.Message, !deployed.Accepted);
            if (isolatedWolf != null) StartCoroutine(PulseBattlefieldObject(isolatedWolf.InstanceId));
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
            SelectCard(templeDefinition.id);
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

        private IEnumerator SetupCraftingInteractionPreview()
        {
            SelectFaction("desert_badlands");
            if (!_registry.TryGetDefinition("db_007", out var templeDefinition))
            {
                Debug.LogError("Crafting interaction preview could not resolve the registered temple.");
                yield break;
            }

            _match.ResetDeckAndHand(
                new[] { templeDefinition.id, "db_002", "tk_006" },
                Array.Empty<string>());
            SelectCard(templeDefinition.id);
            RefreshAll();
            yield return null;

            var craftingButton = _inspectorRoot.Find("PayCrafting")?.GetComponent<Button>();
            if (!ClickButtonThroughEventSystem(craftingButton) || _selectedPaymentMethod != MatchPaymentMethods.Crafting)
            {
                Debug.LogError("Crafting interaction preview could not select the crafting payment through the inspector UI.");
                yield break;
            }
            if (!DemoDeploymentRules.CanPayWithCrafting(_match, templeDefinition, out var paymentMessage,
                    GetSelectedHandCardInstanceId()))
            {
                Debug.LogError("Crafting interaction preview did not have the required materials after payment selection: " + paymentMessage);
                yield break;
            }

            // Selecting the payment refreshes the hand UI; let the card arrival animation restore raycasts before dragging.
            yield return new WaitForSecondsRealtime(0.28f);
            var energyBeforeCraft = _match.Energy;
            if (!DragHandCardToBattlefieldThroughEventSystem(
                    templeDefinition.id, true, DemoSlotKind.Building, 0))
            {
                Debug.LogError("Crafting interaction preview could not drag the structure through UI and battlefield pointer input.");
                yield break;
            }

            yield return null;
            var temple = _match.GetObject(true, DemoSlotKind.Building, 0);
            var settled = temple != null && temple.CardId == templeDefinition.id && temple.MaxHealth == 10 &&
                temple.Health == 10 && _match.BuildingSlots[0] == templeDefinition.id &&
                _match.BuildingSlots[1] == templeDefinition.id && string.IsNullOrEmpty(_match.BuildingSlots[2]) &&
                _match.Energy == energyBeforeCraft && _match.HandCards.Count == 0 &&
                _match.DiscardPile.Contains("db_002") && _match.DiscardPile.Contains("tk_006") &&
                _match.BuriedCount == 2 && _match.DeckCount == 2;
            Debug.Log($"Crafting interaction preview settled: {settled} " +
                $"(GraphicRaycaster selected crafting payment; 3D pointer deployed a 10/10 temple into building slots 1-2; " +
                $"materials discarded; energy {energyBeforeCraft}/{_match.Energy}; buried={_match.BuriedCount}).");
            if (!settled)
            {
                Debug.LogError("Crafting interaction preview did not atomically consume its materials and deploy the crafted temple.");
                yield break;
            }

            ShowStatus("合成部署完成：沙漠神殿 10/10 · 红石未消耗 · 配方材料已弃置 · 藏宝图与炸药机关已掩埋。", false);
            yield return new WaitForSecondsRealtime(0.75f);
            var bannerTimeout = 0f;
            while (_turnBanner != null && _turnBanner.alpha > 0.01f && bannerTimeout < 2f)
            {
                bannerTimeout += Time.unscaledDeltaTime;
                yield return null;
            }
            if (_turnBanner != null && _turnBanner.alpha > 0.01f)
            {
                Debug.LogError("Crafting interaction preview could not wait for its completion banner to settle.");
                yield break;
            }
            yield return null;
            _craftingInteractionPreviewCompleted = true;
        }

        private IEnumerator SetupArchaeologyPreview()
        {
            SelectFaction("desert_badlands");
            if (!_registry.TryGetDefinition("db_003", out var archaeologistDefinition)) yield break;
            _match.ResetDeckAndHand(
                new[] { archaeologistDefinition.id },
                new[] { "db_004", "tk_006", "db_001" },
                new[] { "tk_006" });
            _match.ResetPlayerRedstoneForScenario(archaeologistDefinition.cost, archaeologistDefinition.cost);
            _selectedCardId = archaeologistDefinition.id;
            var result = _match.ApplyDeploy(
                archaeologistDefinition,
                _match.CreateDeployCommand(archaeologistDefinition.id, DemoSlotKind.Unit, 0));
            if (!result.Accepted) throw new InvalidOperationException("Archaeology reading fixture deployment failed: " + result.Message);
            SelectFirstHandCard();
            RefreshAll();
            var entranceTimeout = 0f;
            while (_choiceOverlayEntranceActive && entranceTimeout < 2f)
            {
                entranceTimeout += Time.unscaledDeltaTime;
                yield return null;
            }
            var buriedCard = _choiceCardsRoot.Find("ChoiceSlot1")?.GetComponentInChildren<CardUI>(true);
            if (entranceTimeout >= 2f || buriedCard == null ||
                !ClickButtonThroughEventSystem(buriedCard.GetComponent<Button>()))
            {
                Debug.LogError("Archaeology preview could not select the buried card through the UI event system.");
                yield break;
            }
            yield return null;
            var previewChoice = _match.PendingChoice;
            var choiceOptionCount = previewChoice?.options?.Length ?? 0;
            var renderedChoiceCardCount = _choiceCardsRoot.GetComponentsInChildren<CardUI>(true).Length;
            var choicePreviewReady = result.Accepted && previewChoice != null && choiceOptionCount == 3 &&
                _selectedChoiceOptionIndex == 1 && renderedChoiceCardCount == 3;
            Debug.Log($"Archaeology three-card choice preview settled: {choicePreviewReady} " +
                      $"(GraphicRaycaster selected option 2; options={choiceOptionCount}; rendered={renderedChoiceCardCount}; " +
                      $"kind={previewChoice?.kind ?? "none"}).");
            ShowStatus(result.Accepted ? "考古学家正在查看牌库顶三张牌；只有金色标记的掩埋牌可以出土。" : result.Message, !result.Accepted);
        }

        private void ApplyPlayerFactionVisuals(FactionSpec spec)
        {
            _activeFaction = spec.Id;
            RefreshDeckShell();
            if (_registry.TryGetTheme(spec.Id, out var selectedTheme))
            {
                _playerAvatarImage.color = Color.Lerp(selectedTheme.FrameDark, selectedTheme.Accent, 0.24f);
                _playerAvatarGlyph.color = selectedTheme.Accent;
            }
            ApplyFactionAvatar(_playerAvatarIcon, _playerAvatarGlyph, spec);
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
            _selectedCardTargetInstanceIds.Clear();
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
            ApplyFactionAvatar(_opponentAvatarIcon, _opponentAvatarGlyph, spec);
            var battlefieldTheme = DemoBattlefieldThemeCatalog.Get(spec.Id);
            _opponentTint.color = new Color(battlefieldTheme.UiTint.r, battlefieldTheme.UiTint.g, battlefieldTheme.UiTint.b, 0.085f);
            _opponentNameText.text = spec.PlayerTitle;
            _opponentFactionLabel.text = "敌方 · " + spec.Label;
        }

        private static void ApplyFactionAvatar(Image portrait, Text fallbackGlyph, FactionSpec spec)
        {
            var sprite = DemoCardArtProvider.Load(spec.PortraitCardId);
            portrait.sprite = sprite;
            portrait.gameObject.SetActive(sprite != null);
            fallbackGlyph.gameObject.SetActive(sprite == null);
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

        private static string FormatOpponentEnergy(IDemoMatchView match)
        {
            if (match == null) return "敌方红石 · --";
            if (match.IsFinished) return "敌方红石 · 终局";
            if (match.IsPlayerTurn)
            {
                var nextCapacity = Mathf.Min(10, match.OpponentMaxEnergy + (match.Round > 1 ? 1 : 0));
                return $"敌方红石 · 下回合 {nextCapacity}/{nextCapacity}";
            }

            var current = Mathf.Max(0, match.OpponentEnergy);
            var capacity = Mathf.Max(0, match.OpponentMaxEnergy);
            return match.OpponentTemporaryEnergy > 0
                ? $"敌方红石 ◆ {current}/{capacity} · 临时 +{match.OpponentTemporaryEnergy}"
                : $"敌方红石 ◆ {current}/{capacity}";
        }

        private Color ResolveRoundIndicatorColor(IDemoMatchView match)
        {
            if (match.IsFinished)
                return !match.HasWinner ? Gold : match.IsPlayerWinner ? Gold : Danger;

            var activeFactionId = !match.IsMulligan && !match.IsPlayerTurn
                ? match.OpponentFactionId
                : match.PlayerFactionId;
            return _registry != null && _registry.TryGetTheme(activeFactionId, out var theme)
                ? theme.Accent
                : Pale;
        }

        private void RefreshAll()
        {
            RefreshAllInternal(true);
        }

        private void RefreshAllInternal(bool refreshHand)
        {
            if (!_built || _battlefield == null) return;
            var match = MatchView;
            SynchronizeArenaTopology(match.ArenaId);
            RefreshHandInspection();
            RefreshStatusInspection();
            GetComponent<DemoBattlefieldPointerController>()?.SetInputEnabled(!match.IsFinished && !HasPendingOnlineCommand && !IsReadOnlyOverlayOpen &&
                !match.IsMulligan && (match.IsPlayerTurn || match.PendingChoice != null && match.IsChoiceOwner));
            if (match.IsFinished)
            {
                _selectedCardId = null;
                _selectedHandCardInstanceId = null;
                _pendingTargetCardId = null;
                _selectedAttackerInstanceId = null;
                _selectedDeploymentTargetInstanceId = null;
                _selectedCardTargetInstanceIds.Clear();
                _battlefield.ClearSlotInteractions();
            }
            RefreshMulligan();
            RefreshPendingChoice();
            RefreshFactionButtons();
            if (refreshHand) RefreshHand();
            RefreshOpponentHand(match.OpponentHandCount);
            RefreshBattlefieldSlots();
            RefreshOpponentHeroTarget();
            _battlefield.SyncPieces(match.PlayerBattlefield, match.OpponentBattlefield, _registry);
            RefreshInspector();
            _energyText.text = match.TemporaryEnergy > 0
                ? $"红石 ◆ {match.Energy}/{match.MaxEnergy}\n临时 +{match.TemporaryEnergy}"
                : $"红石 ◆ {match.Energy}/{match.MaxEnergy}";
            _opponentEnergyText.text = FormatOpponentEnergy(match);
            _titleText.text = IsOnlineBoard ? "群系竞逐  ·  权威联机对局"
                : _previewOnlineStatus ? "群系竞逐  ·  联机状态预览" : "群系竞逐  ·  本地战场演示";
            _roundText.text = match.IsFinished
                ? $"对局结束 · {(!match.HasWinner ? "平局" : match.IsPlayerWinner ? "胜利" : "战败")}"
                : match.IsMulligan ? "开局 · 起手调度" : $"第 {match.Round} 回合 · {(match.Phase == DemoTurnPhase.Main ? "主行动" : "战斗")}";
            _roundText.color = ResolveRoundIndicatorColor(match);
            _opponentHealthText.text = match.OpponentArmor > 0
                ? $"❤ {match.OpponentLife}  ◈ {match.OpponentArmor}"
                : $"❤ {match.OpponentLife}";
            _playerHealthText.text = match.PlayerArmor > 0 ? $"❤ {match.PlayerLife}  ◈ {match.PlayerArmor}" : $"❤ {match.PlayerLife}";
            if (match.HasPlayerStatus(false, "DARK")) _opponentHealthText.text += "  ◉ 黑暗";
            if (match.HasPlayerStatus(true, "DARK")) _playerHealthText.text += "  ◉ 黑暗";
            _opponentHealthText.color = match.HasPlayerStatus(false, "DARK") ? Hex("#62E3D4") : Hex("#F4C18A");
            _playerHealthText.color = match.HasPlayerStatus(true, "DARK") ? Hex("#62E3D4") : Hex("#B8E5A9");
            _playerEquipmentText.text = FormatEquipment(match.PlayerEquipment);
            _playerEquipmentText.color = match.PlayerEquipment == null ? Muted : Cyan;
            _opponentEquipmentText.text = FormatEquipment(match.OpponentEquipment);
            _opponentEquipmentText.color = match.OpponentEquipment == null ? Muted : Ember;
            var canIssueOnlineCommand = !IsReadOnlyOverlayOpen && (!IsOnlineBoard || _onlineSession.CanIssueCommand);
            _playerHeroButton.interactable = !match.IsFinished && canIssueOnlineCommand &&
                match.Phase == DemoTurnPhase.Combat && match.CanAttackWithHero(out _);
            SetHeroHudControlAvailability(_playerHeroControlAlpha, _playerHeroButton.interactable);
            _handLabel.text = match.BuriedCount > 0
                ? $"手牌 {match.Hand.Count}/7 · 牌库 {match.DeckCount}（掩埋 {match.BuriedCount}）· 弃牌 {match.DiscardCount}"
                : $"手牌 {match.Hand.Count}/7 · 牌库 {match.DeckCount} · 弃牌 {match.DiscardCount}";
            var canUseHand = !match.IsFinished && !match.IsMulligan && match.PendingChoice == null &&
                match.Phase == DemoTurnPhase.Main && match.IsPlayerTurn && canIssueOnlineCommand;
            // A locked hand is still information. Preserve readability without enabling any input.
            _handCanvasGroup.alpha = 1f;
            _handCanvasGroup.interactable = canUseHand;
            _handCanvasGroup.blocksRaycasts = canUseHand;
            _endTurnButton.interactable = !match.IsMulligan && match.PendingChoice == null && match.IsPlayerTurn && !match.IsFinished && canIssueOnlineCommand;
            var monumentThreatCount = CountOceanMonumentThreats(true);
            _endTurnLabel.text = IsOnlineBoard && _onlineSession.HasPendingCommand
                ? "等待服务器"
                : match.IsMulligan ? "等待起手确认"
                : match.PendingChoice != null ? match.PendingChoice.kind == "MOVE_UNIT"
                    ? match.IsChoiceOwner ? "选择移动地块" : "对手正在移动"
                    : match.PendingChoice.kind == "HEAL_UNIT"
                        ? match.IsChoiceOwner ? "选择治疗单位" : "对手正在治疗"
                    : match.PendingChoice.kind == "TOP_CARD_SCRY"
                        ? match.IsChoiceOwner ? "决定牌库顶" : "对手正在窥视"
                        : match.IsChoiceOwner ? "完成考古选择" : "对手正在选择"
                : match.IsFinished ? "本局已结束" : !match.IsPlayerTurn ? "对手行动中" : match.Phase == DemoTurnPhase.Main ? "进入战斗" :
                    monumentThreatCount > 0 ? $"结束回合 · 神殿 {monumentThreatCount}" : "结束回合";
            if (match.IsFinished)
            {
                var result = !match.HasWinner ? "平局" : match.IsPlayerWinner ? "胜利" : "战败";
                _statusSummary.SetFullText($"本局{result} · 所有操作已锁定。");
                _statusText.color = !match.HasWinner ? Gold : match.IsPlayerWinner ? Cyan : Danger;
            }
            RefreshStatusInspection();
        }

        private void RefreshPendingChoice()
        {
            if (_choiceOverlay == null) return;
            var match = MatchView;
            var choice = match.PendingChoice;
            var visible = choice != null && choice.kind != "MOVE_UNIT" && choice.kind != "HEAL_UNIT";
            var isNewChoice = visible && _renderedChoiceId != choice.choiceId;
            _choiceOverlay.gameObject.SetActive(visible);
            if (!visible)
            {
                CancelChoiceOverlayEntrance();
                _selectedChoiceOptionIndex = -1;
                _renderedChoiceId = choice?.choiceId;
                return;
            }

            if (isNewChoice)
            {
                _renderedChoiceId = choice.choiceId;
                _selectedChoiceOptionIndex = -1;
                BeginChoiceOverlayEntrance();
            }
            if (!IsHandInspectionOpen) _choiceOverlay.SetAsLastSibling();
            _choiceOverlayCanvasGroup.interactable = !_choiceOverlayEntranceActive && !IsHandInspectionOpen;
            ClearChildren(_choiceCardsRoot);
            var choiceOptions = (choice.options ?? Array.Empty<PendingChoiceOptionDto>())
                .Where(option => option != null).ToArray();
            ConfigureChoiceLayout(choiceOptions.Length == 1,
                choice.kind == "ARCHAEOLOGY_TOP_3" && choiceOptions.Length == 3);

            if (!match.IsChoiceOwner)
            {
                var opponentScry = choice.kind == "TOP_CARD_SCRY";
                _choiceTitleText.text = opponentScry ? "洞穴回声" : "沙漠考古";
                _choiceTitleText.color = opponentScry ? Ember : Gold;
                _choiceRuleText.text = opponentScry
                    ? "对手正在查看自己的牌库顶牌；牌面信息对你保密"
                    : "对手正在查看自己的牌库顶三张牌；牌面信息对你保密";
                _choiceStatusText.text = opponentScry ? "等待对手决定保留或置底…" : "等待对手完成考古选择…";
                _choiceConfirmButton.gameObject.SetActive(false);
                var hiddenCount = choiceOptions.Length;
                for (var index = 0; index < hiddenCount; index++) CreateHiddenChoiceCard(index, hiddenCount);
                return;
            }

            _choiceConfirmButton.gameObject.SetActive(true);
            var topCardScry = choice.kind == "TOP_CARD_SCRY";
            _choiceTitleText.text = topCardScry ? "洞穴回声" : "沙漠考古";
            _choiceTitleText.color = topCardScry ? Cyan : Gold;
            _choiceRuleText.text = topCardScry
                ? "查看牌库顶牌；直接确认可保留，选中卡牌后确认则将它置于牌库底"
                : "查看牌库顶 3 张；选择一张带“掩埋”标记的牌立即出土；若对局未结束，再正常抽一张牌";
            var options = choiceOptions;
            var hasSelectable = options.Any(option => option.selectable);
            foreach (var option in options) CreateChoiceCard(option, options.Length);
            if (topCardScry)
            {
                _choiceStatusText.text = _selectedChoiceOptionIndex < 0
                    ? "当前将保留这张牌；点击卡牌可改为置底"
                    : "已选择将这张牌置于牌库底";
                _choiceConfirmLabel.text = _selectedChoiceOptionIndex < 0 ? "保留牌库顶" : "置于牌库底";
            }
            else if (hasSelectable)
            {
                _choiceStatusText.text = _selectedChoiceOptionIndex < 0 ? "请选择一张金色标记的掩埋牌" : "已选择出土目标";
                _choiceConfirmLabel.text = _selectedChoiceOptionIndex < 0 ? "选择一张掩埋牌" : "确认出土";
            }
            else
            {
                _choiceStatusText.text = "这三张牌中没有掩埋牌，将保持原顺序放回";
                _choiceConfirmLabel.text = "确认未发现";
            }
            _choiceConfirmButton.interactable = !match.IsFinished &&
                (!IsOnlineBoard || _onlineSession.CanIssueCommand) &&
                (topCardScry || !hasSelectable || _selectedChoiceOptionIndex >= 0);
        }

        private void BeginChoiceOverlayEntrance()
        {
            CancelChoiceOverlayEntrance();
            if (_choiceOverlayCanvasGroup == null) return;
            _choiceOverlayCanvasGroup.alpha = 0f;
            _choiceOverlayCanvasGroup.interactable = false;
            _choiceOverlayCanvasGroup.blocksRaycasts = true;
            _choiceOverlayEntranceElapsed = 0f;
            _choiceOverlayEntranceActive = true;
        }

        private void AdvanceChoiceOverlayEntrance(float deltaTime)
        {
            if (!_choiceOverlayEntranceActive || _choiceOverlayCanvasGroup == null) return;
            _choiceOverlayEntranceElapsed += Mathf.Max(0f, deltaTime);
            var progress = Mathf.Clamp01(_choiceOverlayEntranceElapsed / ChoiceOverlayEntranceDuration);
            var eased = 1f - Mathf.Pow(1f - progress, 3f);
            _choiceOverlayCanvasGroup.alpha = eased;
            if (progress < 1f) return;

            _choiceOverlayEntranceActive = false;
            _choiceOverlayEntranceElapsed = 0f;
            _choiceOverlayCanvasGroup.alpha = 1f;
            _choiceOverlayCanvasGroup.interactable = !IsHandInspectionOpen;
            _choiceOverlayCanvasGroup.blocksRaycasts = true;
        }

        private void CancelChoiceOverlayEntrance()
        {
            _choiceOverlayEntranceActive = false;
            _choiceOverlayEntranceElapsed = 0f;
            if (_choiceOverlayCanvasGroup == null) return;
            _choiceOverlayCanvasGroup.alpha = 1f;
            _choiceOverlayCanvasGroup.interactable = !IsHandInspectionOpen;
            _choiceOverlayCanvasGroup.blocksRaycasts = true;
        }

        private void ConfigureChoiceLayout(bool singleOption, bool threeCardArchaeology)
        {
            if (_choicePanel == null) return;
            var expandedChoice = singleOption || threeCardArchaeology;
            var panelSize = singleOption ? new Vector2(860, 790) :
                threeCardArchaeology ? new Vector2(1080, 790) : new Vector2(1080, 670);
            _hudMaterialFactory?.ResizeDecoratedPanel(_choicePanel, panelSize);
            _choiceTitleText.rectTransform.anchoredPosition = new Vector2(0, expandedChoice ? 332 : 282);
            _choiceRuleText.rectTransform.anchoredPosition = new Vector2(0, expandedChoice ? 284 : 235);
            _choiceCardsRoot.anchoredPosition = new Vector2(0, expandedChoice ? -20 : 20);
            _choiceCardsRoot.sizeDelta = singleOption ? new Vector2(390, 480) :
                threeCardArchaeology ? new Vector2(780, 480) : new Vector2(780, 390);
            _choiceStatusText.rectTransform.anchoredPosition = new Vector2(0, expandedChoice ? -266 : -218);
            _choiceConfirmButton.GetComponent<RectTransform>().anchoredPosition =
                new Vector2(0, expandedChoice ? -338 : -280);
        }

        private void CreateChoiceCard(PendingChoiceOptionDto option, int optionCount)
        {
            var selected = option.optionIndex == _selectedChoiceOptionIndex;
            var topCardScry = MatchView.PendingChoice?.kind == "TOP_CARD_SCRY";
            var fullChoiceCard = optionCount == 1 ||
                MatchView.PendingChoice?.kind == "ARCHAEOLOGY_TOP_3" && optionCount == 3;
            var archaeologyChoice = !topCardScry && fullChoiceCard;
            var x = (option.optionIndex - (optionCount - 1) * 0.5f) * (archaeologyChoice ? 260f : 244f);
            var slotSize = optionCount == 1 ? new Vector2(286, 452) :
                archaeologyChoice ? new Vector2(254, 452) : new Vector2(220, 350);
            var slot = CreateBasePanel(_choiceCardsRoot, "ChoiceSlot" + option.optionIndex,
                new Vector2(x, selected ? 18f : 0f), slotSize);
            var accent = option.selectable ? topCardScry ? Cyan : Gold : Muted;
            slot.GetComponent<Image>().color = selected
                ? Color.Lerp(DemoUiStyleCatalog.GetRootFill(DemoUiStyleClass.BasePanel), accent, 0.46f)
                : DemoUiStyleCatalog.GetRootFill(DemoUiStyleClass.BasePanel);
            var materialFill = slot.Find("MaterialFill")?.GetComponent<Image>();
            if (materialFill != null && (option.selectable || selected))
                materialFill.color = Color.Lerp(materialFill.color, accent, selected ? 0.42f : 0.18f);
            var optionIndex = option.optionIndex;
            var card = DemoCardUiFactory.Create(
                slot, _registry, option.cardId, fullChoiceCard ? new Vector2(238, 342) : new Vector2(188, 270),
                !fullChoiceCard, UiFont,
                option.selectable ? (Action)(() => SelectChoiceOption(optionIndex)) : null);
            card.RectTransform.anchoredPosition = new Vector2(0, 18);
            if (option.selectable) card.gameObject.AddComponent<DemoHoverScale>().Configure(1.045f, 16f);
            var readRules = CreateSecondaryButton(slot, "ReadRules", new Vector2(0, fullChoiceCard ? -174 : -130),
                new Vector2(160, 24), "查看完整规则", 14);
            readRules.onClick.AddListener(() => OpenChoiceRules(option.cardId));
            CreateText(slot, "ChoiceLabel", new Vector2(0, fullChoiceCard ? -204 : -158),
                new Vector2(fullChoiceCard ? 248 : 188, 30),
                topCardScry
                    ? selected ? "◆ 将置于牌库底" : "点击选择置底"
                    : option.selectable ? selected ? "◆ 已选中" : "◆ 可出土" : "保持牌库顺序",
                14, accent, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void CreateHiddenChoiceCard(int index, int optionCount)
        {
            var x = (index - (optionCount - 1) * 0.5f) * 244f;
            var singleOption = optionCount <= 1;
            var slot = CreateBasePanel(_choiceCardsRoot, "HiddenChoiceSlot" + index, new Vector2(x, 0),
                singleOption ? new Vector2(286, 452) : new Vector2(220, 350));
            CreateText(slot, "HiddenGlyph", new Vector2(0, singleOption ? 34 : 22),
                singleOption ? new Vector2(220, 300) : new Vector2(160, 210),
                "◇\n?", singleOption ? 64 : 46, Ember, TextAnchor.MiddleCenter, FontStyle.Bold);
            CreateText(slot, "HiddenLabel", new Vector2(0, singleOption ? -200 : -144),
                new Vector2(singleOption ? 248 : 180, 30), "牌库信息保密", 14, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
        }

        private void SelectChoiceOption(int optionIndex)
        {
            if (IsReadOnlyOverlayOpen) return;
            var choice = MatchView.PendingChoice;
            if (MatchView.IsFinished || choice == null || !MatchView.IsChoiceOwner || (IsOnlineBoard && !_onlineSession.CanIssueCommand)) return;
            var option = (choice.options ?? Array.Empty<PendingChoiceOptionDto>())
                .FirstOrDefault(value => value != null && value.optionIndex == optionIndex && value.selectable);
            if (option == null) return;
            _selectedChoiceOptionIndex = choice.kind == "TOP_CARD_SCRY" && _selectedChoiceOptionIndex == optionIndex
                ? -1
                : optionIndex;
            RefreshPendingChoice();
        }

        private async void ConfirmChoice()
        {
            if (IsReadOnlyOverlayOpen) return;
            var choice = MatchView.PendingChoice;
            if (MatchView.IsFinished || choice == null || !MatchView.IsChoiceOwner) return;
            var options = choice.options ?? Array.Empty<PendingChoiceOptionDto>();
            var hasSelectable = options.Any(option => option != null && option.selectable);
            var topCardScry = choice.kind == "TOP_CARD_SCRY";
            if (!topCardScry && hasSelectable && _selectedChoiceOptionIndex < 0) return;
            var selectedOptionIndex = topCardScry ? _selectedChoiceOptionIndex : hasSelectable ? _selectedChoiceOptionIndex : -1;
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
            ShowLocalCommandResultStatus(result, message);
            if (result.Accepted && !TryShowLocalMatchOutcome())
                StartCoroutine(ShowTurnBanner(topCardScry
                    ? selectedOptionIndex < 0 ? "保留牌顶" : "沉入牌底"
                    : hasSelectable ? "出土" : "未发现", topCardScry ? Cyan : hasSelectable ? Gold : Muted));
            RefreshAll();
        }

        private async void ResolveMovementChoice(int optionIndex)
        {
            var choice = MatchView.PendingChoice;
            if (MatchView.IsFinished || choice == null || choice.kind != "MOVE_UNIT" || !MatchView.IsChoiceOwner) return;
            if (choice.effectId == "effect.tk_012.01" && optionIndex < 0) return;
            if (IsOnlineBoard)
            {
                if (!_onlineSession.CanIssueCommand) return;
                await SendOnline(() => _onlineSession.ResolveChoiceAsync(choice.choiceId, optionIndex));
                RefreshAll();
                return;
            }
            var waterCurrent = choice.effectId == "effect.or_001.01";
            var prismarineShard = choice.effectId == "effect.tk_012.01";
            var result = _match.ApplyResolveChoice(_match.CreateResolveChoiceCommand(choice.choiceId, optionIndex));
            ShowLocalCommandResultStatus(result);
            if (result.Accepted && !TryShowLocalMatchOutcome())
                StartCoroutine(ShowTurnBanner(optionIndex < 0 ? "保持原位" : prismarineShard ? "碎片涌流" : waterCurrent ? "水流移动" : "激流位移", Gold));
            RefreshAll();
        }

        private async void ResolveHealingChoice(int optionIndex)
        {
            var choice = MatchView.PendingChoice;
            if (MatchView.IsFinished || choice == null || choice.kind != "HEAL_UNIT" || !MatchView.IsChoiceOwner) return;
            if (IsOnlineBoard)
            {
                if (!_onlineSession.CanIssueCommand) return;
                await SendOnline(() => _onlineSession.ResolveChoiceAsync(choice.choiceId, optionIndex));
                RefreshAll();
                return;
            }
            var result = _match.ApplyResolveChoice(_match.CreateResolveChoiceCommand(choice.choiceId, optionIndex));
            var message = result.Message;
            foreach (var option in choice.options ?? Array.Empty<PendingChoiceOptionDto>())
                if (option != null && !string.IsNullOrEmpty(option.cardId)) message = message.Replace(option.cardId, GetCardName(option.cardId));
            ShowLocalCommandResultStatus(result, message);
            if (result.Accepted) StartCoroutine(ShowTurnBanner("恢复 1 点生命", Cyan));
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
            var openingHandCard = index < match.HandCards.Count ? match.HandCards[index] : null;
            var card = DemoCardUiFactory.Create(slot, _registry, openingHand[index], new Vector2(184, 262), true, UiFont,
                () => ToggleMulliganCard(selectedIndex), handCardInstanceId: openingHandCard?.handCardInstanceId ?? string.Empty);
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
            var selectionLocked = IsFactionSelectionLocked;
            foreach (var view in _factionButtons)
            {
                var active = view.Id == _activeFaction;
                view.Image.color = DemoUiStyleCatalog.GetRootFill(DemoUiStyleClass.SecondaryButton);
                view.SelectionAccent.gameObject.SetActive(active);
                view.Button.interactable = !selectionLocked;
                view.Label.color = selectionLocked ? Muted : Pale;
            }
            if (_previousOpponentFactionButton != null) _previousOpponentFactionButton.interactable = !selectionLocked;
            if (_nextOpponentFactionButton != null) _nextOpponentFactionButton.interactable = !selectionLocked;
            if (_previousOpponentFactionLabelText != null) _previousOpponentFactionLabelText.color = selectionLocked ? Muted : Pale;
            if (_nextOpponentFactionLabelText != null) _nextOpponentFactionLabelText.color = selectionLocked ? Muted : Pale;
        }

        private void RefreshHand()
        {
            var match = MatchView;
            var count = match.Hand.Count;
            var currentInstanceIds = new HashSet<string>(match.HandCards
                .Where(value => value != null && !string.IsNullOrEmpty(value.handCardInstanceId))
                .Select(value => value.handCardInstanceId), StringComparer.Ordinal);
            var hasHandContinuity = _hasRenderedHandSnapshot && _renderedHandCardInstanceIds.Count > 0 &&
                currentInstanceIds.Overlaps(_renderedHandCardInstanceIds);
            ClearChildren(_handRoot);
            var maximumFanOffset = Mathf.Max(0f, (count - 1) * 0.5f);
            var maximumProjectedHalfWidth = (166f * Mathf.Cos(maximumFanOffset * 0.5f * Mathf.Deg2Rad) +
                216f * Mathf.Sin(maximumFanOffset * 0.5f * Mathf.Deg2Rad)) * 0.5f;
            var handPlate = _handRoot.parent as RectTransform;
            var maximumCenterX = (handPlate != null ? handPlate.rect.width * 0.5f : 680f) - 24f -
                Mathf.Abs(_handRoot.anchoredPosition.x) - maximumProjectedHalfWidth;
            var handSpacing = count > 1 ? Mathf.Min(190f, maximumCenterX * 2f / (count - 1)) : 0f;
            for (var i = 0; i < count; i++)
            {
                var cardId = match.Hand[i];
                if (!_registry.TryGetDefinition(cardId, out var definition)) continue;
                var handCard = i < match.HandCards.Count ? match.HandCards[i] : null;
                var handCardInstanceId = handCard?.handCardInstanceId ?? string.Empty;
                var selected = handCard != null
                    ? handCardInstanceId == _selectedHandCardInstanceId
                    : cardId == _selectedCardId;
                var fanOffset = i - (count - 1) * 0.5f;
                var fanRotation = fanOffset * -0.5f;
                var x = fanOffset * handSpacing;
                var y = selected ? 20f : Mathf.Abs(fanOffset) * 9f;
                var supportsBoardDrop = definition.cardType == "UNIT" || definition.cardType == "BUILDING" ||
                    definition.cardType == "STRUCTURE";
                var effectiveCost = match.GetEffectiveCost(definition, handCardInstanceId);
                var canPay = DemoDeploymentRules.CanPayWithRedstoneOrCrafting(match, definition, handCardInstanceId);
                var card = DemoCardUiFactory.Create(_handRoot, _registry, cardId, new Vector2(166, 216), true, UiFont,
                    () => SelectHandCard(cardId, handCardInstanceId), effectiveCost,
                    handCardInstanceId,
                    supportsBoardDrop ? () => SelectHandCardInternal(cardId, handCardInstanceId, false) : null,
                    supportsBoardDrop ? OnHandCardDragMoved : null,
                    supportsBoardDrop ? OnHandCardDragDropped : null);
                card.SetResourceAffordable(canPay);
                card.RectTransform.anchoredPosition = new Vector2(x, y);
                card.RectTransform.localRotation = Quaternion.Euler(0, 0, fanRotation);
                var isNewHandInstance = handCard != null && !string.IsNullOrEmpty(handCardInstanceId) &&
                    hasHandContinuity && !_renderedHandCardInstanceIds.Contains(handCardInstanceId);
                if (isNewHandInstance) card.PlayArrivalAnimation();
                card.gameObject.AddComponent<DemoHoverScale>().Configure(
                    1.22f, 16f, raiseToFrontOnHover: true, lift: 32f);
                if (selected)
                {
                    card.RectTransform.SetAsLastSibling();
                }
            }
            _renderedHandCardInstanceIds.Clear();
            _renderedHandCardInstanceIds.UnionWith(currentInstanceIds);
            _hasRenderedHandSnapshot = true;
        }

        private void SetupFullHandPreview()
        {
            _match.ResetHand(new[] { "ed_002", "ed_003", "ed_004", "ed_005", "ed_006", "ed_007", "ed_008" });
            SelectFirstHandCard();
            if (HasCommandLineFlag("-previewFullHandCombat"))
            {
                var entered = _match.ApplyEnterCombat(_match.CreateEnterCombatCommand());
                if (!entered.Accepted) throw new InvalidOperationException("Full-hand combat preview could not enter combat: " + entered.Message);
            }
            RefreshAll();
            ShowStatus("七张末地手牌预览：长卡名避开费用徽章，卡牌完整落在石砖底板内。", false);
        }

        private bool IsFullHandLayoutWithinPlate()
        {
            var handPlate = _canvasRoot.Find("HandPlate") as RectTransform;
            if (handPlate == null || !Mathf.Approximately(handPlate.rect.width, 1360f)) return false;
            var cards = _handRoot.GetComponentsInChildren<CardUI>(true);
            if (cards.Length != 7) return false;
            var corners = new Vector3[4];
            var horizontalBounds = new List<Vector2>(cards.Length);
            var halfPlateWidth = handPlate.rect.width * 0.5f;
            foreach (var card in cards)
            {
                if (card.RectTransform.sizeDelta != new Vector2(166f, 216f)) return false;
                card.RectTransform.GetWorldCorners(corners);
                for (var cornerIndex = 0; cornerIndex < corners.Length; cornerIndex++)
                {
                    var localCorner = handPlate.InverseTransformPoint(corners[cornerIndex]);
                    if (localCorner.x < -halfPlateWidth + 24f - FullHandLayoutCoordinateTolerance ||
                        localCorner.x > halfPlateWidth - 24f + FullHandLayoutCoordinateTolerance)
                    {
                        Debug.LogError($"Full hand card escaped stone hand plate: card={card.CardId}, x={localCorner.x:F3}, safe=[{-halfPlateWidth + 24f:F3},{halfPlateWidth - 24f:F3}].");
                        return false;
                    }
                }
                var localXs = corners.Select(corner => handPlate.InverseTransformPoint(corner).x).ToArray();
                horizontalBounds.Add(new Vector2(localXs.Min(), localXs.Max()));
            }
            horizontalBounds.Sort((left, right) => left.x.CompareTo(right.x));
            for (var index = 1; index < horizontalBounds.Count; index++)
                if (horizontalBounds[index - 1].y + 8f > horizontalBounds[index].x)
                {
                    Debug.LogError($"Full hand cards overlap in projection: previous={horizontalBounds[index - 1]}, next={horizontalBounds[index]}.");
                    return false;
                }
            return true;
        }

        private IEnumerator SetupCardArrivalPreview()
        {
            SelectFaction("plains_forest");
            if (!_registry.TryGetDefinition("pf_001", out _) || !_registry.TryGetDefinition("pf_002", out _))
            {
                Debug.LogError("Hand card arrival preview could not resolve its registered draw cards.");
                yield break;
            }

            _match.ResetDeckAndHand(new[] { "pf_001" }, new[] { "pf_002" });
            RefreshAll();
            yield return null;
            if (_match.HandCards.Count != 1)
            {
                Debug.LogError("Hand card arrival preview could not establish its initial stable hand instance.");
                yield break;
            }

            var existingInstanceId = _match.HandCards[0].handCardInstanceId;
            _match.EndPlayerTurn();
            var draw = _match.BeginNextPlayerTurn();
            if (draw.Outcome != DemoDrawOutcome.Drawn || draw.CardId != "pf_002")
            {
                Debug.LogError($"Hand card arrival preview expected a normal draw of pf_002, got {draw.Outcome}/{draw.CardId}.");
                yield break;
            }

            RefreshAll();
            var newInstanceId = _match.HandCards.Last().handCardInstanceId;
            var cards = _handRoot.GetComponentsInChildren<CardUI>(true);
            var existingCard = cards.FirstOrDefault(value => value.HandCardInstanceId == existingInstanceId);
            var arrivingCard = cards.FirstOrDefault(value => value.HandCardInstanceId == newInstanceId);
            if (existingCard == null || arrivingCard == null || existingCard.IsArrivalAnimating || !arrivingCard.IsArrivalAnimating)
            {
                Debug.LogError("Hand card arrival preview did not animate only the newly drawn hand instance.");
                yield break;
            }

            var canvasGroup = arrivingCard.GetComponent<CanvasGroup>();
            yield return new WaitForSecondsRealtime(0.08f);
            var visibleMidAnimation = canvasGroup != null && canvasGroup.alpha > 0f && canvasGroup.alpha < 1f &&
                !canvasGroup.interactable && !canvasGroup.blocksRaycasts;
            var timeout = 0f;
            while (arrivingCard.IsArrivalAnimating && timeout < 1f)
            {
                timeout += Time.unscaledDeltaTime;
                yield return null;
            }

            var settled = visibleMidAnimation && !arrivingCard.IsArrivalAnimating &&
                Mathf.Approximately(arrivingCard.ArrivalAnimationAlpha, 1f) &&
                canvasGroup != null && canvasGroup.interactable && canvasGroup.blocksRaycasts &&
                !existingCard.IsArrivalAnimating;
            if (settled) ShowStatus("抽到的卡牌已平滑进入手牌。", false);
            Debug.Log($"Hand card arrival preview settled: {settled} (new instance faded in; existing instance stayed still; input restored).");
            _cardArrivalPreviewCompleted = true;
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
            var selectedHero = _selectedAttackerInstanceId == MatchAttackerIds.Hero;
            var selectedAttackerCanAttack = attacker != null
                ? match.CanAttackWith(attacker, out _)
                : selectedHero && match.CanAttackWithHero(out _);
            _opponentHeroTargetButton.interactable = !IsReadOnlyOverlayOpen && !match.IsFinished && match.Phase == DemoTurnPhase.Combat &&
                match.IsPlayerTurn && selectedAttackerCanAttack &&
                (!IsOnlineBoard || _onlineSession.CanIssueCommand) && match.CanAttackTarget(null, "HERO", out _);
            SetHeroHudControlAvailability(_opponentHeroControlAlpha, _opponentHeroTargetButton.interactable);
        }

        private static void SetHeroHudControlAvailability(CanvasGroup canvasGroup, bool available)
        {
            if (canvasGroup != null) canvasGroup.alpha = available ? 1f : InactiveHeroHudAlpha;
        }

        private void RefreshSlot(bool player, SlotView view, string cardId)
        {
            ClearChildrenExcept(view.Content, view.EmptyLabel.gameObject);
            var empty = string.IsNullOrEmpty(cardId);
            view.EmptyLabel.gameObject.SetActive(empty);
            var match = MatchView;
            var battlefieldObject = match.GetObject(player, view.Kind, view.Index);
            var selectingCardTarget = !string.IsNullOrEmpty(_pendingTargetCardId);
            var canInteract = !match.IsFinished && (!IsOnlineBoard || _onlineSession.CanIssueCommand);
            var valid = false;
            var movementChoice = match.PendingChoice != null && match.PendingChoice.kind == "MOVE_UNIT";
            var healingChoice = match.PendingChoice != null && match.PendingChoice.kind == "HEAL_UNIT";
            var movementTargetIsPlayer = movementChoice && match.PlayerBattlefield.Any(value =>
                value != null && value.InstanceId == match.PendingChoice.targetInstanceId);
            if (canInteract && movementChoice)
                valid = player == movementTargetIsPlayer && view.Kind == DemoSlotKind.Unit && empty && match.IsChoiceOwner &&
                    (match.PendingChoice.options ?? Array.Empty<PendingChoiceOptionDto>()).Any(option =>
                        option != null && option.selectable && option.slotIndex == view.Index);
            else if (canInteract && healingChoice)
                valid = player && view.Kind == DemoSlotKind.Unit && !empty && match.IsChoiceOwner &&
                    (match.PendingChoice.options ?? Array.Empty<PendingChoiceOptionDto>()).Any(option =>
                        option != null && option.selectable && option.slotIndex == view.Index && option.cardId == battlefieldObject?.CardId);
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
            var activatesTamedWolf = valid && player && empty && view.Kind == DemoSlotKind.Unit &&
                !string.IsNullOrEmpty(_selectedCardId) &&
                _registry.TryGetDefinition(_selectedCardId, out var wolfDefinition) && IsTamedWolf(wolfDefinition) &&
                TamedWolfBattlecryActivatesAt(view.Index);
            var activatesGoat = valid && player && empty && view.Kind == DemoSlotKind.Unit &&
                !string.IsNullOrEmpty(_selectedCardId) && FindSelectedDeploymentTarget() != null &&
                _registry.TryGetDefinition(_selectedCardId, out var goatDefinition) && IsGoat(goatDefinition);
            var selectedCardTarget = battlefieldObject != null &&
                _selectedCardTargetInstanceIds.Contains(battlefieldObject.InstanceId);
            var selectedDeploymentTarget = battlefieldObject != null &&
                battlefieldObject.InstanceId == _selectedDeploymentTargetInstanceId;
            var priorityTarget = canInteract && (selectedCardTarget || selectedDeploymentTarget ||
                movementChoice && battlefieldObject?.InstanceId == match.PendingChoice.targetInstanceId ||
                healingChoice && valid ||
                valid && !player && battlefieldObject?.HasKeyword("TAUNT") == true || activatesDrowned || activatesTamedWolf || activatesGoat);
            var auraBattlefield = player ? match.PlayerBattlefield : match.OpponentBattlefield;
            var auraLayers = view.Kind == DemoSlotKind.Unit
                ? auraBattlefield.Count(value => value != null && value.Health > 0 && value.CardId == "or_005" &&
                    value.InstanceId != battlefieldObject?.InstanceId && Mathf.Abs(value.SlotIndex - view.Index) == 1)
                : 0;
            _battlefield.SetSlotAura(player, view.Kind, view.Index, auraLayers);
            var engineReadyKind = ResolveSlotEngineReadyKind(player, view.Kind, battlefieldObject);
            _battlefield.SetSlotEngineReady(
                player,
                view.Kind,
                view.Index,
                engineReadyKind,
                engineReadyKind == DemoEngineReadyKind.None ? null : battlefieldObject?.InstanceId);
            _battlefield.SetSlotEndPhaseThreat(player, view.Kind, view.Index,
                IsOceanMonumentThreat(player, battlefieldObject));
            _battlefield.SetSlotPoisoned(player, view.Kind, view.Index,
                battlefieldObject?.HasStatus("POISON") == true);
            _battlefield.SetSlotBurning(player, view.Kind, view.Index,
                battlefieldObject?.HasStatus("FIRE") == true);
            _battlefield.SetSlotWithered(player, view.Kind, view.Index,
                battlefieldObject?.HasStatus("WITHER") == true);
            _battlefield.SetSlotState(player, view.Kind, view.Index, valid, !empty, priorityTarget);

            if (!empty)
            {
                if (view.Kind == DemoSlotKind.Building && battlefieldObject != null && battlefieldObject.SlotIndex != view.Index)
                    CreateText(view.Content, "Occupied", Vector2.zero, view.Content.sizeDelta, "结构占用", 14, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
                else
                    CreateWorldPieceLabel(view.Content, cardId, view.Content.sizeDelta, !player, battlefieldObject);
            }
        }

        private DemoEngineReadyKind ResolveSlotEngineReadyKind(bool player, DemoSlotKind kind, DemoBattlefieldObject battlefieldObject)
        {
            var match = MatchView;
            if (match.IsFinished || battlefieldObject == null || battlefieldObject.Health <= 0)
                return DemoEngineReadyKind.None;
            var engineReadyKind = DemoEngineReadyKind.None;
            if (kind == DemoSlotKind.Building && battlefieldObject?.CardId == "pf_005" &&
                !match.HasTriggeredEffect(player, battlefieldObject.InstanceId, "effect.pf_005.01"))
                engineReadyKind = DemoEngineReadyKind.Nursery;
            else if (kind == DemoSlotKind.Building && battlefieldObject?.CardId == "or_007" &&
                !match.HasTriggeredEffect(player, battlefieldObject.InstanceId, "effect.or_007.01"))
                engineReadyKind = DemoEngineReadyKind.Coral;
            else if (kind == DemoSlotKind.Building && battlefieldObject?.CardId == "db_004" &&
                !match.HasTriggeredEffect(player, battlefieldObject.InstanceId, "effect.db_004.01"))
                engineReadyKind = DemoEngineReadyKind.Cactus;
            else if (kind == DemoSlotKind.Building && battlefieldObject?.CardId == "cd_004" &&
                match.IsPlayerTurn != player && match.CardsPlayedThisTurn(!player) == 1)
                engineReadyKind = DemoEngineReadyKind.Sculk;
            else if (kind == DemoSlotKind.Building && battlefieldObject?.CardId == "db_007")
                engineReadyKind = DemoEngineReadyKind.Temple;
            else if (kind == DemoSlotKind.Building && battlefieldObject?.CardId == "cd_007" &&
                match.IsPlayerTurn == player && match.CardsPlayedThisTurn(player) == 1)
                engineReadyKind = DemoEngineReadyKind.Mine;
            else if (kind == DemoSlotKind.Building && battlefieldObject?.CardId == "cd_008" &&
                TryGetEndPhaseSummonReadiness(player, false, out _))
                engineReadyKind = DemoEngineReadyKind.Mansion;
            else if (kind == DemoSlotKind.Building && battlefieldObject?.CardId == "si_008")
                engineReadyKind = DemoEngineReadyKind.IceSpire;
            else if (kind == DemoSlotKind.Building && battlefieldObject?.CardId == "si_007" &&
                !match.HasTriggeredEffect(player, battlefieldObject.InstanceId, "effect.si_007.01") &&
                (player ? match.PlayerBattlefield : match.OpponentBattlefield).Any(value =>
                    value != null && value.SlotKind == DemoSlotKind.Unit && value.Health > 0 && value.Health < value.MaxHealth))
                engineReadyKind = DemoEngineReadyKind.SnowHut;
            else if (kind == DemoSlotKind.Building && battlefieldObject?.CardId == "ed_007" &&
                match.IsPlayerTurn == player)
                engineReadyKind = DemoEngineReadyKind.EndCrystal;
            else if (kind == DemoSlotKind.Building && battlefieldObject?.CardId == "nt_008" &&
                TryGetEndPhaseSummonReadiness(player, true, out _))
                engineReadyKind = DemoEngineReadyKind.NetherFortress;
            else if (kind == DemoSlotKind.Unit && battlefieldObject?.CardId == "nt_003" &&
                match.IsPlayerTurn == player && match.Phase == DemoTurnPhase.Combat && !battlefieldObject.HasAttacked &&
                !battlefieldObject.HasStatus("SLOW"))
                engineReadyKind = DemoEngineReadyKind.Blaze;
            return engineReadyKind;
        }

        private bool IsOceanMonumentThreat(bool targetPlayer, DemoBattlefieldObject target)
        {
            var match = MatchView;
            if (match.IsFinished || target == null || target.Health <= 0 || target.SlotKind != DemoSlotKind.Unit) return false;
            var sourcePlayer = match.IsPlayerTurn;
            if (targetPlayer == sourcePlayer) return false;
            var sourceBattlefield = sourcePlayer ? match.PlayerBattlefield : match.OpponentBattlefield;
            if (!sourceBattlefield.Any(value => value != null && value.Health > 0 &&
                value.SlotKind == DemoSlotKind.Building && value.CardId == "or_008")) return false;
            var targetBattlefield = targetPlayer ? match.PlayerBattlefield : match.OpponentBattlefield;
            return !targetBattlefield.Any(value => value != null && value.Health > 0 &&
                value.SlotKind == DemoSlotKind.Unit && value.InstanceId != target.InstanceId &&
                Mathf.Abs(value.SlotIndex - target.SlotIndex) == 1);
        }

        private bool HasEmptyUnitSlot(bool player)
        {
            var battlefield = player ? MatchView.PlayerBattlefield : MatchView.OpponentBattlefield;
            return DemoDeploymentRules.FindFirstEmptyUnitSlot(
                battlefield, _battlefield.GetSlotCount(DemoSlotKind.Unit)) >= 0;
        }

        // Read only public match state, so local and authoritative views report the same
        // current prerequisites. End-phase ordering can still change them before resolution.
        private bool TryGetEndPhaseSummonReadiness(bool player, bool requiresRedstone, out string status)
        {
            var match = MatchView;
            if (match.IsFinished) status = "对局已结束";
            else if (match.IsPlayerTurn != player) status = player ? "等待己方回合" : "等待敌方回合";
            else if (!HasEmptyUnitSlot(player)) status = "单位格已满";
            else if (requiresRedstone && (player ? match.Energy : match.OpponentEnergy) < 1) status = "红石不足";
            else
            {
                status = "结束阶段就绪";
                return true;
            }
            return false;
        }

        private int CountOceanMonumentThreats(bool sourcePlayer)
        {
            var match = MatchView;
            if (match.IsPlayerTurn != sourcePlayer) return 0;
            var targetBattlefield = sourcePlayer ? match.OpponentBattlefield : match.PlayerBattlefield;
            return targetBattlefield.Count(value => IsOceanMonumentThreat(!sourcePlayer, value));
        }

        private void RefreshInspector()
        {
            var match = MatchView;
            ClearChildren(_inspectorRoot);
            var moving = match.PendingChoice != null && match.PendingChoice.kind == "MOVE_UNIT";
            var healing = match.PendingChoice != null && match.PendingChoice.kind == "HEAL_UNIT";
            CreateText(_inspectorRoot, "Header", new Vector2(0, 360), new Vector2(250, 38),
                match.IsFinished ? "对局结果" : moving ? "位移指令" : healing ? "雪屋疗愈" : match.Phase == DemoTurnPhase.Main ? "卡牌详情" : "战斗指令",
                20, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
            if (match.IsFinished)
            {
                _cardDetailsView.Clear();
                var outcome = !match.HasWinner ? "平局" : match.IsPlayerWinner ? "胜利" : "战败";
                var outcomeColor = !match.HasWinner ? Gold : match.IsPlayerWinner ? Cyan : Danger;
                CreateText(_inspectorRoot, "MatchOutcome", new Vector2(0, 142), new Vector2(235, 74),
                    outcome, 32, outcomeColor, TextAnchor.MiddleCenter, FontStyle.Bold);
                CreateText(_inspectorRoot, "MatchSummary", new Vector2(0, 28), new Vector2(235, 150),
                    $"最终英雄生命\n己方  {match.PlayerLife}\n对手  {match.OpponentLife}\n\n所有操作已锁定",
                    17, Pale, TextAnchor.MiddleCenter, FontStyle.Normal);
                return;
            }
            if (moving)
            {
                _cardDetailsView.Clear();
                var waterCurrent = match.PendingChoice.effectId == "effect.or_001.01";
                var prismarineShard = match.PendingChoice.effectId == "effect.tk_012.01";
                var guideReady = (waterCurrent || prismarineShard) && match.PlayerBattlefield.Any(value => value != null &&
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
                    prismarineShard ? "海晶碎片 · 涌流\n选择发光的相邻地块" :
                    waterCurrent ? "鲑鱼群 · 水流\n选择发光的相邻地块" : "激流三叉戟\n选择发光的相邻地块", 19, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
                var movementHint = prismarineShard
                    ? "必须移动；落位后恢复 1 点生命。"
                    : "直接点击目标旁的发光地块，\n或保持目标原位。";
                if (guideReady) movementHint += "\n海豚向导将追加 +1 攻击。";
                if (guardianThreats > 0) movementHint += prismarineShard
                    ? $"\n警告：治疗后承受 {guardianThreats} 点守卫者伤害。"
                    : $"\n警告：移动后承受 {guardianThreats} 点守卫者伤害；保持原位不触发。";
                CreateText(_inspectorRoot, "CombatHint", new Vector2(0, -5), new Vector2(245, 100),
                    match.IsChoiceOwner ? movementHint : "等待对手决定目标单位的位置。",
                    guardianThreats > 0 ? 14 : 15, guardianThreats > 0 ? Gold : Pale, TextAnchor.MiddleCenter, FontStyle.Normal);
                if (match.IsChoiceOwner && !prismarineShard)
                {
                    var stay = CreateSecondaryButton(_inspectorRoot, "KeepPosition", new Vector2(0, -105), new Vector2(220, 52), "保持原位", 16);
                    stay.interactable = !IsOnlineBoard || _onlineSession.CanIssueCommand;
                    stay.onClick.AddListener(() => ResolveMovementChoice(-1));
                }
                return;
            }
            if (healing)
            {
                _cardDetailsView.Clear();
                CreateText(_inspectorRoot, "HealingTitle", new Vector2(0, 105), new Vector2(245, 120),
                    "雪屋 · 起始阶段\n选择发光的受伤单位", 19, Cyan, TextAnchor.MiddleCenter, FontStyle.Bold);
                CreateText(_inspectorRoot, "HealingHint", new Vector2(0, -10), new Vector2(235, 115),
                    match.IsChoiceOwner
                        ? "这些单位缺失的生命值并列最多。\n直接点击场内模型完成治疗。"
                        : "等待对手选择一个并列的\n最重伤单位。",
                    15, Pale, TextAnchor.MiddleCenter, FontStyle.Normal);
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
                var combatAccent = _registry != null && _registry.TryGetTheme(_activeFaction, out var playerTheme)
                    ? playerTheme.Accent
                    : Cyan;
                CreateText(_inspectorRoot, "CombatTitle", new Vector2(0, 100), new Vector2(245, 130), title, 19, combatAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
                var combatHint = "再点击敌方生物、建筑，\n或左上角敌方英雄面板。\n单位会同步反击，建筑不会反击。";
                if (match.HasPlayerStatus(true, "DARK") && !match.HasTargetedEnemyObjectThisTurn(true))
                    combatHint = "黑暗笼罩：第一次指定战场对象\n只能选择每排最左或最右的青色目标。\n敌方英雄不受此限制。";
                if ((attacker != null || heroSelected) && !match.CanAttackTarget(null, "HERO", out var tauntMessage))
                    combatHint = tauntMessage + "\n只有金色地表目标可被攻击。";
                var canvasScaleFactor = _canvasRoot.GetComponent<Canvas>().scaleFactor;
                var combatHintFontSize = DemoUiMetrics.GetScreenReadableFontSize(15, 12f, canvasScaleFactor);
                CreateText(_inspectorRoot, "CombatHint", new Vector2(0, -25), new Vector2(245, 120), combatHint, combatHintFontSize, Pale, TextAnchor.MiddleCenter, FontStyle.Normal);
                return;
            }
            if (string.IsNullOrEmpty(_selectedCardId) || !_registry.TryGetDefinition(_selectedCardId, out var definition))
            {
                _cardDetailsView.Clear();
                CreateText(_inspectorRoot, "Empty", new Vector2(0, 40), new Vector2(230, 180), "手牌已打空。\n切换左侧群系可重新装填演示手牌。", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
                return;
            }

            var selectedHandCardInstanceId = GetSelectedHandCardInstanceId();
            var hasSelectedHandCard = match.HandCards.Any(value => value != null &&
                value.handCardInstanceId == selectedHandCardInstanceId && value.cardId == definition.id);
            var cardAccent = _registry.TryGetTheme(definition.themeId, out var selectedCardTheme)
                ? selectedCardTheme.Accent
                : Cyan;
            var effectiveCost = hasSelectedHandCard
                ? match.GetEffectiveCost(definition, selectedHandCardInstanceId)
                : definition.cost;
            var isDiscounted = effectiveCost < definition.cost;
            _cardDetailsView.ShowCard(_selectedCardId, new Vector2(250, 430), new Vector2(0, 120), effectiveCost,
                selectedHandCardInstanceId, summarizeRules: true);
            var deployType = definition.cardType == "UNIT" || definition.cardType == "BUILDING" || definition.cardType == "STRUCTURE";
            var pendingDeployEffect = deployType && definition.effectImplementationStatus == "PENDING";
            if (!hasSelectedHandCard)
            {
                CreateText(_inspectorRoot, "StaleHandSelection", new Vector2(0, -140), new Vector2(246, 72),
                    "所选手牌副本已不在手牌中\n请重新选择一张卡", 15, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
                return;
            }
            if (!match.IsPlayerTurn)
            {
                CreateText(_inspectorRoot, "TurnLockedHint", new Vector2(0, -146), new Vector2(246, 82),
                    "对手行动中\n手牌操作已锁定", 16, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            else if (deployType)
            {
                var target = definition.cardType == "UNIT" ? "单位格" : $"建筑格（占 {Mathf.Max(1, definition.buildingSlots)} 格）";
                var requiresBattlecryTarget = DemoCardTargeting.TryGetRule(definition, out var battlecryTargetRule);
                var selectedBattlecryTarget = requiresBattlecryTarget ? FindSelectedDeploymentTarget() : null;
                var conditionalDrowned = IsDrowned(definition);
                var drownedCanActivate = conditionalDrowned && HasPotentialDrownedBattlecrySlot();
                var drownedHasTarget = conditionalDrowned && DemoCardTargeting.HasLegalTarget(match, battlecryTargetRule);
                var hasLegalBattlecryTarget = requiresBattlecryTarget && DemoCardTargeting.HasLegalTarget(match, battlecryTargetRule);
                var requiresBattlecryTargetNow = requiresBattlecryTarget &&
                    (conditionalDrowned
                        ? drownedCanActivate && drownedHasTarget
                        : DemoCardTargeting.RequiresTargetNow(match, battlecryTargetRule));
                var canDeployWithoutBattlecryTarget = requiresBattlecryTarget &&
                    battlecryTargetRule.AllowNoTargetWhenNoLegalTarget && !hasLegalBattlecryTarget;
                if (requiresBattlecryTarget && (battlecryTargetRule.Optional || requiresBattlecryTargetNow))
                {
                    var targeting = _pendingTargetCardId == definition.id;
                    var actionLabel = targeting
                        ? "取消目标选择"
                        : selectedBattlecryTarget != null
                            ? battlecryTargetRule.Optional
                                ? $"取消战吼：{GetCardName(selectedBattlecryTarget.CardId)}"
                                : $"战吼目标：{GetCardName(selectedBattlecryTarget.CardId)}"
                            : hasLegalBattlecryTarget ? battlecryTargetRule.ActionLabel : battlecryTargetRule.Optional ? "没有可移动友军（可直接部署）" : "没有合法目标";
                    var targetButton = CreateSecondaryButton(_inspectorRoot, "BattlecryTarget", new Vector2(0, -138), new Vector2(235, 54), actionLabel, 15);
                    targetButton.interactable = !HasPendingOnlineCommand &&
                        (targeting || selectedBattlecryTarget != null ||
                         (hasLegalBattlecryTarget && match.IsPlayerTurn && hasSelectedHandCard &&
                          (!IsOnlineBoard || _onlineSession.CanIssueCommand)));
                    targetButton.onClick.AddListener(targeting
                        ? (UnityEngine.Events.UnityAction)CancelTargetSelection
                        : selectedBattlecryTarget != null && battlecryTargetRule.Optional
                            ? ClearSelectedDeploymentTarget
                            : CastSelectedCard);
                    ConfigureHoverScale(targetButton.gameObject, 1.04f, 16f);
                    var targetHint = selectedBattlecryTarget == null
                        ? battlecryTargetRule.Optional
                            ? hasLegalBattlecryTarget ? "可直接部署并跳过战吼，或先锁定友军再选择金色中间格。" : "当前没有合法越位路径；仍可把山羊部署到任意空单位格。"
                            : battlecryTargetRule.Owner == DemoTargetOwner.Friendly
                                ? "先锁定发光的着火友军，再选择己方单位格。"
                                : "先锁定敌方战吼目标，再选择己方单位格。"
                        : battlecryTargetRule.Optional
                            ? $"已锁定 {GetCardName(selectedBattlecryTarget.CardId)} · 仅金色中间格可触发越位"
                            : $"已锁定 {GetCardName(selectedBattlecryTarget.CardId)} · 选择发光的{target}部署";
                    CreateText(_inspectorRoot, "DeployHint", new Vector2(0, -194), new Vector2(246, 48), targetHint, 13,
                        selectedBattlecryTarget == null ? Gold : cardAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
                else if (canDeployWithoutBattlecryTarget)
                {
                    CreateText(_inspectorRoot, "DeployHint", new Vector2(0, -146), new Vector2(246, 82),
                        "当前没有着火友军；\n可直接部署，战吼不会触发。",
                        14, Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
                else if (conditionalDrowned)
                {
                    CreateText(_inspectorRoot, "DeployHint", new Vector2(0, -146), new Vector2(246, 82),
                        drownedCanActivate
                            ? "金色地表格可激活战吼，\n但当前没有敌方生物可选。"
                            : "当前没有可相邻的水生友军；\n仍可部署，但不会造成战吼伤害。",
                        14, drownedCanActivate ? Gold : Muted, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
                else if (definition.hasCraftingRecipe)
                {
                    var canInteract = hasSelectedHandCard && match.IsPlayerTurn && match.Phase == DemoTurnPhase.Main &&
                                      (!IsOnlineBoard || _onlineSession.CanIssueCommand);
                    var redstoneSelected = _selectedPaymentMethod == MatchPaymentMethods.Redstone;
                    var craftingSelected = _selectedPaymentMethod == MatchPaymentMethods.Crafting;
                    var redstoneLabel = $"{(redstoneSelected ? "◆ " : string.Empty)}红石 {effectiveCost}";
                    var redstoneButton = CreateSecondaryButton(_inspectorRoot, "PayRedstone", new Vector2(-61, -132), new Vector2(116, 52), redstoneLabel, 14);
                    redstoneButton.interactable = canInteract;
                    redstoneButton.onClick.AddListener(() => SelectPaymentMethod(MatchPaymentMethods.Redstone));
                    ConfigureButtonColors(redstoneButton, redstoneSelected ? Color.Lerp(Panel, Ember, 0.45f) : Panel, Ember);

                    var recipeLabel = string.Join(" + ", definition.craftingRecipe.Select(value => $"{GetCardName(value.cardId)}×{value.count}"));
                    var hasMaterials = DemoDeploymentRules.CanPayWithCrafting(
                        match, definition, out var materialMessage, selectedHandCardInstanceId);
                    var craftingLabel = $"{(craftingSelected ? "◆ " : string.Empty)}合成\n{recipeLabel}";
                    var craftingButton = CreateSecondaryButton(_inspectorRoot, "PayCrafting", new Vector2(61, -132), new Vector2(116, 52), craftingLabel, 11);
                    craftingButton.interactable = canInteract;
                    craftingButton.onClick.AddListener(() => SelectPaymentMethod(MatchPaymentMethods.Crafting));
                    ConfigureButtonColors(craftingButton, craftingSelected ? Color.Lerp(Panel, cardAccent, 0.48f) : Panel, cardAccent);

                    var paymentHint = craftingSelected && !hasMaterials
                        ? ReplaceCardIdsWithNames(materialMessage, definition)
                        : $"{(craftingSelected ? "合成 " + GetCraftingBonusLabel(definition) : $"消耗 {effectiveCost} 红石")} · 选择发光的{target}";
                    CreateText(_inspectorRoot, "DeployHint", new Vector2(0, -194), new Vector2(246, 48), paymentHint, 13,
                        craftingSelected && !hasMaterials ? Danger : cardAccent, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
                else
                {
                    var insufficientRedstone = effectiveCost > match.Energy;
                    var deploymentHint = insufficientRedstone
                        ? $"红石不足\n需要 {effectiveCost} · 当前 {match.Energy}"
                        : isDiscounted
                            ? $"考古触发 · 费用 {definition.cost} → {effectiveCost}\n选择发光的{target}完成部署"
                            : $"选择发光的{target}完成部署";
                    CreateText(_inspectorRoot, "DeployHint", new Vector2(0, -138), new Vector2(246, 64), deploymentHint,
                        insufficientRedstone ? 17 : 15, insufficientRedstone ? Danger : isDiscounted ? Gold : cardAccent,
                        TextAnchor.MiddleCenter, FontStyle.Bold);
                }
            }
            else
            {
                var implemented = definition.effectImplementationStatus == "IMPLEMENTED";
                var automaticExcavation = !definition.manualPlayAllowed;
                var targeting = _pendingTargetCardId == definition.id;
                var requiresTarget = DemoCardTargeting.TryGetRule(definition, out var targetRule);
                var hasLegalTarget = !requiresTarget || DemoCardTargeting.HasLegalTarget(match, targetRule);
                var canPlay = hasSelectedHandCard && match.IsPlayerTurn && match.Phase == DemoTurnPhase.Main && effectiveCost <= match.Energy;
                var multiTargeting = targeting && requiresTarget && targetRule.RequiredTargetCount > 1;
                if (multiTargeting)
                {
                    var confirm = CreateSecondaryButton(_inspectorRoot, "Cast", new Vector2(-61, -132), new Vector2(116, 52),
                        $"确认 {_selectedCardTargetInstanceIds.Count}/{targetRule.RequiredTargetCount}", 15);
                    confirm.interactable = _selectedCardTargetInstanceIds.Count == targetRule.RequiredTargetCount &&
                        (!IsOnlineBoard || _onlineSession.CanIssueCommand);
                    confirm.onClick.AddListener(ConfirmMultiTargetCard);
                    ConfigureHoverScale(confirm.gameObject, 1.04f, 16f);
                    var cancel = CreateSecondaryButton(_inspectorRoot, "CancelTarget", new Vector2(61, -132), new Vector2(116, 52), "取消选择", 15);
                    cancel.interactable = !HasPendingOnlineCommand;
                    cancel.onClick.AddListener(CancelTargetSelection);
                    CreateText(_inspectorRoot, "TargetProgress", new Vector2(0, -173), new Vector2(246, 28),
                        "绿色可选 · 金色已选 · 再次点击可撤销", 12, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
                }
                else
                {
                    var actionLabel = automaticExcavation
                        ? "出土效果 · 自动结算"
                        : !implemented ? "效果尚未接入" : targeting ? "取消目标选择" : !hasLegalTarget ? "没有合法目标" : requiresTarget ? targetRule.ActionLabel : definition.cardType == "EQUIPMENT" ? "装备武器" : "释放卡牌";
                    var cast = CreateSecondaryButton(_inspectorRoot, "Cast", new Vector2(0, -138), new Vector2(235, 60), actionLabel, 17);
                    cast.interactable = !automaticExcavation && !HasPendingOnlineCommand &&
                        (targeting || (implemented && hasLegalTarget && canPlay && (!IsOnlineBoard || _onlineSession.CanIssueCommand)));
                    cast.onClick.AddListener(targeting ? (UnityEngine.Events.UnityAction)CancelTargetSelection : CastSelectedCard);
                    ConfigureHoverScale(cast.gameObject, 1.04f, 16f);
                }
            }
            if (pendingDeployEffect)
            {
                CreateText(_inspectorRoot, "PendingEffectNotice", new Vector2(0, -244), new Vector2(250, 52),
                    "仅基础属性可用 · 卡牌效果尚未接入", 12, Gold, TextAnchor.MiddleCenter, FontStyle.Bold);
            }
            if (_showRuleDiagnostics)
            {
                var excavationToken = !definition.manualPlayAllowed;
                var implementationLabel = excavationToken
                    ? "已接入（出土触发）"
                    : definition.effectImplementationStatus == "IMPLEMENTED" ? "已接入" : definition.effectImplementationStatus == "NONE" ? "无额外效果" : "待接入";
                var targetedDeployment = deployType && DemoCardTargeting.TryGetRule(definition, out _);
                var multiTargetInspector = _pendingTargetCardId == definition.id &&
                    DemoCardTargeting.TryGetRule(definition, out var inspectorTargetRule) && inspectorTargetRule.RequiredTargetCount > 1;
                var implementationY = pendingDeployEffect ? -300 :
                    deployType && (definition.hasCraftingRecipe || targetedDeployment) ? -256 : multiTargetInspector ? -240 : -210;
                var implementationText = isDiscounted
                    ? $"考古联动：本回合费用 -1（{definition.cost} → {effectiveCost}）\n稳定效果槽：{string.Join(", ", definition.effectIds ?? Array.Empty<string>())}"
                    : definition.hasCraftingRecipe
                    ? $"配方：已接入 · {definition.recipeId}\n卡牌效果：{implementationLabel}\n{string.Join(", ", definition.effectIds ?? Array.Empty<string>())}"
                    : excavationToken
                        ? $"规则状态：{implementationLabel}\n不能从手牌主动释放 · {string.Join(", ", definition.effectIds ?? Array.Empty<string>())}"
                        : $"规则状态：{implementationLabel}\n稳定效果槽：{string.Join(", ", definition.effectIds ?? Array.Empty<string>())}";
                CreateText(_inspectorRoot, "Implementation", new Vector2(0, implementationY), new Vector2(250, 68), implementationText,
                    definition.hasCraftingRecipe ? 11 : 12, Muted, TextAnchor.MiddleCenter, FontStyle.Normal);
            }
        }

        private DemoDeploymentPreview EvaluateSelectedDeployment(DemoSlotKind kind, int index)
        {
            if (string.IsNullOrEmpty(_selectedCardId) || !_registry.TryGetDefinition(_selectedCardId, out var definition))
                return new DemoDeploymentPreview(false, 1, "请先选择一张战场部署牌。");
            var preview = DemoDeploymentRules.Evaluate(MatchView, definition, kind, index, _selectedPaymentMethod,
                GetSelectedHandCardInstanceId());
            if (!preview.IsLegal || !IsGoat(definition)) return preview;
            var selectedTarget = FindSelectedDeploymentTarget();
            if (selectedTarget == null) return preview;
            var destination = index * 2 - selectedTarget.SlotIndex;
            if (kind != DemoSlotKind.Unit || Mathf.Abs(selectedTarget.SlotIndex - index) != 1 || destination < 0 ||
                destination >= MatchView.UnitSlots.Length || !string.IsNullOrEmpty(MatchView.UnitSlots[destination]))
                return new DemoDeploymentPreview(false, 1, "山羊必须部署在目标旁，并让目标另一侧的相邻格保持为空。");
            return new DemoDeploymentPreview(true, 1,
                $"越位路径：{selectedTarget.SlotIndex + 1} → {destination + 1}；山羊落在中间的 {index + 1} 号格。");
        }

        private bool IsPreviewingDeployment(bool player, out int occupiedSlots)
        {
            occupiedSlots = 1;
            if (MatchView.IsFinished || !MatchView.IsPlayerTurn || !player || !string.IsNullOrEmpty(_pendingTargetCardId) || MatchView.Phase != DemoTurnPhase.Main ||
                string.IsNullOrEmpty(_selectedCardId) || !_registry.TryGetDefinition(_selectedCardId, out var definition)) return false;
            if (definition.cardType != "UNIT" && definition.cardType != "BUILDING" && definition.cardType != "STRUCTURE") return false;
            if (DemoCardTargeting.TryGetRule(definition, out var targetRule) && !targetRule.Optional &&
                FindSelectedDeploymentTarget() == null)
            {
                var targetRequiredNow = IsDrowned(definition)
                    ? HasPotentialDrownedBattlecrySlot() && DemoCardTargeting.HasLegalTarget(MatchView, targetRule)
                    : DemoCardTargeting.RequiresTargetNow(MatchView, targetRule);
                if (targetRequiredNow) return false;
            }
            occupiedSlots = definition.cardType == "UNIT" ? 1 : Mathf.Max(1, definition.buildingSlots);
            return true;
        }

        private void OnSlotHovered(bool player, DemoSlotKind kind, int index, bool hovered)
        {
            if (HasPendingOnlineCommand)
            {
                var pendingDeployment = IsPreviewingDeployment(player, out var pendingOccupiedSlots);
                _battlefield.SetSlotRangeHovered(player, kind, index, pendingDeployment ? pendingOccupiedSlots : 1, false, false);
                return;
            }
            if (MatchView.IsFinished)
            {
                _battlefield.SetSlotRangeHovered(player, kind, index, 1, false, false);
                return;
            }
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
                else if (!rejected && _registry.TryGetDefinition(_selectedCardId, out var wolfHoverDefinition) &&
                    IsTamedWolf(wolfHoverDefinition))
                {
                    ShowStatus(TamedWolfBattlecryActivatesAt(index)
                        ? "金色地表：此位置相邻另一个己方动物，驯服的狼落位后永久获得 +1 生命。"
                        : "普通地表：可以部署驯服的狼，但没有相邻动物，忠诚战吼不会触发。", false);
                }
                else if (!rejected && _registry.TryGetDefinition(_selectedCardId, out var goatHoverDefinition) &&
                    IsGoat(goatHoverDefinition) && FindSelectedDeploymentTarget() != null)
                {
                    ShowStatus(preview.Message, false);
                }
                else ShowStatus(preview.Message, rejected);
            }
        }

        private void OnSlotPressed(bool player, DemoSlotKind kind, int index, bool pressed)
        {
            if (HasPendingOnlineCommand)
            {
                var pendingDeployment = IsPreviewingDeployment(player, out var pendingOccupiedSlots);
                _battlefield.SetSlotRangePressed(player, kind, index, pendingDeployment ? pendingOccupiedSlots : 1, false, false);
                return;
            }
            if (MatchView.IsFinished)
            {
                _battlefield.SetSlotRangePressed(player, kind, index, 1, false, false);
                return;
            }
            if (MatchView.PendingChoice != null) return;
            var preview = EvaluateSelectedDeployment(kind, index);
            var isDeployment = IsPreviewingDeployment(player, out var occupiedSlots);
            _battlefield.SetSlotRangePressed(player, kind, index, isDeployment ? occupiedSlots : 1, pressed, isDeployment && !preview.IsLegal);
        }

        private void SelectCard(string cardId)
        {
            if (HasPendingOnlineCommand || MatchView.IsFinished || MatchView.PendingChoice != null) return;
            _pendingTargetCardId = null;
            _selectedCardTargetInstanceIds.Clear();
            _selectedDeploymentTargetInstanceId = null;
            _selectedCardId = cardId;
            _selectedHandCardInstanceId = MatchView.HandCards.FirstOrDefault(value => value != null && value.cardId == cardId)?.handCardInstanceId;
            _selectedPaymentMethod = MatchPaymentMethods.Redstone;
            RefreshAll();
            if (_registry.TryGetText(cardId, out var text)) ShowStatus($"已选择：{text.name} · 按 Esc 或右键取消", false);
        }

        private void SelectHandCard(string cardId, string handCardInstanceId)
        {
            SelectHandCardInternal(cardId, handCardInstanceId, true);
        }

        private void SelectHandCardInternal(string cardId, string handCardInstanceId, bool refreshHand)
        {
            if (IsReadOnlyOverlayOpen) return;
            if (HasPendingOnlineCommand || MatchView.IsFinished || MatchView.PendingChoice != null) return;
            var sameHandCard = _selectedCardId == cardId && _selectedHandCardInstanceId == handCardInstanceId;
            _pendingTargetCardId = null;
            _selectedCardTargetInstanceIds.Clear();
            _selectedDeploymentTargetInstanceId = null;
            _selectedCardId = cardId;
            _selectedHandCardInstanceId = handCardInstanceId;
            if (!sameHandCard) _selectedPaymentMethod = MatchPaymentMethods.Redstone;
            RefreshAllInternal(refreshHand);
            if (_registry.TryGetText(cardId, out var text)) ShowStatus($"已选择：{text.name} · 按 Esc 或右键取消", false);
        }

        private void OnHandCardDragMoved(Vector2 screenPosition)
        {
            var pointer = GetComponent<DemoBattlefieldPointerController>();
            if (pointer == null) return;
            var blockedByUi = pointer.IsPointerOverUiAtPosition(screenPosition);
            pointer.ProcessPointerFrame(screenPosition, blockedByUi, false, false);
        }

        private void OnHandCardDragDropped(Vector2 screenPosition)
        {
            if (IsReadOnlyOverlayOpen) return;
            if (HasPendingOnlineCommand) return;
            var pointer = GetComponent<DemoBattlefieldPointerController>();
            if (pointer == null)
            {
                RefreshAll();
                return;
            }

            var blockedByUi = pointer.IsPointerOverUiAtPosition(screenPosition);
            var target = pointer.ProcessPointerFrame(screenPosition, blockedByUi, true, false);
            if (target != null)
            {
                pointer.ProcessPointerFrame(screenPosition, blockedByUi, false, true);
                return;
            }

            pointer.ProcessPointerFrame(screenPosition, blockedByUi, false, true);
            RefreshAll();
        }

        private void SelectFirstHandCard()
        {
            var first = MatchView?.HandCards?.FirstOrDefault(value => value != null);
            _selectedCardId = first?.cardId;
            _selectedHandCardInstanceId = first?.handCardInstanceId;
        }

        private string GetSelectedHandCardInstanceId()
        {
            return ResolveHandCardInstanceId(MatchView.HandCards, _selectedCardId, _selectedHandCardInstanceId);
        }

        private static string ResolveHandCardInstanceId(
            IReadOnlyList<HandCardStateDto> handCards,
            string cardId,
            string selectedHandCardInstanceId)
        {
            if (handCards == null || string.IsNullOrEmpty(cardId)) return string.Empty;

            if (!string.IsNullOrEmpty(selectedHandCardInstanceId))
            {
                var selectedHandCard = handCards.FirstOrDefault(value => value != null &&
                    value.handCardInstanceId == selectedHandCardInstanceId && value.cardId == cardId);
                return selectedHandCard?.handCardInstanceId ?? string.Empty;
            }

            // Scenario helpers sometimes select by card definition only. Resolve that
            // legacy selection once; never substitute a different copy for a stale ID.
            return handCards.FirstOrDefault(value => value != null && value.cardId == cardId)?.handCardInstanceId ?? string.Empty;
        }

        private async void OnSlotClicked(bool player, DemoSlotKind kind, int index)
        {
            if (IsReadOnlyOverlayOpen) return;
            if (HasPendingOnlineCommand) return;
            if (MatchView.IsFinished)
            {
                ShowStatus("对局已经结束，所有战场操作均已锁定。", true);
                return;
            }
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
                else if (choice.kind == "HEAL_UNIT" && MatchView.IsChoiceOwner && player && kind == DemoSlotKind.Unit)
                {
                    var battlefieldObject = MatchView.GetObject(true, kind, index);
                    var option = (choice.options ?? Array.Empty<PendingChoiceOptionDto>()).FirstOrDefault(value =>
                        value != null && value.selectable && value.slotIndex == index && value.cardId == battlefieldObject?.CardId);
                    if (option != null) ResolveHealingChoice(option.optionIndex);
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
            if (DemoCardTargeting.TryGetRule(definition, out var deploymentTargetRule) && !deploymentTargetRule.Optional &&
                FindSelectedDeploymentTarget() == null &&
                (IsDrowned(definition)
                    ? DrownedBattlecryActivatesAt(index) && DemoCardTargeting.HasLegalTarget(MatchView, deploymentTargetRule)
                    : DemoCardTargeting.RequiresTargetNow(MatchView, deploymentTargetRule)))
            {
                CastSelectedCard();
                return;
            }
            // Clicks and hand-card drops must enforce the same target/path checks as
            // the in-world hover preview, before reaching either rules backend.
            var deploymentPreview = EvaluateSelectedDeployment(kind, index);
            if (!deploymentPreview.IsLegal)
            {
                ShowStatus(deploymentPreview.Message, true);
                RefreshAll();
                return;
            }
            if (IsOnlineBoard)
            {
                var onlineResult = await SendOnline(() => _onlineSession.DeployAsync(
                    cardId: _selectedCardId, kind: kind, slotIndex: index,
                    handCardInstanceId: GetSelectedHandCardInstanceId(),
                    paymentMethod: _selectedPaymentMethod,
                    targetType: deploymentTargetRule?.TargetType ?? string.Empty,
                    targetInstanceId: _selectedDeploymentTargetInstanceId ?? string.Empty));
                if (onlineResult?.Outcome == MatchCommandOutcome.Accepted)
                {
                    SelectFirstHandCard();
                    _selectedPaymentMethod = MatchPaymentMethods.Redstone;
                    _selectedDeploymentTargetInstanceId = null;
                }
                RefreshAll();
                return;
            }
            var crafted = _selectedPaymentMethod == MatchPaymentMethods.Crafting;
            var deploymentTarget = FindSelectedDeploymentTarget();
            var command = _match.CreateDeployCommand(
                _selectedCardId, kind, index, _selectedPaymentMethod,
                deploymentTargetRule?.TargetType ?? string.Empty,
                _selectedDeploymentTargetInstanceId ?? string.Empty,
                GetSelectedHandCardInstanceId());
            var result = _match.ApplyDeploy(definition, command);
            if (result.Accepted)
            {
                SelectFirstHandCard();
                _selectedPaymentMethod = MatchPaymentMethods.Redstone;
                _selectedDeploymentTargetInstanceId = null;
                if (crafted) StartCoroutine(ShowTurnBanner("合成完成", Cyan));
                if (deploymentTarget != null && IsGoat(definition))
                {
                    StartCoroutine(PulseBattlefieldObject(deploymentTarget.InstanceId));
                    StartCoroutine(ShowTurnBanner("山羊越位", Cyan));
                }
                else if (deploymentTarget != null && IsStrider(definition))
                {
                    StartCoroutine(PulseBattlefieldObject(deploymentTarget.InstanceId));
                    StartCoroutine(ShowTurnBanner("净火疗愈", Gold));
                }
            }
            ShowLocalCommandResultStatus(result);
            RefreshAll();
        }

        private void HandleCombatSlotClick(bool player, DemoSlotKind kind, int index)
        {
            var match = MatchView;
            if (match.IsFinished) return;
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
                ShowStatus($"已选择攻击者：{GetCardName(attacker.CardId)}（{attacker.Attack}/{attacker.Health}）· 按 Esc 或右键取消", false);
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
            if (IsReadOnlyOverlayOpen) return;
            if (HasPendingOnlineCommand) return;
            if (MatchView.IsFinished) return;
            if (MatchView.Phase != DemoTurnPhase.Combat) return;
            if (!MatchView.CanAttackWithHero(out var message))
            {
                ShowStatus(message, true);
                return;
            }
            ClearSelectedAttackerHighlight();
            _selectedAttackerInstanceId = MatchAttackerIds.Hero;
            ShowStatus($"已选择英雄：{GetCardName(MatchView.PlayerEquipment.CardId)}（{MatchView.PlayerEquipment.Attack} 攻击）· 按 Esc 或右键取消", false);
            RefreshAll();
        }

        private void AttackOpponentHero()
        {
            if (HasPendingOnlineCommand) return;
            if (MatchView.IsFinished) return;
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
            if (HasPendingOnlineCommand) return;
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
            var attackerIsHero = string.Equals(attackerInstanceId, MatchAttackerIds.Hero, StringComparison.Ordinal);
            var targetIsHero = string.Equals(targetType, "HERO", StringComparison.OrdinalIgnoreCase);
            var targetBefore = targetIsHero ? null : FindBattlefieldObject(targetInstanceId);
            var attackPower = attackerIsHero ? (_match.PlayerEquipment?.Attack ?? 0) : (attacker?.Attack ?? 0);
            var attackDamage = targetBefore == null ? 0 : Mathf.Max(0, attackPower);
            var retaliationDamage = attackerIsHero || attacker == null || targetBefore?.SlotKind != DemoSlotKind.Unit
                ? 0
                : Mathf.Max(0, targetBefore.Attack);
            var playerLifeBefore = _match.PlayerLife;
            var playerArmorBefore = _match.PlayerArmor;
            var opponentLifeBefore = _match.OpponentLife;
            var opponentArmorBefore = _match.OpponentArmor;
            var command = _match.CreateAttackCommand(attackerInstanceId, targetType, targetInstanceId);
            var result = _match.ApplyAttack(command);
            if (result.Accepted)
            {
                if (attacker != null) _battlefield.SetSlotPressed(true, DemoSlotKind.Unit, attacker.SlotIndex, false);
                _selectedAttackerInstanceId = null;
            }
            ShowLocalCommandResultStatus(result);
            RefreshAll();
            if (result.Accepted)
            {
                StartCoroutine(PresentLocalAttackFeedback(
                    attacker, targetBefore, attackerIsHero, targetIsHero,
                    playerLifeBefore, playerArmorBefore, opponentLifeBefore, opponentArmorBefore,
                    attackDamage, retaliationDamage));
                if (TryShowLocalMatchOutcome()) return;
                var summoned = _match.PlayerBattlefield.Concat(_match.OpponentBattlefield)
                    .FirstOrDefault(value => value != null && !knownInstanceIds.Contains(value.InstanceId));
                if (summoned != null) StartCoroutine(PulseBattlefieldObject(summoned.InstanceId));
            }
        }

        private IEnumerator PresentLocalAttackFeedback(
            DemoBattlefieldObject attackerBefore,
            DemoBattlefieldObject targetBefore,
            bool attackerIsHero,
            bool targetIsHero,
            int playerLifeBefore,
            int playerArmorBefore,
            int opponentLifeBefore,
            int opponentArmorBefore,
            int attackDamage,
            int retaliationDamage)
        {
            if (attackerBefore != null && targetBefore != null)
                yield return AnimateAttackLunge(attackerBefore.InstanceId, targetBefore.Player,
                    targetBefore.SlotKind, targetBefore.SlotIndex);

            if (_battlefield != null && targetBefore != null && attackDamage > 0)
                _battlefield.ShowCombatDamageNumber(targetBefore.Player, targetBefore.SlotKind,
                    targetBefore.SlotIndex, targetBefore.OccupiedSlots, attackDamage);
            if (_battlefield != null && attackerBefore != null && retaliationDamage > 0)
                _battlefield.ShowCombatDamageNumber(attackerBefore.Player, attackerBefore.SlotKind,
                    attackerBefore.SlotIndex, attackerBefore.OccupiedSlots, retaliationDamage);

            if (attackerBefore != null)
            {
                var attackerAfter = FindBattlefieldObject(attackerBefore.InstanceId);
                if (attackerAfter == null)
                    yield return PulseBattlefieldSlot(attackerBefore.Player, attackerBefore.SlotKind,
                        attackerBefore.SlotIndex, attackerBefore.OccupiedSlots, Danger);
                else yield return PulseBattlefieldObject(attackerAfter.InstanceId);
            }

            if (targetBefore != null)
            {
                var targetAfter = FindBattlefieldObject(targetBefore.InstanceId);
                if (targetAfter == null)
                    yield return PulseBattlefieldSlot(targetBefore.Player, targetBefore.SlotKind,
                        targetBefore.SlotIndex, targetBefore.OccupiedSlots, Danger);
                else yield return PulseBattlefieldObject(targetAfter.InstanceId);
            }

            if (targetIsHero && (_match.OpponentLife < opponentLifeBefore || _match.OpponentArmor < opponentArmorBefore))
                yield return PulseOpponentHud(Danger);
            if (attackerIsHero && (_match.PlayerLife < playerLifeBefore || _match.PlayerArmor < playerArmorBefore))
                yield return PulsePlayerHud(Danger);
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

        private DemoBattlefieldObject FindBattlefieldObject(string instanceId) =>
            string.IsNullOrEmpty(instanceId)
                ? null
                : MatchView.PlayerBattlefield.Concat(MatchView.OpponentBattlefield)
                    .FirstOrDefault(value => value != null && value.InstanceId == instanceId);

        private static string FormatAttackResolvedStatus(
            MatchEventPayloadDto payload,
            string viewerId,
            string attackerName,
            string targetName)
        {
            if (payload == null) return string.Empty;
            var attackerSide = payload.attackerPlayerId == viewerId ? "己方" : "敌方";
            var targetSide = payload.targetPlayerId == viewerId ? "己方" : "敌方";
            var targetKind = string.Equals(payload.targetType, "HERO", StringComparison.OrdinalIgnoreCase)
                ? "英雄"
                : string.Equals(payload.targetType, "BUILDING", StringComparison.OrdinalIgnoreCase) ? "建筑" : "单位";
            var retaliation = payload.damageToAttacker > 0 ? $"；反击造成 {payload.damageToAttacker} 点伤害" : string.Empty;
            return $"{attackerSide}{(attackerName ?? "单位")}攻击{targetSide}{targetKind}{(targetName == targetKind ? string.Empty : targetName ?? string.Empty)}，造成 {Math.Max(0, payload.damageToTarget)} 点伤害{retaliation}。";
        }

        private static string FormatObjectReturnedStatus(
            MatchEventPayloadDto payload,
            string viewerId,
            string cardName,
            string sourceCardName)
        {
            if (payload == null) return string.Empty;
            var controllerSide = payload.controllerPlayerId == viewerId ? "己方" : "敌方";
            var isOwner = !string.IsNullOrEmpty(viewerId) &&
                !string.IsNullOrEmpty(payload.ownerPlayerId) && payload.ownerPlayerId == viewerId;
            var ownerHand = isOwner ? "你的" : "对手的";
            var sourceName = string.IsNullOrEmpty(sourceCardName) ? "效果" : sourceCardName;
            var returnedName = string.IsNullOrEmpty(cardName) ? "单位" : cardName;
            var destination = payload.destination == "DISCARD"
                ? $"因手牌已满进入{ownerHand}弃牌堆"
                : $"返回{ownerHand}手牌";
            var discount = isOwner && payload.destination == "HAND" && payload.costModifier < 0
                ? $"；本次费用减少 {Math.Abs(payload.costModifier)}（{(payload.expiresAtEndOfTurnPlayerId == viewerId ? "己方" : "敌方")}回合结束时恢复）"
                : string.Empty;
            return $"{sourceName}：{controllerSide}{returnedName}从战场{destination}{discount}。";
        }

        private static string FormatHandCardCostModifierExpiredStatus(
            MatchEventPayloadDto payload,
            string viewerId,
            string cardName)
        {
            if (payload == null || string.IsNullOrEmpty(viewerId) ||
                string.IsNullOrEmpty(payload.playerId) || payload.playerId != viewerId || string.IsNullOrEmpty(payload.cardId))
                return string.Empty;
            var name = string.IsNullOrEmpty(cardName) ? "手牌" : cardName;
            var reduction = Math.Abs(Math.Min(0, payload.expiredCostModifier));
            return $"{name}的回手减费（-{reduction}）已到期，费用恢复为 {payload.effectiveCost}。";
        }

        private string FormatEquipment(DemoEquipment equipment) => equipment == null
            ? "装备槽 · 未装备"
            : $"{GetCardName(equipment.CardId)}\n攻击 {equipment.Attack} · 耐久 {equipment.Durability}/{equipment.MaxDurability}";

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
            if (HasPendingOnlineCommand || MatchView.IsFinished || MatchView.PendingChoice != null) return;
            var handCardInstanceId = GetSelectedHandCardInstanceId();
            if (string.IsNullOrEmpty(handCardInstanceId))
            {
                ShowStatus("所选手牌实例已不存在，请重新选择卡牌。", true);
                RefreshAll();
                return;
            }
            _selectedPaymentMethod = paymentMethod;
            if (_registry.TryGetDefinition(_selectedCardId, out var definition) && paymentMethod == MatchPaymentMethods.Crafting)
            {
                var ready = DemoDeploymentRules.CanPayWithCrafting(
                    MatchView, definition, out var message, handCardInstanceId);
                ShowStatus(ready ? "合成支付已选择：材料充足。" : ReplaceCardIdsWithNames(message, definition), !ready);
            }
            else ShowStatus("红石支付已选择。", false);
            RefreshAll();
        }

        private async void CastSelectedCard()
        {
            if (HasPendingOnlineCommand || MatchView.IsFinished || MatchView.PendingChoice != null) return;
            if (string.IsNullOrEmpty(_selectedCardId) || !_registry.TryGetDefinition(_selectedCardId, out var definition)) return;
            if (DemoCardTargeting.TryGetRule(definition, out var targetRule))
            {
                if (!DemoCardTargeting.HasLegalTarget(MatchView, targetRule))
                {
                    ShowStatus(targetRule.MissingTargetMessage, true);
                    return;
                }
                _pendingTargetCardId = definition.id;
                _selectedCardTargetInstanceIds.Clear();
                ShowStatus(targetRule.SelectionPrompt, false);
                RefreshAll();
                return;
            }
            if (IsOnlineBoard)
            {
                var onlineResult = await SendOnline(() => _onlineSession.PlayCardAsync(definition.id,
                    GetSelectedHandCardInstanceId()));
                if (onlineResult?.Outcome == MatchCommandOutcome.Accepted) SelectFirstHandCard();
                RefreshAll();
                return;
            }
            var lifeBefore = MatchView.PlayerLife;
            var armorBefore = MatchView.PlayerArmor;
            var success = _match.TryCast(definition, out var message, GetSelectedHandCardInstanceId());
            if (success && _match.LastDrawResult != null && !string.IsNullOrEmpty(_match.LastDrawResult.CardId))
                message = message.Replace(_match.LastDrawResult.CardId, GetCardName(_match.LastDrawResult.CardId));
            if (success) SelectFirstHandCard();
            ShowStatus(message, !success);
            RefreshAll();
            if (success)
            {
                if (TryShowLocalMatchOutcome()) return;
                var color = MatchView.PlayerArmor > armorBefore ? Cyan : MatchView.PlayerLife < lifeBefore ? Danger : Hex("#B8E5A9");
                StartCoroutine(PulsePlayerHud(color));
            }
        }

        private async void ResolveTargetedCard(bool player, DemoSlotKind kind, int index)
        {
            if (HasPendingOnlineCommand) return;
            if (MatchView.IsFinished) return;
            if (!_registry.TryGetDefinition(_pendingTargetCardId, out var definition) ||
                !DemoCardTargeting.TryGetRule(definition, out var targetRule))
            {
                CancelTargetSelection();
                return;
            }
            var target = MatchView.GetObject(player, kind, index);
            if (!DemoCardTargeting.IsLegalTarget(MatchView, targetRule, player, kind, target))
            {
                ShowStatus(targetRule.SelectionPrompt, true);
                return;
            }
            if (targetRule.RequiredTargetCount > 1)
            {
                if (_selectedCardTargetInstanceIds.Contains(target.InstanceId))
                    _selectedCardTargetInstanceIds.Remove(target.InstanceId);
                else if (_selectedCardTargetInstanceIds.Count < targetRule.RequiredTargetCount)
                    _selectedCardTargetInstanceIds.Add(target.InstanceId);
                ShowStatus($"繁殖目标：已选择 {_selectedCardTargetInstanceIds.Count}/{targetRule.RequiredTargetCount}；金色为已选目标。", false);
                RefreshAll();
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
                var onlineResult = await SendOnline(() => _onlineSession.PlayCardAsync(definition.id,
                    GetSelectedHandCardInstanceId(), targetRule.TargetType, target.InstanceId));
                if (onlineResult?.Outcome == MatchCommandOutcome.Accepted)
                {
                    _pendingTargetCardId = null;
                    SelectFirstHandCard();
                }
                RefreshAll();
                return;
            }
            var command = _match.CreatePlayCardCommand(definition.id, targetRule.TargetType, target.InstanceId,
                handCardInstanceId: GetSelectedHandCardInstanceId());
            var result = _match.ApplyPlayCard(definition, command);
            if (result.Accepted)
            {
                _pendingTargetCardId = null;
                SelectFirstHandCard();
            }
            var message = result.Message.Replace(target.CardId, GetCardName(target.CardId));
            ShowLocalCommandResultStatus(result, message);
            RefreshAll();
            if (result.Accepted)
            {
                if (definition.id == "tk_001") StartCoroutine(PulseBattlefieldObject(target.InstanceId));
                TryShowLocalMatchOutcome();
            }
        }

        private async void ConfirmMultiTargetCard()
        {
            if (HasPendingOnlineCommand) return;
            if (MatchView.IsFinished) return;
            if (!_registry.TryGetDefinition(_pendingTargetCardId, out var definition) ||
                !DemoCardTargeting.TryGetRule(definition, out var targetRule) || targetRule.RequiredTargetCount <= 1)
            {
                CancelTargetSelection();
                return;
            }
            var targets = _selectedCardTargetInstanceIds
                .Select(instanceId => MatchView.PlayerBattlefield.Concat(MatchView.OpponentBattlefield)
                    .FirstOrDefault(value => value != null && value.InstanceId == instanceId))
                .ToArray();
            if (targets.Length != targetRule.RequiredTargetCount || targets.Any(target => target == null) ||
                targets.Distinct().Count() != targetRule.RequiredTargetCount ||
                targets.Any(target => !DemoCardTargeting.IsLegalTarget(MatchView, targetRule, target.Player, target.SlotKind, target)))
            {
                ShowStatus(targetRule.MissingTargetMessage, true);
                return;
            }
            var targetInstanceIds = targets.OrderBy(target => target.SlotIndex)
                .ThenBy(target => target.InstanceId, StringComparer.Ordinal)
                .Select(target => target.InstanceId).ToArray();
            if (IsOnlineBoard)
            {
                var onlineResult = await SendOnline(() =>
                    _onlineSession.PlayCardAsync(definition.id, GetSelectedHandCardInstanceId(), targetRule.TargetType, "", targetInstanceIds));
                if (onlineResult?.Outcome == MatchCommandOutcome.Accepted)
                {
                    _pendingTargetCardId = null;
                    _selectedCardTargetInstanceIds.Clear();
                    SelectFirstHandCard();
                }
                RefreshAll();
                return;
            }
            var command = _match.CreatePlayCardCommand(definition.id, targetRule.TargetType, "", targetInstanceIds,
                GetSelectedHandCardInstanceId());
            var result = _match.ApplyPlayCard(definition, command);
            if (result.Accepted)
            {
                _pendingTargetCardId = null;
                _selectedCardTargetInstanceIds.Clear();
                SelectFirstHandCard();
            }
            var displayMessage = result.Message;
            foreach (var target in targets)
                displayMessage = displayMessage.Replace(target.CardId, GetCardName(target.CardId));
            ShowLocalCommandResultStatus(result, displayMessage);
            RefreshAll();
            if (result.Accepted) TryShowLocalMatchOutcome();
        }

        private void CancelTargetSelection()
        {
            if (HasPendingOnlineCommand || string.IsNullOrEmpty(_pendingTargetCardId)) return;
            _pendingTargetCardId = null;
            _selectedCardTargetInstanceIds.Clear();
            ShowStatus("已取消目标选择。", false);
            RefreshAll();
        }

        private bool CancelCurrentInteraction()
        {
            if (HasPendingOnlineCommand) return false;

            if (!string.IsNullOrEmpty(_pendingTargetCardId))
            {
                CancelTargetSelection();
                return true;
            }

            if (!string.IsNullOrEmpty(_selectedDeploymentTargetInstanceId))
            {
                _selectedDeploymentTargetInstanceId = null;
                ShowStatus("已取消战吼目标选择。", false);
                RefreshAll();
                return true;
            }

            if (!string.IsNullOrEmpty(_selectedAttackerInstanceId))
            {
                ClearSelectedAttackerHighlight();
                _selectedAttackerInstanceId = null;
                ShowStatus("已取消攻击者选择。", false);
                RefreshAll();
                return true;
            }

            if (!string.IsNullOrEmpty(_selectedCardId) || !string.IsNullOrEmpty(_selectedHandCardInstanceId))
            {
                _selectedCardId = null;
                _selectedHandCardInstanceId = null;
                _selectedPaymentMethod = MatchPaymentMethods.Redstone;
                ShowStatus("已取消手牌选择。", false);
                RefreshAll();
                return true;
            }

            return false;
        }

        private void ClearSelectedDeploymentTarget()
        {
            if (HasPendingOnlineCommand) return;
            _selectedDeploymentTargetInstanceId = null;
            ShowStatus("已跳过可选战吼；现在可以把山羊部署到任意空单位格。", false);
            RefreshAll();
        }

        private bool IsValidPendingCardTarget(bool player, DemoSlotKind kind, DemoBattlefieldObject target)
        {
            if (!_registry.TryGetDefinition(_pendingTargetCardId, out var definition) ||
                !DemoCardTargeting.TryGetRule(definition, out var targetRule)) return false;
            return DemoCardTargeting.IsLegalTarget(MatchView, targetRule, player, kind, target);
        }

        private DemoBattlefieldObject FindSelectedDeploymentTarget()
        {
            if (string.IsNullOrEmpty(_selectedDeploymentTargetInstanceId)) return null;
            return MatchView.PlayerBattlefield.Concat(MatchView.OpponentBattlefield)
                .FirstOrDefault(value => value != null && value.InstanceId == _selectedDeploymentTargetInstanceId && value.Health > 0);
        }

        private static bool IsDrowned(CardDefinitionEntry definition) =>
            definition?.effectIds?.Contains("effect.or_003.01") == true;

        private static bool IsTamedWolf(CardDefinitionEntry definition) =>
            definition?.effectIds?.Contains("effect.pf_003.01") == true;

        private static bool IsGoat(CardDefinitionEntry definition) =>
            definition?.effectIds?.Contains("effect.si_004.01") == true;

        private static bool IsStrider(CardDefinitionEntry definition) =>
            definition?.effectIds?.Contains("effect.nt_004.01") == true;

        private bool TamedWolfBattlecryActivatesAt(int slotIndex)
        {
            return MatchView.PlayerBattlefield.Any(value => value != null && value.Health > 0 &&
                value.SlotKind == DemoSlotKind.Unit && Mathf.Abs(value.SlotIndex - slotIndex) == 1 &&
                _registry.TryGetDefinition(value.CardId, out var adjacentDefinition) &&
                (adjacentDefinition.tags ?? Array.Empty<string>()).Contains("animal"));
        }

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
                ShowOnlineException(exception, connecting: false);
                Debug.LogWarning($"Online command failed ({exception.GetType().Name}).", this);
                RefreshAll();
                return null;
            }
        }

        private async void RunOnlineProbe(string reportPath, string capturePath, bool performAction)
        {
            try
            {
                if (HasCommandLineFlag("-onlineExpectedCompatibilityFailure"))
                {
                    await AuditExpectedCompatibilityFailure(reportPath, capturePath);
                    Application.Quit(0);
                    return;
                }
                var deadline = Time.realtimeSinceStartup + (HasCommandLineFlag("-onlineDeploymentRejections") ? 240f : 90f);
                while ((!IsOnlineBoard || _onlineGateway.CurrentStatus.Phase != MatchConnectionPhase.Ready) && Time.realtimeSinceStartup < deadline)
                    await Task.Yield();
                if (!IsOnlineBoard || _onlineGateway.CurrentStatus.Phase != MatchConnectionPhase.Ready)
                    throw new TimeoutException("Unity client did not receive an authoritative snapshot within 90 seconds.");

                var expectedArena = GetCommandLineValue("-onlineExpectedArena");
                if (!string.IsNullOrEmpty(expectedArena) && MatchView.ArenaId != expectedArena)
                    throw new InvalidOperationException("Server arena does not match the requested audit expectation.");
                if (!VerifyOnlineArenaGeometry()) throw new InvalidOperationException("Authoritative arena UI/3D topology is inconsistent.");
                if (!VerifyOnlineStatusTextLayout()) throw new InvalidOperationException("Online status text overflowed its material column.");

                if (HasCommandLineFlag("-onlineGoatProbe"))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    using (var probe = new DemoOnlineGoatProbe(_onlineGateway,
                        GameCompositionRoot.Instance.MatchStateStore, _onlineSession, _registry))
                    {
                        var report = await probe.RunAsync(deadline, GetCommandLineValue("-onlineGoatBarrier"),
                            DeployOnlineThroughUi, DeployOnlineGoatThroughUi, InspectOnlineGoatWorld, VerifyReconnectRecovery);
                        var account = _accountService?.CurrentStatus;
                        report.accountPhase = account?.Phase.ToString();
                        report.accountUserId = account?.Profile?.UserId;
                        report.accountDisplayName = account?.Profile?.DisplayName;
                        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                        Debug.Log($"Unity online goat UI probe passed: {report.role}, {report.matchId}, revision {report.revision}.");
                    }
                    if (!string.IsNullOrWhiteSpace(capturePath)) StartCoroutine(CaptureDemo(capturePath));
                    else if (HasCommandLineFlag("-quitAfterOnlineProbe")) Application.Quit(0);
                    return;
#else
                    throw new NotSupportedException("Goat UI diagnostics are unavailable in release builds.");
#endif
                }

                if (HasCommandLineFlag("-onlineBuildingProbe"))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    using (var probe = new DemoOnlineBuildingProbe(_onlineGateway,
                        GameCompositionRoot.Instance.MatchStateStore, _onlineSession, _registry))
                    using (var audit = HasCommandLineFlag("-onlineDeploymentRejections")
                        ? new DemoOnlineDeploymentAudit(_onlineGateway, GameCompositionRoot.Instance.MatchStateStore,
                            _onlineSession, _registry, GetCommandLineValue("-onlineBuildingBarrier"), VerifyReconnectRecovery) : null)
                    {
                        var report = await probe.RunAsync(deadline, GetCommandLineValue("-onlineBuildingBarrier"),
                            DeployOnlineThroughUi, InspectOnlineBuildingWorld, VerifyReconnectRecovery, audit);
                        var account = _accountService?.CurrentStatus;
                        report.accountPhase = account?.Phase.ToString();
                        report.accountUserId = account?.Profile?.UserId;
                        report.accountDisplayName = account?.Profile?.DisplayName;
                        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                        Debug.Log($"Unity online building UI probe passed: {report.role}, {report.matchId}, revision {report.revision}.");
                    }
                    if (!string.IsNullOrWhiteSpace(capturePath)) StartCoroutine(CaptureDemo(capturePath));
                    else if (HasCommandLineFlag("-quitAfterOnlineProbe")) Application.Quit(0);
                    return;
#else
                    throw new NotSupportedException("Building UI diagnostics are unavailable in release builds.");
#endif
                }

                if (HasCommandLineFlag("-onlinePendingProbe"))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    using (var probe = new DemoOnlinePendingProbe(_onlineGateway,
                        GameCompositionRoot.Instance.MatchStateStore, _onlineSession))
                    {
                        var report = await probe.RunAsync(deadline, GetCommandLineValue("-onlinePendingBarrier"),
                            Path.ChangeExtension(reportPath, "pending.png"), InspectOnlinePendingUi);
                        var account = _accountService?.CurrentStatus;
                        report.accountPhase = account?.Phase.ToString();
                        report.accountUserId = account?.Profile?.UserId;
                        report.accountDisplayName = account?.Profile?.DisplayName;
                        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                    }
                    if (HasCommandLineFlag("-quitAfterOnlineProbe")) Application.Quit(0);
                    return;
#else
                    throw new NotSupportedException("Pending diagnostics are unavailable in release builds.");
#endif
                }
                if (HasCommandLineFlag("-onlineDrawProbe"))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    using (var probe = new DemoOnlineDrawProbe(_onlineGateway,
                        GameCompositionRoot.Instance.MatchStateStore, _onlineSession))
                    {
                        var report = await probe.RunAsync(deadline, InspectOnlineDrawUi, VerifyReconnectRecovery,
                            HasCommandLineFlag("-onlineDrawReadability"));
                        var account = _accountService?.CurrentStatus;
                        report.accountPhase = account?.Phase.ToString();
                        report.accountUserId = account?.Profile?.UserId;
                        report.accountDisplayName = account?.Profile?.DisplayName;
                        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                        Debug.Log($"Unity online draw UI probe passed: {report.role}, {report.matchId}, revision {report.revision}.");
                    }
                    if (!string.IsNullOrWhiteSpace(capturePath)) StartCoroutine(CaptureDemo(capturePath));
                    else if (HasCommandLineFlag("-quitAfterOnlineProbe")) Application.Quit(0);
                    return;
#else
                    throw new NotSupportedException("Draw fixture diagnostics are unavailable in release builds.");
#endif
                }
                var returnCardId = GetCommandLineValue("-onlineReturnCard");
                if (!string.IsNullOrEmpty(returnCardId))
                {
#if UNITY_EDITOR || DEVELOPMENT_BUILD
                    using (var probe = new DemoOnlineEndReturnProbe(_onlineGateway,
                        GameCompositionRoot.Instance.MatchStateStore, _onlineSession, _registry, returnCardId))
                    {
                        var report = await probe.RunAsync(deadline, GetCommandLineValue("-onlineReturnBarrier"),
                            VerifyReconnectRecovery, InspectOnlineReturnCard, PlayOnlineReturnThroughUi);
                        var account = _accountService?.CurrentStatus;
                        report.accountPhase = account?.Phase.ToString();
                        report.accountUserId = account?.Profile?.UserId;
                        report.accountDisplayName = account?.Profile?.DisplayName;
                        File.WriteAllText(reportPath, JsonUtility.ToJson(report, true));
                        Debug.Log($"Online return probe passed: {returnCardId}, {report.role}, {report.matchId}, revision {report.revision}.");
                    }
                    if (HasCommandLineFlag("-quitAfterOnlineProbe")) Application.Quit(0);
                    return;
#else
                    throw new NotSupportedException("Return-card diagnostics are unavailable in release builds.");
#endif
                }

                if (MatchView.IsMulligan && !MatchView.PlayerMulliganCompleted)
                {
                    var mulliganResult = await SendOnline(() => _onlineSession.MulliganAsync(Array.Empty<int>()));
                    if (mulliganResult?.Outcome != MatchCommandOutcome.Accepted)
                        throw new InvalidOperationException("The Unity client did not receive an accepted MULLIGAN acknowledgement.");
                }
                while (MatchView.IsMulligan && Time.realtimeSinceStartup < deadline) await Task.Yield();
                if (MatchView.IsMulligan) throw new TimeoutException("Both Unity clients did not finish opening hand selection.");

                var reconnectRecovered = HasCommandLineFlag("-autoReconnectProbe") &&
                    await VerifyReconnectRecovery(deadline);
                if (!VerifyOnlineArenaGeometry()) throw new InvalidOperationException("Arena topology was not preserved by recovery.");

                var actions = performAction
                    ? await RunOnlineActionScenario(deadline)
                    : new OnlineProbeActions();
                if (performAction)
                {
                    var presentationQueue = GameCompositionRoot.Instance?.PresentationQueue;
                    while (presentationQueue != null && presentationQueue.IsPlaying && Time.realtimeSinceStartup < deadline) await Task.Yield();
                }

                if (!string.IsNullOrWhiteSpace(reportPath))
                {
                    var directory = Path.GetDirectoryName(reportPath);
                    if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);
                    var accountStatus = _accountService?.CurrentStatus ??
                        new PlayerAccountStatus(PlayerAccountPhase.SignedOut);
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
                        opponentFaction = _opponentFaction,
                        accountPhase = accountStatus.Phase.ToString(),
                        accountUserId = accountStatus.Profile?.UserId,
                        accountDisplayName = accountStatus.Profile?.DisplayName,
                        winnerPlayerId = GameCompositionRoot.Instance.MatchStateStore.Current.winnerPlayerId,
                        arenaId = MatchView.ArenaId,
                        unitSlotCount = _battlefield.GetSlotCount(DemoSlotKind.Unit),
                        buildingSlotCount = _battlefield.GetSlotCount(DemoSlotKind.Building),
                        arenaUiVerified = VerifyOnlineArenaGeometry(),
                        playerUnitCount = MatchView.PlayerBattlefield.Count(value => value.SlotKind == DemoSlotKind.Unit && value.Health > 0),
                        opponentUnitCount = MatchView.OpponentBattlefield.Count(value => value.SlotKind == DemoSlotKind.Unit && value.Health > 0),
                        performedDeploy = actions.PerformedDeploy,
                        performedUiDeploy = actions.PerformedUiDeploy,
                        firstUiDeploySlotIndex = actions.FirstUiDeploySlotIndex,
                        performedAttack = actions.PerformedAttack,
                        performedEndTurn = actions.PerformedEndTurn,
                        performedConcede = actions.PerformedConcede,
                        reconnectRecovered = reconnectRecovered
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

        private bool VerifyOnlineArenaGeometry()
        {
            if (!IsOnlineBoard || !ArenaLayouts.TryGet(MatchView.ArenaId, out var layout) ||
                _battlefield.ArenaId != layout.Id ||
                _playerUnitSlots.Count != layout.UnitSlotCount || _opponentUnitSlots.Count != layout.UnitSlotCount ||
                _playerBuildingSlots.Count != layout.BuildingSlotCount || _opponentBuildingSlots.Count != layout.BuildingSlotCount ||
                GetComponentsInChildren<DemoBattlefieldSlotTarget>().Length != 28) return false; // Surface + footprint per slot.
            foreach (var player in GameCompositionRoot.Instance.MatchStateStore.Current.players)
                if (player.unitSlots.Length != layout.UnitSlotCount || player.buildingSlots.Length != layout.BuildingSlotCount) return false;
            Physics.SyncTransforms();
            foreach (var side in new[] { true, false })
            foreach (var kind in new[] { DemoSlotKind.Unit, DemoSlotKind.Building })
            {
                var count = kind == DemoSlotKind.Unit ? layout.UnitSlotCount : layout.BuildingSlotCount;
                for (var index = 0; index < count; index++)
                {
                    var screen = _battlefield.BoardCamera.WorldToScreenPoint(_battlefield.GetSlotInteractionWorldPosition(side, kind, index));
                    if (!_battlefield.TryRaycastSlot(screen, out var target) || target.Player != side || target.Kind != kind ||
                        target.Index != index || _canvasRoot.Find($"{(side ? "Player" : "Opponent")}{kind}Slot{index}") == null) return false;
                }
                if (_canvasRoot.Find($"{(side ? "Player" : "Opponent")}{kind}Slot{count}") != null) return false;
            }
            return true;
        }

#if UNITY_EDITOR || DEVELOPMENT_BUILD
        private async Task<bool> InspectOnlineDrawUi(float deadline)
        {
            var queue = GameCompositionRoot.Instance?.PresentationQueue;
            while ((queue?.IsPlaying == true || _turnBanner?.alpha > 0.01f) && Time.realtimeSinceStartup < deadline)
                await Task.Yield();
            if (queue?.IsPlaying == true || _turnBanner?.alpha > 0.01f) return false;
            await Task.Yield();
            var outcome = _inspectorRoot.Find("MatchOutcome")?.GetComponent<Text>();
            var pointer = GetComponent<DemoBattlefieldPointerController>();
            var revision = MatchView.Revision;
            var screen = _battlefield.BoardCamera.WorldToScreenPoint(_battlefield.GetSlotWorldPosition(true, DemoSlotKind.Unit, 0));
            var hasRealSlot = _battlefield.TryRaycastSlot(screen, out var slot) &&
                slot.Player && slot.Kind == DemoSlotKind.Unit && slot.Index == 0;
            var pressed = pointer?.ProcessPointerFrame(screen, true, false);
            var released = pointer?.ProcessPointerFrame(screen, false, true);
            var uiClickRejected = !ClickButtonThroughEventSystem(_endTurnButton);
            var sessionRejected = false;
            try { await _onlineSession.EndTurnAsync(); }
            catch (InvalidOperationException) { sessionRejected = true; }
            var result = outcome != null && outcome.text == "平局" && ((Color32)outcome.color).Equals((Color32)Gold) &&
                MatchView.IsFinished && !MatchView.HasWinner && MatchView.PlayerLife == 0 && MatchView.OpponentLife == 0 &&
                pointer != null && !pointer.InputEnabled && hasRealSlot && pressed == null && released == null &&
                uiClickRejected && sessionRejected && !HasPendingOnlineCommand && MatchView.Revision == revision &&
                !_handCanvasGroup.interactable && !_handCanvasGroup.blocksRaycasts && !_endTurnButton.interactable &&
                !_battlefield.HasActiveGameplayHighlights && string.IsNullOrEmpty(_selectedHandCardInstanceId) &&
                string.IsNullOrEmpty(_selectedAttackerInstanceId) && string.IsNullOrEmpty(_pendingTargetCardId);
            if (result && HasCommandLineFlag("-onlineDrawReadability"))
                result = await InspectOnlineFullHandReadability(deadline);
            Debug.Log($"Unity draw UI settled: {result}; text={outcome?.text}; slot={hasRealSlot}; pointer={pointer?.InputEnabled}; revision={revision}.");
            return result;
        }
#endif

        private async Task<bool> PlayOnlineReturnThroughUi(string cardId, string instanceId,
            DemoBattlefieldObject target, float deadline)
        {
            var queue = GameCompositionRoot.Instance?.PresentationQueue;
            while (queue?.IsPlaying == true && Time.realtimeSinceStartup < deadline) await Task.Yield();
            if (queue?.IsPlaying == true) return false;
            var handCard = _handRoot.GetComponentsInChildren<CardUI>(true)
                .SingleOrDefault(value => value.CardId == cardId && value.HandCardInstanceId == instanceId);
            if (!ClickButtonThroughEventSystem(handCard?.GetComponent<Button>()) ||
                _selectedCardId != cardId || _selectedHandCardInstanceId != instanceId) return false;
            await Task.Yield();
            if (!ClickSelectedCardActionThroughEventSystem() || _pendingTargetCardId != cardId) return false;
            await Task.Yield();
            MatchCommandDispatchResult? completion = null;
            void ObserveCommand(MatchCommandDispatchResult result) => completion = result;
            _onlineSession.CommandCompleted += ObserveCommand;
            try
            {
                if (!ClickBattlefieldSlotThroughPointer(true, DemoSlotKind.Unit, target.SlotIndex)) return false;
                while (!completion.HasValue && Time.realtimeSinceStartup < deadline) await Task.Yield();
                return completion?.Outcome == MatchCommandOutcome.Accepted;
            }
            finally { _onlineSession.CommandCompleted -= ObserveCommand; }
        }

        private async Task<bool> InspectOnlineReturnCard(string cardId, string instanceId, int cost, float deadline)
        {
            var queue = GameCompositionRoot.Instance?.PresentationQueue;
            while ((queue?.IsPlaying == true || HasPendingOnlineCommand) && Time.realtimeSinceStartup < deadline)
                await Task.Yield();
            if (queue?.IsPlaying == true || HasPendingOnlineCommand) return false;
            SelectHandCard(cardId, instanceId);
            await Task.Yield();
            var handCard = _handRoot.GetComponentsInChildren<CardUI>(true)
                .SingleOrDefault(value => value.HandCardInstanceId == instanceId);
            var detailCard = _inspectorRoot.GetComponentsInChildren<CardUI>(true)
                .SingleOrDefault(value => value.HandCardInstanceId == instanceId);
            return _selectedHandCardInstanceId == instanceId && handCard != null && detailCard != null &&
                handCard.CardId == cardId && detailCard.CardId == cardId &&
                handCard.DisplayedCost == cost && detailCard.DisplayedCost == cost;
        }

        private async Task<bool> VerifyReconnectRecovery(float deadline)
        {
            if (!(_onlineGateway is IMatchReconnectDiagnostics diagnostics))
                throw new InvalidOperationException("The online gateway does not support reconnect diagnostics.");
            var stateStore = GameCompositionRoot.Instance?.MatchStateStore;
            var before = stateStore?.Current;
            if (before == null) throw new InvalidOperationException("Reconnect probe requires an authoritative snapshot.");

            var matchId = before.matchId;
            var minimumRevision = before.revision;
            var sawReconnecting = false;
            var sawReadyAfterReconnect = false;
            var receivedRecoverySnapshot = false;
            void ObserveStatus(MatchConnectionStatus status)
            {
                if (status.Phase == MatchConnectionPhase.Reconnecting) sawReconnecting = true;
                if (sawReconnecting && status.Phase == MatchConnectionPhase.Ready && status.MatchId == matchId)
                    sawReadyAfterReconnect = true;
            }
            void ObserveSnapshot(MatchStateDto snapshot)
            {
                if (snapshot != null && snapshot.matchId == matchId && snapshot.revision >= minimumRevision)
                    receivedRecoverySnapshot = true;
            }

            _onlineGateway.ConnectionStateChanged += ObserveStatus;
            _onlineGateway.SnapshotReceived += ObserveSnapshot;
            try
            {
                await diagnostics.SimulateUnexpectedDisconnectAsync();
                while ((!sawReconnecting || !sawReadyAfterReconnect || !receivedRecoverySnapshot) &&
                       Time.realtimeSinceStartup < deadline)
                    await Task.Yield();
                if (!sawReconnecting || !sawReadyAfterReconnect || !receivedRecoverySnapshot)
                    throw new TimeoutException("Client did not restore its authoritative match and snapshot after connection loss.");
                return true;
            }
            finally
            {
                _onlineGateway.ConnectionStateChanged -= ObserveStatus;
                _onlineGateway.SnapshotReceived -= ObserveSnapshot;
            }
        }

        private async Task<OnlineProbeActions> RunOnlineActionScenario(float deadline)
        {
            var actions = new OnlineProbeActions();
            var useDeploymentUi = HasCommandLineFlag("-onlineUiDeploy");
            while (!MatchView.IsFinished && Time.realtimeSinceStartup < deadline)
            {
                if (!MatchView.IsPlayerTurn || !_onlineSession.CanIssueCommand)
                {
                    await Task.Yield();
                    continue;
                }

                if (MatchView.PendingChoice != null)
                    throw new InvalidOperationException("The online action probe entered an unexpected pending choice.");

                if (MatchView.Phase == DemoTurnPhase.Main)
                {
                    var deployCard = FindOnlineProbeHandCard();
                    // The UI audit intentionally exercises the capacity-dependent end cell.
                    var emptySlot = useDeploymentUi
                        ? Array.FindLastIndex(MatchView.UnitSlots, string.IsNullOrEmpty)
                        : Array.FindIndex(MatchView.UnitSlots, string.IsNullOrEmpty);
                    if (deployCard != null && emptySlot >= 0)
                    {
                        if (useDeploymentUi)
                        {
                            await DeployOnlineThroughUi(deployCard, DemoSlotKind.Unit, emptySlot, deadline);
                            if (!actions.PerformedUiDeploy) actions.FirstUiDeploySlotIndex = emptySlot;
                            actions.PerformedUiDeploy = true;
                        }
                        else
                            RequireAccepted(
                                await SendOnline(() => _onlineSession.DeployAsync(deployCard.cardId, DemoSlotKind.Unit, emptySlot,
                                    deployCard.handCardInstanceId)),
                                "DEPLOY_CARD");
                        actions.PerformedDeploy = true;
                        continue;
                    }

                    RequireAccepted(await SendOnline(() => _onlineSession.EnterCombatAsync()), "ENTER_COMBAT");
                    continue;
                }

                var attacker = MatchView.PlayerBattlefield.FirstOrDefault(value =>
                    value.SlotKind == DemoSlotKind.Unit && MatchView.CanAttackWith(value, out _));
                if (attacker != null && MatchView.CanAttackTarget(null, "HERO", out _))
                {
                    RequireAccepted(
                        await SendOnline(() => _onlineSession.AttackAsync(attacker.InstanceId, "HERO")),
                        "ATTACK");
                    actions.PerformedAttack = true;
                    RequireAccepted(await SendOnline(() => _onlineSession.ConcedeAsync()), "CONCEDE");
                    actions.PerformedConcede = true;
                    continue;
                }

                RequireAccepted(await SendOnline(() => _onlineSession.EndTurnAsync()), "END_TURN");
                actions.PerformedEndTurn = true;
            }

            if (!MatchView.IsFinished)
                throw new TimeoutException("The Unity clients did not complete deploy, attack, end-turn and concede actions before the probe deadline.");
            return actions;
        }

        private Task DeployOnlineThroughUi(HandCardStateDto card, DemoSlotKind kind, int slotIndex, float deadline) =>
            DeployOnlineCardThroughUi(card, kind, slotIndex, deadline, null);

        private Task DeployOnlineGoatThroughUi(HandCardStateDto card, int slotIndex, string targetInstanceId, float deadline) =>
            DeployOnlineCardThroughUi(card, DemoSlotKind.Unit, slotIndex, deadline, targetInstanceId);

        private async Task DeployOnlineCardThroughUi(HandCardStateDto card, DemoSlotKind kind, int slotIndex, float deadline, string targetInstanceId)
        {
            var queue = GameCompositionRoot.Instance?.PresentationQueue;
            while ((queue?.IsPlaying == true || _turnBanner?.alpha > 0.01f || HasPendingOnlineCommand) &&
                   Time.realtimeSinceStartup < deadline) await Task.Yield();
            if (Time.realtimeSinceStartup >= deadline) throw new TimeoutException(
                $"UI deployment did not become available (queue={queue?.IsPlaying}, banner={_turnBanner?.alpha}, pending={HasPendingOnlineCommand}).");
            if (!_registry.TryGetDefinition(card.cardId, out var definition)) throw new InvalidOperationException("Missing deployment definition.");
            var energyBefore = MatchView.Energy;
            var revisionBefore = MatchView.Revision;
            var cost = MatchView.GetEffectiveCost(definition, card.handCardInstanceId);
            var handCard = await WaitForOnlineHandPointerReady(card, deadline);
            if (!ClickButtonThroughEventSystem(handCard?.GetComponent<Button>()) ||
                _selectedCardId != card.cardId || _selectedHandCardInstanceId != card.handCardInstanceId)
                throw new InvalidOperationException("The exact hand instance could not be selected through the UGUI raycaster.");
            await Task.Yield();
            if (!string.IsNullOrEmpty(targetInstanceId))
            {
                var target = MatchView.PlayerBattlefield.SingleOrDefault(value => value.InstanceId == targetInstanceId);
                if (target == null || !ClickSelectedCardActionThroughEventSystem() || _pendingTargetCardId != card.cardId)
                    throw new InvalidOperationException("Goat target selection did not open through the card action UI.");
                await Task.Yield();
                if (!ClickBattlefieldSlotThroughPointer(true, DemoSlotKind.Unit, target.SlotIndex) ||
                    _selectedDeploymentTargetInstanceId != targetInstanceId || !string.IsNullOrEmpty(_pendingTargetCardId) ||
                    MatchView.Revision != revisionBefore || MatchView.Energy != energyBefore || HasPendingOnlineCommand)
                    throw new InvalidOperationException("Goat friendly target selection did not stay local and select the exact 3D object.");
                await Task.Yield();
            }
            MatchCommandDispatchResult? completion = null;
            void ObserveCommand(MatchCommandDispatchResult result) => completion = result;
            _onlineSession.CommandCompleted += ObserveCommand;
            try
            {
                if (!ClickBattlefieldSlotThroughPointer(true, kind, slotIndex))
                    throw new InvalidOperationException("UI deployment missed the intended 3D slot.");
                while (!completion.HasValue && Time.realtimeSinceStartup < deadline) await Task.Yield();
                RequireAccepted(completion, "UI DEPLOY_CARD");
                if (MatchView.Revision != revisionBefore + 1 || MatchView.Energy != energyBefore - cost ||
                    MatchView.HandCards.Any(value => value?.handCardInstanceId == card.handCardInstanceId))
                    throw new InvalidOperationException("UI deployment did not pay once and consume the exact hand instance.");
                while (queue?.IsPlaying == true && Time.realtimeSinceStartup < deadline) await Task.Yield();
                if (queue?.IsPlaying == true) throw new TimeoutException("Deployment presentation did not settle.");
                var deployed = MatchView.GetObject(true, kind, slotIndex);
                var piece = _battlefield.FindPieceTransform(deployed?.InstanceId);
                var width = kind == DemoSlotKind.Unit ? 1 : Mathf.Max(1, definition.buildingSlots);
                var position = (_battlefield.GetSlotWorldPosition(true, kind, slotIndex) +
                    _battlefield.GetSlotWorldPosition(true, kind, slotIndex + width - 1)) * 0.5f;
                if (deployed?.CardId != card.cardId || piece == null ||
                    deployed.OccupiedSlots != width ||
                    Mathf.Abs(piece.localPosition.x - position.x) > 0.001f || Mathf.Abs(piece.localPosition.z - position.z) > 0.001f)
                    throw new InvalidOperationException("The authoritative deployed model did not occupy the clicked 3D cell.");
                Debug.Log($"Unity online UI deploy verified: {card.cardId}, {card.handCardInstanceId}, slot {slotIndex}, cost {cost}.");
            }
            finally { _onlineSession.CommandCompleted -= ObserveCommand; }
        }

        private async Task<CardUI> WaitForOnlineHandPointerReady(HandCardStateDto card, float deadline)
        {
            // A recovery snapshot rebuilds the hand hierarchy. ForceUpdateCanvases alone
            // does not guarantee newly-created Graphics have entered the rendered raycast
            // registry this frame. Wait for that exact instance, never bypass the raycaster.
            while (Time.realtimeSinceStartup < deadline)
            {
                Canvas.ForceUpdateCanvases();
                var candidate = _handRoot.GetComponentsInChildren<CardUI>(true)
                    .SingleOrDefault(value => value.CardId == card.cardId && value.HandCardInstanceId == card.handCardInstanceId);
                var graphic = candidate?.GetComponent<Image>();
                var button = candidate?.GetComponent<Button>();
                if (candidate != null && !candidate.IsArrivalAnimating && graphic != null && graphic.depth >= 0 &&
                    button != null && button.isActiveAndEnabled && button.IsInteractable()) return candidate;
                if (!MatchView.HandCards.Any(value => value?.handCardInstanceId == card.handCardInstanceId && value.cardId == card.cardId))
                    throw new InvalidOperationException("The deployment hand instance disappeared before pointer readiness.");
                await Task.Yield();
            }
            throw new TimeoutException("The exact deployment hand instance never became rendered/pointer-ready.");
        }

        private async Task<bool> InspectOnlineGoatWorld(string targetId, string goatId, float deadline)
        {
            var queue = GameCompositionRoot.Instance?.PresentationQueue;
            while ((queue?.IsPlaying == true || _turnBanner?.alpha > 0.01f || HasPendingOnlineCommand) &&
                   Time.realtimeSinceStartup < deadline) await Task.Yield();
            if (Time.realtimeSinceStartup >= deadline || !VerifyOnlineArenaGeometry()) return false;
            foreach (var id in new[] { targetId, goatId })
            {
                var friendly = MatchView.PlayerBattlefield.Any(value => value.InstanceId == id);
                var value = (friendly ? MatchView.PlayerBattlefield : MatchView.OpponentBattlefield).SingleOrDefault(item => item.InstanceId == id);
                if (value == null || value.SlotKind != DemoSlotKind.Unit || value.Health <= 0) return false;
                var piece = _battlefield.FindPieceTransform(id);
                var position = _battlefield.GetSlotWorldPosition(friendly, DemoSlotKind.Unit, value.SlotIndex);
                if (piece == null || Mathf.Abs(piece.localPosition.x - position.x) > 0.001f ||
                    Mathf.Abs(piece.localPosition.z - position.z) > 0.001f || piece.GetComponentsInChildren<MeshRenderer>().Length == 0) return false;
            }
            return true;
        }

        private async Task<bool> InspectOnlineBuildingWorld(float deadline)
        {
            var queue = GameCompositionRoot.Instance?.PresentationQueue;
            while ((queue?.IsPlaying == true || _turnBanner?.alpha > 0.01f || HasPendingOnlineCommand) &&
                   Time.realtimeSinceStartup < deadline) await Task.Yield();
            if (Time.realtimeSinceStartup >= deadline || !VerifyOnlineArenaGeometry()) return false;
            Canvas.ForceUpdateCanvases();
            var rightPanel = _canvasRoot.Find("CardDetailsPanel") as RectTransform;
            if (rightPanel == null) return false;
            var panelCorners = new Vector3[4];
            rightPanel.GetWorldCorners(panelCorners);
            foreach (var friendly in new[] { true, false })
            foreach (var value in friendly ? MatchView.PlayerBattlefield : MatchView.OpponentBattlefield)
            {
                if (value.SlotKind != DemoSlotKind.Building || value.Health <= 0) continue;
                var piece = _battlefield.FindPieceTransform(value.InstanceId);
                var first = _battlefield.GetSlotWorldPosition(friendly, value.SlotKind, value.SlotIndex);
                var last = _battlefield.GetSlotWorldPosition(friendly, value.SlotKind, value.SlotIndex + value.OccupiedSlots - 1);
                var center = (first + last) * 0.5f;
                if (piece == null || Mathf.Abs(piece.localPosition.x - center.x) > 0.001f ||
                    Mathf.Abs(piece.localPosition.z - center.z) > 0.001f) return false;
                var renderers = piece.GetComponentsInChildren<MeshRenderer>();
                if (renderers.Length == 0) return false;
                var bounds = renderers[0].bounds;
                foreach (var renderer in renderers.Skip(1)) bounds.Encapsulate(renderer.bounds);
                for (var corner = 0; corner < 8; corner++)
                {
                    var point = bounds.center + Vector3.Scale(bounds.extents, new Vector3(
                        (corner & 1) == 0 ? -1 : 1, (corner & 2) == 0 ? -1 : 1, (corner & 4) == 0 ? -1 : 1));
                    if (_battlefield.BoardCamera.WorldToScreenPoint(point).x > panelCorners[0].x - 8f) return false;
                }
                if (value.OccupiedSlots > 1)
                {
                    var firstWorld = _battlefield.transform.TransformPoint(first);
                    var lastWorld = _battlefield.transform.TransformPoint(last);
                    if (bounds.min.x > Mathf.Min(firstWorld.x, lastWorld.x) || bounds.max.x < Mathf.Max(firstWorld.x, lastWorld.x)) return false;
                }
            }
            return true;
        }

        private HandCardStateDto FindOnlineProbeHandCard()
        {
            var safeUnits = _activeFaction == FactionIds.DesertBadlands
                ? new[] { "db_001", "db_005" }
                : new[] { "pf_001", "pf_002", "pf_003", "pf_004", "pf_008" };
            foreach (var handCard in MatchView.HandCards)
            {
                if (handCard == null || Array.IndexOf(safeUnits, handCard.cardId) < 0 ||
                    !_registry.TryGetDefinition(handCard.cardId, out var definition)) continue;
                if (definition.cardType == "UNIT" && definition.manualPlayAllowed &&
                    definition.effectImplementationStatus == "IMPLEMENTED" &&
                    MatchView.GetEffectiveCost(definition, handCard.handCardInstanceId) <= MatchView.Energy)
                    return handCard;
            }
            return null;
        }

        private static void RequireAccepted(MatchCommandDispatchResult? result, string commandType)
        {
            if (!result.HasValue || result.Value.Outcome != MatchCommandOutcome.Accepted)
                throw new InvalidOperationException($"The Unity client did not receive an accepted {commandType} acknowledgement.");
        }

        private IEnumerator PulsePlayerHud(Color color) => PulseHeroHud(_playerHud, _playerEffectFlash, color);

        private IEnumerator PulseOpponentHud(Color color) => PulseHeroHud(_opponentHud, _opponentEffectFlash, color);

        private IEnumerator PulseHeroHudForPlayer(string playerId, string viewerId, Color color)
        {
            if (string.IsNullOrEmpty(playerId) || string.IsNullOrEmpty(viewerId)) yield break;
            if (playerId == viewerId) yield return PulsePlayerHud(color);
            else yield return PulseOpponentHud(color);
        }

        private IEnumerator PulseBattlefieldSlot(bool player, DemoSlotKind kind, int index, int occupiedSlots, Color color)
        {
            if (_battlefield == null || index < 0) yield break;
            var slotCount = _battlefield.GetSlotCount(kind);
            if (index >= slotCount) yield break;
            var range = Mathf.Clamp(occupiedSlots, 1, slotCount - index);
            _battlefield.PulseSlotRange(player, kind, index, range, color);
            yield return new WaitForSecondsRealtime(0.24f);
            RefreshAll();
        }

        private IEnumerator AnimateAttackLunge(string attackerInstanceId, bool targetPlayer, DemoSlotKind targetKind, int targetSlotIndex)
        {
            if (_battlefield == null) yield break;
            var attacker = _battlefield.FindPieceTransform(attackerInstanceId);
            if (attacker == null) yield break;
            var start = attacker.localPosition;
            var impact = GetAttackLungePoint(start,
                _battlefield.GetSlotWorldPosition(targetPlayer, targetKind, targetSlotIndex));
            if ((impact - start).sqrMagnitude < 0.0001f) yield break;
            const float approachDuration = 0.12f;
            const float retreatDuration = 0.18f;
            for (var time = 0f; time < approachDuration; time += Time.unscaledDeltaTime)
            {
                if (attacker == null) yield break;
                var progress = Mathf.Clamp01(time / approachDuration);
                progress = 1f - (1f - progress) * (1f - progress);
                var current = Vector3.Lerp(start, impact, progress);
                current.y = attacker.localPosition.y;
                attacker.localPosition = current;
                yield return null;
            }
            for (var time = 0f; time < retreatDuration; time += Time.unscaledDeltaTime)
            {
                if (attacker == null) yield break;
                var progress = Mathf.Clamp01(time / retreatDuration);
                progress *= progress;
                var current = Vector3.Lerp(impact, start, progress);
                current.y = attacker.localPosition.y;
                attacker.localPosition = current;
                yield return null;
            }
            if (attacker != null)
            {
                var current = attacker.localPosition;
                current.x = start.x;
                current.z = start.z;
                attacker.localPosition = current;
            }
        }

        private static Vector3 GetAttackLungePoint(Vector3 start, Vector3 target)
        {
            var direction = target - start;
            direction.y = 0f;
            if (direction.sqrMagnitude < 0.0001f) return start;
            var distance = Mathf.Min(0.95f, direction.magnitude * 0.42f);
            return start + direction.normalized * distance;
        }

        private IEnumerator PulseBothHeroHuds(Color color)
        {
            if (_playerHud == null || _playerEffectFlash == null || _opponentHud == null || _opponentEffectFlash == null)
                yield break;
            var flashColor = new Color(color.r, color.g, color.b, 0.55f);
            _playerEffectFlash.GetComponent<Image>().color = flashColor;
            _opponentEffectFlash.GetComponent<Image>().color = flashColor;
            for (var time = 0f; time < 0.14f; time += Time.unscaledDeltaTime)
            {
                var progress = Mathf.Clamp01(time / 0.14f);
                _playerEffectFlash.alpha = progress;
                _opponentEffectFlash.alpha = progress;
                var scale = Vector3.one * Mathf.Lerp(1f, 1.035f, progress);
                _playerHud.localScale = scale;
                _opponentHud.localScale = scale;
                yield return null;
            }
            for (var time = 0f; time < 0.28f; time += Time.unscaledDeltaTime)
            {
                var progress = Mathf.Clamp01(time / 0.28f);
                _playerEffectFlash.alpha = 1f - progress;
                _opponentEffectFlash.alpha = 1f - progress;
                var scale = Vector3.one * Mathf.Lerp(1.035f, 1f, progress);
                _playerHud.localScale = scale;
                _opponentHud.localScale = scale;
                yield return null;
            }
            _playerEffectFlash.alpha = 0f;
            _opponentEffectFlash.alpha = 0f;
            _playerHud.localScale = Vector3.one;
            _opponentHud.localScale = Vector3.one;
        }

        private static IEnumerator PulseHeroHud(RectTransform hud, CanvasGroup flash, Color color)
        {
            if (flash == null || hud == null) yield break;
            flash.GetComponent<Image>().color = new Color(color.r, color.g, color.b, 0.55f);
            for (var time = 0f; time < 0.14f; time += Time.unscaledDeltaTime)
            {
                var progress = Mathf.Clamp01(time / 0.14f);
                flash.alpha = progress;
                hud.localScale = Vector3.one * Mathf.Lerp(1f, 1.035f, progress);
                yield return null;
            }
            for (var time = 0f; time < 0.28f; time += Time.unscaledDeltaTime)
            {
                var progress = Mathf.Clamp01(time / 0.28f);
                flash.alpha = 1f - progress;
                hud.localScale = Vector3.one * Mathf.Lerp(1.035f, 1f, progress);
                yield return null;
            }
            flash.alpha = 0f;
            hud.localScale = Vector3.one;
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
            if (IsReadOnlyOverlayOpen) return;
            if (HasPendingOnlineCommand) return;
            var match = MatchView;
            if (match.IsFinished) return;
            if (match.PendingChoice != null) return;
            if (!match.IsPlayerTurn) return;
            _pendingTargetCardId = null;
            _selectedCardTargetInstanceIds.Clear();
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
            var endResult = _match.ApplyEndTurn(_match.CreateEndTurnCommand());
            ShowStatus(endResult.Message, !endResult.Accepted);
            if (!endResult.Accepted) return;
            ClearSelectedAttackerHighlight();
            _selectedAttackerInstanceId = null;
            RefreshAll();
            if (TryShowLocalMatchOutcome()) return;
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
            else if (draw.Outcome == DemoDrawOutcome.MatchEnded)
            {
                ShowStatus(_match.PlayerLife <= 0 && _match.OpponentBattlefield.Any(value => value?.CardId == "ed_007")
                    ? "对手的末影水晶完成结束阶段脉冲，你的英雄生命归零。"
                    : "结算伤害使一方英雄生命归零，对局已经结束。", true);
                var outcome = ResolveLocalMatchEndOutcome(_match.HasWinner, _match.IsPlayerWinner);
                yield return ShowTurnBanner(GetMatchEndBannerText(outcome), GetMatchEndBannerColor(outcome));
                yield break;
            }
            else
                ShowStatus($"牌库为空，受到 {draw.FatigueDamage} 点疲劳伤害。", true);
            if (_match.IsFinished)
            {
                var outcome = ResolveLocalMatchEndOutcome(_match.HasWinner, _match.IsPlayerWinner);
                yield return ShowTurnBanner(GetMatchEndBannerText(outcome), GetMatchEndBannerColor(outcome));
                yield break;
            }
            yield return ShowTurnBanner("你的回合", Cyan);
        }

        private static MatchEndOutcome ResolveMatchEndOutcome(string winnerPlayerId, string viewerPlayerId)
        {
            if (string.IsNullOrWhiteSpace(winnerPlayerId)) return MatchEndOutcome.Draw;
            return winnerPlayerId == viewerPlayerId ? MatchEndOutcome.PlayerWon : MatchEndOutcome.PlayerLost;
        }

        private static MatchEndOutcome ResolveLocalMatchEndOutcome(bool hasWinner, bool isPlayerWinner)
        {
            if (!hasWinner) return MatchEndOutcome.Draw;
            return isPlayerWinner ? MatchEndOutcome.PlayerWon : MatchEndOutcome.PlayerLost;
        }

        private static string GetMatchEndBannerText(MatchEndOutcome outcome)
        {
            return outcome == MatchEndOutcome.PlayerWon ? "胜利"
                : outcome == MatchEndOutcome.PlayerLost ? "战败" : "平局";
        }

        private static Color GetMatchEndBannerColor(MatchEndOutcome outcome)
        {
            return outcome == MatchEndOutcome.PlayerWon ? Cyan
                : outcome == MatchEndOutcome.PlayerLost ? Danger : Gold;
        }

        private bool TryShowLocalMatchOutcome()
        {
            if (IsOnlineBoard || !_match.IsFinished) return false;
            ClearSelectedAttackerHighlight();
            _selectedAttackerInstanceId = null;
            _pendingTargetCardId = null;
            _selectedCardTargetInstanceIds.Clear();
            var outcome = ResolveLocalMatchEndOutcome(_match.HasWinner, _match.IsPlayerWinner);
            StartCoroutine(ShowTurnBanner(GetMatchEndBannerText(outcome), GetMatchEndBannerColor(outcome)));
            return true;
        }

        private IEnumerator ShowTurnBanner(string value, Color color)
        {
            var sequence = ++_turnBannerSequence;
            var bannerRect = _turnBanner.transform as RectTransform;
            if (bannerRect != null && !_hasTurnBannerRestingPosition)
            {
                _turnBannerRestingPosition = bannerRect.anchoredPosition;
                _hasTurnBannerRestingPosition = true;
            }
            var restingPosition = bannerRect != null ? _turnBannerRestingPosition : Vector2.zero;
            _turnBannerText.text = value;
            _turnBannerText.color = color;
            _turnBanner.alpha = 0f;
            if (bannerRect != null)
                bannerRect.anchoredPosition = restingPosition + Vector2.up * -14f;

            for (var time = 0f; time < 0.18f; time += Time.unscaledDeltaTime)
            {
                if (sequence != _turnBannerSequence) yield break;
                var eased = Mathf.SmoothStep(0f, 1f, time / 0.18f);
                _turnBanner.alpha = eased;
                if (bannerRect != null)
                    bannerRect.anchoredPosition = restingPosition + Vector2.up * Mathf.RoundToInt(Mathf.Lerp(-14f, 0f, eased));
                yield return null;
            }
            if (sequence != _turnBannerSequence) yield break;
            _turnBanner.alpha = 1f;
            if (bannerRect != null) bannerRect.anchoredPosition = restingPosition;
            yield return new WaitForSecondsRealtime(0.32f);

            for (var time = 0f; time < 0.2f; time += Time.unscaledDeltaTime)
            {
                if (sequence != _turnBannerSequence) yield break;
                var eased = Mathf.SmoothStep(0f, 1f, time / 0.2f);
                _turnBanner.alpha = 1f - eased;
                if (bannerRect != null)
                    bannerRect.anchoredPosition = restingPosition + Vector2.up * Mathf.RoundToInt(Mathf.Lerp(0f, 12f, eased));
                yield return null;
            }
            if (sequence != _turnBannerSequence) yield break;
            _turnBanner.alpha = 0f;
            if (bannerRect != null) bannerRect.anchoredPosition = restingPosition;
        }

        private IEnumerator SetupTurnBannerPreview()
        {
            yield return null;
            yield return null;
            var match = MatchView;
            var bannerText = match.IsPlayerTurn ? "你的回合" : "对手回合";
            yield return ShowTurnBanner(bannerText, ResolveRoundIndicatorColor(match));
        }

        private IEnumerator CaptureDemo(string path)
        {
            yield return null;
            yield return null;
            if (HasCommandLineFlag("-previewTurnBanner"))
            {
                var elapsed = 0f;
                while (_turnBanner != null && _turnBanner.alpha < 0.58f && elapsed < 1f)
                {
                    elapsed += Time.unscaledDeltaTime;
                    yield return null;
                }

                var bannerRect = _turnBanner != null ? _turnBanner.transform as RectTransform : null;
                var offset = bannerRect != null ? bannerRect.anchoredPosition.y - _turnBannerRestingPosition.y : 0f;
                var visibleMidSlide = _turnBanner != null && _turnBanner.alpha >= 0.58f && _turnBanner.alpha < 1f &&
                    offset < 0f && Mathf.Approximately(offset, Mathf.Round(offset));
                Debug.Log($"Turn banner motion preview settled: {visibleMidSlide} " +
                          $"(alpha {_turnBanner?.alpha ?? 0f:0.00}; snapped vertical offset {offset:0}px).");
                if (!visibleMidSlide)
                {
                    Debug.LogError("Turn banner motion preview did not capture a visible snapped slide-in frame.");
                    Application.Quit(2);
                    yield break;
                }
            }
            if (HasCommandLineFlag("-previewUnaffordableCardSelection"))
            {
                var timeout = 0f;
                while (!_unaffordableCardSelectionPreviewCompleted && timeout < 5f)
                {
                    timeout += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!_unaffordableCardSelectionPreviewCompleted)
                {
                    Debug.LogError("Unaffordable-card inspection preview did not complete before screenshot capture.");
                    Application.Quit(2);
                    yield break;
                }
            }
            if (HasCommandLineFlag("-previewMatchOutcome"))
            {
                var timeout = 0f;
                while ((!_matchOutcomePreviewCompleted || _turnBanner.alpha > 0.01f) && timeout < 5f)
                {
                    timeout += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!_matchOutcomePreviewCompleted || _turnBanner.alpha > 0.01f)
                {
                    Debug.LogError("Match outcome preview did not complete or its result banner did not settle before screenshot capture.");
                    Application.Quit(2);
                    yield break;
                }
            }
            if (HasCommandLineFlag("-previewEndReturnInteraction"))
            {
                var timeout = 0f;
                while (!_returnPreviewCompleted && timeout < 5f)
                {
                    timeout += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!_returnPreviewCompleted)
                {
                    Debug.LogError("End return interaction preview did not complete before screenshot capture.");
                    Application.Quit(2);
                    yield break;
                }
            }
            if (HasCommandLineFlag("-previewCaveSpiderPoison"))
            {
                var timeout = 0f;
                while (!_spiderPreviewCompleted && timeout < 10f) { timeout += Time.unscaledDeltaTime; yield return null; }
                if (!_spiderPreviewCompleted)
                {
                    Debug.LogError("Spider interaction preview did not complete before screenshot capture.");
                    Application.Quit(2); yield break;
                }
            }
            if (HasCommandLineFlag("-previewBabySheepPose"))
            {
                var timeout = 0f;
                while (!_babySheepPreviewCompleted && timeout < 10f) { timeout += Time.unscaledDeltaTime; yield return null; }
                if (!_babySheepPreviewCompleted)
                {
                    Debug.LogError("Baby sheep interaction preview did not complete before capture.");
                    Application.Quit(2); yield break;
                }
                AuditBabySheepPoseCapture();
            }
            if (HasCommandLineFlag("-previewGuardianPose"))
            {
                var timeout = 0f;
                while (!_guardianPosePreviewCompleted && timeout < 10f) { timeout += Time.unscaledDeltaTime; yield return null; }
                if (!_guardianPosePreviewCompleted)
                {
                    Debug.LogError("Guardian interaction preview did not complete before capture.");
                    Application.Quit(2); yield break;
                }
                AuditGuardianPoseCapture();
            }
            if (HasCommandLineFlag("-previewBlazePose"))
            {
                var timeout = 0f;
                while (!_blazePosePreviewCompleted && timeout < 10f) { timeout += Time.unscaledDeltaTime; yield return null; }
                if (!_blazePosePreviewCompleted)
                {
                    Debug.LogError("Blaze interaction preview did not complete before capture.");
                    Application.Quit(2); yield break;
                }
                AuditBlazePoseCapture();
            }
            if (HasCommandLineFlag("-previewCombatInteraction"))
            {
                var timeout = 0f;
                while (!_combatPreviewCompleted && timeout < 10f)
                {
                    timeout += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!_combatPreviewCompleted)
                {
                    Debug.LogError("Combat interaction preview did not complete before screenshot capture.");
                    Application.Quit(2);
                    yield break;
                }
            }
            if (HasCommandLineFlag("-previewStructureDragDeployment"))
            {
                var timeout = 0f;
                while (!_structureDragPreviewCompleted && timeout < 10f)
                {
                    timeout += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!_structureDragPreviewCompleted)
                {
                    Debug.LogError("Structure drag deployment preview did not complete before screenshot capture.");
                    Application.Quit(2);
                    yield break;
                }
            }
            if (HasCommandLineFlag("-previewCraftingInteraction"))
            {
                var timeout = 0f;
                while (!_craftingInteractionPreviewCompleted && timeout < 10f)
                {
                    timeout += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!_craftingInteractionPreviewCompleted)
                {
                    Debug.LogError("Crafting interaction preview did not complete before screenshot capture.");
                    Application.Quit(2);
                    yield break;
                }
            }
            if (HasCommandLineFlag("-previewCardArrival"))
            {
                var timeout = 0f;
                while (!_cardArrivalPreviewCompleted && timeout < 5f)
                {
                    timeout += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!_cardArrivalPreviewCompleted)
                {
                    Debug.LogError("Hand card arrival preview did not complete before screenshot capture.");
                    Application.Quit(2);
                    yield break;
                }
            }
            if (HasCommandLineFlag("-previewChoiceInteraction"))
            {
                var timeout = 0f;
                while (!_choiceInteractionPreviewCompleted && timeout < 5f)
                {
                    timeout += Time.unscaledDeltaTime;
                    yield return null;
                }
                if (!_choiceInteractionPreviewCompleted)
                {
                    Debug.LogError("Choice interaction preview did not complete before screenshot capture.");
                    Application.Quit(2);
                    yield break;
                }
            }
            if (HasCommandLineFlag("-previewHandHover")) yield return new WaitForSecondsRealtime(0.25f);
            if (HasCommandLineFlag("-previewAttackFeedback")) yield return new WaitForSecondsRealtime(0.46f);
            if (HasCommandLineFlag("-previewButtonFeedback")) yield return new WaitForSecondsRealtime(0.2f);
            if (HasCommandLineFlag("-previewCombatInteraction")) yield return new WaitForSecondsRealtime(0.46f);
            if (HasCommandLineFlag("-previewStructureDragDeployment")) yield return new WaitForSecondsRealtime(0.18f);
            if (HasCommandLineFlag("-previewCraftingInteraction")) yield return new WaitForSecondsRealtime(0.18f);
            if (HasCommandLineFlag("-previewCardArrival")) yield return new WaitForSecondsRealtime(0.08f);
            if (HasCommandLineFlag("-previewEffect")) yield return new WaitForSecondsRealtime(0.1f);
            if (HasCommandLineFlag("-previewGroundHover")) yield return new WaitForSecondsRealtime(0.2f);
            if (HasCommandLineFlag("-previewGroundReturnPulse")) yield return new WaitForSecondsRealtime(0.04f);
            if (HasCommandLineFlag("-previewNetherStatusSummon")) yield return new WaitForSecondsRealtime(0.25f);
            if (HasCommandLineFlag("-previewHandHover"))
            {
                PreviewHandCardHover();
                var hoveredCard = _handRoot.GetComponentsInChildren<DemoHoverScale>(true)
                    .FirstOrDefault(value => value.PreviewHoverPinned && value.transform.localScale.x >= 1.2f);
                Debug.Log("Hand hover preview settled: " + (hoveredCard != null));
            }
            if (HasCommandLineFlag("-previewFullHand"))
            {
                yield return null;
                Canvas.ForceUpdateCanvases();
                var settled = IsFullHandLayoutWithinPlate();
                var handCardCount = _handRoot.GetComponentsInChildren<CardUI>(true).Length;
                Debug.Log($"Full hand layout preview settled: {settled} (cards={handCardCount}, registered long titles clear cost sockets, outer cards inside stone hand plate, larger 166x216 layout).");
                if (!settled)
                {
                    Debug.LogError("Seven-card hand layout did not fit the stone hand plate before screenshot capture.");
                    Application.Quit(2);
                    yield break;
                }
                var locked = HasCommandLineFlag("-previewFullHandCombat");
                var cards = _handRoot.GetComponentsInChildren<CardUI>(true);
                var readable = Mathf.Approximately(_handCanvasGroup.alpha, 1f) &&
                    _handCanvasGroup.interactable == !locked && _handCanvasGroup.blocksRaycasts == !locked &&
                    cards.All(card => Mathf.Approximately(card.GetComponent<Button>().colors.disabledColor.a, 1f)) &&
                    cards.SelectMany(card => card.GetComponentsInChildren<Text>()).All(text =>
                        text.color.a * text.GetComponentsInParent<CanvasGroup>().Aggregate(1f, (alpha, group) => alpha * group.alpha) >= 0.799f) &&
                    (!locked || _match.Phase == DemoTurnPhase.Combat);
                Debug.Log($"Full hand readability preview settled: {readable} (state={(locked ? "combat" : "active")}; cards=7; input locked={locked}).");
                if (!readable)
                {
                    Debug.LogError("Full-hand information opacity or input gates did not settle before capture.");
                    Application.Quit(2);
                    yield break;
                }
            }
            if (HasCommandLineFlag("-previewHandInspection")) yield return PrepareHandInspectionCapture();
            if (HasCommandLineFlag("-previewResponsiveHandInspection")) yield return PrepareResponsiveHandInspectionCapture();
            if (HasCommandLineFlag("-previewStatusInspection")) yield return PrepareStatusInspectionCapture();
            if (HasCommandLineFlag("-previewOnlineFeedback")) yield return PrepareOnlineFeedbackCapture(GetCommandLineValue("-previewOnlineFeedback"));
            if (HasCommandLineFlag("-previewCardPaper")) yield return PrepareCardPaperCapture();
            if (HasCommandLineFlag("-previewChoiceRules")) yield return PrepareChoiceRulesCapture();
            if (HasCommandLineFlag("-previewHudResources") || HasCommandLineFlag("-previewCardNotes"))
                yield return PrepareHudResourceCapture(HasCommandLineFlag("-previewCardNotes"));
            if (HasCommandLineFlag("-previewHandState")) yield return AuditHandReadabilityStateCapture(GetCommandLineValue("-previewHandState"));
            WriteDemoScreenshot(path);
            Application.Quit(0);
        }

        private void WriteDemoScreenshot(string path)
        {
            var canvas = GetComponentInChildren<Canvas>();
            var camera = _battlefield.BoardCamera;
            if (camera == null) throw new InvalidOperationException("2.5D battlefield camera is not available.");
            if (HasCommandLineFlag("-previewTurtlePose")) AuditTurtlePoseCapture();
            if (HasCommandLineFlag("-previewDesertVillagerSurface")) AuditDesertVillagerSurfaceCapture();
            if (HasCommandLineFlag("-previewPolarBearPose"))
            {
                Physics.SyncTransforms();
                foreach (var player in new[] { true, false })
                {
                    var bear = _match.GetObject(player, DemoSlotKind.Unit, 1);
                    var piece = _battlefield.FindPieceTransform(bear?.InstanceId);
                    var point = camera.WorldToScreenPoint(_battlefield.GetSlotInteractionWorldPosition(player, DemoSlotKind.Unit, 1));
                    if (bear?.CardId != "si_005" || piece == null || point.z <= 0 ||
                        Quaternion.Angle(piece.localRotation, Quaternion.Euler(0, player ? 180f : 0f, 0)) > 5.001f ||
                        !_battlefield.TryRaycastSlot(point, out var target) || target.Player != player ||
                        target.Kind != DemoSlotKind.Unit || target.Index != 1)
                        throw new InvalidOperationException("Polar bear runtime facing or original slot raycast changed.");
                }
                Debug.Log("Polar bear runtime audit: True (both initial facings retained; original unit slots raycast).");
            }
            var backdropCamera = _battlefield.LetterboxCamera;
            if (backdropCamera == null) throw new InvalidOperationException("Battlefield letterbox camera is not available.");
            var directory = Path.GetDirectoryName(path);
            if (!string.IsNullOrEmpty(directory)) Directory.CreateDirectory(directory);

            var originalMode = canvas.renderMode;
            var originalCamera = canvas.worldCamera;
            var originalTarget = camera.targetTexture;
            var originalBackdropTarget = backdropCamera.targetTexture;
            var captureWidth = GetCaptureDimension("-captureWidth", 1920);
            var captureHeight = GetCaptureDimension("-captureHeight", 1080);
            var renderTexture = new RenderTexture(captureWidth, captureHeight, 24, RenderTextureFormat.ARGB32);
            renderTexture.Create();
            canvas.renderMode = RenderMode.ScreenSpaceCamera;
            canvas.worldCamera = camera;
            canvas.planeDistance = 1f;
            backdropCamera.targetTexture = renderTexture;
            camera.targetTexture = renderTexture;
            _battlefield.RefreshCameraViewport();
            Canvas.ForceUpdateCanvases();
            backdropCamera.Render();
            camera.Render();

            var previousActive = RenderTexture.active;
            RenderTexture.active = renderTexture;
            var texture = new Texture2D(captureWidth, captureHeight, TextureFormat.RGB24, false);
            texture.ReadPixels(new Rect(0, 0, captureWidth, captureHeight), 0, 0, false);
            texture.Apply(false, false);
            File.WriteAllBytes(path, texture.EncodeToPNG());

            RenderTexture.active = previousActive;
            camera.targetTexture = originalTarget;
            backdropCamera.targetTexture = originalBackdropTarget;
            _battlefield.RefreshCameraViewport();
            canvas.renderMode = originalMode;
            canvas.worldCamera = originalCamera;
            renderTexture.Release();
            Destroy(renderTexture);
            Destroy(texture);
            Debug.Log($"Demo screenshot dimensions: {captureWidth}x{captureHeight}; camera aspect {captureWidth / (float)captureHeight:F4}");
            Debug.Log("Demo screenshot saved: " + path);
        }

        private static int GetCaptureDimension(string argument, int defaultValue)
        {
            var value = GetCommandLineValue(argument);
            if (string.IsNullOrEmpty(value)) return defaultValue;
            if (!int.TryParse(value, out var dimension) || dimension < 320 || dimension > 7680)
                throw new ArgumentOutOfRangeException(argument, "Capture dimensions must be integers from 320 through 7680 pixels.");
            return dimension;
        }

        private IEnumerator PreviewHandCardHoverAfterUiSettles()
        {
            yield return null;
            PreviewHandCardHover();
        }

        private void PreviewHandCardHover()
        {
            var handCardInstanceId = MatchView.HandCards.FirstOrDefault()?.handCardInstanceId;
            if (string.IsNullOrEmpty(handCardInstanceId))
                throw new InvalidOperationException("Hand hover preview requires a current hand card instance.");
            var hover = _handRoot.GetComponentsInChildren<DemoHoverScale>(true).FirstOrDefault(value =>
                value.GetComponent<CardUI>()?.HandCardInstanceId == handCardInstanceId);
            if (hover == null) throw new InvalidOperationException("Hand hover preview requires at least one visible card.");
            hover.PinHoverForPreview();
            Debug.Log($"Hand hover pin invoked: controller={GetInstanceID()} card={hover.name} target={hover.TargetScale:F2} scale={hover.transform.localScale.x:F2}");
        }

        private IEnumerator PreviewUnaffordableCardSelectionAfterUiSettles()
        {
            yield return null;
            Canvas.ForceUpdateCanvases();
            var candidate = _handRoot.GetComponentsInChildren<CardUI>(true)
                .FirstOrDefault(value => value != null && !value.IsResourceAffordable);
            var button = candidate != null ? candidate.GetComponent<Button>() : null;
            var energyBefore = MatchView.Energy;
            var instanceId = candidate != null ? candidate.HandCardInstanceId : string.Empty;
            var cardId = candidate != null ? candidate.CardId : string.Empty;
            var clicked = ClickButtonThroughEventSystem(button);
            yield return null;

            var stillInHand = !string.IsNullOrEmpty(instanceId) && MatchView.HandCards.Any(value =>
                value != null && value.handCardInstanceId == instanceId && value.cardId == cardId);
            var selected = _selectedCardId == cardId && _selectedHandCardInstanceId == instanceId;
            var detailCard = _inspectorRoot.GetComponentsInChildren<CardUI>(true)
                .FirstOrDefault(value => value != null && value.CardId == cardId);
            var deploymentHint = _inspectorRoot.Find("DeployHint")?.GetComponent<Text>()?.text ?? string.Empty;
            var multilineHint = deploymentHint.Contains("\n");
            var settled = clicked && !string.IsNullOrEmpty(cardId) && stillInHand && selected && detailCard != null &&
                MatchView.Energy == energyBefore && deploymentHint.Contains("红石不足") && multilineHint;
            Debug.Log($"Unaffordable card inspection preview settled: {settled} " +
                      $"(raycast click={clicked}; selected={selected}; still in hand={stillInHand}; " +
                      $"energy unchanged={MatchView.Energy == energyBefore}; multiline hint={multilineHint}; hint='{deploymentHint}').");
            if (!settled) Debug.LogError("A dimmed unaffordable hand card did not remain inspectable without spending resources.");
            _unaffordableCardSelectionPreviewCompleted = true;
        }

        private void ShowStatus(string value, bool error)
        {
            if (_statusText == null) return;
            // Event playback may use a lightweight view before the full HUD is built.
            if (_statusSummary != null) _statusSummary.SetFullText(value);
            else _statusText.text = value;
            _statusText.color = error ? Danger : Pale;
            RefreshStatusInspection();
        }

        private void ShowLocalCommandResultStatus(DemoCommandResult result, string displayMessage = null)
        {
            var message = string.IsNullOrWhiteSpace(displayMessage) ? result.Message : displayMessage;
            if (string.IsNullOrWhiteSpace(message)) message = result.Accepted ? "操作已完成。" : "操作未生效。";
            ShowStatus(message, !result.Accepted);
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
            var summonReadinessVisible = battlefieldObject?.CardId == "cd_008" || battlefieldObject?.CardId == "nt_008";
            if (battlefieldObject?.HasKeyword("TAUNT") == true) stats = $"◆ 嘲讽   {stats}";
            else if (battlefieldObject?.HasKeyword("CHARGE") == true) stats = $"➤ 冲锋   {stats}";
            if ((battlefieldObject?.TemporaryHealthModifier ?? 0) > 0)
            {
                stats = $"羊毛护持 +{battlefieldObject.TemporaryHealthModifier}临时生命 · {stats}";
                accent = Pale;
            }
            if ((battlefieldObject?.AdjacencyHealthModifier ?? 0) > 0)
            {
                stats = $"海龟光环 +{battlefieldObject.AdjacencyHealthModifier}生命 · {stats}";
                accent = Cyan;
            }
            if (!MatchView.IsFinished && battlefieldObject?.CardId == "or_007")
            {
                var ready = !MatchView.HasTriggeredEffect(!enemy, battlefieldObject.InstanceId, "effect.or_007.01");
                stats = $"珊瑚滋养：{(ready ? "待触发" : "本回合已触发")} · {stats}";
                accent = ready ? Hex("#F08FB4") : accent;
            }
            if (!MatchView.IsFinished && battlefieldObject?.CardId == "pf_005")
            {
                var ready = !MatchView.HasTriggeredEffect(!enemy, battlefieldObject.InstanceId, "effect.pf_005.01");
                stats = $"苗圃培育：{(ready ? "待触发" : "本回合已触发")} · {stats}";
                accent = ready ? Leaf : accent;
            }
            if (!MatchView.IsFinished && battlefieldObject?.CardId == "db_004")
            {
                var ready = !MatchView.HasTriggeredEffect(!enemy, battlefieldObject.InstanceId, "effect.db_004.01");
                stats = $"尖刺反击：{(ready ? "待触发" : "本回合已触发")} · {stats}";
                accent = ready ? Hex("#C7D65A") : accent;
            }
            if (!MatchView.IsFinished && battlefieldObject?.CardId == "db_007")
            {
                stats = $"遗迹修复：等待出土 · {stats}";
                accent = Gold;
            }
            if (!MatchView.IsFinished && battlefieldObject?.CardId == "cd_007")
            {
                var ready = MatchView.IsPlayerTurn == !enemy && MatchView.CardsPlayedThisTurn(!enemy) == 1;
                stats = $"矿井产出：{(ready ? "结束阶段就绪" : "需恰好打出一张牌")} · {stats}";
                if (ready) accent = Gold;
            }
            if (battlefieldObject?.CardId == "cd_008")
            {
                var ready = TryGetEndPhaseSummonReadiness(!enemy, false, out var readiness);
                stats = $"{stats}\n{readiness}";
                if (ready) accent = Leaf;
            }
            if (battlefieldObject?.CardId == "nt_008")
            {
                var ready = TryGetEndPhaseSummonReadiness(!enemy, true, out var readiness);
                stats = $"{stats}\n{readiness}";
                if (ready) accent = Gold;
            }
            if (!MatchView.IsFinished && battlefieldObject?.CardId == "si_008")
            {
                stats = $"冰刺警戒：监听敌方边缘召唤 · {stats}";
                accent = Cyan;
            }
            if (battlefieldObject?.CardId == "pf_003")
            {
                var permanentGrowth = Mathf.Max(0,
                    battlefieldObject.MaxHealth - definition.health - battlefieldObject.AdjacencyHealthModifier - battlefieldObject.TemporaryHealthModifier);
                if (permanentGrowth > 0)
                {
                    stats = $"忠诚 +{permanentGrowth}生命 · {stats}";
                    accent = Gold;
                }
            }
            if (IsOceanMonumentThreat(!enemy, battlefieldObject))
            {
                stats = $"神殿锁定 · {stats}";
                accent = Hex("#FF8865");
            }
            var slow = (battlefieldObject?.Statuses ?? Array.Empty<BattlefieldStatusStateDto>())
                .FirstOrDefault(value => value != null && value.statusId == "SLOW");
            if (slow != null)
            {
                stats = $"缓慢 {slow.remainingDuration} · {stats}";
                accent = Cyan;
            }
            var poison = (battlefieldObject?.Statuses ?? Array.Empty<BattlefieldStatusStateDto>())
                .FirstOrDefault(value => value != null && value.statusId == "POISON");
            if (poison != null)
            {
                stats = $"中毒 {poison.remainingDuration} · {stats}";
                accent = Hex("#A6F04D");
            }
            var fire = (battlefieldObject?.Statuses ?? Array.Empty<BattlefieldStatusStateDto>())
                .FirstOrDefault(value => value != null && value.statusId == "FIRE");
            if (fire != null)
            {
                stats = $"着火 {fire.remainingDuration} · {stats}";
                accent = Hex("#FF8A2A");
            }
            var wither = (battlefieldObject?.Statuses ?? Array.Empty<BattlefieldStatusStateDto>())
                .FirstOrDefault(value => value != null && value.statusId == "WITHER");
            if (wither != null)
            {
                stats = $"凋零 {wither.remainingDuration} · {stats}";
                accent = Hex("#8E61C7");
            }
            var polarBearBonus = battlefieldObject?.CardId == "si_005"
                ? Mathf.Max(0, battlefieldObject.Attack - definition.attack - battlefieldObject.TemporaryAttackModifier)
                : 0;
            var polarBearModifiersVisible = polarBearBonus > 0 || (battlefieldObject?.TemporaryHealthModifier ?? 0) > 0;
            if (polarBearModifiersVisible)
            {
                var modifierLines = new List<string> { $"{text.name} {attack}/{health}" };
                var permanentLine = battlefieldObject.HasKeyword("TAUNT") ? "◆ 嘲讽" : string.Empty;
                if (polarBearBonus > 0)
                    permanentLine = string.IsNullOrEmpty(permanentLine)
                        ? $"攻+{polarBearBonus}永久"
                        : $"{permanentLine} · 攻+{polarBearBonus}永久";
                if (!string.IsNullOrEmpty(permanentLine)) modifierLines.Add(permanentLine);
                if (battlefieldObject.TemporaryHealthModifier > 0)
                    modifierLines.Add($"血+{battlefieldObject.TemporaryHealthModifier}临时");
                stats = string.Join("\n", modifierLines);
                accent = polarBearBonus > 0 ? Gold : Pale;
            }
            var labelHeight = polarBearModifiersVisible ? 64f : summonReadinessVisible ? 58f : 38f;
            // Grow summon labels upward, preserving clearance above the hand tray.
            var labelY = -size.y * 0.34f + (summonReadinessVisible ? (labelHeight - 38f) * 0.5f : 0f);
            var plate = CreatePanel(parent, "WorldLabel", new Vector2(0, labelY), new Vector2(size.x - 8, labelHeight), new Color(Ink.r, Ink.g, Ink.b, 0.84f));
            plate.raycastTarget = false;
            CreatePanel(parent, "WorldLabelAccent", new Vector2(0, labelY + labelHeight * 0.5f - 1), new Vector2(size.x - 8, 2), new Color(accent.r, accent.g, accent.b, 0.82f)).raycastTarget = false;
            var labelFontSize = summonReadinessVisible ? 18 : WorldLabelFontSize;
            var labelText = CreateText(parent, "WorldLabelText", new Vector2(0, labelY), new Vector2(size.x - 20, labelHeight - 10), stats, labelFontSize, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
            labelText.resizeTextForBestFit = true;
            labelText.resizeTextMinSize = summonReadinessVisible ? labelFontSize : WorldLabelMinimumFontSize;
            labelText.resizeTextMaxSize = labelFontSize;
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
            var frame = root.Find("FrameSlice")?.GetComponent<Image>();
            button.targetGraphic = frame != null ? frame : root.GetComponent<Image>();
            ConfigureButtonColors(button, DemoUiStyleCatalog.GetFrameTint(styleClass), DemoUiStyleCatalog.GetInteractionTint(styleClass));
            ConfigureHoverScale(root.gameObject, 1.035f, 16f);
            CreateText(root, "Label", Vector2.zero, size - new Vector2(12, 8), label, fontSize, Pale, TextAnchor.MiddleCenter, FontStyle.Bold);
            return button;
        }

        private static DemoHoverScale ConfigureHoverScale(GameObject target, float scale, float speed)
        {
            var hover = target.GetComponent<DemoHoverScale>() ?? target.AddComponent<DemoHoverScale>();
            hover.Configure(scale, speed);
            return hover;
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
            if (parent == _inspectorRoot && name != "Implementation")
            {
                root.gameObject.AddComponent<DemoReadableSummary>().Configure(fontSize);
                return text;
            }
            if (name == "Account" || name == "Deck" || name == "Name" || name == "Health" ||
                name == "Resource" || name == "Energy" || name == "Round" || name == "HandLabel" ||
                name == "FactionLabel" || name == "Header" || name == "ReadOnlyHint" || name == "Position" ||
                (name == "Label" && parent.GetComponent<DemoUiStyleComponent>() != null))
                root.gameObject.AddComponent<DemoHudTypography>().Configure(fontSize);
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
            if (Application.isPlaying)
            {
                // Deferred destruction must not leave retired text/buttons active for this frame.
                target.SetActive(false);
                Destroy(target);
            }
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
            public readonly Text Label;
            public readonly Image SelectionAccent;

            public FactionButtonView(string id, Button button, Image image, Text label, Image selectionAccent)
            {
                Id = id;
                Button = button;
                Image = image;
                Label = label;
                SelectionAccent = selectionAccent;
            }
        }

        private sealed class FactionSpec
        {
            public readonly string Id;
            public readonly string Label;
            public readonly string Prefix;
            public readonly string PlayerTitle;
            public readonly string PortraitCardId;

            public FactionSpec(string id, string label, string prefix, string playerTitle, string portraitCardId)
            {
                Id = id;
                Label = label;
                Prefix = prefix;
                PlayerTitle = playerTitle;
                PortraitCardId = portraitCardId;
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
            public string accountPhase;
            public string accountUserId;
            public string accountDisplayName;
            public string winnerPlayerId;
            public int playerUnitCount;
            public int opponentUnitCount;
            public bool performedDeploy;
            public bool performedUiDeploy;
            public int firstUiDeploySlotIndex = -1;
            public bool performedAttack;
            public bool performedEndTurn;
            public bool performedConcede;
            public bool reconnectRecovered;
            public string arenaId;
            public int unitSlotCount;
            public int buildingSlotCount;
            public bool arenaUiVerified;
        }

        private sealed class OnlineProbeActions
        {
            public bool PerformedDeploy;
            public bool PerformedUiDeploy;
            public int FirstUiDeploySlotIndex = -1;
            public bool PerformedAttack;
            public bool PerformedEndTurn;
            public bool PerformedConcede;
        }
    }
}
