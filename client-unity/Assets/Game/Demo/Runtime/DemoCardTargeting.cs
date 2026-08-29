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
            string missingTargetMessage)
        {
            EffectId = effectId ?? throw new ArgumentNullException(nameof(effectId));
            Owner = owner;
            SlotKind = slotKind;
            TargetType = targetType ?? throw new ArgumentNullException(nameof(targetType));
            ActionLabel = actionLabel ?? throw new ArgumentNullException(nameof(actionLabel));
            SelectionPrompt = selectionPrompt ?? throw new ArgumentNullException(nameof(selectionPrompt));
            MissingTargetMessage = missingTargetMessage ?? throw new ArgumentNullException(nameof(missingTargetMessage));
        }

        public string EffectId { get; }
        public DemoTargetOwner Owner { get; }
        public DemoSlotKind SlotKind { get; }
        public string TargetType { get; }
        public string ActionLabel { get; }
        public string SelectionPrompt { get; }
        public string MissingTargetMessage { get; }

        public bool IsLegal(bool player, DemoSlotKind kind, DemoBattlefieldObject target) =>
            target != null && target.Health > 0 && kind == SlotKind && target.SlotKind == SlotKind &&
            player == (Owner == DemoTargetOwner.Friendly);
    }

    public static class DemoCardTargeting
    {
        private static readonly DemoCardTargetRule Snowball = new DemoCardTargetRule(
            "effect.si_001.01", DemoTargetOwner.Enemy, DemoSlotKind.Unit, "UNIT",
            "选择敌方目标", "请选择一个发光的敌方生物；右键或 Esc 取消。", "当前没有可选择的敌方生物。");

        private static readonly DemoCardTargetRule PowderSnowBucket = new DemoCardTargetRule(
            "effect.si_006.01", DemoTargetOwner.Enemy, DemoSlotKind.Unit, "UNIT",
            "选择缓慢目标", "请选择一个发出冰蓝光的敌方生物；右键或 Esc 取消。", "当前没有可施加缓慢的敌方生物。");

        private static readonly DemoCardTargetRule Bone = new DemoCardTargetRule(
            "effect.tk_009.01", DemoTargetOwner.Friendly, DemoSlotKind.Unit, "UNIT",
            "选择己方目标", "请选择一个发光的己方生物；右键或 Esc 取消。", "当前没有可选择的己方生物。");

        private static readonly DemoCardTargetRule Cobblestone = new DemoCardTargetRule(
            "effect.tk_010.01", DemoTargetOwner.Friendly, DemoSlotKind.Building, "BUILDING",
            "选择己方建筑", "请选择一个发光的己方建筑或结构；右键或 Esc 取消。", "当前没有可选择的己方建筑或结构。");

        public static bool TryGetRule(CardDefinitionEntry definition, out DemoCardTargetRule rule)
        {
            rule = null;
            if (definition?.effectIds == null) return false;
            foreach (var effectId in definition.effectIds)
            {
                switch (effectId)
                {
                    case "effect.si_001.01": rule = Snowball; return true;
                    case "effect.si_006.01": rule = PowderSnowBucket; return true;
                    case "effect.tk_009.01": rule = Bone; return true;
                    case "effect.tk_010.01": rule = Cobblestone; return true;
                }
            }
            return false;
        }

        public static bool HasLegalTarget(IDemoMatchView match, DemoCardTargetRule rule)
        {
            if (match == null) throw new ArgumentNullException(nameof(match));
            if (rule == null) return true;
            var battlefield = rule.Owner == DemoTargetOwner.Friendly ? match.PlayerBattlefield : match.OpponentBattlefield;
            foreach (var target in battlefield)
                if (target != null && target.Health > 0 && target.SlotKind == rule.SlotKind) return true;
            return false;
        }
    }
}
