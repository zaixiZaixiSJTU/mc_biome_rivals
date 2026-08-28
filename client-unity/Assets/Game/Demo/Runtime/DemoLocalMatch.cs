using System;
using System.Collections.Generic;
using System.Linq;
using BiomeRivals.Content;
using BiomeRivals.Core;

namespace BiomeRivals.Demo
{
    public enum DemoSlotKind
    {
        Unit,
        Building
    }

    public sealed class DemoLocalMatch : IDemoMatchView
    {
        private readonly List<string> _hand = new List<string>();
        private readonly List<string> _deck = new List<string>();
        private readonly List<string> _discardPile = new List<string>();
        private readonly List<DemoBattlefieldObject> _playerBattlefield = new List<DemoBattlefieldObject>();
        private readonly List<DemoBattlefieldObject> _opponentBattlefield = new List<DemoBattlefieldObject>();
        private readonly HashSet<string> _processedCommandIds = new HashSet<string>(StringComparer.Ordinal);
        private int _nextLocalCommandId = 1;
        private int _nextBattlefieldInstanceId = 1;
        private int _opponentHandCount = 5;

        public IReadOnlyList<string> Hand => _hand;
        public IReadOnlyList<string> Deck => _deck;
        public IReadOnlyList<string> DiscardPile => _discardPile;
        public string[] UnitSlots { get; } = new string[4];
        public string[] BuildingSlots { get; } = new string[3];
        public string[] OpponentUnitSlots { get; } = new string[4];
        public string[] OpponentBuildingSlots { get; } = new string[3];
        public IReadOnlyList<DemoBattlefieldObject> PlayerBattlefield => _playerBattlefield;
        public IReadOnlyList<DemoBattlefieldObject> OpponentBattlefield => _opponentBattlefield;
        public bool IsAuthoritative => false;
        public string PlayerFactionId { get; private set; } = FactionIds.PlainsForest;
        public string OpponentFactionId { get; private set; } = FactionIds.Nether;
        public bool IsMulligan => false;
        public bool PlayerMulliganCompleted => true;
        public bool OpponentMulliganCompleted => true;
        public int ViewerIndex => 0;
        public int DeckCount => _deck.Count;
        public int DiscardCount => _discardPile.Count;
        public int OpponentHandCount => _opponentHandCount;
        public int Round { get; private set; } = 1;
        public int MaxEnergy { get; private set; } = 6;
        public int Energy { get; private set; } = 6;
        public bool IsPlayerTurn { get; private set; } = true;
        public DemoTurnPhase Phase { get; private set; } = DemoTurnPhase.Main;
        public int PlayerLife { get; private set; } = 30;
        public int PlayerArmor { get; private set; }
        public int FatigueCount { get; private set; }
        public DemoDrawResult LastDrawResult { get; private set; }
        public int OpponentLife { get; private set; } = 30;
        public bool IsFinished { get; private set; }
        public int Revision { get; private set; }

        public void SetPlayerFaction(string factionId)
        {
            if (!FactionIds.IsSupported(factionId)) throw new ArgumentOutOfRangeException(nameof(factionId));
            PlayerFactionId = factionId;
        }

        public void SetOpponentFaction(string factionId)
        {
            if (!FactionIds.IsSupported(factionId)) throw new ArgumentOutOfRangeException(nameof(factionId));
            OpponentFactionId = factionId;
        }

        public void ResetHand(IEnumerable<string> cardIds)
        {
            if (cardIds == null) throw new ArgumentNullException(nameof(cardIds));
            _hand.Clear();
            _hand.AddRange(cardIds);
        }

        public void ResetDeckAndHand(IEnumerable<string> handCardIds, IEnumerable<string> deckCardIds)
        {
            if (handCardIds == null) throw new ArgumentNullException(nameof(handCardIds));
            if (deckCardIds == null) throw new ArgumentNullException(nameof(deckCardIds));
            _hand.Clear();
            _hand.AddRange(handCardIds);
            if (_hand.Count > 7) throw new ArgumentException("Hand cannot exceed seven cards.", nameof(handCardIds));
            _deck.Clear();
            _deck.AddRange(deckCardIds);
            _discardPile.Clear();
            FatigueCount = 0;
            LastDrawResult = null;
        }

