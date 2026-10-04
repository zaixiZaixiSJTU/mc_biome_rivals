using System;
using System.Linq;
using BiomeRivals.Content;
using BiomeRivals.Core;
using BiomeRivals.Networking;

namespace BiomeRivals.Demo
{
    // Deterministic baseline, not a complete card-effect planner or a second rules engine.
    public sealed class BasicMatchAgentPolicy : IMatchAgentPolicy
    {
        private readonly CardContentRegistry _registry;
        public BasicMatchAgentPolicy(CardContentRegistry registry) =>
            _registry = registry ?? throw new ArgumentNullException(nameof(registry));

        public PlayerActionRequest Decide(PlayerObservation observation)
        {
            var state = observation?.ReadState();
            if (state == null || !observation.CanIssueCommand || state.status == "FINISHED") return null;
            var store = new MatchStateStore();
            store.Replace(state);
            var view = new DemoAuthoritativeMatchView(store);
            if (view.IsMulligan)
                return view.PlayerMulliganCompleted ? null : Action(observation, MatchCommandTypes.Mulligan);
            if (view.PendingChoice != null)
            {
                if (!view.IsChoiceOwner) return null;
                var option = (view.PendingChoice.options ?? Array.Empty<PendingChoiceOptionDto>())
                    .FirstOrDefault(value => value != null && value.selectable);
                return option == null ? null : Action(observation, MatchCommandTypes.ResolveChoice,
                    new MatchCommandPayloadDto { choiceId = view.PendingChoice.choiceId, selectedOptionIndex = option.optionIndex });
            }
            if (!view.IsPlayerTurn) return null;
            if (view.Phase == DemoTurnPhase.Main)
            {
                foreach (var card in view.HandCards.Where(value => value != null))
                {
                    if (!_registry.TryGetDefinition(card.cardId, out var definition) || !definition.manualPlayAllowed ||
                        DemoCardTargeting.TryGetRule(definition, out _)) continue;
                    var kind = definition.cardType == "UNIT" ? DemoSlotKind.Unit : DemoSlotKind.Building;
                    var slots = kind == DemoSlotKind.Unit ? view.UnitSlots : view.BuildingSlots;
                    foreach (var payment in new[] { MatchPaymentMethods.Redstone, MatchPaymentMethods.Crafting })
                    for (var i = 0; i < slots.Length; i++)
                        if (DemoDeploymentRules.Evaluate(view, definition, kind, i, payment, card.handCardInstanceId).IsLegal)
                            return Action(observation, MatchCommandTypes.DeployCard, new MatchCommandPayloadDto {
                                cardId = card.cardId, handCardInstanceId = card.handCardInstanceId,
                                slotKind = kind == DemoSlotKind.Unit ? "UNIT" : "BUILDING", slotIndex = i, paymentMethod = payment });
                }
                return Action(observation, MatchCommandTypes.EnterCombat);
            }
            var attackers = view.PlayerBattlefield.Where(value => view.CanAttackWith(value, out _))
                .Select(value => value.InstanceId).ToList();
            if (view.CanAttackWithHero(out _)) attackers.Add(MatchAttackerIds.Hero);
            foreach (var attacker in attackers)
            {
                if (view.CanAttackTarget(null, "HERO", out _))
                    return Attack(observation, attacker, "HERO", string.Empty);
                foreach (var target in view.OpponentBattlefield)
                {
                    var type = target.SlotKind == DemoSlotKind.Unit ? "UNIT" : "BUILDING";
                    if (view.CanAttackTarget(target, type, out _)) return Attack(observation, attacker, type, target.InstanceId);
                }
            }
            return Action(observation, MatchCommandTypes.EndTurn);
        }

        private static PlayerActionRequest Action(PlayerObservation observation, string type, MatchCommandPayloadDto payload = null) =>
            PlayerActionRequest.Create(observation, type, payload);
        private static PlayerActionRequest Attack(PlayerObservation observation, string attacker, string type, string target) =>
            Action(observation, MatchCommandTypes.Attack, new MatchCommandPayloadDto {
                attackerInstanceId = attacker, targetType = type, targetInstanceId = target });
    }
}
