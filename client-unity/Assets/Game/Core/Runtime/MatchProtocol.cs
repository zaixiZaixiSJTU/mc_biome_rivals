using System;

namespace BiomeRivals.Core
{
    public static class MatchOpcodes
    {
        public const int Command = 1;
        public const int EventBatch = 2;
        public const int Rejection = 3;
        public const int Snapshot = 4;
    }

    public static class MatchCommandTypes
    {
        public const string Mulligan = "MULLIGAN";
        public const string DeployCard = "DEPLOY_CARD";
        public const string PlayCard = "PLAY_CARD";
        public const string EnterCombat = "ENTER_COMBAT";
        public const string Attack = "ATTACK";
        public const string EndTurn = "END_TURN";
        public const string Concede = "CONCEDE";
    }

    public static class MatchEventTypes
    {
        public const string MulliganCompleted = "MULLIGAN_COMPLETED";
        public const string MatchStarted = "MATCH_STARTED";
        public const string MaterialsConsumed = "MATERIALS_CONSUMED";
        public const string CardDeployed = "CARD_DEPLOYED";
        public const string ObjectSummoned = "OBJECT_SUMMONED";
        public const string CardPlayed = "CARD_PLAYED";
        public const string CardBuried = "CARD_BURIED";
        public const string CardExcavated = "CARD_EXCAVATED";
        public const string CardDrawn = "CARD_DRAWN";
        public const string CardBurned = "CARD_BURNED";
        public const string CardGenerated = "CARD_GENERATED";
        public const string FatigueDamage = "FATIGUE_DAMAGE";
        public const string HeroDamaged = "HERO_DAMAGED";
        public const string HeroHealed = "HERO_HEALED";
        public const string ArmorGained = "ARMOR_GAINED";
        public const string ObjectStatsChanged = "OBJECT_STATS_CHANGED";
        public const string PhaseChanged = "PHASE_CHANGED";
        public const string AttackResolved = "ATTACK_RESOLVED";
        public const string ObjectDied = "OBJECT_DIED";
        public const string TurnEnded = "TURN_ENDED";
        public const string TurnStarted = "TURN_STARTED";
        public const string PlayerConceded = "PLAYER_CONCEDED";
        public const string MatchEnded = "MATCH_ENDED";
    }

    public static class MatchPaymentMethods
    {
        public const string Redstone = "REDSTONE";
        public const string Crafting = "CRAFTING";
    }

    [Serializable]
    public sealed class MatchCommandPayloadDto
    {
        public int[] cardIndices = Array.Empty<int>();
        public string cardId = string.Empty;
        public string slotKind = string.Empty;
        public int slotIndex;
        public string paymentMethod = string.Empty;
        public string attackerInstanceId = string.Empty;
        public string targetType = string.Empty;
        public string targetInstanceId = string.Empty;
    }

    [Serializable]
    public sealed class MatchCommandDto
    {
        public int protocolVersion;
        public string rulesetVersion = string.Empty;
        public string commandId = string.Empty;
        public int expectedRevision;
        public string type = string.Empty;
        public MatchCommandPayloadDto payload = new MatchCommandPayloadDto();
    }

    public static class MatchCommandFactory
    {
        public static MatchCommandDto Mulligan(string commandId, int revision, int[] cardIndices) =>
            new MatchCommandDto
            {
                protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset,
                commandId = commandId,
                expectedRevision = revision,
                type = MatchCommandTypes.Mulligan,
                payload = new MatchCommandPayloadDto { cardIndices = cardIndices ?? Array.Empty<int>() }
            };

        public static MatchCommandDto DeployCard(
            string commandId,
            int revision,
            string cardId,
            string slotKind,
            int slotIndex,
            string paymentMethod = MatchPaymentMethods.Redstone) =>
            new MatchCommandDto
            {
                protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset,
                commandId = commandId,
                expectedRevision = revision,
                type = MatchCommandTypes.DeployCard,
                payload = new MatchCommandPayloadDto
                {
                    cardId = cardId,
                    slotKind = slotKind,
                    slotIndex = slotIndex,
                    paymentMethod = paymentMethod
                }
            };