        public bool TryDeploy(
            CardDefinitionEntry definition,
            DemoSlotKind slotKind,
            int slotIndex,
            out string message,
            string paymentMethod = MatchPaymentMethods.Redstone)
        {
            var cardId = definition == null ? string.Empty : definition.id;
            var command = CreateDeployCommand(cardId, slotKind, slotIndex, paymentMethod);
            var result = ApplyDeploy(definition, command);
            message = result.Message;
            return result.Accepted;
        }

        public MatchCommandDto CreateDeployCommand(
            string cardId,
            DemoSlotKind slotKind,
            int slotIndex,
            string paymentMethod = MatchPaymentMethods.Redstone) =>
            MatchCommandFactory.DeployCard(
                NextCommandId(), Revision, cardId, slotKind == DemoSlotKind.Unit ? "UNIT" : "BUILDING", slotIndex, paymentMethod);

        public DemoCommandResult ApplyDeploy(CardDefinitionEntry definition, MatchCommandDto command)
        {
            if (!ValidateCommand(command, MatchCommandTypes.DeployCard, out var rejection)) return rejection;
            if (definition == null) return Reject(DemoCommandRejectionCode.UnknownCard, "卡牌定义不存在。");
            if (command.payload == null || !string.Equals(command.payload.cardId, definition.id, StringComparison.Ordinal))
                return Reject(DemoCommandRejectionCode.UnknownCard, "命令中的卡牌与注册定义不一致。");
            if (Phase != DemoTurnPhase.Main)
                return Reject(DemoCommandRejectionCode.WrongPhase, "进入战斗阶段后不能继续部署卡牌。");
            if (!CanDeploy(definition, command.payload.paymentMethod, out var message)) return RejectFromMessage(message, definition);
            if (definition.cardType == "UNIT")
            {
                if (!string.Equals(command.payload.slotKind, "UNIT", StringComparison.Ordinal) || command.payload.slotIndex < 0 || command.payload.slotIndex >= UnitSlots.Length)
                    return Reject(DemoCommandRejectionCode.InvalidTarget, "生物只能部署到有效的单位格。");
                if (!IsIndexFree(UnitSlots, command.payload.slotIndex))
                    return Reject(DemoCommandRejectionCode.SlotOccupied, "这个单位格已经被占用。");
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
            }
            else
            {
                return Reject(DemoCommandRejectionCode.InvalidTarget, "这张牌不是部署牌，请使用右侧的“释放”按钮。");
            }

            ConsumeDeployment(definition, command.payload.paymentMethod);
            var crafted = command.payload.paymentMethod == MatchPaymentMethods.Crafting;
            var deploymentSlots = command.payload.slotKind == "UNIT" ? UnitSlots : BuildingSlots;
            var occupiedSlots = definition.cardType == "UNIT" ? 1 : Math.Max(1, definition.buildingSlots);
            for (var index = command.payload.slotIndex; index < command.payload.slotIndex + occupiedSlots; index++)
                deploymentSlots[index] = definition.id;
            var deployedObject = new DemoBattlefieldObject
            {
                InstanceId = $"object-{_nextBattlefieldInstanceId++}",
                CardId = definition.id,
                Player = true,
                SlotKind = command.payload.slotKind == "UNIT" ? DemoSlotKind.Unit : DemoSlotKind.Building,
                SlotIndex = command.payload.slotIndex,
                OccupiedSlots = occupiedSlots,
                Attack = definition.attack + (crafted ? definition.craftedAttackBonus : 0),
                Health = definition.health + (crafted ? definition.craftedHealthBonus : 0),
                MaxHealth = definition.health + (crafted ? definition.craftedHealthBonus : 0),
                SummonedRound = Round,
                Keywords = (definition.keywords ?? Array.Empty<string>()).ToArray()
            };
            _playerBattlefield.Add(deployedObject);
            var craftingBonuses = new List<string>();
            if (definition.craftedAttackBonus > 0) craftingBonuses.Add($"+{definition.craftedAttackBonus} 攻击");
            if (definition.craftedHealthBonus > 0) craftingBonuses.Add($"+{definition.craftedHealthBonus} 最大生命");
            if (definition.craftedDurabilityBonus > 0) craftingBonuses.Add($"+{definition.craftedDurabilityBonus} 耐久");
            var deployMessage = crafted
                ? $"已合成并部署：{definition.designId}（{string.Join("，", craftingBonuses)}）"
                : $"已部署：{definition.designId}";
            if (definition.effectImplementationStatus == "IMPLEMENTED" &&
                definition.effectIds != null && definition.effectIds.Contains("effect.pf_001.01"))
            {
                var lifeBefore = PlayerLife;
                PlayerLife = Math.Min(30, PlayerLife + 1);
                deployMessage += $"；蜜蜂战吼恢复 {PlayerLife - lifeBefore} 点生命。";
            }
            AcceptCommand(command);
            return DemoCommandResult.Accept(deployMessage, Revision);
        }

