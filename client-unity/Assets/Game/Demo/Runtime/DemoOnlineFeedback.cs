using System;
using System.Collections.Generic;
using BiomeRivals.Networking;

namespace BiomeRivals.Demo
{
    // Player copy is a closed registration, never a raw exception/JSON/credential dump.
    // scripts/test-player-feedback-contract.ps1 checks the authoritative literal reasons.
    public static class DemoOnlineFeedback
    {
        public const string ConnectionFailureText = "连接失败。\n\n请检查网络与服务器地址，然后点击匹配重试。";
        private sealed class Reason
        {
            public readonly string Code, Source, Text;
            public Reason(string code, string source, string text) { Code = code; Source = source; Text = text; }
        }

        private static readonly Reason[] Reasons =
        {
            new Reason("ATTACK_ALREADY_USED", "hero has already attacked this turn", "英雄本回合已经攻击过，不能再次攻击。"),
            new Reason("ATTACK_ALREADY_USED", "unit has already attacked this turn", "该生物本回合已经攻击过，不能再次攻击。"),
            new Reason("ATTACKER_NOT_READY", "unit cannot attack on its summoned turn without CHARGE", "该生物刚入场，且没有冲锋；请等到你的下一回合。"),
            new Reason("ATTACKER_NOT_READY", "unit cannot attack while slowed", "该生物处于缓慢状态，当前不能攻击。"),
            new Reason("CARD_NOT_IN_HAND", "card is not in the active players hand", "所选手牌已不在你的手中；请重新选择。"),
            new Reason("CARD_NOT_IN_HAND", "card is not in the actor hand", "所选手牌已不在你的手中；请重新选择。"),
            new Reason("CARD_NOT_PLAYABLE", "card resolves automatically and cannot be deployed", "这张卡自动结算，不能从手牌部署。"),
            new Reason("CARD_NOT_PLAYABLE", "card resolves automatically and cannot be played", "这张卡自动结算，不能从手牌打出。"),
            new Reason("CHOICE_REQUIRED", "only the choice owner may resolve it", "这是对手的待决选择；请等待对手完成。"),
            new Reason("CHOICE_REQUIRED", "the pending card choice must be resolved before another action", "请先完成当前的卡牌选择，再进行其他操作。"),
            new Reason("DUPLICATE_COMMAND", "command was already processed", "此操作已经处理；请等待状态同步，无需重复提交。"),
            new Reason("EFFECT_NOT_IMPLEMENTED", "card effect is registered but not implemented", "该卡牌效果尚未接入，当前不能使用。"),
            new Reason("EFFECT_NOT_IMPLEMENTED", "effect handler is not registered", "该卡牌效果尚未接入，当前不能使用。"),
            new Reason("INSUFFICIENT_REDSTONE", "not enough redstone", "红石不足；请选择费用更低的卡牌，或等待下一回合补充。"),
            new Reason("INVALID_ATTACKER", "attacker must be a living friendly unit or HERO", "请选择仍在场的己方生物，或已装备武器的英雄。"),
            new Reason("INVALID_ATTACKER", "attacker must have attack", "该生物没有可用攻击力，当前不能攻击。"),
            new Reason("INVALID_ATTACKER", "hero requires a usable equipment card to attack", "英雄需要一件仍有耐久的武器才能攻击。"),
            new Reason("INVALID_CHOICE", "prismarine shard movement requires a destination", "海晶碎片需要选择一个合法的相邻空格作为目的地。"),
            new Reason("INVALID_CHOICE", "RESOLVE_CHOICE requires the current choiceId and an integer selectedOptionIndex", "这个选择已经变化；请使用当前显示的选项。"),
            new Reason("INVALID_CHOICE", "the selected healing target is not available", "该治疗目标已不可选；请重新选择发光的受伤单位。"),
            new Reason("INVALID_CHOICE", "the selected movement destination is not available", "该移动地块已不可用；请选择当前发光的目的地。"),
            new Reason("INVALID_CHOICE", "the selected option is not a buried card", "该选项不是可出土的掩埋牌；请选择标记的掩埋牌。"),
            new Reason("INVALID_CHOICE", "there is no pending card choice", "当前没有待决选择，原选择可能已经完成。"),
            new Reason("INVALID_CHOICE", "this inspection has no selectable buried card", "本次查看没有可出土的掩埋牌；请结束查看。"),
            new Reason("INVALID_CHOICE", "top-card scry must keep the card or move it to the deck bottom", "请选择保留牌库顶牌，或将它放到牌库底。"),
            new Reason("INVALID_COMMAND", "ATTACK requires an object payload", "攻击指令不完整；请取消选择后重新指定攻击者与目标。"),
            new Reason("INVALID_COMMAND", "ATTACK requires attackerInstanceId and a valid target", "攻击指令缺少有效目标；请重新指定攻击者与目标。"),
            new Reason("INVALID_COMMAND", "command is incomplete", "操作指令不完整；请取消选择后重试。"),
            new Reason("INVALID_COMMAND", "DEPLOY_CARD requires an object payload", "部署指令不完整；请重新选择手牌与地块。"),
            new Reason("INVALID_COMMAND", "DEPLOY_CARD requires cardId, slotKind, integer slotIndex and paymentMethod", "部署参数不完整；请重新选择手牌、地块和支付方式。"),
            new Reason("INVALID_COMMAND", "mulligan indices must be unique positions in the opening hand", "起手调度不能重复选择同一张手牌；请重新选择。"),
            new Reason("INVALID_COMMAND", "MULLIGAN requires an array of opening hand indices", "起手调度选择无效；请重新勾选要更换的手牌。"),
            new Reason("INVALID_COMMAND", "PLAY_CARD requires cardId", "出牌指令不完整；请重新选择手牌。"),
            new Reason("INVALID_COMMAND", "unknown command type", "当前客户端不支持此操作；请更新客户端后重试。"),
            new Reason("INVALID_COMMAND", "DEPLOY_CARD and PLAY_CARD require a valid handCardInstanceId", "所选手牌副本无效；请等待同步后重新选择手牌。"),
            new Reason("INVALID_COMMAND", "DEPLOY_CARD requires a valid handCardInstanceId", "所选部署手牌副本无效；请等待同步后重新选择手牌。"),
            new Reason("INVALID_COMMAND", "PLAY_CARD requires a valid handCardInstanceId", "所选手牌副本无效；请等待同步后重新选择手牌。"),
            new Reason("INVALID_COMMAND", "malformed JSON command", "操作数据无法识别；请重新连接后重试。"),
            new Reason("INVALID_COMMAND", "malformed test fixture request", "验收操作数据无法识别；请重新启动验收流程。"),
            new Reason("INVALID_PAYMENT_METHOD", "card does not have a crafting recipe", "这张卡没有合成配方，请使用红石支付。"),
            new Reason("INVALID_STATE", "crafted product was removed while consuming materials", "合成过程中的对局数据不一致；请等待状态恢复，不要重复支付。"),
            new Reason("INVALID_STATE", "equipment requires positive attack and durability", "这件装备的攻击或耐久数据无效；请更新客户端与服务器。"),
            new Reason("INVALID_TARGET", "breeding season requires two different friendly Animal targets", "繁殖季节需要两个不同的己方动物目标。"),
            new Reason("INVALID_TARGET", "breeding season targets must be living friendly Animals", "繁殖季节只能选择仍在场的己方动物。"),
            new Reason("INVALID_TARGET", "building cards require enough consecutive building slots", "这座建筑需要足够的连续建筑格；请选择能容纳完整占格的位置。"),
            new Reason("INVALID_TARGET", "card type cannot be deployed to the battlefield", "这张卡不能部署到战场，请使用详情中的出牌按钮。"),
            new Reason("INVALID_TARGET", "cobblestone requires a friendly building target", "圆石需要选择一座己方建筑。"),
            new Reason("INVALID_TARGET", "cobblestone target must be a living friendly building or structure", "圆石只能修复仍在场的己方建筑或结构。"),
            new Reason("INVALID_TARGET", "darkness restricts the first enemy battlefield target to a legal row edge", "黑暗限制了首次指定敌方场上目标；请选择所在排的合法边缘目标。"),
            new Reason("INVALID_TARGET", "drowned battlecry target must be a living enemy unit", "溺尸战吼只能指定仍在场的敌方生物。"),
            new Reason("INVALID_TARGET", "drowned requires an enemy unit target when deployed beside an aquatic unit", "溺尸部署在水生友军旁时，需要先选择一个敌方生物作为战吼目标。"),
            new Reason("INVALID_TARGET", "ender dragon avatar cannot be returned by its controller's spell", "末影龙化身不能被控制者自己的法术回手；材料不受这条限制。"),
            new Reason("INVALID_TARGET", "goat battlecry requires the opposite adjacent unit slot to be empty", "山羊跳跃需要另一侧的相邻单位格为空；请选择其他目标或跳过战吼。"),
            new Reason("INVALID_TARGET", "goat battlecry target must be a living adjacent friendly unit", "山羊战吼需要选择仍在场的相邻己方生物。"),
            new Reason("INVALID_TARGET", "goat battlecry target must be a living friendly unit", "山羊战吼只能选择仍在场的己方生物。"),
            new Reason("INVALID_TARGET", "PLAY_CARD accepts spells, materials, and equipment", "生物和建筑应部署到合法地块，不能作为法术释放。"),
            new Reason("INVALID_TARGET", "prismarine shard requires a friendly aquatic unit with an adjacent empty slot", "海晶碎片需要一个相邻有空格的己方水生单位。"),
            new Reason("INVALID_TARGET", "stray battlecry target must be a living enemy unit", "流浪者战吼只能指定仍在场的敌方生物。"),
            new Reason("INVALID_TARGET", "stray requires an enemy unit battlecry target", "部署流浪者前，请先选择一个敌方生物作为战吼目标。"),
            new Reason("INVALID_TARGET", "strider battlecry target must be a living burning friendly character", "炽足兽战吼只能选择仍在场、处于着火状态的己方角色。"),
            new Reason("INVALID_TARGET", "strider requires a burning friendly character while one is available", "有着火友军时，炽足兽需要先选择其中一个作为战吼目标。"),
            new Reason("INVALID_TARGET", "target is not a living enemy object of the requested type", "目标已不在场，或目标类型不符合要求；请重新选择。"),
            new Reason("INVALID_TARGET", "unit cards require a valid unit slot", "生物卡只能部署到合法的己方单位格。"),
            new Reason("INVALID_TARGET", "material requires a friendly unit target", "这张材料需要选择一个己方生物目标。"),
            new Reason("INVALID_TARGET", "blaze rod requires an enemy unit target", "烈焰棒需要选择一个敌方生物目标。"),
            new Reason("INVALID_TARGET", "snow spell requires an enemy unit target", "雪球或粉雪桶需要选择一个敌方生物目标。"),
            new Reason("INVALID_TARGET", "material target must be a living friendly unit", "这张材料只能选择仍在场的己方生物。"),
            new Reason("INVALID_TARGET", "blaze rod target must be a living enemy unit", "烈焰棒只能选择仍在场的敌方生物。"),
            new Reason("INVALID_TARGET", "snow spell target must be a living enemy unit", "雪球或粉雪桶只能选择仍在场的敌方生物。"),
            new Reason("MATCH_FINISHED", "match has finished", "对局已经结束，所有战场操作均已锁定。"),
            new Reason("MISSING_MATERIALS", "crafting recipe materials are missing from hand", "手牌中的合成材料不足；请补齐配方材料，或改用红石支付。"),
            new Reason("MULLIGAN_ALREADY_COMPLETED", "the opening hand phase has ended", "起手调度已经结束，不能再次更换起手。"),
            new Reason("MULLIGAN_ALREADY_COMPLETED", "this player already confirmed an opening hand", "你已确认起手；请等待对手确认。"),
            new Reason("MULLIGAN_REQUIRED", "both players must confirm their opening hands first", "双方都需确认起手后，才能开始战场操作。"),
            new Reason("NOT_A_PLAYER", "actor does not belong to this match", "当前账号不属于这个对局；请重新连接自己的对局。"),
            new Reason("NOT_ACTIVE_PLAYER", "only the active player may attack", "当前是对手的回合，请等待你的回合再攻击。"),
            new Reason("NOT_ACTIVE_PLAYER", "only the active player may deploy a card", "当前是对手的回合，请等待你的回合再部署。"),
            new Reason("NOT_ACTIVE_PLAYER", "only the active player may end the turn", "当前是对手的回合，不能替对手结束回合。"),
            new Reason("NOT_ACTIVE_PLAYER", "only the active player may enter combat", "当前是对手的回合，请等待你的回合再进入战斗。"),
            new Reason("NOT_ACTIVE_PLAYER", "only the active player may play a card", "当前是对手的回合，请等待你的回合再出牌。"),
            new Reason("PROTOCOL_MISMATCH", "unsupported protocol version", "客户端与服务器的通信版本不一致；请更新两端后重连。"),
            new Reason("REVISION_MISMATCH", "client state is stale", "对局状态已更新；请等待同步后重新选择，不要连续重复操作。"),
            new Reason("RULESET_MISMATCH", "ruleset version differs from match", "客户端与对局的规则版本不一致；请更新两端后重连。"),
            new Reason("SLOT_OCCUPIED", "required building slots are occupied", "所需的建筑格已被占用；请选择另一段连续空建筑格。"),
            new Reason("SLOT_OCCUPIED", "unit slot is occupied", "这个单位格已被占用；请选择一个空单位格。"),
            new Reason("TAUNT_TARGET_REQUIRED", "a legal enemy TAUNT object must be attacked first", "嘲讽生效：必须先攻击带嘲讽的合法敌方目标。"),
            new Reason("UNKNOWN_CARD", "card definition is not registered", "当前版本未登记这张卡牌；请更新两端后重连。"),
            new Reason("UNKNOWN_CARD", "card is not registered", "当前版本未登记这张卡牌；请更新两端后重连。"),
            new Reason("WRONG_PHASE", "attacks require the combat phase", "主行动阶段不能攻击；请先进入战斗阶段。"),
            new Reason("WRONG_PHASE", "cards may only be deployed during the main phase", "只能在主行动阶段部署卡牌；请等待下一次主行动阶段。"),
            new Reason("WRONG_PHASE", "cards may only be played during the main phase", "只能在主行动阶段打出卡牌；请等待下一次主行动阶段。"),
            new Reason("WRONG_PHASE", "match is not in the main phase", "当前不能再次进入战斗；请按当前阶段继续操作。")
        };

