using System;
using System.Collections.Generic;
using BiomeRivals.Content;
using BiomeRivals.Core;

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
        private readonly HashSet<string> _processedCommandIds = new HashSet<string>(StringComparer.Ordinal);
        private int _nextLocalCommandId = 1;

        public IReadOnlyList<string> Hand => _hand;
        public string[] UnitSlots { get; } = new string[4];
        public string[] BuildingSlots { get; } = new string[3];
        public int Round { get; private set; } = 1;
        public int MaxEnergy { get; private set; } = 6;
        public int Energy { get; private set; } = 6;
        public bool IsPlayerTurn { get; private set; } = true;
        public int Revision { get; private set; }

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
            var cardId = definition == null ? string.Empty : definition.id;
            var command = CreateDeployCommand(cardId, slotKind, slotIndex);
            var result = ApplyDeploy(definition, command);
            message = result.Message;
            return result.Accepted;
        }

        public MatchCommandDto CreateDeployCommand(string cardId, DemoSlotKind slotKind, int slotIndex) =>
            MatchCommandFactory.DeployCard(NextCommandId(), Revision, cardId, slotKind == DemoSlotKind.Unit ? "UNIT" : "BUILDING", slotIndex);

        public DemoCommandResult ApplyDeploy(CardDefinitionEntry definition, MatchCommandDto command)
        {
            if (!ValidateCommand(command, MatchCommandTypes.DeployCard, out var rejection)) return rejection;
            if (!CanPlay(definition, out var message)) return RejectFromMessage(message, definition);
            if (command.payload == null || !string.Equals(command.payload.cardId, definition.id, StringComparison.Ordinal))
                return Reject(DemoCommandRejectionCode.UnknownCard, "命令中的卡牌与注册定义不一致。");
            if (definition.cardType == "UNIT")
            {
                if (!string.Equals(command.payload.slotKind, "UNIT", StringComparison.Ordinal) || command.payload.slotIndex < 0 || command.payload.slotIndex >= UnitSlots.Length)
                    return Reject(DemoCommandRejectionCode.InvalidTarget, "生物只能部署到有效的单位格。");
                if (!IsIndexFree(UnitSlots, command.payload.slotIndex))
                    return Reject(DemoCommandRejectionCode.SlotOccupied, "这个单位格已经被占用。");
                UnitSlots[command.payload.slotIndex] = definition.id;
            }
            else if (definition.cardType == "BUILDING" || definition.cardType == "STRUCTURE")
            {
                if (!string.Equals(command.payload.slotKind, "BUILDING", StringComparison.Ordinal))
                    return Reject(DemoCommandRejectionCode.InvalidTarget, "建筑与结构只能部署到建筑格。");
                var requiredSlots = Math.Max(1, definition.buildingSlots);
                if (command.payload.slotIndex < 0 || command.payload.slotIndex + requiredSlots > BuildingSlots.Length)
                    return Reject(DemoCommandRejectionCode.InvalidTarget, $"该结构需要连续 {requiredSlots} 个建筑格。");
                for (var i = command.payload.slotIndex; i < command.payload.slotIndex + requiredSlots; i++)
                    if (!string.IsNullOrEmpty(BuildingSlots[i]))
                        return Reject(DemoCommandRejectionCode.SlotOccupied, "所需建筑格并非全部空闲。");
                for (var i = command.payload.slotIndex; i < command.payload.slotIndex + requiredSlots; i++) BuildingSlots[i] = definition.id;
            }
            else
            {
                return Reject(DemoCommandRejectionCode.InvalidTarget, "这张牌不是部署牌，请使用右侧的“释放”按钮。");
            }

            Consume(definition);
            AcceptCommand(command);
            return DemoCommandResult.Accept($"已部署：{definition.designId}", Revision);
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
            ApplyEndTurn(MatchCommandFactory.EndTurn(NextCommandId(), Revision));
        }

        public DemoCommandResult ApplyEndTurn(MatchCommandDto command)
        {
            if (!ValidateCommand(command, MatchCommandTypes.EndTurn, out var rejection)) return rejection;
            if (!IsPlayerTurn) return Reject(DemoCommandRejectionCode.NotActivePlayer, "当前不是你的回合。");
            IsPlayerTurn = false;
            AcceptCommand(command);
            return DemoCommandResult.Accept("已结束回合。", Revision);
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

        private bool ValidateCommand(MatchCommandDto command, string expectedType, out DemoCommandResult rejection)
        {
            if (command == null || string.IsNullOrWhiteSpace(command.commandId) || !string.Equals(command.type, expectedType, StringComparison.Ordinal) ||
                command.protocolVersion != GameVersions.Protocol ||
                !string.Equals(command.rulesetVersion, GameVersions.Ruleset, StringComparison.Ordinal))
            {
                rejection = Reject(DemoCommandRejectionCode.InvalidCommand, "命令格式或协议版本无效。");
                return false;
            }
            if (command.expectedRevision != Revision)
            {
                rejection = Reject(DemoCommandRejectionCode.RevisionMismatch, "客户端状态已经过期，请同步后重试。");
                return false;
            }
            if (_processedCommandIds.Contains(command.commandId))
            {
                rejection = Reject(DemoCommandRejectionCode.DuplicateCommand, "该命令已经处理过。");
                return false;
            }
            rejection = null;
            return true;
        }

        private DemoCommandResult RejectFromMessage(string message, CardDefinitionEntry definition)
        {
            if (definition == null) return Reject(DemoCommandRejectionCode.UnknownCard, message);
            if (!IsPlayerTurn) return Reject(DemoCommandRejectionCode.NotActivePlayer, message);
            if (!_hand.Contains(definition.id)) return Reject(DemoCommandRejectionCode.CardNotInHand, message);
            if (definition.cost > Energy) return Reject(DemoCommandRejectionCode.InsufficientRedstone, message);
            return Reject(DemoCommandRejectionCode.InvalidCommand, message);
        }

        private void AcceptCommand(MatchCommandDto command)
        {
            _processedCommandIds.Add(command.commandId);
            Revision++;
        }

        private DemoCommandResult Reject(DemoCommandRejectionCode code, string message) =>
            DemoCommandResult.Reject(code, message, Revision);

        private string NextCommandId() => $"local-{_nextLocalCommandId++}";

        private static bool IsIndexFree(string[] slots, int index) =>
            index >= 0 && index < slots.Length && string.IsNullOrEmpty(slots[index]);

        private static bool Fail(string value, out string message)
        {
            message = value;
            return false;
        }
    }
}
