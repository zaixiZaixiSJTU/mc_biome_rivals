using System;
using System.Collections.Generic;
using System.Linq;
using BiomeRivals.Content;
using BiomeRivals.Core;

namespace BiomeRivals.Demo
{
    public sealed class DemoAuthoritativeMatchView : IDemoMatchView
    {
        private static readonly string[] EmptySlots = Array.Empty<string>();
        private readonly MatchStateStore _store;

        public DemoAuthoritativeMatchView(MatchStateStore store)
        {
            _store = store ?? throw new ArgumentNullException(nameof(store));
        }

        public bool IsAuthoritative => Current != null;
        public string PlayerFactionId => Player?.factionId ?? FactionIds.PlainsForest;
        public string OpponentFactionId => Opponent?.factionId ?? FactionIds.Nether;
        public bool IsMulligan => Current?.status == "MULLIGAN";
        public bool PlayerMulliganCompleted => Player?.mulliganCompleted == true;
        public bool OpponentMulliganCompleted => Opponent?.mulliganCompleted == true;
        public int ViewerIndex => FindViewerIndex();
        public IReadOnlyList<string> Hand => Player?.hand ?? EmptySlots;
        public string[] UnitSlots => MapSlots(Player, DemoSlotKind.Unit);
        public string[] BuildingSlots => MapSlots(Player, DemoSlotKind.Building);
        public string[] OpponentUnitSlots => MapSlots(Opponent, DemoSlotKind.Unit);
        public string[] OpponentBuildingSlots => MapSlots(Opponent, DemoSlotKind.Building);
        public IReadOnlyList<DemoBattlefieldObject> PlayerBattlefield => MapBattlefield(Player, true);
        public IReadOnlyList<DemoBattlefieldObject> OpponentBattlefield => MapBattlefield(Opponent, false);
        public int DeckCount => Player?.deckCount ?? 0;
        public int BuriedCount => Player?.buriedCount ?? 0;
        public bool ExcavatedThisTurn => Player?.excavatedThisTurn == true;
        public PendingChoiceDto PendingChoice => Current?.pendingChoice;
        public bool IsChoiceOwner => PendingChoice != null && PendingChoice.playerId == Current?.viewerPlayerId;
        public int DiscardCount => Player?.discardPile?.Length ?? 0;
        public int OpponentHandCount => Opponent?.hand?.Length ?? 0;
        public int Round => Current?.turn ?? 0;
        public int MaxEnergy => Player?.redstoneCapacity ?? 0;
        public int Energy => Player?.redstone ?? 0;
        public bool IsPlayerTurn => Current != null && ViewerIndex >= 0 && Current.activePlayerIndex == ViewerIndex;
        public DemoTurnPhase Phase => Current?.phase == "COMBAT" ? DemoTurnPhase.Combat : DemoTurnPhase.Main;
        public int PlayerLife => Player?.life ?? 0;
        public int PlayerArmor => Player?.armor ?? 0;
        public int OpponentLife => Opponent?.life ?? 0;
        public bool IsFinished => Current?.status == "FINISHED";
        public int Revision => Current?.revision ?? 0;

        public int GetEffectiveCost(CardDefinitionEntry definition)
        {
            if (definition == null) return 0;
            return definition.id == "db_005" && ExcavatedThisTurn
                ? Math.Max(0, definition.cost - 1)
                : definition.cost;
        }

        private MatchStateDto Current => _store.Current;

        private PlayerStateDto Player
        {
            get
            {
                var index = FindViewerIndex();
                return index < 0 ? null : Current.players[index];
            }
        }

        private PlayerStateDto Opponent
        {
            get
            {
                var index = FindViewerIndex();
                return index < 0 ? null : Current.players[index == 0 ? 1 : 0];
            }
        }

        public DemoBattlefieldObject GetObject(bool player, DemoSlotKind kind, int slotIndex)
        {
            var owner = player ? Player : Opponent;
            if (owner == null) return null;
            var slots = kind == DemoSlotKind.Unit ? owner.unitSlots : owner.buildingSlots;
            if (slots == null || slotIndex < 0 || slotIndex >= slots.Length || string.IsNullOrEmpty(slots[slotIndex])) return null;
            var value = (owner.battlefield ?? Array.Empty<BattlefieldObjectStateDto>())
                .FirstOrDefault(item => item != null && item.instanceId == slots[slotIndex]);
            return value == null ? null : Map(value, player);
        }

