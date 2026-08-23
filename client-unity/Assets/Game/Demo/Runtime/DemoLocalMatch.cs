using System;
using System.Collections.Generic;
using BiomeRivals.Content;

namespace BiomeRivals.Demo
{
    public enum DemoSlotKind
    {
        Unit,
        Building
    }

    public sealed class DemoLocalMatch
    {
        private readonly List<string> _hand = new List<string>();

        public IReadOnlyList<string> Hand => _hand;
        public string[] UnitSlots { get; } = new string[4];
        public string[] BuildingSlots { get; } = new string[3];
        public int Round { get; private set; } = 1;
        public int MaxEnergy { get; private set; } = 6;
        public int Energy { get; private set; } = 6;
        public bool IsPlayerTurn { get; private set; } = true;

        public void ResetHand(IEnumerable<string> cardIds)
        {
            if (cardIds == null) throw new ArgumentNullException(nameof(cardIds));
            _hand.Clear();
            _hand.AddRange(cardIds);
        }

        public bool TryDeploy(
            CardDefinitionEntry definition,
            DemoSlotKind slotKind,
            int slotIndex,
            out string message)
        {
            if (!CanPlay(definition, out message)) return false;

            if (definition.cardType == "UNIT")
            {
                if (slotKind != DemoSlotKind.Unit) return Fail("生物只能部署到单位格。", out message);
                if (!IsIndexFree(UnitSlots, slotIndex)) return Fail("这个单位格已经被占用。", out message);
                UnitSlots[slotIndex] = definition.id;
            }
            else if (definition.cardType == "BUILDING" || definition.cardType == "STRUCTURE")
            {
                if (slotKind != DemoSlotKind.Building) return Fail("建筑与结构只能部署到建筑格。", out message);
                var requiredSlots = Math.Max(1, definition.buildingSlots);
                if (slotIndex < 0 || slotIndex + requiredSlots > BuildingSlots.Length)
                    return Fail($"该结构需要连续 {requiredSlots} 个建筑格。", out message);
                for (var i = slotIndex; i < slotIndex + requiredSlots; i++)
                    if (!string.IsNullOrEmpty(BuildingSlots[i])) return Fail("所需建筑格并非全部空闲。", out message);
                for (var i = slotIndex; i < slotIndex + requiredSlots; i++) BuildingSlots[i] = definition.id;
            }
            else
            {
                return Fail("这张牌不是部署牌，请使用右侧的“释放”按钮。", out message);
            }

            Consume(definition);
            message = $"已部署：{definition.designId}";
            return true;
        }

        public bool TryCast(CardDefinitionEntry definition, out string message)
        {
            if (!CanPlay(definition, out message)) return false;
            if (definition.cardType == "UNIT" || definition.cardType == "BUILDING" || definition.cardType == "STRUCTURE")
                return Fail("部署牌需要先选择战场槽位。", out message);

            Consume(definition);
            message = $"已打出 {definition.designId}；规则效果将在后续版本接入。";
            return true;
        }

        public void EndPlayerTurn()
        {
            if (!IsPlayerTurn) return;
            IsPlayerTurn = false;
        }

        public void BeginNextPlayerTurn()
        {
            Round++;
            MaxEnergy = Math.Min(10, MaxEnergy + 1);
            Energy = MaxEnergy;
            IsPlayerTurn = true;
        }

        private bool CanPlay(CardDefinitionEntry definition, out string message)
        {
            if (definition == null) return Fail("卡牌定义不存在。", out message);
            if (!IsPlayerTurn) return Fail("当前是对手回合。", out message);
            if (!_hand.Contains(definition.id)) return Fail("该牌不在手牌中。", out message);
            if (definition.cost > Energy) return Fail("红石能量不足。", out message);
            message = string.Empty;
            return true;
        }

        private void Consume(CardDefinitionEntry definition)
        {
            Energy -= definition.cost;
            _hand.Remove(definition.id);
        }

        private static bool IsIndexFree(string[] slots, int index) =>
            index >= 0 && index < slots.Length && string.IsNullOrEmpty(slots[index]);

        private static bool Fail(string value, out string message)
        {
            message = value;
            return false;
        }
    }
}
