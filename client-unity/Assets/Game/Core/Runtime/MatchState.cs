using System;
using System.Collections.Generic;
using System.Linq;

namespace BiomeRivals.Core
{
    [Serializable]
    public sealed class BattlefieldStatusStateDto
    {
        public string statusId = string.Empty;
        public int remainingDuration;
        public string sourcePlayerId = string.Empty;
        public string sourceCardId = string.Empty;
        public string sourceInstanceId = string.Empty;
        public string effectId = string.Empty;
        public int attackModifier;
        public int boundAttackModifier;
    }

    [Serializable]
    public sealed class PlayerStatusStateDto
    {
        public string statusId = string.Empty;
        public int remainingDuration;
        public string sourcePlayerId = string.Empty;
        public string sourceCardId = string.Empty;
        public string sourceInstanceId = string.Empty;
        public string effectId = string.Empty;
    }

    [Serializable]
    public sealed class BattlefieldObjectStateDto
    {
        public string instanceId = string.Empty;
        public string cardId = string.Empty;
        public string cardType = string.Empty;
        public int attack;
        public int health;
        public int maxHealth;
        public int adjacencyHealthModifier;
        public string slotKind = string.Empty;
        public int slotIndex;
        public int occupiedSlots;
        public int summonedTurn;
        public bool hasAttacked;
        public string[] keywords = Array.Empty<string>();
        public int temporaryAttackModifier;
        public int temporaryAttackModifierExpiresOnTurn;
        public int temporaryHealthModifier;
        public int temporaryHealthModifierExpiresOnTurn;
        public BattlefieldStatusStateDto[] statuses = Array.Empty<BattlefieldStatusStateDto>();
    }