        public bool TryCast(CardDefinitionEntry definition, out string message)
        {
            var result = ApplyPlayCard(definition, CreatePlayCardCommand(definition == null ? string.Empty : definition.id));
            message = result.Message;
            return result.Accepted;
        }

        public MatchCommandDto CreatePlayCardCommand(string cardId, string targetType = "", string targetInstanceId = "") =>
            MatchCommandFactory.PlayCard(NextCommandId(), Revision, cardId, targetType, targetInstanceId);

        public DemoCommandResult ApplyPlayCard(CardDefinitionEntry definition, MatchCommandDto command)
        {
            if (!ValidateCommand(command, MatchCommandTypes.PlayCard, out var rejection)) return rejection;
            if (!CanPlay(definition, out var message)) return RejectFromMessage(message, definition);
            if (definition.cardType == "UNIT" || definition.cardType == "BUILDING" || definition.cardType == "STRUCTURE" || definition.cardType == "EQUIPMENT")
                return Reject(DemoCommandRejectionCode.InvalidTarget, "该卡牌需要对应的部署或装备目标。");
            if (definition.effectImplementationStatus != "IMPLEMENTED" || definition.effectIds == null || definition.effectIds.Length != 1)
                return Reject(DemoCommandRejectionCode.EffectNotImplemented, "该卡牌效果已注册，但尚未接入规则执行器。");

            var effectId = definition.effectIds[0];
            if (effectId != "effect.db_006.01" && effectId != "effect.nt_006.01" &&
                effectId != "effect.si_001.01" && effectId != "effect.tk_005.01" &&
                effectId != "effect.tk_009.01" && effectId != "effect.tk_010.01" &&
                effectId != "effect.tk_016.01")
                return Reject(DemoCommandRejectionCode.EffectNotImplemented, "找不到该 effectId 的规则处理器。");
            DemoBattlefieldObject targetedObject = null;
            if (DemoCardTargeting.TryGetRule(definition, out var targetRule))
            {
                if (command.payload == null || command.payload.targetType != targetRule.TargetType)
                    return Reject(DemoCommandRejectionCode.InvalidTarget, targetRule.MissingTargetMessage);
                var playerTarget = targetRule.Owner == DemoTargetOwner.Friendly;
                var battlefield = playerTarget ? _playerBattlefield : _opponentBattlefield;
                targetedObject = battlefield.Find(value => value.InstanceId == command.payload.targetInstanceId);
                if (!targetRule.IsLegal(playerTarget, targetRule.SlotKind, targetedObject))
                    return Reject(DemoCommandRejectionCode.InvalidTarget, targetRule.MissingTargetMessage);
            }
            Consume(definition);
            _discardPile.Add(definition.id);
            switch (effectId)
            {
                case "effect.db_006.01":
                    var damaged = 0;
                    var destroyed = 0;
                    foreach (var value in _playerBattlefield.ToArray())
                    {
                        if (value.SlotKind != DemoSlotKind.Unit) continue;
                        value.Health = Math.Max(0, value.Health - 2);
                        damaged++;
                        if (value.Health == 0) destroyed++;
                    }
                    foreach (var value in _opponentBattlefield.ToArray())
                    {
                        if (value.SlotKind != DemoSlotKind.Unit) continue;
                        value.Health = Math.Max(0, value.Health - 2);
                        damaged++;
                        if (value.Health == 0) destroyed++;
                    }
                    var deathrattleMessages = SettleDeaths();
                    message = $"沙尘暴：对 {damaged} 个生物造成 2 点伤害，消灭 {destroyed} 个。";
                    if (deathrattleMessages.Count > 0) message += " " + string.Join(" ", deathrattleMessages);
                    break;
                case "effect.nt_006.01":
                    PlayerLife = Math.Max(0, PlayerLife - 2);
                    if (PlayerLife == 0)
                    {
                        IsFinished = true;
                        message = "熔岩献祭造成致命自伤，对局结束。";
                    }
                    else
                    {
                        var draw = DrawCard();
                        message = draw.Outcome == DemoDrawOutcome.Drawn
                            ? $"熔岩献祭：受到 2 点真实伤害，抽到 {draw.CardId}。"
                            : draw.Outcome == DemoDrawOutcome.Burned
                                ? $"熔岩献祭：受到 2 点真实伤害，{draw.CardId} 因满手爆牌。"
                                : $"熔岩献祭：受到 2 点真实伤害，并受到 {draw.FatigueDamage} 点疲劳伤害。";
                    }
                    break;
                case "effect.si_001.01":
                    var attackBefore = targetedObject.Attack;
                    targetedObject.Attack = Math.Max(0, targetedObject.Attack - 1);
                    targetedObject.TemporaryAttackModifier += targetedObject.Attack - attackBefore;
                    if (targetedObject.TemporaryAttackModifier != 0) targetedObject.TemporaryAttackModifierExpiresOnRound = Round;
                    var reduced = attackBefore - targetedObject.Attack;
                    message = reduced > 0
                        ? $"雪球：{targetedObject.CardId} 的攻击力降低 {reduced}，持续到本回合结束。"
                        : $"雪球命中 {targetedObject.CardId}，但其攻击力已经为 0。";
                    break;
                case "effect.tk_005.01":
                    PlayerLife = Math.Min(30, PlayerLife + 2);
                    PlayerLife = Math.Max(0, PlayerLife - 1);
                    message = "腐肉：先恢复 2 点生命，再受到 1 点真实伤害。";
                    break;
                case "effect.tk_009.01":
                    targetedObject.Attack += 1;
                    targetedObject.TemporaryAttackModifier += 1;
                    targetedObject.TemporaryAttackModifierExpiresOnRound = Round;
                    message = $"骨头：{targetedObject.CardId} 本回合获得 +1 攻击力。";
                    break;
                case "effect.tk_010.01":
                    var healthBefore = targetedObject.Health;
                    targetedObject.Health = Math.Min(targetedObject.MaxHealth, targetedObject.Health + 2);
                    message = $"圆石：{targetedObject.CardId} 恢复 {targetedObject.Health - healthBefore} 点生命。";
                    break;
                case "effect.tk_016.01":
                    PlayerArmor += 2;
                    message = "潜影壳：获得 2 点护甲。";
                    break;
                default:
                    throw new InvalidOperationException("Validated effect handler was not dispatched.");
            }

            AcceptCommand(command);
            return DemoCommandResult.Accept(message, Revision);
        }

