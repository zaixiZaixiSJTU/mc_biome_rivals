using System;
using System.Collections.Generic;
using System.Linq;
using BiomeRivals.Content;
using BiomeRivals.Core;

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
            int slotIndex,
            string paymentMethod = MatchPaymentMethods.Redstone)
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

            if (paymentMethod == MatchPaymentMethods.Redstone)
            {
                if (definition.cost > match.Energy) return Reject(occupiedSlots, "红石能量不足。");
            }
            else if (paymentMethod == MatchPaymentMethods.Crafting)
            {
                if (!CanPayWithCrafting(match, definition, out var missingMaterials))
                    return Reject(occupiedSlots, missingMaterials);
            }
            else return Reject(occupiedSlots, "部署支付方式无效。");

            return new DemoDeploymentPreview(true, occupiedSlots, occupiedSlots > 1
                ? $"可部署：将连续占用建筑格 {slotIndex + 1}—{slotIndex + occupiedSlots}。"
                : "可部署到这个战场格。");
        }

        private static DemoDeploymentPreview Reject(int occupiedSlots, string message) =>
            new DemoDeploymentPreview(false, occupiedSlots, message);

        public static bool CanPayWithCrafting(
            IDemoMatchView match,
            CardDefinitionEntry definition,
            out string message)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (definition == null || !definition.hasCraftingRecipe ||
                definition.craftingRecipe == null || definition.craftingRecipe.Length == 0)
            {
                message = "这张牌没有已注册的合成配方。";
                return false;
            }

            var available = new List<string>(match.Hand ?? Array.Empty<string>());
            var productIndex = available.IndexOf(definition.id);
            if (productIndex < 0)
            {
                message = "成品卡不在手牌中。";
                return false;
            }
            available.RemoveAt(productIndex);
            var missing = new List<string>();
            foreach (var ingredient in definition.craftingRecipe)
            {
                if (ingredient == null || string.IsNullOrWhiteSpace(ingredient.cardId) || ingredient.count < 1)
                {
                    message = "合成配方数据无效。";
                    return false;
                }
                var missingCount = 0;
                for (var count = 0; count < ingredient.count; count++)
                {
                    var index = available.IndexOf(ingredient.cardId);
                    if (index < 0) missingCount++;
                    else available.RemoveAt(index);
                }
                if (missingCount > 0) missing.Add($"{ingredient.cardId}×{missingCount}");
            }

            message = missing.Count == 0 ? string.Empty : "缺少材料：" + string.Join(" + ", missing);
            return missing.Count == 0;
        }
    }
}
