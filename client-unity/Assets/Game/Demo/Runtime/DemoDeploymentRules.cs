using System;
using System.Linq;
using BiomeRivals.Content;

namespace BiomeRivals.Demo
{
    public readonly struct DemoDeploymentPreview
    {
        public DemoDeploymentPreview(bool isLegal, int occupiedSlots, string message)
        {
            IsLegal = isLegal;
            OccupiedSlots = Math.Max(1, occupiedSlots);
            Message = message ?? string.Empty;
        }

        public bool IsLegal { get; }
        public int OccupiedSlots { get; }
        public string Message { get; }
    }

    public static class DemoDeploymentRules
    {
        public static DemoDeploymentPreview Evaluate(
            IDemoMatchView match,
            CardDefinitionEntry definition,
            DemoSlotKind slotKind,
            int slotIndex)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (definition == null) return Reject(1, "卡牌定义不存在。");

            var deploysToUnits = string.Equals(definition.cardType, "UNIT", StringComparison.Ordinal);
            var deploysToBuildings = string.Equals(definition.cardType, "BUILDING", StringComparison.Ordinal) ||
                                     string.Equals(definition.cardType, "STRUCTURE", StringComparison.Ordinal);
            var occupiedSlots = deploysToBuildings ? Math.Max(1, definition.buildingSlots) : 1;

            if (!deploysToUnits && !deploysToBuildings)
                return Reject(occupiedSlots, "这张牌不是战场部署牌。");
            if (!match.IsPlayerTurn) return Reject(occupiedSlots, "当前是对手回合。");
            if (match.Phase != DemoTurnPhase.Main) return Reject(occupiedSlots, "进入战斗阶段后不能继续部署卡牌。");
            if (!match.Hand.Contains(definition.id)) return Reject(occupiedSlots, "该牌不在手牌中。");
            if (definition.cost > match.Energy) return Reject(occupiedSlots, "红石能量不足。");

            if (deploysToUnits && slotKind != DemoSlotKind.Unit)
                return Reject(occupiedSlots, "生物只能部署到单位格。");
            if (deploysToBuildings && slotKind != DemoSlotKind.Building)
                return Reject(occupiedSlots, "建筑与结构只能部署到建筑格。");

            var slots = slotKind == DemoSlotKind.Unit ? match.UnitSlots : match.BuildingSlots;
            if (slots == null || slotIndex < 0 || slotIndex + occupiedSlots > slots.Length)
                return Reject(occupiedSlots, deploysToBuildings
                    ? $"该结构需要连续 {occupiedSlots} 个建筑格。"
                    : "生物只能部署到有效的单位格。");

            for (var index = slotIndex; index < slotIndex + occupiedSlots; index++)
                if (!string.IsNullOrEmpty(slots[index]))
                    return Reject(occupiedSlots, occupiedSlots > 1
                        ? $"从建筑格 {slotIndex + 1} 开始的 {occupiedSlots} 格范围并非全部空闲。"
                        : "这个战场格已经被占用。");

            return new DemoDeploymentPreview(true, occupiedSlots, occupiedSlots > 1
                ? $"可部署：将连续占用建筑格 {slotIndex + 1}—{slotIndex + occupiedSlots}。"
                : "可部署到这个战场格。");
        }

        private static DemoDeploymentPreview Reject(int occupiedSlots, string message) =>
            new DemoDeploymentPreview(false, occupiedSlots, message);
    }
}
