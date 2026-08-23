using System;

namespace BiomeRivals.Core
{
    public static class MatchCommandTypes
    {
        public const string EndTurn = "END_TURN";
        public const string Concede = "CONCEDE";
    }

    public static class MatchEventTypes
    {
        public const string TurnEnded = "TURN_ENDED";
        public const string TurnStarted = "TURN_STARTED";
        public const string PlayerConceded = "PLAYER_CONCEDED";
        public const string MatchEnded = "MATCH_ENDED";
    }

    [Serializable]
    public sealed class EmptyPayload
    {
    }

    [Serializable]
    public sealed class MatchCommandDto
    {
        public int protocolVersion;
        public string rulesetVersion = string.Empty;
        public string commandId = string.Empty;
        public int expectedRevision;
        public string type = string.Empty;
        public EmptyPayload payload = new EmptyPayload();
    }

    [Serializable]
    public sealed class MatchEventPayloadDto
    {
        public string playerId = string.Empty;
        public int turn;
        public string winnerPlayerId = string.Empty;
        public string reason = string.Empty;
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