        private static readonly Dictionary<string, string> Details = BuildDetails();
        private static Dictionary<string, string> BuildDetails()
        {
            var result = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var reason in Reasons) result.Add(reason.Code + "|" + reason.Source, reason.Text);
            return result;
        }

        public static bool TryGetRegisteredReason(string code, string message, out string playerText) =>
            Details.TryGetValue((code ?? string.Empty) + "|" + (message ?? string.Empty), out playerText);

        public static string FormatCommand(MatchCommandDispatchResult result)
        {
            if (result.Outcome == MatchCommandOutcome.Accepted) return "操作已确认。";
            if (result.Outcome == MatchCommandOutcome.TimedOut)
                return "操作结果待确认。\n\n服务器暂未回复；请等待状态同步，或恢复连接后查看当前对局。不要连续重复提交。";
            if (result.Outcome == MatchCommandOutcome.TransportFailed)
                return "连接中断，操作结果待确认。\n\n请先恢复连接并等待对局状态同步，再决定是否重新操作。";
            if (TryGetRegisteredReason(result.Code, result.Message, out var detail))
                return result.Code == "DUPLICATE_COMMAND" ? detail : "操作未生效。\n\n" + detail;
            var fallback = CodeFallback(result.Code);
            return result.Code == "DUPLICATE_COMMAND" ? fallback : "操作未生效。\n\n" + fallback;
        }