        public static MatchCommandDto EndTurn(string commandId, int revision) =>
            new MatchCommandDto
            {
                protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset,
                commandId = commandId,
                expectedRevision = revision,
                type = MatchCommandTypes.EndTurn,
                payload = new MatchCommandPayloadDto()
            };

        public static MatchCommandDto PlayCard(string commandId, int revision, string cardId, string targetType = "", string targetInstanceId = "") =>
            new MatchCommandDto
            {
                protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset,
                commandId = commandId,
                expectedRevision = revision,
                type = MatchCommandTypes.PlayCard,
                payload = new MatchCommandPayloadDto
                {
                    cardId = cardId,
                    targetType = targetType,
                    targetInstanceId = targetInstanceId
                }
            };

        public static MatchCommandDto EnterCombat(string commandId, int revision) =>
            new MatchCommandDto
            {
                protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset,
                commandId = commandId,
                expectedRevision = revision,
                type = MatchCommandTypes.EnterCombat,
                payload = new MatchCommandPayloadDto()
            };

        public static MatchCommandDto Attack(
            string commandId,
            int revision,
            string attackerInstanceId,
            string targetType,
            string targetInstanceId = "") =>
            new MatchCommandDto
            {
                protocolVersion = GameVersions.Protocol,
                rulesetVersion = GameVersions.Ruleset,
                commandId = commandId,
                expectedRevision = revision,
                type = MatchCommandTypes.Attack,
                payload = new MatchCommandPayloadDto
                {
                    attackerInstanceId = attackerInstanceId,
                    targetType = targetType,
                    targetInstanceId = targetInstanceId
                }
            };
    }

    [Serializable]
    public sealed class CraftingMaterialDto
    {
        public string cardId = string.Empty;
        public int count;
    }

    [Serializable]
    public sealed class MatchEventPayloadDto
    {
        public string playerId = string.Empty;
        public int turn;
        public string winnerPlayerId = string.Empty;
        public string reason = string.Empty;
        public string cardId = string.Empty;
        public string[] hand = Array.Empty<string>();
        public string[] keywords = Array.Empty<string>();
        public string slotKind = string.Empty;
        public int slotIndex;
        public int occupiedSlots;
        public string paymentMethod = string.Empty;
        public int redstone;
        public int redstoneCapacity;
        public int activePlayerIndex;
        public string phase = string.Empty;
        public string instanceId = string.Empty;
        public string cardType = string.Empty;
        public int attack;
        public int health;
        public int maxHealth;
        public int summonedTurn;
        public int nextInstanceId;
        public string attackerPlayerId = string.Empty;
        public string attackerInstanceId = string.Empty;
        public string targetPlayerId = string.Empty;
        public string targetType = string.Empty;
        public string targetInstanceId = string.Empty;
        public int damageToTarget;
        public int damageToAttacker;
        public int attackerHealth;
        public int targetHealth;
        public int targetArmor;
        public int handCount;
        public int replacedCount;
        public int deckCount;
        public int buriedCount;
        public int discardCount;
        public int fatigueCount;
        public int damage;
        public int life;
        public int armor;
        public string effectId = string.Empty;
        public string sourceCardId = string.Empty;
        public string sourceInstanceId = string.Empty;
        public string destination = string.Empty;
        public string damageType = string.Empty;
        public int healing;
        public int amount;
        public int temporaryAttackModifier;
        public int temporaryAttackModifierExpiresOnTurn;
        public string craftedCardId = string.Empty;
        public string recipeId = string.Empty;
        public CraftingMaterialDto[] materials = Array.Empty<CraftingMaterialDto>();
    }

    [Serializable]
    public sealed class MatchEventDto
    {
        public long eventId;
        public string type = string.Empty;
        public MatchEventPayloadDto payload = new MatchEventPayloadDto();
    }

    [Serializable]
    public sealed class MatchEventBatchDto
    {
        public int protocolVersion;
        public string rulesetVersion = string.Empty;
        public int revision;
        public string acknowledgedCommandId = string.Empty;
        public MatchEventDto[] events = Array.Empty<MatchEventDto>();
    }

    [Serializable]
    public sealed class CommandRejectionDto
    {
        public string commandId = string.Empty;
        public string code = string.Empty;
        public string message = string.Empty;
        public int revision;
    }
}
