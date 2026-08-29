using System;
using System.Collections.Generic;
using System.Linq;

namespace BiomeRivals.Core
{
    [Serializable]
    public sealed class BattlefieldObjectStateDto
    {
        public string instanceId = string.Empty;
        public string cardId = string.Empty;
        public string cardType = string.Empty;
        public int attack;
        public int health;
        public int maxHealth;
        public string slotKind = string.Empty;
        public int slotIndex;
        public int occupiedSlots;
        public int summonedTurn;
        public bool hasAttacked;
        public string[] keywords = Array.Empty<string>();
        public int temporaryAttackModifier;
        public int temporaryAttackModifierExpiresOnTurn;
    }

    [Serializable]
    public sealed class PlayerStateDto
    {
        public string playerId = string.Empty;
        public string factionId = FactionIds.PlainsForest;
        public bool mulliganCompleted;
        public int life;
        public int armor;
        public int redstone;
        public int redstoneCapacity;
        public string[] hand = Array.Empty<string>();
        public int deckCount;
        public int buriedCount;
        public string[] discardPile = Array.Empty<string>();
        public int fatigueCount;
        public string[] unitSlots = Array.Empty<string>();
        public string[] buildingSlots = Array.Empty<string>();
        public BattlefieldObjectStateDto[] battlefield = Array.Empty<BattlefieldObjectStateDto>();
    }

    [Serializable]
    public sealed class MatchStateDto
    {
        public string matchId = string.Empty;
        public string viewerPlayerId = string.Empty;
        public int protocolVersion;
        public string rulesetVersion = string.Empty;
        public int revision;
        public long lastEventId;
        public string status = string.Empty;
        public int turn;
        public string phase = string.Empty;
        public int activePlayerIndex;
        public int nextInstanceId;
        public PlayerStateDto[] players = Array.Empty<PlayerStateDto>();
        public PendingChoiceDto pendingChoice;
        public string winnerPlayerId = string.Empty;
    }

    public sealed class MatchStateStore
    {
        public MatchStateDto Current { get; private set; }
        public event Action<MatchStateDto> Changed;

        public void Replace(MatchStateDto snapshot)
        {
            if (snapshot == null) throw new ArgumentNullException(nameof(snapshot));
            if (snapshot.protocolVersion != GameVersions.Protocol || snapshot.rulesetVersion != GameVersions.Ruleset)
                throw new InvalidOperationException("Snapshot protocol or ruleset version is unsupported.");
            if (snapshot.players == null || snapshot.players.Length != 2)
                throw new InvalidOperationException("Snapshot must contain exactly two players.");
            foreach (var player in snapshot.players)
                if (player == null || !FactionIds.IsSupported(player.factionId))
                    throw new InvalidOperationException("Snapshot contains an unsupported player faction.");
                else if (player.buriedCount < 0 || player.buriedCount > player.deckCount)
                    throw new InvalidOperationException("Snapshot contains an invalid buried card count.");
            if (snapshot.pendingChoice != null)
            {
                ValidatePendingChoice(snapshot, snapshot.pendingChoice, "Snapshot");
            }
            Current = snapshot;
            Changed?.Invoke(Current);
        }

        public void Clear()
        {
            Current = null;
            Changed?.Invoke(null);
        }

        public void Apply(MatchEventBatchDto batch)
        {
            if (Current == null) throw new InvalidOperationException("An authoritative snapshot is required before applying events.");
            if (batch == null) throw new ArgumentNullException(nameof(batch));
            if (batch.protocolVersion != GameVersions.Protocol || batch.rulesetVersion != Current.rulesetVersion)
                throw new InvalidOperationException("Event batch version does not match the current snapshot.");
            if (batch.revision != Current.revision + 1)
                throw new InvalidOperationException($"Expected revision {Current.revision + 1}, received {batch.revision}.");

            var nextEventId = Current.lastEventId + 1;
            foreach (var matchEvent in batch.events ?? Array.Empty<MatchEventDto>())
            {
                if (matchEvent == null || matchEvent.eventId != nextEventId)
                    throw new InvalidOperationException($"Expected event {nextEventId}, received {matchEvent?.eventId ?? 0}.");
                nextEventId++;
            }
            foreach (var matchEvent in batch.events ?? Array.Empty<MatchEventDto>()) Apply(matchEvent);
            if (batch.events != null && batch.events.Length > 0) Current.lastEventId = batch.events[batch.events.Length - 1].eventId;
            Current.revision = batch.revision;
            Changed?.Invoke(Current);
        }