        public void ResetOpponent(IEnumerable<CardDefinitionEntry> definitions)
        {
            Array.Clear(OpponentUnitSlots, 0, OpponentUnitSlots.Length);
            Array.Clear(OpponentBuildingSlots, 0, OpponentBuildingSlots.Length);
            _opponentBattlefield.Clear();
            OpponentLife = 30;
            _opponentHandCount = 5;
            var unitIndex = 0;
            var buildingIndex = 0;
            foreach (var definition in definitions)
            {
                if (definition == null) continue;
                if (definition.cardType == "UNIT" && unitIndex < OpponentUnitSlots.Length)
                {
                    SeedOpponent(definition, DemoSlotKind.Unit, unitIndex);
                    unitIndex += 2;
                }
                else if ((definition.cardType == "BUILDING" || definition.cardType == "STRUCTURE") && buildingIndex < OpponentBuildingSlots.Length)
                {
                    SeedOpponent(definition, DemoSlotKind.Building, buildingIndex);
                    buildingIndex += Math.Max(1, definition.buildingSlots);
                }
            }
        }

        public DemoBattlefieldObject GetObject(bool player, DemoSlotKind kind, int slotIndex)
        {
            var objects = player ? _playerBattlefield : _opponentBattlefield;
            foreach (var value in objects)
                if (value.SlotKind == kind && slotIndex >= value.SlotIndex && slotIndex < value.SlotIndex + value.OccupiedSlots) return value;
            return null;
        }