    [Serializable]
    public sealed class EquipmentStateDto
    {
        public string instanceId = string.Empty;
        public string cardId = string.Empty;
        public int attack;
        public int durability;
        public int maxDurability;
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
        public int temporaryRedstone;
        public int totalRedstone;
        public int redstoneCapacity;
        public string[] hand = Array.Empty<string>();
        public int deckCount;
        public int buriedCount;
        public bool excavatedThisTurn;
        public string[] discardPile = Array.Empty<string>();
        public int fatigueCount;
        public EquipmentStateDto equipment;
        public bool heroHasAttacked;
        public int cardsPlayedThisTurn;
        public bool hasTargetedEnemyObjectThisTurn;
        public bool heroLifeLostThisTurn;
        public string[] triggeredEffectKeysThisTurn = Array.Empty<string>();
        public PlayerStatusStateDto[] statuses = Array.Empty<PlayerStatusStateDto>();
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
            if (snapshot.activePlayerIndex < 0 || snapshot.activePlayerIndex >= snapshot.players.Length)
                throw new InvalidOperationException("Snapshot contains an invalid active player index.");
            foreach (var player in snapshot.players)
            {
                if (player == null || !FactionIds.IsSupported(player.factionId))
                    throw new InvalidOperationException("Snapshot contains an unsupported player faction.");
                if (player.cardsPlayedThisTurn < 0)
                    throw new InvalidOperationException("Snapshot contains an invalid card play counter.");
                if (player.redstoneCapacity < 0 || player.redstoneCapacity > 10 ||
                    player.redstone < 0 || player.redstone > player.redstoneCapacity ||
                    player.temporaryRedstone < 0 || player.temporaryRedstone > 3 ||
                    player.totalRedstone != player.redstone + player.temporaryRedstone ||
                    player.temporaryRedstone > 0 &&
                    (snapshot.status != "ACTIVE" || snapshot.players[snapshot.activePlayerIndex] != player))
                    throw new InvalidOperationException("Snapshot contains contradictory redstone pools.");
                if (player.statuses == null) player.statuses = Array.Empty<PlayerStatusStateDto>();
                var seenPlayerStatuses = new HashSet<string>(StringComparer.Ordinal);
                foreach (var status in player.statuses)
                {
                    if (status == null || status.statusId != "DARK" || status.remainingDuration != 1 ||
                        string.IsNullOrWhiteSpace(status.sourcePlayerId) ||
                        status.sourcePlayerId == player.playerId ||
                        string.IsNullOrWhiteSpace(status.sourceInstanceId) ||
                        (status.sourceCardId != "cd_004" && status.sourceCardId != "cd_006") ||
                        (status.effectId != "effect.cd_004.01" && status.effectId != "effect.cd_006.01") ||
                        ((status.sourceCardId == "cd_004") != (status.effectId == "effect.cd_004.01")) ||
                        !seenPlayerStatuses.Add(status.statusId))
                        throw new InvalidOperationException("Snapshot contains an invalid player status.");
                }
                if (player.triggeredEffectKeysThisTurn == null) player.triggeredEffectKeysThisTurn = Array.Empty<string>();
                if (player.triggeredEffectKeysThisTurn.Any(value => string.IsNullOrWhiteSpace(value) ||
                    !System.Text.RegularExpressions.Regex.IsMatch(value, "^object-[0-9]+:effect\\.(db_004|pf_005|si_007|or_(002|004|007))\\.01$")) ||
                    player.triggeredEffectKeysThisTurn.Distinct(StringComparer.Ordinal).Count() != player.triggeredEffectKeysThisTurn.Length)
                    throw new InvalidOperationException("Snapshot contains invalid once-per-turn effect markers.");
                else if (player.buriedCount < 0 || player.buriedCount > player.deckCount)
                    throw new InvalidOperationException("Snapshot contains an invalid buried card count.");
                if (player.equipment != null && string.IsNullOrEmpty(player.equipment.instanceId) &&
                    string.IsNullOrEmpty(player.equipment.cardId) && player.equipment.attack == 0 &&
                    player.equipment.durability == 0 && player.equipment.maxDurability == 0)
                    player.equipment = null;
                if (player.equipment != null && (string.IsNullOrWhiteSpace(player.equipment.instanceId) ||
                    string.IsNullOrWhiteSpace(player.equipment.cardId) || player.equipment.attack <= 0 ||
                    player.equipment.durability <= 0 || player.equipment.durability > player.equipment.maxDurability))
                    throw new InvalidOperationException("Snapshot contains invalid equipment.");
                foreach (var battlefieldObject in player.battlefield ?? Array.Empty<BattlefieldObjectStateDto>())
                {
                    if (battlefieldObject == null) throw new InvalidOperationException("Snapshot contains a missing battlefield object.");
                    if (battlefieldObject.temporaryHealthModifier < 0 || battlefieldObject.temporaryHealthModifierExpiresOnTurn < 0 ||
                        (battlefieldObject.temporaryHealthModifier == 0) != (battlefieldObject.temporaryHealthModifierExpiresOnTurn == 0) ||
                        (battlefieldObject.temporaryHealthModifier > 0 &&
                            battlefieldObject.maxHealth - battlefieldObject.temporaryHealthModifier < 1))
                        throw new InvalidOperationException("Snapshot contains an invalid temporary health modifier.");
                    var seenStatuses = new HashSet<string>(StringComparer.Ordinal);
                    foreach (var status in battlefieldObject.statuses ?? Array.Empty<BattlefieldStatusStateDto>())
                    {
                        if (status == null || (status.statusId != "SLOW" && status.statusId != "POISON" && status.statusId != "FIRE") || status.remainingDuration < 1 ||
                            string.IsNullOrWhiteSpace(status.sourcePlayerId) || string.IsNullOrWhiteSpace(status.sourceCardId) ||
                            string.IsNullOrWhiteSpace(status.effectId) || status.attackModifier > 0 ||
                            status.boundAttackModifier > 0 || status.attackModifier < status.boundAttackModifier ||
                            !seenStatuses.Add(status.statusId) ||
                            (status.statusId == "POISON" && (status.remainingDuration > 3 ||
                                status.attackModifier != 0 || status.boundAttackModifier != 0)) ||
                            (status.statusId == "FIRE" && (status.remainingDuration > 2 ||
                                status.attackModifier != 0 || status.boundAttackModifier != 0)))
                            throw new InvalidOperationException("Snapshot contains an invalid battlefield status.");
                    }
                }
            }
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
            MatchEventDto previousEvent = null;
            int[] lifeBeforePreviousEvent = null;
            foreach (var matchEvent in batch.events ?? Array.Empty<MatchEventDto>())
            {
                var lifeBeforeEvent = Current.players.Select(player => player.life).ToArray();
                Apply(matchEvent, previousEvent, lifeBeforePreviousEvent);
                previousEvent = matchEvent;
                lifeBeforePreviousEvent = lifeBeforeEvent;
            }
            if (batch.events != null && batch.events.Length > 0) Current.lastEventId = batch.events[batch.events.Length - 1].eventId;
            Current.revision = batch.revision;
            Changed?.Invoke(Current);
        }