        private void Apply(MatchEventDto matchEvent)
        {
            if (matchEvent == null || matchEvent.payload == null) throw new InvalidOperationException("Event payload is missing.");
            var payload = matchEvent.payload;
            switch (matchEvent.type)
            {
                case MatchEventTypes.MulliganCompleted:
                    var mulliganPlayer = FindPlayer(payload.playerId);
                    mulliganPlayer.mulliganCompleted = true;
                    mulliganPlayer.hand = payload.hand ?? Array.Empty<string>();
                    if (mulliganPlayer.hand.Length != payload.handCount)
                        throw new InvalidOperationException("Mulligan event hand count does not match projected hand.");
                    mulliganPlayer.deckCount = payload.deckCount;
                    break;
                case MatchEventTypes.MatchStarted:
                    if (payload.activePlayerIndex < 0 || payload.activePlayerIndex >= Current.players.Length ||
                        Current.players[payload.activePlayerIndex].playerId != payload.playerId)
                        throw new InvalidOperationException("Match start event active player does not match its index.");
                    Current.status = "ACTIVE";
                    Current.turn = payload.turn;
                    Current.phase = payload.phase;
                    Current.activePlayerIndex = payload.activePlayerIndex;
                    break;
                case MatchEventTypes.CardDeployed:
                    var player = FindPlayer(payload.playerId);
                    player.hand = RemoveFirst(player.hand, payload.cardId);
                    player.redstone = payload.redstone;
                    AddBattlefieldObject(player, payload, "Deployment");
                    break;
                case MatchEventTypes.MaterialsConsumed:
                    var craftingPlayer = FindPlayer(payload.playerId);
                    var craftingHand = craftingPlayer.hand ?? Array.Empty<string>();
                    var craftingDiscard = new List<string>(craftingPlayer.discardPile ?? Array.Empty<string>());
                    foreach (var material in payload.materials ?? Array.Empty<CraftingMaterialDto>())
                    {
                        if (material == null || string.IsNullOrWhiteSpace(material.cardId) || material.count < 1)
                            throw new InvalidOperationException("Material consumption event contains an invalid ingredient.");
                        for (var count = 0; count < material.count; count++)
                        {
                            craftingHand = RemoveFirst(craftingHand, material.cardId);
                            craftingDiscard.Add(material.cardId);
                        }
                    }
                    if (craftingHand.Length != payload.handCount)
                        throw new InvalidOperationException("Material consumption event hand count does not match projected hand.");
                    if (craftingDiscard.Count != payload.discardCount)
                        throw new InvalidOperationException("Material consumption event discard count does not match projected discard pile.");
                    craftingPlayer.hand = craftingHand;
                    craftingPlayer.discardPile = craftingDiscard.ToArray();
                    break;
                case MatchEventTypes.ObjectSummoned:
                    AddBattlefieldObject(FindPlayer(payload.playerId), payload, "Summon");
                    break;
                case MatchEventTypes.CardPlayed:
                    var playingPlayer = FindPlayer(payload.playerId);
                    playingPlayer.hand = RemoveFirst(playingPlayer.hand, payload.cardId);
                    if (playingPlayer.hand.Length != payload.handCount) throw new InvalidOperationException("Play event hand count does not match projected hand.");
                    playingPlayer.redstone = payload.redstone;
                    var playedDiscard = new List<string>(playingPlayer.discardPile ?? Array.Empty<string>()) { payload.cardId };
                    if (playedDiscard.Count != payload.discardCount) throw new InvalidOperationException("Play event discard count does not match projected discard pile.");
                    playingPlayer.discardPile = playedDiscard.ToArray();
                    break;
                case MatchEventTypes.CardBuried:
                    var buryingPlayer = FindPlayer(payload.playerId);
                    if (payload.deckCount != buryingPlayer.deckCount + 1 || payload.buriedCount != buryingPlayer.buriedCount + 1)
                        throw new InvalidOperationException("Burial event counts do not match the projected deck.");
                    buryingPlayer.deckCount = payload.deckCount;
                    buryingPlayer.buriedCount = payload.buriedCount;
                    break;
                case MatchEventTypes.ChoiceOffered:
                    if (Current.pendingChoice != null) throw new InvalidOperationException("Choice event overlaps an existing pending choice.");
                    var offeredChoice = new PendingChoiceDto
                    {
                        choiceId = payload.choiceId,
                        playerId = payload.playerId,
                        sourceCardId = payload.sourceCardId,
                        sourceInstanceId = payload.sourceInstanceId,
                        effectId = payload.effectId,
                        kind = payload.kind,
                        options = CloneChoiceOptions(payload.options)
                    };
                    ValidatePendingChoice(Current, offeredChoice, "Choice event");
                    Current.pendingChoice = offeredChoice;
                    break;
                case MatchEventTypes.ChoiceResolved:
                    if (Current.pendingChoice == null || Current.pendingChoice.choiceId != payload.choiceId ||
                        Current.pendingChoice.playerId != payload.playerId)
                        throw new InvalidOperationException("Choice resolution does not match the pending choice.");
                    Current.pendingChoice = null;
                    break;
                case MatchEventTypes.CardExcavated:
                    var excavatingPlayer = FindPlayer(payload.playerId);
                    if (payload.deckCount != excavatingPlayer.deckCount - 1 || payload.buriedCount != excavatingPlayer.buriedCount - 1)
                        throw new InvalidOperationException("Excavation event counts do not match the projected deck.");
                    var excavatedHand = new List<string>(excavatingPlayer.hand ?? Array.Empty<string>());
                    var excavatedDiscard = new List<string>(excavatingPlayer.discardPile ?? Array.Empty<string>());
                    if (payload.destination == "HAND")
                        excavatedHand.Add(payload.playerId == Current.viewerPlayerId ? payload.cardId : string.Empty);
                    else if (payload.destination == "DISCARD") excavatedDiscard.Add(payload.cardId);
                    else throw new InvalidOperationException("Excavation event destination is invalid.");
                    if (excavatedHand.Count != payload.handCount || excavatedDiscard.Count != payload.discardCount)
                        throw new InvalidOperationException("Excavation event zone counts do not match their projections.");
                    excavatingPlayer.hand = excavatedHand.ToArray();
                    excavatingPlayer.discardPile = excavatedDiscard.ToArray();
                    excavatingPlayer.deckCount = payload.deckCount;
                    excavatingPlayer.buriedCount = payload.buriedCount;
                    break;
                case MatchEventTypes.CardDrawn:
                    var drawingPlayer = FindPlayer(payload.playerId);
                    var drawnHand = new List<string>(drawingPlayer.hand ?? Array.Empty<string>()) { payload.cardId };
                    if (drawnHand.Count != payload.handCount) throw new InvalidOperationException("Draw event hand count does not match projected hand.");
                    drawingPlayer.hand = drawnHand.ToArray();
                    drawingPlayer.deckCount = payload.deckCount;
                    break;
                case MatchEventTypes.CardBurned:
                    var burningPlayer = FindPlayer(payload.playerId);
                    if ((burningPlayer.hand?.Length ?? 0) != payload.handCount) throw new InvalidOperationException("Burn event hand count does not match projected hand.");
                    var discard = new List<string>(burningPlayer.discardPile ?? Array.Empty<string>()) { payload.cardId };
                    if (discard.Count != payload.discardCount) throw new InvalidOperationException("Burn event discard count does not match projected discard pile.");
                    burningPlayer.discardPile = discard.ToArray();
                    burningPlayer.deckCount = payload.deckCount;
                    break;
                case MatchEventTypes.CardGenerated:
                    var generatedPlayer = FindPlayer(payload.playerId);
                    if (payload.destination == "HAND")
                    {
                        var generatedHand = new List<string>(generatedPlayer.hand ?? Array.Empty<string>()) { payload.cardId };
                        if (generatedHand.Count != payload.handCount) throw new InvalidOperationException("Generated event hand count does not match projected hand.");
                        generatedPlayer.hand = generatedHand.ToArray();
                        if ((generatedPlayer.discardPile?.Length ?? 0) != payload.discardCount)
                            throw new InvalidOperationException("Generated event discard count does not match projected discard pile.");
                    }
                    else if (payload.destination == "DISCARD")
                    {
                        if ((generatedPlayer.hand?.Length ?? 0) != payload.handCount)
                            throw new InvalidOperationException("Generated discard event hand count does not match projected hand.");
                        var generatedDiscard = new List<string>(generatedPlayer.discardPile ?? Array.Empty<string>()) { payload.cardId };
                        if (generatedDiscard.Count != payload.discardCount)
                            throw new InvalidOperationException("Generated discard event discard count does not match projected discard pile.");
                        generatedPlayer.discardPile = generatedDiscard.ToArray();
                    }
                    else throw new InvalidOperationException("Generated event destination is invalid.");
                    break;
                case MatchEventTypes.FatigueDamage:
                    var fatiguedPlayer = FindPlayer(payload.playerId);
                    if ((fatiguedPlayer.hand?.Length ?? 0) != payload.handCount) throw new InvalidOperationException("Fatigue event hand count does not match projected hand.");
                    fatiguedPlayer.deckCount = payload.deckCount;
                    fatiguedPlayer.fatigueCount = payload.fatigueCount;
                    fatiguedPlayer.life = payload.life;
                    fatiguedPlayer.armor = payload.armor;
                    break;
                case MatchEventTypes.HeroDamaged:
                    var damagedPlayer = FindPlayer(payload.playerId);
                    damagedPlayer.life = payload.life;
                    damagedPlayer.armor = payload.armor;
                    break;
                case MatchEventTypes.HeroHealed:
                    FindPlayer(payload.playerId).life = payload.life;
                    break;
                case MatchEventTypes.ArmorGained:
                    FindPlayer(payload.playerId).armor = payload.armor;
                    break;
                case MatchEventTypes.ObjectStatsChanged:
                    var statsObject = FindObject(FindPlayer(payload.playerId), payload.instanceId);
                    statsObject.attack = payload.attack;
                    statsObject.health = payload.health;
                    statsObject.temporaryAttackModifier = payload.temporaryAttackModifier;
                    statsObject.temporaryAttackModifierExpiresOnTurn = payload.temporaryAttackModifierExpiresOnTurn;
                    break;
                case MatchEventTypes.PhaseChanged:
                    Current.phase = payload.phase;
                    break;
                case MatchEventTypes.AttackResolved:
                    var attackerPlayer = FindPlayer(payload.attackerPlayerId);
                    var attacker = FindObject(attackerPlayer, payload.attackerInstanceId);
                    attacker.health = payload.attackerHealth;
                    attacker.hasAttacked = true;
                    var targetPlayer = FindPlayer(payload.targetPlayerId);
                    if (payload.targetType == "HERO")
                    {
                        targetPlayer.life = payload.targetHealth;
                        targetPlayer.armor = payload.targetArmor;
                    }
                    else
                    {
                        FindObject(targetPlayer, payload.targetInstanceId).health = payload.targetHealth;
                    }
                    break;
                case MatchEventTypes.ObjectDied:
                    var deadObjectPlayer = FindPlayer(payload.playerId);
                    var deadCardId = RemoveObject(deadObjectPlayer, payload.instanceId);
                    var deathDiscard = new List<string>(deadObjectPlayer.discardPile ?? Array.Empty<string>()) { deadCardId };
                    if (deathDiscard.Count != payload.discardCount) throw new InvalidOperationException("Death event discard count does not match projected discard pile.");
                    deadObjectPlayer.discardPile = deathDiscard.ToArray();
                    break;
                case MatchEventTypes.TurnStarted:
                    var activePlayer = FindPlayer(payload.playerId);
                    if (payload.activePlayerIndex < 0 || payload.activePlayerIndex >= Current.players.Length ||
                        !ReferenceEquals(activePlayer, Current.players[payload.activePlayerIndex]))
                        throw new InvalidOperationException("Turn event active player index does not match its player id.");
                    Current.turn = payload.turn;
                    Current.phase = payload.phase;
                    Current.activePlayerIndex = payload.activePlayerIndex;
                    activePlayer.redstone = payload.redstone;
                    activePlayer.redstoneCapacity = payload.redstoneCapacity;
                    foreach (var battlefieldObject in activePlayer.battlefield ?? Array.Empty<BattlefieldObjectStateDto>())
                        if (battlefieldObject != null) battlefieldObject.hasAttacked = false;
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

        private static PlayerStateDto FindPlayer(MatchStateDto state, string playerId)
        {
            foreach (var player in state?.players ?? Array.Empty<PlayerStateDto>())
                if (player != null && string.Equals(player.playerId, playerId, StringComparison.Ordinal)) return player;
            return null;
        }

        private static void ValidatePendingChoice(MatchStateDto state, PendingChoiceDto choice, string source)
        {
            if (choice == null || choice.kind != "ARCHAEOLOGY_TOP_3" || choice.effectId != "effect.db_003.01" ||
                choice.sourceCardId != "db_003" || string.IsNullOrWhiteSpace(choice.choiceId) ||
                string.IsNullOrWhiteSpace(choice.sourceInstanceId) || choice.options == null || choice.options.Length > 3 ||
                state.status != "ACTIVE" || state.phase != "MAIN")
                throw new InvalidOperationException($"{source} contains an invalid pending card choice.");
            var owner = FindPlayer(state, choice.playerId);
            if (owner == null || state.activePlayerIndex < 0 || state.activePlayerIndex >= state.players.Length ||
                !ReferenceEquals(state.players[state.activePlayerIndex], owner) ||
                !(owner.battlefield ?? Array.Empty<BattlefieldObjectStateDto>()).Any(value =>
                    value != null && value.instanceId == choice.sourceInstanceId && value.cardId == choice.sourceCardId))
                throw new InvalidOperationException($"{source} pending choice has no active source object.");

            var isOwnerProjection = choice.playerId == state.viewerPlayerId;
            for (var index = 0; index < choice.options.Length; index++)
            {
                var option = choice.options[index];
                if (option == null || option.optionIndex != index ||
                    (isOwnerProjection && string.IsNullOrWhiteSpace(option.cardId)) ||
                    (!isOwnerProjection && (!string.IsNullOrEmpty(option.cardId) || option.selectable)))
                    throw new InvalidOperationException($"{source} pending choice violates option ordering or privacy projection.");
            }
        }

        private static PendingChoiceOptionDto[] CloneChoiceOptions(PendingChoiceOptionDto[] options)
        {
            var source = options ?? Array.Empty<PendingChoiceOptionDto>();
            var result = new PendingChoiceOptionDto[source.Length];
            for (var index = 0; index < source.Length; index++)
            {
                var option = source[index] ?? throw new InvalidOperationException("Choice contains a missing option.");
                result[index] = new PendingChoiceOptionDto
                {
                    optionIndex = option.optionIndex,
                    cardId = option.cardId,
                    selectable = option.selectable
                };
            }
            return result;
        }

        private static BattlefieldObjectStateDto FindObject(PlayerStateDto player, string instanceId)
        {
            foreach (var battlefieldObject in player.battlefield ?? Array.Empty<BattlefieldObjectStateDto>())
                if (battlefieldObject != null && string.Equals(battlefieldObject.instanceId, instanceId, StringComparison.Ordinal)) return battlefieldObject;
            throw new InvalidOperationException($"Event references unknown battlefield object '{instanceId}'.");
        }

        private void AddBattlefieldObject(PlayerStateDto player, MatchEventPayloadDto payload, string eventName)
        {
            if (payload.slotKind != "UNIT" && payload.slotKind != "BUILDING")
                throw new InvalidOperationException($"{eventName} event contains an invalid slot kind.");
            if ((payload.slotKind == "UNIT" && payload.cardType != "UNIT") ||
                (payload.slotKind == "BUILDING" && payload.cardType != "BUILDING" && payload.cardType != "STRUCTURE"))
                throw new InvalidOperationException($"{eventName} event card type does not match its slot kind.");
            var slots = payload.slotKind == "UNIT" ? player.unitSlots : player.buildingSlots;
            var occupiedSlots = Math.Max(1, payload.occupiedSlots);
            if (payload.slotIndex < 0 || payload.slotIndex + occupiedSlots > slots.Length)
                throw new InvalidOperationException($"{eventName} event contains an invalid slot range.");
            for (var index = payload.slotIndex; index < payload.slotIndex + occupiedSlots; index++)
                if (!string.IsNullOrEmpty(slots[index]))
                    throw new InvalidOperationException($"{eventName} event overlaps an occupied slot.");
            foreach (var existing in player.battlefield ?? Array.Empty<BattlefieldObjectStateDto>())
                if (existing != null && existing.instanceId == payload.instanceId)
                    throw new InvalidOperationException($"{eventName} event repeats an existing instance id.");

            var battlefield = new List<BattlefieldObjectStateDto>(player.battlefield ?? Array.Empty<BattlefieldObjectStateDto>())
            {
                new BattlefieldObjectStateDto
                {
                    instanceId = payload.instanceId,
                    cardId = payload.cardId,
                    cardType = payload.cardType,
                    attack = payload.attack,
                    health = payload.health,
                    maxHealth = payload.maxHealth,
                    slotKind = payload.slotKind,
                    slotIndex = payload.slotIndex,
                    occupiedSlots = occupiedSlots,
                    summonedTurn = payload.summonedTurn,
                    hasAttacked = false,
                    keywords = payload.keywords ?? Array.Empty<string>(),
                    temporaryAttackModifier = 0,
                    temporaryAttackModifierExpiresOnTurn = 0
                }
            };
            player.battlefield = battlefield.ToArray();
            Current.nextInstanceId = payload.nextInstanceId;
            for (var index = payload.slotIndex; index < payload.slotIndex + occupiedSlots; index++) slots[index] = payload.instanceId;
        }

        private static string RemoveObject(PlayerStateDto player, string instanceId)
        {
            var battlefield = new List<BattlefieldObjectStateDto>(player.battlefield ?? Array.Empty<BattlefieldObjectStateDto>());
            var removedObject = battlefield.Find(item => item != null && string.Equals(item.instanceId, instanceId, StringComparison.Ordinal));
            if (removedObject == null) throw new InvalidOperationException($"Death event references unknown battlefield object '{instanceId}'.");
            battlefield.Remove(removedObject);
            player.battlefield = battlefield.ToArray();
            ClearSlots(player.unitSlots, instanceId);
            ClearSlots(player.buildingSlots, instanceId);
            return removedObject.cardId;
        }

        private static void ClearSlots(string[] slots, string instanceId)
        {
            for (var index = 0; index < (slots?.Length ?? 0); index++)
                if (string.Equals(slots[index], instanceId, StringComparison.Ordinal)) slots[index] = null;
        }

        private static string[] RemoveFirst(string[] values, string value)
        {
            var result = new List<string>(values ?? Array.Empty<string>());
            var index = result.IndexOf(value);
            if (index < 0) index = result.FindIndex(string.IsNullOrEmpty);
            if (index < 0) throw new InvalidOperationException($"Card '{value}' is not present in the authoritative hand projection.");
            result.RemoveAt(index);
            return result.ToArray();
        }
    }
}
