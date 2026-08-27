using System;
using System.Collections.Generic;
using System.Linq;
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
        public int ViewerIndex => FindViewerIndex();
        public IReadOnlyList<string> Hand => Player?.hand ?? EmptySlots;
        public string[] UnitSlots => MapSlots(Player, DemoSlotKind.Unit);
        public string[] BuildingSlots => MapSlots(Player, DemoSlotKind.Building);
        public string[] OpponentUnitSlots => MapSlots(Opponent, DemoSlotKind.Unit);
        public string[] OpponentBuildingSlots => MapSlots(Opponent, DemoSlotKind.Building);
        public IReadOnlyList<DemoBattlefieldObject> PlayerBattlefield => MapBattlefield(Player, true);
        public IReadOnlyList<DemoBattlefieldObject> OpponentBattlefield => MapBattlefield(Opponent, false);
        public int DeckCount => Player?.deckCount ?? 0;
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
            if (attacker.SummonedRound >= Round) return Fail("该生物本回合刚部署，暂时不能攻击。", out message);
            if (attacker.HasAttacked) return Fail("该生物本回合已经攻击过。", out message);
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
