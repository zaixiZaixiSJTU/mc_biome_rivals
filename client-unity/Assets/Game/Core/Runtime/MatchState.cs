using System;
using System.Collections.Generic;

namespace BiomeRivals.Core
{
    [Serializable]
    public sealed class PlayerStateDto
    {
        public string playerId = string.Empty;
        public int life;
        public int armor;
        public int redstone;
        public int redstoneCapacity;
        public string[] hand = Array.Empty<string>();
        public string[] unitSlots = Array.Empty<string>();
        public string[] buildingSlots = Array.Empty<string>();
    }

    [Serializable]
    public sealed class MatchStateDto
    {
        public string matchId = string.Empty;
        public int protocolVersion;
        public string rulesetVersion = string.Empty;
        public int revision;
        public long lastEventId;
        public string status = string.Empty;
        public int turn;
        public int activePlayerIndex;
        public PlayerStateDto[] players = Array.Empty<PlayerStateDto>();
        public string winnerPlayerId = string.Empty;
    }

    public sealed class MatchStateStore
    {
        public MatchStateDto Current { get; private set; }

        public void Replace(MatchStateDto snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.protocolVersion != GameVersions.Protocol || snapshot.rulesetVersion != GameVersions.Ruleset)
                throw new InvalidOperationException("Snapshot protocol or ruleset version is unsupported.");
            if (snapshot.players == null || snapshot.players.Length != 2)
                throw new InvalidOperationException("Snapshot must contain exactly two players.");
            Current = snapshot;
        }

        public void Clear()
        {
            Current = null;
        }

        public void Apply(MatchEventBatchDto batch)
        {
            if (Current == null) throw new InvalidOperationException("An authoritative snapshot is required before applying events.");
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            if (batch.protocolVersion != GameVersions.Protocol || batch.rulesetVersion != Current.rulesetVersion)
                throw new InvalidOperationException("Event batch version does not match the current snapshot.");
            if (batch.revision != Current.revision + 1)
                throw new InvalidOperationException($"Expected revision {Current.revision + 1}, received {batch.revision}.");

            foreach (var matchEvent in batch.events ?? Array.Empty<MatchEventDto>()) Apply(matchEvent);
            Current.revision = batch.revision;
        }

        private void Apply(MatchEventDto matchEvent)
        {
            if (matchEvent == null || matchEvent.payload == null) throw new InvalidOperationException("Event payload is missing.");
            var payload = matchEvent.payload;
            switch (matchEvent.type)
            {
                case MatchEventTypes.CardDeployed:
                    var player = FindPlayer(payload.playerId);
                    if (payload.slotKind != "UNIT" && payload.slotKind != "BUILDING")
                        throw new InvalidOperationException("Deployment event contains an invalid slot kind.");
                    var slots = payload.slotKind == "UNIT" ? player.unitSlots : player.buildingSlots;
                    var occupiedSlots = Math.Max(1, payload.occupiedSlots);
                    if (payload.slotIndex < 0 || payload.slotIndex + occupiedSlots > slots.Length)
                        throw new InvalidOperationException("Deployment event contains an invalid slot range.");
                    var nextHand = RemoveFirst(player.hand, payload.cardId);
                    player.hand = nextHand;
                    player.redstone = payload.redstone;
                    for (var index = payload.slotIndex; index < payload.slotIndex + occupiedSlots; index++) slots[index] = payload.cardId;
                    break;
                case MatchEventTypes.TurnStarted:
                    var activePlayer = FindPlayer(payload.playerId);
                    if (payload.activePlayerIndex < 0 || payload.activePlayerIndex >= Current.players.Length ||
                        !ReferenceEquals(activePlayer, Current.players[payload.activePlayerIndex]))
                        throw new InvalidOperationException("Turn event active player index does not match its player id.");
                    Current.turn = payload.turn;
                    Current.activePlayerIndex = payload.activePlayerIndex;
                    activePlayer.redstone = payload.redstone;
                    activePlayer.redstoneCapacity = payload.redstoneCapacity;
                    break;
                case MatchEventTypes.MatchEnded:
                    Current.status = "FINISHED";
                    Current.winnerPlayerId = payload.winnerPlayerId;
                    break;
            }
        }

        private PlayerStateDto FindPlayer(string playerId)
        {
            foreach (var player in Current.players ?? Array.Empty<PlayerStateDto>())
                if (player != null && string.Equals(player.playerId, playerId, StringComparison.Ordinal)) return player;
            throw new InvalidOperationException($"Event references unknown player '{playerId}'.");
        }

        private static string[] RemoveFirst(string[] values, string value)
        {
            var result = new List<string>(values ?? Array.Empty<string>());
            var index = result.IndexOf(value);
            if (index < 0) throw new InvalidOperationException($"Card '{value}' is not present in the authoritative hand projection.");
            result.RemoveAt(index);
            return result.ToArray();
        }
    }
}