        private static string CodeFallback(string code)
        {
            switch (code)
            {
                case "INSUFFICIENT_REDSTONE": return "红石不足；请选择其他卡牌，或等待下一回合补充。";
                case "SLOT_OCCUPIED": return "目标地块已被占用；请选择其他合法空格。";
                case "INVALID_TARGET": return "目标不符合当前操作要求；请重新选择发光的合法目标。服务器返回了尚未识别的目标说明。";
                case "MISSING_MATERIALS": return "合成材料不足；请补齐配方材料，或改用红石支付。";
                case "REVISION_MISMATCH": case "STALE_OBSERVATION": return "对局状态已变化；请等待同步后重新选择。";
                case "DUPLICATE_COMMAND": return "此操作已经处理；请等待状态同步，无需重复提交。";
                case "MATCH_FINISHED": return "对局已经结束，所有战场操作均已锁定。";
                case "NOT_READY": case "NOT_CONNECTED": return "对局尚未准备好；请等待连接与状态同步完成。";
                case "MATCH_MISMATCH": case "NOT_A_PLAYER": return "当前操作不属于这个对局；请重新连接自己的对局。";
                case "PROTOCOL_MISMATCH": case "RULESET_MISMATCH": case "UNKNOWN_CARD": return "两端版本不兼容；请更新客户端与服务器后重连。";
                case "CHOICE_REQUIRED": return "请先完成当前待决选择，再进行其他操作。";
                case "INVALID_CHOICE": return "当前选择已变化或不可用；请使用最新显示的选项。";
                case "NOT_ACTIVE_PLAYER": return "当前是对手的回合，请等待你的回合。";
                case "WRONG_PHASE": return "当前阶段不允许此操作；请按当前阶段继续。";
                case "CARD_NOT_IN_HAND": return "所选手牌已不在手中；请重新选择。";
                case "CARD_NOT_PLAYABLE": return "这张卡自动结算，不能从手牌打出。";
                case "INVALID_PAYMENT_METHOD": return "这张卡不支持所选支付方式；请重新选择支付方式。";
                case "TAUNT_TARGET_REQUIRED": return "嘲讽生效：必须先攻击带嘲讽的合法敌方目标。";
                case "INVALID_ATTACKER": return "攻击者不可用；请选择有攻击力的己方生物，或有武器的英雄。";
                case "ATTACKER_NOT_READY": return "攻击者尚未就绪；请检查召唤回合、冲锋与缓慢状态。";
                case "ATTACK_ALREADY_USED": return "该攻击者本回合已经攻击过。";
                case "EFFECT_NOT_IMPLEMENTED": return "该效果尚未接入，当前不能使用。";
                case "MULLIGAN_REQUIRED": return "请等待双方确认起手后再操作。";
                case "MULLIGAN_ALREADY_COMPLETED": return "起手调度已经确认或结束，不能重复确认。";
                case "INVALID_STATE": return "对局数据暂不一致；请等待状态恢复，不要重复支付。";
                case "INVALID_COMMAND": case "INVALID_PAYLOAD": case "UNKNOWN_ACTION": return "操作参数无法识别；请取消选择后重试，仍失败时重新连接。";
                default: return "服务器返回了尚未识别的拒绝说明；请取消选择后重试，仍失败时重新连接。";
            }
        }