        public bool CanAttackWith(DemoBattlefieldObject attacker, out string message)
        {
            if (IsFinished) return Fail("对局已经结束。", out message);
            if (!IsPlayerTurn || Phase != DemoTurnPhase.Combat) return Fail("请先进入战斗阶段。", out message);
            if (attacker == null || !attacker.Player || attacker.SlotKind != DemoSlotKind.Unit || attacker.Attack <= 0)
                return Fail("请选择一个可攻击的己方生物。", out message);
            if (attacker.SummonedRound == Round && !attacker.HasKeyword("CHARGE")) return Fail("该生物本回合刚被召唤，且不具有冲锋。", out message);
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
            if (HasLivingOpponentTaunt() && (targetType == "HERO" || target == null || !target.HasKeyword("TAUNT")))
                return Fail("敌方存在嘲讽单位，必须先攻击一个发出金光的嘲讽目标。", out message);
            message = string.Empty;
            return true;
        }

        public MatchCommandDto CreateEnterCombatCommand() =>
            MatchCommandFactory.EnterCombat(NextCommandId(), Revision);

        public DemoCommandResult ApplyEnterCombat(MatchCommandDto command)
        {
            if (!ValidateCommand(command, MatchCommandTypes.EnterCombat, out var rejection)) return rejection;
            if (!IsPlayerTurn) return Reject(DemoCommandRejectionCode.NotActivePlayer, "当前不是你的回合。");
            if (Phase != DemoTurnPhase.Main) return Reject(DemoCommandRejectionCode.WrongPhase, "当前已经处于战斗阶段。");
            Phase = DemoTurnPhase.Combat;
            AcceptCommand(command);
            return DemoCommandResult.Accept("已进入战斗阶段：选择己方生物，再选择敌方目标。", Revision);
        }

        public MatchCommandDto CreateAttackCommand(string attackerInstanceId, string targetType, string targetInstanceId = "") =>
            MatchCommandFactory.Attack(NextCommandId(), Revision, attackerInstanceId, targetType, targetInstanceId);

