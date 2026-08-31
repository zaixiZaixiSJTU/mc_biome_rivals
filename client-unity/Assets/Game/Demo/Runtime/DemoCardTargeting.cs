using System;
using BiomeRivals.Content;

namespace BiomeRivals.Demo
{
    public enum DemoTargetOwner
    {
        Friendly,
        Enemy
    }

    public sealed class DemoCardTargetRule
    {
        public DemoCardTargetRule(
            string effectId,
            DemoTargetOwner owner,
            DemoSlotKind slotKind,
            string targetType,
            string actionLabel,
            string selectionPrompt,
            string missingTargetMessage,
            Func<IDemoMatchView, DemoBattlefieldObject, bool> additionalValidation = null,
            int requiredTargetCount = 1)
        {
            EffectId = effectId ?? throw new ArgumentNullException(nameof(effectId));
            Owner = owner;
            SlotKind = slotKind;
            TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
            ActionLabel = actionLabel ?? throw new ArgumentNullException(nameof(actionLabel));
            SelectionPrompt = selectionPrompt ?? throw new ArgumentNullException(nameof(selectionPrompt));
            MissingTargetMessage = missingTargetMessage ?? throw new ArgumentNullException(nameof(missingTargetMessage));
            AdditionalValidation = additionalValidation;
            RequiredTargetCount = Math.Max(1, requiredTargetCount);
        }

        public string EffectId { get; }
        public DemoTargetOwner Owner { get; }
        public DemoSlotKind SlotKind { get; }
        public string TargetType { get; }
        public string ActionLabel { get; }
        public string SelectionPrompt { get; }
        public string MissingTargetMessage { get; }
        public int RequiredTargetCount { get; }
        private Func<IDemoMatchView, DemoBattlefieldObject, bool> AdditionalValidation { get; }

        public bool IsLegal(IDemoMatchView match, bool player, DemoSlotKind kind, DemoBattlefieldObject target) =>
            target != null && target.Health > 0 && kind == SlotKind && target.SlotKind == SlotKind &&
            player == (Owner == DemoTargetOwner.Friendly) &&
            (AdditionalValidation == null || AdditionalValidation(match, target));
    }

    public static class DemoCardTargeting
    {
        private static readonly DemoCardTargetRule Snowball = new DemoCardTargetRule(
            "effect.si_001.01", DemoTargetOwner.Enemy, DemoSlotKind.Unit, "UNIT",
            "选择敌方目标", "请选择一个发光的敌方生物；右键或 Esc 取消。", "当前没有可选择的敌方生物。");

        private static readonly DemoCardTargetRule PowderSnowBucket = new DemoCardTargetRule(
            "effect.si_006.01", DemoTargetOwner.Enemy, DemoSlotKind.Unit, "UNIT",
            "选择缓慢目标", "请选择一个发出冰蓝光的敌方生物；右键或 Esc 取消。", "当前没有可施加缓慢的敌方生物。");

        private static readonly DemoCardTargetRule Stray = new DemoCardTargetRule(
            "effect.si_003.01", DemoTargetOwner.Enemy, DemoSlotKind.Unit, "UNIT",
            "选择战吼目标", "先选择一个发出冰蓝光的敌方生物，再选择己方部署格。", "当前没有可施加缓慢的敌方生物。");

        private static readonly DemoCardTargetRule Drowned = new DemoCardTargetRule(
            "effect.or_003.01", DemoTargetOwner.Enemy, DemoSlotKind.Unit, "UNIT",
            "选择战吼目标", "先选择一个发出金光的敌方生物；部署到水生友军相邻格时造成 1 点伤害。", "当前没有可选择的敌方生物。");

        private static readonly DemoCardTargetRule Bone = new DemoCardTargetRule(
            "effect.tk_009.01", DemoTargetOwner.Friendly, DemoSlotKind.Unit, "UNIT",
            "选择己方目标", "请选择一个发光的己方生物；右键或 Esc 取消。", "当前没有可选择的己方生物。");

        private static readonly DemoCardTargetRule Cobblestone = new DemoCardTargetRule(
            "effect.tk_010.01", DemoTargetOwner.Friendly, DemoSlotKind.Building, "BUILDING",
            "选择己方建筑", "请选择一个发光的己方建筑或结构；右键或 Esc 取消。", "当前没有可选择的己方建筑或结构。");

        private static readonly DemoCardTargetRule PrismarineShard = new DemoCardTargetRule(
            "effect.tk_012.01", DemoTargetOwner.Friendly, DemoSlotKind.Unit, "UNIT",
            "选择水生目标", "请选择一个拥有相邻空格的己方水生生物；右键或 Esc 取消。", "当前没有可移动的己方水生生物。",
            (match, target) => HasRegisteredTag(target, "aquatic") && HasAdjacentEmptyUnitSlot(match, target));

        private static readonly DemoCardTargetRule BreedingSeason = new DemoCardTargetRule(
            "effect.pf_006.01", DemoTargetOwner.Friendly, DemoSlotKind.Unit, "UNIT",
            "选择两个己方动物", "请选择两个发光的己方动物；已选目标会变为金色。", "场上至少需要两个己方动物。",
            (match, target) => HasRegisteredTag(target, "animal"), 2);

        public static bool TryGetRule(CardDefinitionEntry definition, out DemoCardTargetRule rule)
        {
            rule = null;
            if (definition?.effectIds == null) return false;
            foreach (var effectId in definition.effectIds)
            {
                switch (effectId)
                {
                    case "effect.si_001.01": rule = Snowball; return true;
                    case "effect.si_003.01": rule = Stray; return true;
                    case "effect.si_006.01": rule = PowderSnowBucket; return true;
                    case "effect.or_003.01": rule = Drowned; return true;
                    case "effect.tk_009.01": rule = Bone; return true;
                    case "effect.tk_010.01": rule = Cobblestone; return true;
                    case "effect.tk_012.01": rule = PrismarineShard; return true;
                    case "effect.pf_006.01": rule = BreedingSeason; return true;
                }
            }
            return false;
        }

        public static bool HasLegalTarget(IDemoMatchView match, DemoCardTargetRule rule)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (rule == null) return true;
            var battlefield = rule.Owner == DemoTargetOwner.Friendly ? match.PlayerBattlefield : match.OpponentBattlefield;
            var legalTargetCount = 0;
            foreach (var target in battlefield)
            {
                if (!rule.IsLegal(match, rule.Owner == DemoTargetOwner.Friendly, rule.SlotKind, target)) continue;
                legalTargetCount++;
                if (legalTargetCount >= rule.RequiredTargetCount) return true;
            }
            return false;
        }

        private static bool HasRegisteredTag(DemoBattlefieldObject target, string tag)
        {
            if (target?.HasTag(tag) == true) return true;
            return target != null && CardContentLoader.Current.TryGetDefinition(target.CardId, out var definition) &&
                Array.IndexOf(definition.tags ?? Array.Empty<string>(), tag) >= 0;
        }

        private static bool HasAdjacentEmptyUnitSlot(IDemoMatchView match, DemoBattlefieldObject target)
        {
            if (match == null || target == null) return false;
            var slots = target.Player ? match.UnitSlots : match.OpponentUnitSlots;
            foreach (var index in new[] { target.SlotIndex - 1, target.SlotIndex + 1 })
                if (index >= 0 && index < slots.Length && string.IsNullOrEmpty(slots[index])) return true;
            return false;
        }
    }
}