        public static string FormatException(Exception exception, bool connecting)
        {
            if (exception is ServerCompatibilityException compatibility) return compatibility.Failure.UserDetails;
            if (connecting && exception is OperationCanceledException) return "连接已取消。需要继续时，请重新匹配。";
            return connecting ? ConnectionFailureText
                : "操作结果待确认。\n\n连接或发送过程发生异常；请恢复连接并等待对局状态同步，不要连续重复提交。";
        }

        public static string FormatConnectionPhase(MatchConnectionStatus status)
        {
            switch (status.Phase)
            {
                case MatchConnectionPhase.Offline: return "已返回本地模式。需要联机时，请点击匹配。";
                case MatchConnectionPhase.Authenticating: return "正在校验版本并确认登录身份，请稍候。";
                case MatchConnectionPhase.Connecting: return "正在连接服务器，请稍候。";
                case MatchConnectionPhase.Matchmaking: return "正在寻找对手；可以点击取消返回本地模式。";
                case MatchConnectionPhase.Joining: return "正在进入对局并同步状态，请稍候。";
                case MatchConnectionPhase.Ready: return status.CanSendCommands
                    ? "权威对局已连接。请按当前回合与阶段操作。" : "连接已建立，正在等待对局状态就绪。";
                case MatchConnectionPhase.Reconnecting: return "连接中断，正在自动重连。请等待对局状态同步，不要重复提交操作。";
                case MatchConnectionPhase.Disconnecting: return "正在断开连接，请稍候。";
                default: return ConnectionFailureText;
            }
        }
    }
}