        public DemoCommandResult ApplyAttack(MatchCommandDto command)
        {
            if (!ValidateCommand(command, MatchCommandTypes.Attack, out var rejection)) return rejection;
            if (command.payload == null) return Reject(DemoCommandRejectionCode.InvalidCommand, "攻击命令缺少目标。");
            if (command.payload.targetType != "HERO" && command.payload.targetType != "UNIT" && command.payload.targetType != "BUILDING")
                return Reject(DemoCommandRejectionCode.InvalidTarget, "攻击目标类型无效。");
            var attacker = _playerBattlefield.Find(value => value.InstanceId == command.payload.attackerInstanceId);
            if (!CanAttackWith(attacker, out var message)) return Reject(DemoCommandRejectionCode.AttackerNotReady, message);

            var target = command.payload.targetType == "HERO"
                ? null
                : _opponentBattlefield.Find(value => value.InstanceId == command.payload.targetInstanceId);
            if (!CanAttackTarget(target, command.payload.targetType, out message))
                return Reject(HasLivingOpponentTaunt() ? DemoCommandRejectionCode.TauntTargetRequired : DemoCommandRejectionCode.InvalidTarget, message);

            attacker.HasAttacked = true;
            if (command.payload.targetType == "HERO")
            {
                OpponentLife = Math.Max(0, OpponentLife - attacker.Attack);
                if (OpponentLife == 0) IsFinished = true;
                AcceptCommand(command);
                return DemoCommandResult.Accept(IsFinished ? "敌方英雄生命归零，你获得胜利！" : $"对敌方英雄造成 {attacker.Attack} 点伤害。", Revision);
            }

            var expectedKind = command.payload.targetType == "UNIT" ? DemoSlotKind.Unit : DemoSlotKind.Building;
            if (target == null || target.SlotKind != expectedKind)
            {
                attacker.HasAttacked = false;
                return Reject(DemoCommandRejectionCode.InvalidTarget, "攻击目标无效或已经离场。");
            }
            var retaliation = target.SlotKind == DemoSlotKind.Unit ? target.Attack : 0;
            target.Health = Math.Max(0, target.Health - attacker.Attack);
            attacker.Health = Math.Max(0, attacker.Health - retaliation);
            var targetDied = target.Health == 0;
            var deathrattleMessages = SettleDeaths();
            AcceptCommand(command);
            return DemoCommandResult.Accept(
                $"造成 {attacker.Attack} 点伤害，受到 {retaliation} 点反击" + (targetDied ? "；目标死亡。" : "。") +
                (deathrattleMessages.Count > 0 ? " " + string.Join(" ", deathrattleMessages) : string.Empty),
                Revision);
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
            RestoreExpiredAttackModifiers(_playerBattlefield);
            RestoreExpiredAttackModifiers(_opponentBattlefield);
            IsPlayerTurn = false;
            AcceptCommand(command);
            return DemoCommandResult.Accept("已结束回合。", Revision);
        }

        public DemoDrawResult BeginNextPlayerTurn()
        {
            Round++;
            MaxEnergy = Math.Min(10, MaxEnergy + 1);
            Energy = MaxEnergy;
            IsPlayerTurn = true;
            Phase = DemoTurnPhase.Main;
            foreach (var battlefieldObject in _playerBattlefield) battlefieldObject.HasAttacked = false;
            return DrawCard();
        }

        private DemoDrawResult DrawCard()
        {
            if (_deck.Count == 0)
            {
                FatigueCount++;
                PlayerLife = Math.Max(0, PlayerLife - FatigueCount);
                if (PlayerLife == 0) IsFinished = true;
                return RememberDraw(new DemoDrawResult(DemoDrawOutcome.Fatigue, string.Empty, FatigueCount));
            }

            var cardIndex = _deck.Count - 1;
            var cardId = _deck[cardIndex];
            _deck.RemoveAt(cardIndex);
            if (_hand.Count >= 7)
            {
                _discardPile.Add(cardId);
                return RememberDraw(new DemoDrawResult(DemoDrawOutcome.Burned, cardId, 0));
            }

            _hand.Add(cardId);
            return RememberDraw(new DemoDrawResult(DemoDrawOutcome.Drawn, cardId, 0));
        }

        private DemoDrawResult RememberDraw(DemoDrawResult result)
        {
            LastDrawResult = result;
            return result;
        }

        private bool CanPlay(CardDefinitionEntry definition, out string message)
        {
            if (definition == null) return Fail("卡牌定义不存在。", out message);
            if (!IsPlayerTurn) return Fail("当前是对手回合。", out message);
            if (Phase != DemoTurnPhase.Main) return Fail("进入战斗阶段后不能继续打出卡牌。", out message);
            if (!_hand.Contains(definition.id)) return Fail("该牌不在手牌中。", out message);
            if (definition.cost > Energy) return Fail("红石能量不足。", out message);
            message = string.Empty;
            return true;
        }