        private void Apply(MatchEventDto matchEvent, MatchEventDto previousEvent, int[] lifeBeforePreviousEvent)
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
                    ValidatePaidResourceProjection(player, payload, payload.paymentMethod);
                    player.hand = RemoveFirst(player.hand, payload.cardId);
                    ApplyResourceProjection(player, payload, false);
                    player.cardsPlayedThisTurn = payload.cardsPlayedThisTurn;
                    player.hasTargetedEnemyObjectThisTurn = payload.hasTargetedEnemyObjectThisTurn;
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
                    ValidatePaidResourceProjection(playingPlayer, payload, "REDSTONE");
                    playingPlayer.hand = RemoveFirst(playingPlayer.hand, payload.cardId);
                    if (playingPlayer.hand.Length != payload.handCount) throw new InvalidOperationException("Play event hand count does not match projected hand.");
                    ApplyResourceProjection(playingPlayer, payload, false);
                    playingPlayer.cardsPlayedThisTurn = payload.cardsPlayedThisTurn;
                    playingPlayer.hasTargetedEnemyObjectThisTurn = payload.hasTargetedEnemyObjectThisTurn;
                    var playedDiscard = new List<string>(playingPlayer.discardPile ?? Array.Empty<string>()) { payload.cardId };
                    if (playedDiscard.Count != payload.discardCount) throw new InvalidOperationException("Play event discard count does not match projected discard pile.");
                    playingPlayer.discardPile = playedDiscard.ToArray();
                    break;
                case MatchEventTypes.CardEquipped:
                    var equippingPlayer = FindPlayer(payload.playerId);
                    ValidatePaidResourceProjection(equippingPlayer, payload, "REDSTONE");
                    equippingPlayer.hand = RemoveFirst(equippingPlayer.hand, payload.cardId);
                    if (equippingPlayer.hand.Length != payload.handCount)
                        throw new InvalidOperationException("Equipment event hand count does not match projected hand.");
                    ApplyResourceProjection(equippingPlayer, payload, false);
                    equippingPlayer.cardsPlayedThisTurn = payload.cardsPlayedThisTurn;
                    equippingPlayer.hasTargetedEnemyObjectThisTurn = payload.hasTargetedEnemyObjectThisTurn;
                    equippingPlayer.equipment = new EquipmentStateDto
                    {
                        instanceId = payload.instanceId, cardId = payload.cardId, attack = payload.attack,
                        durability = payload.durability, maxDurability = payload.maxDurability
                    };
                    Current.nextInstanceId = payload.nextInstanceId;
                    break;
                case MatchEventTypes.EquipmentDurabilityChanged:
                    var durabilityPlayer = FindPlayer(payload.playerId);
                    if (durabilityPlayer.equipment == null || durabilityPlayer.equipment.instanceId != payload.instanceId)
                        throw new InvalidOperationException("Durability event references unknown equipment.");
                    durabilityPlayer.equipment.durability = payload.durability;
                    durabilityPlayer.equipment.maxDurability = payload.maxDurability;
                    break;
                case MatchEventTypes.EquipmentDestroyed:
                    var destroyedEquipmentPlayer = FindPlayer(payload.playerId);
                    if (destroyedEquipmentPlayer.equipment == null || destroyedEquipmentPlayer.equipment.instanceId != payload.instanceId)
                        throw new InvalidOperationException("Destroy event references unknown equipment.");
                    destroyedEquipmentPlayer.equipment = null;
                    var equipmentDiscard = new List<string>(destroyedEquipmentPlayer.discardPile ?? Array.Empty<string>()) { payload.cardId };
                    if (equipmentDiscard.Count != payload.discardCount)
                        throw new InvalidOperationException("Equipment destroy discard count does not match projection.");
                    destroyedEquipmentPlayer.discardPile = equipmentDiscard.ToArray();
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
                        targetPlayerId = payload.targetPlayerId,
                        targetInstanceId = payload.targetInstanceId,
                        options = CloneChoiceOptions(payload.options)
                    };
                    ValidatePendingChoice(Current, offeredChoice, "Choice event");
                    Current.pendingChoice = offeredChoice;
                    if (payload.effectId == "effect.si_007.01")
                    {
                        var hutOwner = FindPlayer(payload.playerId);
                        var hutTriggerKey = $"{payload.sourceInstanceId}:{payload.effectId}";
                        if (Array.IndexOf(hutOwner.triggeredEffectKeysThisTurn ?? Array.Empty<string>(), hutTriggerKey) < 0)
                            hutOwner.triggeredEffectKeysThisTurn = (hutOwner.triggeredEffectKeysThisTurn ?? Array.Empty<string>())
                                .Concat(new[] { hutTriggerKey }).ToArray();
                    }
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
                    excavatingPlayer.excavatedThisTurn = true;
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
                    if (payload.effectId == "effect.nt_002.01")
                    {
                        var payment = previousEvent?.payload;
                        var sourceOwner = Current.players.FirstOrDefault(candidate =>
                            (candidate?.battlefield ?? Array.Empty<BattlefieldObjectStateDto>()).Any(value =>
                                value != null && value.instanceId == payload.sourceInstanceId && value.cardId == "nt_002" &&
                                value.cardType == "UNIT" && value.health > 0));
                        var expectedArmor = Math.Max(0, damagedPlayer.armor - 1);
                        var expectedLife = Math.Max(0, damagedPlayer.life - Math.Max(0, 1 - damagedPlayer.armor));
                        if (sourceOwner == null || sourceOwner == damagedPlayer ||
                            Current.players[Current.activePlayerIndex] != sourceOwner || payload.sourceCardId != "nt_002" ||
                            payload.damage != 1 || payload.damageType != "NORMAL" ||
                            payload.armor != expectedArmor || payload.life != expectedLife ||
                            previousEvent == null || previousEvent.type != MatchEventTypes.RedstoneChanged || payment == null ||
                            payment.playerId != sourceOwner.playerId || payment.reason != "AUTOMATIC_PAYMENT" ||
                            payment.sourceCardId != "nt_002" || payment.sourceInstanceId != payload.sourceInstanceId ||
                            payment.effectId != "effect.nt_002.01")
                            throw new InvalidOperationException("Piglin magma damage does not follow its authoritative payment.");
                    }
                    damagedPlayer.life = payload.life;
                    damagedPlayer.armor = payload.armor;
                    break;
                case MatchEventTypes.HeroLifeLossMarked:
                    var markedPlayer = FindPlayer(payload.playerId);
                    var markedPlayerIndex = Array.IndexOf(Current.players, markedPlayer);
                    var sourcePayload = previousEvent?.payload;
                    var validSource = previousEvent != null && sourcePayload != null &&
                        ((previousEvent.type == MatchEventTypes.HeroDamaged ||
                          previousEvent.type == MatchEventTypes.FatigueDamage) &&
                         sourcePayload.playerId == payload.playerId ||
                         previousEvent.type == MatchEventTypes.AttackResolved &&
                         (sourcePayload.targetType == "HERO" && sourcePayload.targetPlayerId == payload.playerId ||
                          sourcePayload.attackerInstanceId == MatchAttackerIds.Hero &&
                          sourcePayload.targetType != "HERO" && sourcePayload.attackerPlayerId == payload.playerId));
                    if (markedPlayer.heroLifeLostThisTurn || markedPlayer.life <= 0 ||
                        markedPlayer.life != payload.life || markedPlayer.armor != payload.armor ||
                        !validSource || lifeBeforePreviousEvent == null ||
                        lifeBeforePreviousEvent[markedPlayerIndex] <= markedPlayer.life ||
                        Current.turn != payload.turn || payload.sourceEventId != previousEvent.eventId ||
                        Current.activePlayerIndex < 0 || Current.activePlayerIndex >= Current.players.Length ||
                        Current.players[Current.activePlayerIndex].playerId != payload.activePlayerId)
                        throw new InvalidOperationException("Hero life-loss marker does not match the authoritative damage window.");
                    markedPlayer.heroLifeLostThisTurn = true;
                    break;
                case MatchEventTypes.HeroHealed:
                    FindPlayer(payload.playerId).life = payload.life;
                    break;
                case MatchEventTypes.ArmorGained:
                    FindPlayer(payload.playerId).armor = payload.armor;
                    break;
                case MatchEventTypes.RedstoneChanged:
                    var resourcePlayer = FindPlayer(payload.playerId);
                    if (payload.turn != Current.turn ||
                        (payload.reason != "TEMPORARY_GRANTED" && payload.reason != "AUTOMATIC_PAYMENT" &&
                         payload.reason != "TEMPORARY_EXPIRED"))
                        throw new InvalidOperationException("Redstone event has an invalid turn or reason.");
                    if (payload.reason == "TEMPORARY_EXPIRED")
                    {
                        if (resourcePlayer.temporaryRedstone <= 0 || payload.temporaryRedstone != 0 ||
                            payload.redstone != resourcePlayer.redstone ||
                            !string.IsNullOrEmpty(payload.sourceCardId) ||
                            !string.IsNullOrEmpty(payload.sourceInstanceId) || !string.IsNullOrEmpty(payload.effectId))
                            throw new InvalidOperationException("Temporary redstone expiry is contradictory.");
                    }
                    else if (string.IsNullOrWhiteSpace(payload.sourceCardId) ||
                             string.IsNullOrWhiteSpace(payload.sourceInstanceId) ||
                             string.IsNullOrWhiteSpace(payload.effectId))
                        throw new InvalidOperationException("Redstone event is missing its source.");
                    else if (payload.reason == "TEMPORARY_GRANTED" &&
                             (payload.redstone != resourcePlayer.redstone ||
                              payload.temporaryRedstone <= resourcePlayer.temporaryRedstone ||
                              payload.redstoneCapacity != resourcePlayer.redstoneCapacity) ||
                             payload.reason == "AUTOMATIC_PAYMENT" &&
                             (payload.redstone > resourcePlayer.redstone ||
                              payload.temporaryRedstone > resourcePlayer.temporaryRedstone ||
                              payload.totalRedstone >= resourcePlayer.totalRedstone ||
                              payload.redstoneCapacity != resourcePlayer.redstoneCapacity))
                        throw new InvalidOperationException("Redstone event does not match its resource change reason.");
                    if (payload.reason == "AUTOMATIC_PAYMENT")
                        ValidatePaidResourceProjection(resourcePlayer, payload, "REDSTONE");
                    if (payload.effectId == "effect.nt_002.01")
                    {
                        var source = FindObject(resourcePlayer, payload.sourceInstanceId);
                        if (payload.reason != "AUTOMATIC_PAYMENT" || payload.sourceCardId != "nt_002" ||
                            source.cardId != "nt_002" || source.cardType != "UNIT" || source.health <= 0 ||
                            Current.players[Current.activePlayerIndex] != resourcePlayer ||
                            resourcePlayer.totalRedstone - payload.totalRedstone != 1)
                            throw new InvalidOperationException("Piglin magma payment is not a valid one-energy end-phase payment.");
                    }
                    else if (payload.effectId == "effect.nt_007.01")
                    {
                        var source = FindObject(resourcePlayer, payload.sourceInstanceId);
                        var previousPayload = previousEvent?.payload;
                        if (payload.reason != "TEMPORARY_GRANTED" || payload.sourceCardId != "nt_007" ||
                            source.cardId != "nt_007" || source.cardType != "BUILDING" || source.health <= 0 ||
                            Current.players[Current.activePlayerIndex] != resourcePlayer ||
                            !resourcePlayer.heroLifeLostThisTurn ||
                            previousEvent == null || previousEvent.type != MatchEventTypes.HeroLifeLossMarked &&
                            !(previousEvent.type == MatchEventTypes.RedstoneChanged &&
                              previousPayload?.effectId == "effect.nt_007.01" &&
                              previousPayload.playerId == resourcePlayer.playerId) ||
                            payload.redstone != resourcePlayer.redstone ||
                            payload.temporaryRedstone != resourcePlayer.temporaryRedstone + 1 ||
                            payload.totalRedstone != resourcePlayer.totalRedstone + 1)
                            throw new InvalidOperationException("Respawn Anchor grant does not match an own-turn first-life-loss trigger.");
                    }
                    ApplyResourceProjection(resourcePlayer, payload, true);
                    break;
                case MatchEventTypes.ObjectStatsChanged:
                    var statsObject = FindObject(FindPlayer(payload.playerId), payload.instanceId);
                    if (payload.effectId == "effect.nt_002.01")
                    {
                        var piglinOwner = FindPlayer(payload.playerId);
                        if (!piglinOwner.heroLifeLostThisTurn || statsObject.cardId != "nt_002" ||
                            statsObject.cardType != "UNIT" || statsObject.health <= 0 ||
                            payload.reason != "PERMANENT_STAT_MODIFIER" ||
                            payload.sourceCardId != "nt_002" || payload.sourceInstanceId != statsObject.instanceId ||
                            payload.attack != statsObject.attack + 1 || payload.health != statsObject.health + 1 ||
                            payload.maxHealth != statsObject.maxHealth + 1 ||
                            payload.temporaryAttackModifier != statsObject.temporaryAttackModifier ||
                            payload.temporaryAttackModifierExpiresOnTurn != statsObject.temporaryAttackModifierExpiresOnTurn ||
                            payload.temporaryHealthModifier != statsObject.temporaryHealthModifier ||
                            payload.temporaryHealthModifierExpiresOnTurn != statsObject.temporaryHealthModifierExpiresOnTurn)
                            throw new InvalidOperationException("Piglin growth does not match a first-life-loss permanent stat change.");
                    }
                    statsObject.attack = payload.attack;
                    statsObject.health = payload.health;
                    if (payload.reason == "AURA_RECALCULATED" || payload.reason == "PERMANENT_HEALTH_MODIFIER" ||
                        payload.reason == "PERMANENT_STAT_MODIFIER" || payload.reason == "TEMPORARY_HEALTH_MODIFIER" ||
                        payload.reason == "TEMPORARY_EXPIRED")
                    {
                        statsObject.maxHealth = payload.maxHealth;
                        if (payload.reason == "AURA_RECALCULATED")
                            statsObject.adjacencyHealthModifier = payload.adjacencyHealthModifier;
                    }
                    statsObject.temporaryAttackModifier = payload.temporaryAttackModifier;
                    statsObject.temporaryAttackModifierExpiresOnTurn = payload.temporaryAttackModifierExpiresOnTurn;
                    statsObject.temporaryHealthModifier = payload.temporaryHealthModifier;
                    statsObject.temporaryHealthModifierExpiresOnTurn = payload.temporaryHealthModifierExpiresOnTurn;
                    if ((payload.effectId == "effect.db_004.01" || payload.effectId == "effect.pf_005.01" ||
                        payload.effectId == "effect.or_002.01" || payload.effectId == "effect.or_004.01" ||
                        payload.effectId == "effect.or_007.01" || payload.effectId == "effect.si_007.01") &&
                        !string.IsNullOrEmpty(payload.sourceInstanceId))
                    {
                        var sourceOwner = Current.players.FirstOrDefault(candidate =>
                            (candidate?.battlefield ?? Array.Empty<BattlefieldObjectStateDto>()).Any(value =>
                                value != null && value.instanceId == payload.sourceInstanceId));
                        if (sourceOwner == null) throw new InvalidOperationException("Movement reaction source is missing from the battlefield.");
                        var triggerKey = $"{payload.sourceInstanceId}:{payload.effectId}";
                        if (Array.IndexOf(sourceOwner.triggeredEffectKeysThisTurn ?? Array.Empty<string>(), triggerKey) < 0)
                            sourceOwner.triggeredEffectKeysThisTurn = (sourceOwner.triggeredEffectKeysThisTurn ?? Array.Empty<string>())
                                .Concat(new[] { triggerKey }).ToArray();
                    }
                    break;
                case MatchEventTypes.ObjectStatusApplied:
                    var statusObject = FindObject(FindPlayer(payload.playerId), payload.instanceId);
                    var statuses = new List<BattlefieldStatusStateDto>(statusObject.statuses ?? Array.Empty<BattlefieldStatusStateDto>());
                    statuses.RemoveAll(value => value != null && value.statusId == payload.statusId);
                    statuses.Add(new BattlefieldStatusStateDto
                    {
                        statusId = payload.statusId,
                        remainingDuration = payload.remainingDuration,
                        sourcePlayerId = payload.sourcePlayerId,
                        sourceCardId = payload.sourceCardId,
                        sourceInstanceId = payload.sourceInstanceId,
                        effectId = payload.effectId,
                        attackModifier = payload.statusAttackModifier,
                        boundAttackModifier = payload.boundAttackModifier
                    });
                    statusObject.statuses = statuses.ToArray();
                    statusObject.attack = payload.attack;
                    statusObject.health = payload.health;
                    break;
                case MatchEventTypes.ObjectStatusTicked:
                    var tickedObject = FindObject(FindPlayer(payload.playerId), payload.instanceId);
                    var tickedStatuses = new List<BattlefieldStatusStateDto>(tickedObject.statuses ?? Array.Empty<BattlefieldStatusStateDto>());
                    var tickedStatus = tickedStatuses.SingleOrDefault(value => value != null && value.statusId == payload.statusId);
                    if (tickedStatus == null)
                        throw new InvalidOperationException("Status tick does not match exactly one projected status.");
                    tickedStatus.remainingDuration = payload.remainingDuration;
                    tickedStatus.sourcePlayerId = payload.sourcePlayerId;
                    tickedStatus.sourceCardId = payload.sourceCardId;
                    tickedStatus.sourceInstanceId = payload.sourceInstanceId;
                    tickedStatus.effectId = payload.effectId;
                    tickedStatus.attackModifier = payload.statusAttackModifier;
                    tickedStatus.boundAttackModifier = payload.boundAttackModifier;
                    tickedObject.attack = payload.attack;
                    tickedObject.health = payload.health;
                    break;
                case MatchEventTypes.ObjectStatusRemoved:
                    var clearedObject = FindObject(FindPlayer(payload.playerId), payload.instanceId);
                    var remainingStatuses = new List<BattlefieldStatusStateDto>(clearedObject.statuses ?? Array.Empty<BattlefieldStatusStateDto>());
                    if (remainingStatuses.RemoveAll(value => value != null && value.statusId == payload.statusId) != 1)
                        throw new InvalidOperationException("Status removal does not match exactly one projected status.");
                    clearedObject.statuses = remainingStatuses.ToArray();
                    clearedObject.attack = payload.attack;
                    clearedObject.health = payload.health;
                    break;
                case MatchEventTypes.PlayerStatusApplied:
                    var statusPlayer = FindPlayer(payload.playerId);
                    var playerStatuses = new List<PlayerStatusStateDto>(statusPlayer.statuses ?? Array.Empty<PlayerStatusStateDto>());
                    playerStatuses.RemoveAll(value => value != null && value.statusId == payload.statusId);
                    playerStatuses.Add(new PlayerStatusStateDto
                    {
                        statusId = payload.statusId,
                        remainingDuration = payload.remainingDuration,
                        sourcePlayerId = payload.sourcePlayerId,
                        sourceCardId = payload.sourceCardId,
                        sourceInstanceId = payload.sourceInstanceId,
                        effectId = payload.effectId
                    });
                    statusPlayer.statuses = playerStatuses.ToArray();
                    statusPlayer.hasTargetedEnemyObjectThisTurn = payload.hasTargetedEnemyObjectThisTurn;
                    break;
                case MatchEventTypes.PlayerStatusTicked:
                    var tickedPlayer = FindPlayer(payload.playerId);
                    var tickedPlayerStatus = (tickedPlayer.statuses ?? Array.Empty<PlayerStatusStateDto>())
                        .SingleOrDefault(value => value != null && value.statusId == payload.statusId);
                    if (tickedPlayerStatus == null)
                        throw new InvalidOperationException("Player status tick does not match exactly one projected status.");
                    tickedPlayerStatus.remainingDuration = payload.remainingDuration;
                    tickedPlayerStatus.sourcePlayerId = payload.sourcePlayerId;
                    tickedPlayerStatus.sourceCardId = payload.sourceCardId;
                    tickedPlayerStatus.sourceInstanceId = payload.sourceInstanceId;
                    tickedPlayerStatus.effectId = payload.effectId;
                    tickedPlayer.hasTargetedEnemyObjectThisTurn = payload.hasTargetedEnemyObjectThisTurn;
                    break;
                case MatchEventTypes.PlayerStatusRemoved:
                    var clearedPlayer = FindPlayer(payload.playerId);
                    var remainingPlayerStatuses = new List<PlayerStatusStateDto>(clearedPlayer.statuses ?? Array.Empty<PlayerStatusStateDto>());
                    if (remainingPlayerStatuses.RemoveAll(value => value != null && value.statusId == payload.statusId) != 1)
                        throw new InvalidOperationException("Player status removal does not match exactly one projected status.");
                    clearedPlayer.statuses = remainingPlayerStatuses.ToArray();
                    clearedPlayer.hasTargetedEnemyObjectThisTurn = payload.hasTargetedEnemyObjectThisTurn;
                    break;
                case MatchEventTypes.ObjectMoved:
                    var movedPlayer = FindPlayer(payload.playerId);
                    var movedObject = FindObject(movedPlayer, payload.instanceId);
                    if (movedObject.cardType != "UNIT" || movedObject.slotIndex != payload.fromSlotIndex ||
                        payload.toSlotIndex < 0 || payload.toSlotIndex >= movedPlayer.unitSlots.Length ||
                        !string.IsNullOrEmpty(movedPlayer.unitSlots[payload.toSlotIndex]))
                        throw new InvalidOperationException("Move event contains an invalid unit destination.");
                    movedPlayer.unitSlots[payload.fromSlotIndex] = null;
                    movedPlayer.unitSlots[payload.toSlotIndex] = movedObject.instanceId;
                    movedObject.slotIndex = payload.toSlotIndex;
                    break;
                case MatchEventTypes.PhaseChanged:
                    Current.phase = payload.phase;
                    break;
                case MatchEventTypes.AttackResolved:
                    var attackerPlayer = FindPlayer(payload.attackerPlayerId);
                    attackerPlayer.hasTargetedEnemyObjectThisTurn = payload.hasTargetedEnemyObjectThisTurn;
                    if (payload.attackerInstanceId == MatchAttackerIds.Hero)
                    {
                        attackerPlayer.life = payload.attackerHealth;
                        attackerPlayer.armor = payload.attackerArmor;
                        attackerPlayer.heroHasAttacked = true;
                    }
                    else
                    {
                        var attacker = FindObject(attackerPlayer, payload.attackerInstanceId);
                        attacker.health = payload.attackerHealth;
                        attacker.hasAttacked = true;
                    }
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
                    activePlayer.excavatedThisTurn = false;
                    activePlayer.heroHasAttacked = false;
                    activePlayer.cardsPlayedThisTurn = 0;
                    activePlayer.hasTargetedEnemyObjectThisTurn = false;
                    ApplyResourceProjection(activePlayer, payload, true);
                    foreach (var battlefieldObject in activePlayer.battlefield ?? Array.Empty<BattlefieldObjectStateDto>())
                        if (battlefieldObject != null) battlefieldObject.hasAttacked = false;
                    break;
                case MatchEventTypes.TurnEnded:
                    var endedPlayer = FindPlayer(payload.playerId);
                    endedPlayer.excavatedThisTurn = false;
                    endedPlayer.cardsPlayedThisTurn = 0;
                    endedPlayer.hasTargetedEnemyObjectThisTurn = false;
                    foreach (var turnPlayer in Current.players)
                    {
                        turnPlayer.triggeredEffectKeysThisTurn = Array.Empty<string>();
                        turnPlayer.heroLifeLostThisTurn = false;
                    }
                    break;
                case MatchEventTypes.MatchEnded:
                    Current.status = "FINISHED";
                    Current.winnerPlayerId = payload.winnerPlayerId;
                    break;
            }
        }

        private void ApplyResourceProjection(PlayerStateDto player, MatchEventPayloadDto payload, bool updateCapacity)
        {
            var capacity = updateCapacity ? payload.redstoneCapacity : player.redstoneCapacity;
            if (capacity < 0 || capacity > 10 || payload.redstone < 0 || payload.redstone > capacity ||
                payload.temporaryRedstone < 0 || payload.temporaryRedstone > 3 ||
                payload.totalRedstone != payload.redstone + payload.temporaryRedstone ||
                payload.temporaryRedstone > 0 &&
                (Current.status != "ACTIVE" || Current.players[Current.activePlayerIndex] != player))
                throw new InvalidOperationException("Redstone event contains contradictory resource pools.");
            player.redstone = payload.redstone;
            player.temporaryRedstone = payload.temporaryRedstone;
            player.totalRedstone = payload.totalRedstone;
            if (updateCapacity) player.redstoneCapacity = capacity;
        }

        private static void ValidatePaidResourceProjection(PlayerStateDto player, MatchEventPayloadDto payload,
            string paymentMethod)
        {
            if (paymentMethod == "CRAFTING")
            {
                if (payload.redstone != player.redstone ||
                    payload.temporaryRedstone != player.temporaryRedstone ||
                    payload.totalRedstone != player.totalRedstone)
                    throw new InvalidOperationException("Crafting material payment cannot spend redstone pools.");
                return;
            }
            if (paymentMethod != "REDSTONE")
                throw new InvalidOperationException("Resource payment method is unsupported.");
            var cost = player.totalRedstone - payload.totalRedstone;
            if (cost < 0 || cost > player.totalRedstone)
                throw new InvalidOperationException("Payment event has an invalid redstone delta.");
            var temporaryPayment = Math.Min(player.temporaryRedstone, cost);
            if (payload.temporaryRedstone != player.temporaryRedstone - temporaryPayment ||
                payload.redstone != player.redstone - (cost - temporaryPayment))
                throw new InvalidOperationException("Payment event did not spend temporary redstone first.");
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
            var archaeology = choice != null && choice.kind == "ARCHAEOLOGY_TOP_3" &&
                choice.effectId == "effect.db_003.01" && choice.sourceCardId == "db_003";
            var topCardScry = choice != null && choice.kind == "TOP_CARD_SCRY" &&
                choice.effectId == "effect.cd_001.01" && choice.sourceCardId == "cd_001";
            var riptideMovement = choice != null && choice.kind == "MOVE_UNIT" &&
                choice.effectId == "effect.or_006.01" && choice.sourceCardId == "or_006";
            var salmonMovement = choice != null && choice.kind == "MOVE_UNIT" &&
                choice.effectId == "effect.or_001.01" && choice.sourceCardId == "or_001";
            var prismarineMovement = choice != null && choice.kind == "MOVE_UNIT" &&
                choice.effectId == "effect.tk_012.01" && choice.sourceCardId == "tk_012";
            var snowHutHealing = choice != null && choice.kind == "HEAL_UNIT" &&
                choice.effectId == "effect.si_007.01" && choice.sourceCardId == "si_007";
            var movement = riptideMovement || salmonMovement || prismarineMovement;
            if (choice == null || (!archaeology && !topCardScry && !movement && !snowHutHealing) || string.IsNullOrWhiteSpace(choice.choiceId) ||
                string.IsNullOrWhiteSpace(choice.sourceInstanceId) || choice.options == null ||
                choice.options.Length > (snowHutHealing ? 4 : movement ? 2 : archaeology ? 3 : 1) || state.status != "ACTIVE" ||
                ((archaeology || topCardScry || snowHutHealing) && state.phase != "MAIN") || (riptideMovement && state.phase != "COMBAT") ||
                ((salmonMovement || prismarineMovement) && state.phase != "MAIN"))
                throw new InvalidOperationException($"{source} contains an invalid pending card choice.");
            if (topCardScry && choice.options.Length != 1)
                throw new InvalidOperationException($"{source} top-card scry must contain exactly one option.");
            var owner = FindPlayer(state, choice.playerId);
            var sourceValid = archaeology || topCardScry || snowHutHealing
                ? (owner?.battlefield ?? Array.Empty<BattlefieldObjectStateDto>()).Any(value =>
                    value != null && value.instanceId == choice.sourceInstanceId && value.cardId == choice.sourceCardId &&
                    (!snowHutHealing || value.cardType == "BUILDING" && value.health > 0)) &&
                    (!snowHutHealing || choice.targetPlayerId == owner.playerId && string.IsNullOrEmpty(choice.targetInstanceId))
                : prismarineMovement
                    ? owner != null && choice.targetPlayerId == owner.playerId &&
                        choice.sourceInstanceId.StartsWith("effect-", StringComparison.Ordinal) &&
                        int.TryParse(choice.sourceInstanceId.Substring("effect-".Length), out _) &&
                        (owner.battlefield ?? Array.Empty<BattlefieldObjectStateDto>()).Any(value =>
                            value != null && value.instanceId == choice.targetInstanceId && value.health > 0)
                : !string.IsNullOrWhiteSpace(choice.targetPlayerId) && !string.IsNullOrWhiteSpace(choice.targetInstanceId) &&
                    (!salmonMovement || (owner?.battlefield ?? Array.Empty<BattlefieldObjectStateDto>()).Any(value =>
                        value != null && value.instanceId == choice.sourceInstanceId && value.cardId == choice.sourceCardId &&
                        value.instanceId == choice.targetInstanceId));
            if (owner == null || state.activePlayerIndex < 0 || state.activePlayerIndex >= state.players.Length ||
                !ReferenceEquals(state.players[state.activePlayerIndex], owner) || !sourceValid)
                throw new InvalidOperationException($"{source} pending choice has no active source object.");

            BattlefieldObjectStateDto[] snowHutCandidates = null;
            if (snowHutHealing)
            {
                var injured = (owner.battlefield ?? Array.Empty<BattlefieldObjectStateDto>())
                    .Where(value => value != null && value.cardType == "UNIT" && value.health > 0 && value.health < value.maxHealth)
                    .ToArray();
                var greatestMissingHealth = injured.Length == 0 ? 0 : injured.Max(value => value.maxHealth - value.health);
                snowHutCandidates = injured.Where(value => value.maxHealth - value.health == greatestMissingHealth)
                    .OrderBy(value => value.slotIndex)
                    .ThenBy(value => value.instanceId, StringComparer.Ordinal)
                    .ToArray();
                if (snowHutCandidates.Length < 2 || choice.options.Length != snowHutCandidates.Length)
                    throw new InvalidOperationException($"{source} snow-hut choice does not contain every tied target.");
            }

            var isOwnerProjection = choice.playerId == state.viewerPlayerId;
            for (var index = 0; index < choice.options.Length; index++)
            {
                var option = choice.options[index];
                if (option == null || option.optionIndex != index ||
                    (isOwnerProjection && string.IsNullOrWhiteSpace(option.cardId)) ||
                    ((archaeology || topCardScry) && !isOwnerProjection && (!string.IsNullOrEmpty(option.cardId) || option.selectable)) ||
                    ((archaeology || topCardScry) && option.slotIndex != -1) ||
                    (topCardScry && isOwnerProjection && (choice.options.Length != 1 || !option.selectable)) ||
                    ((movement || snowHutHealing) && option.slotIndex < 0) ||
                    ((movement || snowHutHealing) && !isOwnerProjection && option.selectable) ||
                    (snowHutHealing && (string.IsNullOrWhiteSpace(option.cardId) || isOwnerProjection && !option.selectable)) ||
                    (snowHutHealing && (option.cardId != snowHutCandidates[index].cardId ||
                        option.slotIndex != snowHutCandidates[index].slotIndex)))
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
                    slotIndex = option.slotIndex,
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
                    adjacencyHealthModifier = 0,
                    slotKind = payload.slotKind,
                    slotIndex = payload.slotIndex,
                    occupiedSlots = occupiedSlots,
                    summonedTurn = payload.summonedTurn,
                    hasAttacked = false,
                    keywords = payload.keywords ?? Array.Empty<string>(),
                    temporaryAttackModifier = 0,
                    temporaryAttackModifierExpiresOnTurn = 0,
                    temporaryHealthModifier = 0,
                    temporaryHealthModifierExpiresOnTurn = 0,
                    statuses = Array.Empty<BattlefieldStatusStateDto>()
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
