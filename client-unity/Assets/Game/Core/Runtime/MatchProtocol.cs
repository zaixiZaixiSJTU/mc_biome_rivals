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
        public const string DeployCard = "DEPLOY_CARD";
        public const string EndTurn = "END_TURN";
        public const string Concede = "CONCEDE";
    }

    public static class MatchEventTypes
    {
        public const string CardDeployed = "CARD_DEPLOYED";
        public const string TurnEnded = "TURN_ENDED";
        public const string TurnStarted = "TURN_STARTED";
        public const string PlayerConceded = "PLAYER_CONCEDED";
        public const string MatchEnded = "MATCH_ENDED";
    }

    [Serializable]
    public sealed class MatchCommandPayloadDto
    {
        public string cardId = string.Empty;
        public string slotKind = string.Empty;
        public int slotIndex;
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
        public static MatchCommandDto DeployCard(string commandId, int revision, string cardId, string slotKind, int slotIndex) =>
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
                    slotIndex = slotIndex
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
    }

    [Serializable]
    public sealed class MatchEventPayloadDto
    {
        public string playerId = string.Empty;
        public int turn;
        public string winnerPlayerId = string.Empty;
        public string reason = string.Empty;
        public string cardId = string.Empty;
        public string slotKind = string.Empty;
        public int slotIndex;
        public int occupiedSlots;
        public int redstone;
        public int redstoneCapacity;
        public int activePlayerIndex;
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