        private bool CanDeploy(CardDefinitionEntry definition, string paymentMethod, out string message)
        {
            if (definition == null) return Fail("卡牌定义不存在。", out message);
            if (!IsPlayerTurn) return Fail("当前是对手回合。", out message);
            if (Phase != DemoTurnPhase.Main) return Fail("进入战斗阶段后不能继续部署卡牌。", out message);
            if (!_hand.Contains(definition.id)) return Fail("该牌不在手牌中。", out message);
            if (paymentMethod == MatchPaymentMethods.Redstone)
            {
                if (definition.cost > Energy) return Fail("红石能量不足。", out message);
                message = string.Empty;
                return true;
            }
            if (paymentMethod == MatchPaymentMethods.Crafting)
                return DemoDeploymentRules.CanPayWithCrafting(this, definition, out message);
            return Fail("部署支付方式无效。", out message);
        }

        private void Consume(CardDefinitionEntry definition)
        {
            Energy -= definition.cost;
            _hand.Remove(definition.id);
        }

        private void ConsumeDeployment(CardDefinitionEntry definition, string paymentMethod)
        {
            if (paymentMethod == MatchPaymentMethods.Redstone)
            {
                Consume(definition);
                return;
            }

            var productIndex = _hand.IndexOf(definition.id);
            var materialIndices = new List<int>();
            var consumedMaterials = new List<string>();
            foreach (var ingredient in definition.craftingRecipe ?? Array.Empty<CraftingIngredientEntry>())
            {
                for (var count = 0; count < ingredient.count; count++)
                {
                    var index = -1;
                    for (var candidate = 0; candidate < _hand.Count; candidate++)
                    {
                        if (candidate != productIndex && !materialIndices.Contains(candidate) && _hand[candidate] == ingredient.cardId)
                        {
                            index = candidate;
                            break;
                        }
                    }
                    if (index < 0) throw new InvalidOperationException("Crafting materials changed after validation.");
                    materialIndices.Add(index);
                    consumedMaterials.Add(ingredient.cardId);
                }
            }
            materialIndices.Sort((left, right) => right.CompareTo(left));
            foreach (var index in materialIndices) _hand.RemoveAt(index);
            _discardPile.AddRange(consumedMaterials);
            if (!_hand.Remove(definition.id)) throw new InvalidOperationException("Crafted product changed after validation.");
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
            if (message.StartsWith("缺少材料", StringComparison.Ordinal)) return Reject(DemoCommandRejectionCode.MissingMaterials, message);
            if (message.Contains("支付方式") || message.Contains("合成配方")) return Reject(DemoCommandRejectionCode.InvalidPaymentMethod, message);
            if (definition.cost > Energy) return Reject(DemoCommandRejectionCode.InsufficientRedstone, message);
            return Reject(DemoCommandRejectionCode.InvalidCommand, message);
        }

        private void SeedOpponent(CardDefinitionEntry definition, DemoSlotKind kind, int slotIndex)
        {
            var occupiedSlots = kind == DemoSlotKind.Unit ? 1 : Math.Max(1, definition.buildingSlots);
            var slots = kind == DemoSlotKind.Unit ? OpponentUnitSlots : OpponentBuildingSlots;
            if (slotIndex + occupiedSlots > slots.Length) return;
            var instance = new DemoBattlefieldObject
            {
                InstanceId = $"object-{_nextBattlefieldInstanceId++}", CardId = definition.id, Player = false,
                SlotKind = kind, SlotIndex = slotIndex, OccupiedSlots = occupiedSlots,
                Attack = definition.attack, Health = definition.health, MaxHealth = definition.health, SummonedRound = 0,
                Keywords = (definition.keywords ?? Array.Empty<string>()).ToArray()
            };
            _opponentBattlefield.Add(instance);
            for (var index = slotIndex; index < slotIndex + occupiedSlots; index++) slots[index] = definition.id;
        }

        private void RestoreExpiredAttackModifiers(List<DemoBattlefieldObject> battlefield)
        {
            foreach (var value in battlefield)
            {
                if (value.TemporaryAttackModifierExpiresOnRound != Round) continue;
                value.Attack -= value.TemporaryAttackModifier;
                value.TemporaryAttackModifier = 0;
                value.TemporaryAttackModifierExpiresOnRound = 0;
            }
        }