        public bool CanAttackWith(DemoBattlefieldObject attacker, out string message)
        {
            if (!IsPlayerTurn || Phase != DemoTurnPhase.Combat) return Fail("请先进入战斗阶段。", out message);
            if (attacker == null || !attacker.Player || attacker.SlotKind != DemoSlotKind.Unit || attacker.Health <= 0 || attacker.Attack <= 0)
                return Fail("请选择一个存活且具有攻击力的己方生物。", out message);
            if (attacker.SummonedRound >= Round && !attacker.HasKeyword("CHARGE")) return Fail("该生物本回合刚被召唤，且不具有冲锋。", out message);
            if (attacker.HasAttacked) return Fail("该生物本回合已经攻击过。", out message);
            message = string.Empty;
            return true;
        }

        public bool CanAttackTarget(DemoBattlefieldObject target, string targetType, out string message)
        {
            if (targetType != "HERO" && targetType != "UNIT" && targetType != "BUILDING")
                return Fail("攻击目标类型无效。", out message);
            if (targetType != "HERO" && (target == null || target.Player ||
                (targetType == "UNIT" && target.SlotKind != DemoSlotKind.Unit) ||
                (targetType == "BUILDING" && target.SlotKind != DemoSlotKind.Building)))
                return Fail("攻击目标无效或已经离场。", out message);
            var taunts = OpponentBattlefield.Where(value => value.Health > 0 && value.HasKeyword("TAUNT")).ToArray();
            if (taunts.Length > 0 && (targetType == "HERO" || target == null || !target.HasKeyword("TAUNT")))
                return Fail("敌方存在嘲讽单位，必须先攻击一个发出金光的嘲讽目标。", out message);
            message = string.Empty;
            return true;
        }

        private int FindViewerIndex()
        {
            if (Current?.players == null || Current.players.Length != 2) return -1;
            for (var index = 0; index < Current.players.Length; index++)
                if (Current.players[index]?.playerId == Current.viewerPlayerId) return index;
            return -1;
        }

        private static IReadOnlyList<DemoBattlefieldObject> MapBattlefield(PlayerStateDto owner, bool player) =>
            (owner?.battlefield ?? Array.Empty<BattlefieldObjectStateDto>())
                .Where(value => value != null)
                .Select(value => Map(value, player))
                .ToArray();

        private static string[] MapSlots(PlayerStateDto owner, DemoSlotKind kind)
        {
            if (owner == null) return EmptySlots;
            var source = kind == DemoSlotKind.Unit ? owner.unitSlots : owner.buildingSlots;
            if (source == null) return EmptySlots;
            var battlefield = owner.battlefield ?? Array.Empty<BattlefieldObjectStateDto>();
            return source.Select(instanceId =>
            {
                if (string.IsNullOrEmpty(instanceId)) return string.Empty;
                var value = battlefield.FirstOrDefault(item => item != null && item.instanceId == instanceId);
                return value?.cardId ?? string.Empty;
            }).ToArray();
        }

        private static DemoBattlefieldObject Map(BattlefieldObjectStateDto value, bool player) =>
            new DemoBattlefieldObject
            {
                InstanceId = value.instanceId,
                CardId = value.cardId,
                Player = player,
                SlotKind = value.slotKind == "BUILDING" ? DemoSlotKind.Building : DemoSlotKind.Unit,
                SlotIndex = value.slotIndex,
                OccupiedSlots = value.occupiedSlots,
                Attack = value.attack,
                Health = value.health,
                MaxHealth = value.maxHealth,
                SummonedRound = value.summonedTurn,
                HasAttacked = value.hasAttacked,
                Keywords = value.keywords ?? Array.Empty<string>(),
                TemporaryAttackModifier = value.temporaryAttackModifier,
                TemporaryAttackModifierExpiresOnRound = value.temporaryAttackModifierExpiresOnTurn
            };

        private static bool Fail(string value, out string message)
        {
            message = value;
            return false;
        }
    }
}