        private bool HasLivingOpponentTaunt() =>
            _opponentBattlefield.Any(value => value.Health > 0 && value.HasKeyword("TAUNT"));

        private List<DemoBattlefieldObject> RemoveDeadObjects(
            List<DemoBattlefieldObject> battlefield,
            string[] unitSlots,
            string[] buildingSlots)
        {
            var deadObjects = battlefield.Where(value => value.Health <= 0)
                .OrderBy(value => value.SlotIndex)
                .ThenBy(value => value.InstanceId, StringComparer.Ordinal)
                .ToList();
            foreach (var value in deadObjects)
            {
                battlefield.Remove(value);
                if (value.Player) _discardPile.Add(value.CardId);
                var slots = value.SlotKind == DemoSlotKind.Unit ? unitSlots : buildingSlots;
                for (var index = value.SlotIndex; index < value.SlotIndex + value.OccupiedSlots; index++) slots[index] = string.Empty;
            }
            return deadObjects;
        }

        private List<string> SettleDeaths()
        {
            var messages = new List<string>();
            while (true)
            {
                var playerDeaths = RemoveDeadObjects(_playerBattlefield, UnitSlots, BuildingSlots);
                var opponentDeaths = RemoveDeadObjects(_opponentBattlefield, OpponentUnitSlots, OpponentBuildingSlots);
                if (playerDeaths.Count == 0 && opponentDeaths.Count == 0) return messages;
                foreach (var value in playerDeaths)
                {
                    var message = ResolveLocalDeathrattle(value);
                    if (!string.IsNullOrEmpty(message)) messages.Add(message);
                }
                foreach (var value in opponentDeaths)
                {
                    var message = ResolveLocalDeathrattle(value);
                    if (!string.IsNullOrEmpty(message)) messages.Add(message);
                }
            }
        }

        private string ResolveLocalDeathrattle(DemoBattlefieldObject value)
        {
            if (value.CardId == "ed_004")
            {
                if (value.Player)
                {
                    if (_hand.Count < 7)
                    {
                        _hand.Add("tk_016");
                        return "潜影贝亡语：潜影壳已置入你的手牌。";
                    }
                    _discardPile.Add("tk_016");
                    return "潜影贝亡语：手牌已满，潜影壳进入弃牌堆。";
                }
                if (_opponentHandCount < 7)
                {
                    _opponentHandCount++;
                    return "敌方潜影贝亡语：对手获得一张牌。";
                }
                return "敌方潜影贝亡语：对手手牌已满，潜影壳进入弃牌堆。";
            }
            if (value.CardId == "nt_001")
            {
                var slots = value.Player ? UnitSlots : OpponentUnitSlots;
                var battlefield = value.Player ? _playerBattlefield : _opponentBattlefield;
                var slotIndex = value.SlotIndex >= 0 && value.SlotIndex < slots.Length && string.IsNullOrEmpty(slots[value.SlotIndex])
                    ? value.SlotIndex
                    : Array.FindIndex(slots, string.IsNullOrEmpty);
                if (slotIndex < 0) return value.Player
                    ? "岩浆怪亡语：没有空单位格，未能召唤小型岩浆怪。"
                    : "敌方岩浆怪亡语：没有空单位格，未能召唤小型岩浆怪。";
                var summoned = new DemoBattlefieldObject
                {
                    InstanceId = $"object-{_nextBattlefieldInstanceId++}",
                    CardId = "tk_014",
                    Player = value.Player,
                    SlotKind = DemoSlotKind.Unit,
                    SlotIndex = slotIndex,
                    OccupiedSlots = 1,
                    Attack = 1,
                    Health = 1,
                    MaxHealth = 1,
                    SummonedRound = Round,
                    Keywords = Array.Empty<string>()
                };
                battlefield.Add(summoned);
                slots[slotIndex] = summoned.CardId;
                return value.Player
                    ? $"岩浆怪亡语：小型岩浆怪已在单位格 {slotIndex + 1} 召唤。"
                    : $"敌方岩浆怪亡语：小型岩浆怪已在单位格 {slotIndex + 1} 召唤。";
            }
            return string.Empty;
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
