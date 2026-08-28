namespace BiomeRivalsRules {
  const HAND_LIMIT = 7;

  function seedFromText(seedText: string): number {
    let seed = 17;
    for (let index = 0; index < seedText.length; index += 1) {
      seed = ((seed * 31) + seedText.charCodeAt(index)) | 0;
    }
    return seed;
  }

  function shuffleDeck(deck: string[], seedText: string): void {
    let seed = seedFromText(seedText);
    for (let index = deck.length - 1; index > 0; index -= 1) {
      seed = ((seed * 1664525) + 1013904223) | 0;
      const swapIndex = (seed >>> 0) % (index + 1);
      const value = deck[index]!;
      deck[index] = deck[swapIndex]!;
      deck[swapIndex] = value;
    }
  }

  function prototypeDeck(prefix: string, seedText: string): string[] {
    const deck: string[] = [];
    for (let index = 0; index < 30; index += 1) deck.push(prefix + '_' + ('00' + String((index % 8) + 1)).slice(-3));
    shuffleDeck(deck, seedText);
    return deck;
  }

  function makePlayer(playerId: string, startingCards: number, matchId: string, factionId: FactionId): PlayerState {
    const deck = prototypeDeck(FACTION_CARD_PREFIXES[factionId]!, matchId + ':' + playerId + ':' + factionId);
    const hand: string[] = [];
    for (let index = 0; index < startingCards; index += 1) hand.push(deck.pop()!);
    return {
      playerId: playerId,
      factionId: factionId,
      mulliganCompleted: false,
      life: 30,
      armor: 0,
      redstone: 1,
      redstoneCapacity: 1,
      hand: hand,
      deck: deck,
      buriedCardIds: [],
      discardPile: [],
      fatigueCount: 0,
      unitSlots: [null, null, null, null],
      buildingSlots: [null, null, null],
      battlefield: []
    };
  }

  export function createInitialState(matchId: string, playerIds: string[], factionIds?: FactionId[]): MatchState {
    if (!matchId) throw new Error('matchId is required');
    if (playerIds.length !== 2 || !playerIds[0] || !playerIds[1] || playerIds[0] === playerIds[1]) {
      throw new Error('exactly two unique player ids are required');
    }

    const selectedFactions = factionIds === undefined ? ['plains_forest', 'nether'] as FactionId[] : factionIds;
    if (selectedFactions.length !== 2 || !isFactionId(selectedFactions[0]) || !isFactionId(selectedFactions[1])) {
      throw new Error('exactly two supported faction ids are required');
    }
    const initiativeSourceIndex = (seedFromText(matchId + ':initiative') >>> 0) % 2;
    const orderedPlayerIds = initiativeSourceIndex === 0 ? playerIds : [playerIds[1]!, playerIds[0]!];
    const orderedFactions = initiativeSourceIndex === 0 ? selectedFactions : [selectedFactions[1]!, selectedFactions[0]!];
    const state: MatchState = {
      matchId: matchId,
      protocolVersion: PROTOCOL_VERSION,
      rulesetVersion: RULESET_VERSION,
      revision: 0,
      lastEventId: 0,
      status: 'MULLIGAN',
      turn: 1,
      phase: 'MAIN',
      activePlayerIndex: 0,
      nextInstanceId: 1,
      players: [
        makePlayer(orderedPlayerIds[0]!, 3, matchId, orderedFactions[0]!),
        makePlayer(orderedPlayerIds[1]!, 4, matchId, orderedFactions[1]!)
      ],
      winnerPlayerId: null,
      processedCommandIds: []
    };
    const violations = validateState(state);
    if (violations.length > 0) throw new Error(violations.join('; '));
    return state;
  }

  export function createClientSnapshot(state: MatchState, viewerPlayerId: string): MatchSnapshot {
    const viewerIndex = state.players.map(function (player): string { return player.playerId; }).indexOf(viewerPlayerId);
    if (viewerIndex < 0) throw new Error('snapshot viewer does not belong to this match');
    return {
      matchId: state.matchId,
      viewerPlayerId: viewerPlayerId,
      protocolVersion: state.protocolVersion,
      rulesetVersion: state.rulesetVersion,
      revision: state.revision,
      lastEventId: state.lastEventId,
      status: state.status,
      turn: state.turn,
      phase: state.phase,
      activePlayerIndex: state.activePlayerIndex,
      nextInstanceId: state.nextInstanceId,
      players: state.players.map(function (player, playerIndex): PlayerSnapshot {
        return {
          playerId: player.playerId,
          factionId: player.factionId,
          mulliganCompleted: player.mulliganCompleted,
          life: player.life,
          armor: player.armor,
          redstone: player.redstone,
          redstoneCapacity: player.redstoneCapacity,
          hand: playerIndex === viewerIndex
            ? player.hand.slice()
            : player.hand.map(function (): null { return null; }),
          deckCount: player.deck.length,
          buriedCount: player.buriedCardIds.length,
          discardPile: player.discardPile.slice(),
          fatigueCount: player.fatigueCount,
          unitSlots: player.unitSlots.slice(),
          buildingSlots: player.buildingSlots.slice(),
          battlefield: player.battlefield.map(function (object): BattlefieldObjectState {
            return {
              instanceId: object.instanceId, cardId: object.cardId, cardType: object.cardType,
              attack: object.attack, health: object.health, maxHealth: object.maxHealth,
              slotKind: object.slotKind, slotIndex: object.slotIndex, occupiedSlots: object.occupiedSlots,
              summonedTurn: object.summonedTurn, hasAttacked: object.hasAttacked,
              keywords: object.keywords.slice(),
              temporaryAttackModifier: object.temporaryAttackModifier,
              temporaryAttackModifierExpiresOnTurn: object.temporaryAttackModifierExpiresOnTurn
            };
          })
        };
      }),
      winnerPlayerId: state.winnerPlayerId
    };
  }

  export function createClientEventBatch(batch: MatchEventBatch, viewerPlayerId: string): MatchEventBatch {
    return {
      protocolVersion: batch.protocolVersion,
      rulesetVersion: batch.rulesetVersion,
      revision: batch.revision,
      acknowledgedCommandId: batch.acknowledgedCommandId,
      events: batch.events.map(function (event): MatchEvent {
        const payload: { [key: string]: unknown } = {};
        Object.keys(event.payload).forEach(function (key): void { payload[key] = event.payload[key]; });
        if (event.type === 'CARD_DRAWN' && payload.playerId !== viewerPlayerId) payload.cardId = null;
        if (event.type === 'CARD_GENERATED' && payload.playerId !== viewerPlayerId && payload.destination === 'HAND') payload.cardId = null;
        if (event.type === 'MULLIGAN_COMPLETED' && payload.playerId !== viewerPlayerId && Array.isArray(payload.hand)) {
          payload.hand = (payload.hand as string[]).map(function (): null { return null; });
        }
        return { eventId: event.eventId, type: event.type, payload: payload };
      })
    };
  }

  function cloneState(state: MatchState): MatchState {
    return {
      matchId: state.matchId,
      protocolVersion: state.protocolVersion,
      rulesetVersion: state.rulesetVersion,
      revision: state.revision,
      lastEventId: state.lastEventId,
      status: state.status,
      turn: state.turn,
      phase: state.phase,
      activePlayerIndex: state.activePlayerIndex,
      nextInstanceId: state.nextInstanceId,
      players: state.players.map(function (player): PlayerState {
        return {
          playerId: player.playerId,
          factionId: player.factionId,
          mulliganCompleted: player.mulliganCompleted,
          life: player.life,
          armor: player.armor,
          redstone: player.redstone,
          redstoneCapacity: player.redstoneCapacity,
          hand: player.hand.slice(),
          deck: player.deck.slice(),
          buriedCardIds: player.buriedCardIds.slice(),
          discardPile: player.discardPile.slice(),
          fatigueCount: player.fatigueCount,
          unitSlots: player.unitSlots.slice(),
          buildingSlots: player.buildingSlots.slice(),
          battlefield: player.battlefield.map(function (object): BattlefieldObjectState {
            return {
              instanceId: object.instanceId,
              cardId: object.cardId,
              cardType: object.cardType,
              attack: object.attack,
              health: object.health,
              maxHealth: object.maxHealth,
              slotKind: object.slotKind,
              slotIndex: object.slotIndex,
              occupiedSlots: object.occupiedSlots,
              summonedTurn: object.summonedTurn,
              hasAttacked: object.hasAttacked,
              keywords: object.keywords.slice(),
              temporaryAttackModifier: object.temporaryAttackModifier,
              temporaryAttackModifierExpiresOnTurn: object.temporaryAttackModifierExpiresOnTurn
            };
          })
        };
      }),
      winnerPlayerId: state.winnerPlayerId,
      processedCommandIds: state.processedCommandIds.slice()
    };
  }

  function reject(state: MatchState, code: RejectionCode, message: string): CommandRejected {
    return { accepted: false, state: state, code: code, message: message };
  }

  export function applyCommand(state: MatchState, actorPlayerId: string, command: MatchCommand): CommandResult {
    const violations = validateState(state);
    if (violations.length > 0) return reject(state, 'INVALID_STATE', violations.join('; '));
    if (!command || !command.commandId || !command.type) return reject(state, 'INVALID_COMMAND', 'command is incomplete');
    if (command.protocolVersion !== PROTOCOL_VERSION) return reject(state, 'PROTOCOL_MISMATCH', 'unsupported protocol version');
    if (command.rulesetVersion !== state.rulesetVersion) return reject(state, 'RULESET_MISMATCH', 'ruleset version differs from match');
    const isConcurrentMulligan = command.type === 'MULLIGAN' && state.status === 'MULLIGAN' &&
      typeof command.expectedRevision === 'number' && command.expectedRevision >= 0 && command.expectedRevision <= state.revision;
    if (command.expectedRevision !== state.revision && !isConcurrentMulligan) return reject(state, 'REVISION_MISMATCH', 'client state is stale');
    if (state.processedCommandIds.indexOf(command.commandId) >= 0) return reject(state, 'DUPLICATE_COMMAND', 'command was already processed');
    if (state.status === 'FINISHED') return reject(state, 'MATCH_FINISHED', 'match has finished');

    const actorIndex = state.players.map(function (player): string { return player.playerId; }).indexOf(actorPlayerId);
    if (actorIndex < 0) return reject(state, 'NOT_A_PLAYER', 'actor does not belong to this match');

    const next = cloneState(state);
    const events: MatchEvent[] = [];
    function emit(type: EventType, payload: { [key: string]: unknown }): void {
      next.lastEventId += 1;
      events.push({ eventId: next.lastEventId, type: type, payload: payload });
    }

    function completeMulligan(): CommandRejected | null {
      if (state.status !== 'MULLIGAN') return reject(state, 'MULLIGAN_ALREADY_COMPLETED', 'the opening hand phase has ended');
      const player = next.players[actorIndex]!;
      if (player.mulliganCompleted) return reject(state, 'MULLIGAN_ALREADY_COMPLETED', 'this player already confirmed an opening hand');
      const cardIndices = command.payload && command.payload.cardIndices;
      if (!Array.isArray(cardIndices) || cardIndices.length > player.hand.length) {
        return reject(state, 'INVALID_COMMAND', 'MULLIGAN requires an array of opening hand indices');
      }
      const selected: { [index: string]: boolean } = {};
      for (let index = 0; index < cardIndices.length; index += 1) {
        const handIndex = cardIndices[index];
        if (typeof handIndex !== 'number' || handIndex % 1 !== 0 || handIndex < 0 || handIndex >= player.hand.length || selected[String(handIndex)]) {
          return reject(state, 'INVALID_COMMAND', 'mulligan indices must be unique positions in the opening hand');
        }
        selected[String(handIndex)] = true;
      }

      const replacedCards: string[] = [];
      const keptCards: string[] = [];
      for (let handIndex = 0; handIndex < player.hand.length; handIndex += 1) {
        if (selected[String(handIndex)]) replacedCards.push(player.hand[handIndex]!);
        else keptCards.push(player.hand[handIndex]!);
      }
      for (let index = 0; index < replacedCards.length; index += 1) keptCards.push(player.deck.pop()!);
      for (let index = 0; index < replacedCards.length; index += 1) player.deck.push(replacedCards[index]!);
      if (replacedCards.length > 0) shuffleDeck(player.deck, next.matchId + ':' + player.playerId + ':mulligan');
      player.hand = keptCards;
      player.mulliganCompleted = true;
      emit('MULLIGAN_COMPLETED', {
        playerId: player.playerId,
        hand: player.hand.slice(),
        handCount: player.hand.length,
        deckCount: player.deck.length,
        replacedCount: replacedCards.length
      });

      if (next.players.every(function (candidate): boolean { return candidate.mulliganCompleted; })) {
        next.status = 'ACTIVE';
        emit('MATCH_STARTED', {
          turn: next.turn,
          activePlayerIndex: next.activePlayerIndex,
          playerId: next.players[next.activePlayerIndex]!.playerId,
          phase: next.phase
        });
        drawCard(next.players[next.activePlayerIndex]!);
      }
      return null;
    }

    function deployCard(): CommandRejected | null {
      if (actorIndex !== state.activePlayerIndex) {
        return reject(state, 'NOT_ACTIVE_PLAYER', 'only the active player may deploy a card');
      }
      if (state.phase !== 'MAIN') return reject(state, 'WRONG_PHASE', 'cards may only be deployed during the main phase');
      if (!command.payload || typeof command.payload !== 'object') {
        return reject(state, 'INVALID_COMMAND', 'DEPLOY_CARD requires an object payload');
      }
      const cardId = command.payload.cardId;
      const slotKind = command.payload.slotKind;
      const slotIndex = command.payload.slotIndex;
      const paymentMethod = command.payload.paymentMethod;
      if (typeof cardId !== 'string' || (slotKind !== 'UNIT' && slotKind !== 'BUILDING') ||
          typeof slotIndex !== 'number' || slotIndex % 1 !== 0 ||
          (paymentMethod !== 'REDSTONE' && paymentMethod !== 'CRAFTING')) {
        return reject(state, 'INVALID_COMMAND', 'DEPLOY_CARD requires cardId, slotKind, integer slotIndex and paymentMethod');
      }
      const definition = getCardDefinition(cardId);
      if (definition === null) return reject(state, 'UNKNOWN_CARD', 'card definition is not registered');
      const player = next.players[actorIndex]!;
      const handIndex = player.hand.indexOf(cardId);
      if (handIndex < 0) return reject(state, 'CARD_NOT_IN_HAND', 'card is not in the actor hand');

      let occupiedSlots = 1;
      let objectCardType: 'UNIT' | 'BUILDING' | 'STRUCTURE';
      if (definition.cardType === 'UNIT') {
        if (slotKind !== 'UNIT' || slotIndex < 0 || slotIndex >= player.unitSlots.length) {
          return reject(state, 'INVALID_TARGET', 'unit cards require a valid unit slot');
        }
        if (player.unitSlots[slotIndex] !== null) return reject(state, 'SLOT_OCCUPIED', 'unit slot is occupied');
        objectCardType = 'UNIT';
      } else if (definition.cardType === 'BUILDING' || definition.cardType === 'STRUCTURE') {
        objectCardType = definition.cardType;
        occupiedSlots = Math.max(1, definition.buildingSlots);
        if (slotKind !== 'BUILDING' || slotIndex < 0 || slotIndex + occupiedSlots > player.buildingSlots.length) {
          return reject(state, 'INVALID_TARGET', 'building cards require enough consecutive building slots');
        }
        for (let index = slotIndex; index < slotIndex + occupiedSlots; index += 1) {
          if (player.buildingSlots[index] !== null) {
            return reject(state, 'SLOT_OCCUPIED', 'required building slots are occupied');
          }
        }
      } else {
        return reject(state, 'INVALID_TARGET', 'card type cannot be deployed to the battlefield');
      }

      const materialIndices: number[] = [];
      const consumedMaterials: string[] = [];
      if (paymentMethod === 'REDSTONE') {
        if (definition.cost > player.redstone) return reject(state, 'INSUFFICIENT_REDSTONE', 'not enough redstone');
      } else {
        if (!definition.hasCraftingRecipe || !definition.recipeId || definition.craftingRecipe.length === 0) {
          return reject(state, 'INVALID_PAYMENT_METHOD', 'card does not have a crafting recipe');
        }
        for (let recipeIndex = 0; recipeIndex < definition.craftingRecipe.length; recipeIndex += 1) {
          const ingredient = definition.craftingRecipe[recipeIndex]!;
          for (let count = 0; count < ingredient.count; count += 1) {
            let foundIndex = -1;
            for (let candidate = 0; candidate < player.hand.length; candidate += 1) {
              if (candidate !== handIndex && materialIndices.indexOf(candidate) < 0 && player.hand[candidate] === ingredient.cardId) {
                foundIndex = candidate;
                break;
              }
            }
            if (foundIndex < 0) return reject(state, 'MISSING_MATERIALS', 'crafting recipe materials are missing from hand');
            materialIndices.push(foundIndex);
            consumedMaterials.push(ingredient.cardId);
          }
        }
      }

      const instanceId = 'object-' + String(next.nextInstanceId);
      next.nextInstanceId += 1;
      const battlefieldObject: BattlefieldObjectState = {
        instanceId: instanceId,
        cardId: cardId,
        cardType: objectCardType,
        attack: definition.attack + (paymentMethod === 'CRAFTING' ? definition.craftedAttackBonus : 0),
        health: definition.health + (paymentMethod === 'CRAFTING' ? definition.craftedHealthBonus : 0),
        maxHealth: definition.health + (paymentMethod === 'CRAFTING' ? definition.craftedHealthBonus : 0),
        slotKind: slotKind,
        slotIndex: slotIndex,
        occupiedSlots: occupiedSlots,
        summonedTurn: state.turn,
        hasAttacked: false,
        keywords: definition.keywords.slice(),
        temporaryAttackModifier: 0,
        temporaryAttackModifierExpiresOnTurn: 0
      };
      player.battlefield.push(battlefieldObject);
      const occupiedRow = slotKind === 'UNIT' ? player.unitSlots : player.buildingSlots;
      for (let index = slotIndex; index < slotIndex + occupiedSlots; index += 1) occupiedRow[index] = instanceId;
      if (paymentMethod === 'CRAFTING') {
        materialIndices.sort(function (left, right): number { return right - left; });
        for (let index = 0; index < materialIndices.length; index += 1) player.hand.splice(materialIndices[index]!, 1);
        for (let index = 0; index < consumedMaterials.length; index += 1) player.discardPile.push(consumedMaterials[index]!);
        emit('MATERIALS_CONSUMED', {
          playerId: actorPlayerId,
          craftedCardId: cardId,
          recipeId: definition.recipeId,
          materials: definition.craftingRecipe.map(function (ingredient): { cardId: string; count: number } {
            return { cardId: ingredient.cardId, count: ingredient.count };
          }),
          handCount: player.hand.length,
          discardCount: player.discardPile.length
        });
      }
      const productHandIndex = player.hand.indexOf(cardId);
      if (productHandIndex < 0) return reject(state, 'INVALID_STATE', 'crafted product was removed while consuming materials');
      player.hand.splice(productHandIndex, 1);
      if (paymentMethod === 'REDSTONE') player.redstone -= definition.cost;
      emit('CARD_DEPLOYED', {
        playerId: actorPlayerId,
        instanceId: instanceId,
        cardId: cardId,
        cardType: battlefieldObject.cardType,
        slotKind: slotKind,
        slotIndex: slotIndex,
        occupiedSlots: occupiedSlots,
        paymentMethod: paymentMethod,
        redstone: player.redstone,
        attack: battlefieldObject.attack,
        health: battlefieldObject.health,
        maxHealth: battlefieldObject.maxHealth,
        summonedTurn: battlefieldObject.summonedTurn,
        keywords: battlefieldObject.keywords.slice(),
        nextInstanceId: next.nextInstanceId
      });
      if (definition.effectImplementationStatus === 'IMPLEMENTED' &&
          definition.effectIds.length === 1 && definition.effectIds[0] === 'effect.pf_001.01') {
        const healedLife = Math.min(30, player.life + 1);
        const healing = healedLife - player.life;
        player.life = healedLife;
        emit('HERO_HEALED', {
          playerId: player.playerId,
          sourceCardId: cardId,
          effectId: definition.effectIds[0],
          healing: healing,
          life: player.life
        });
      }
      return null;
    }

    function enterCombat(): CommandRejected | null {
      if (actorIndex !== state.activePlayerIndex) {
        return reject(state, 'NOT_ACTIVE_PLAYER', 'only the active player may enter combat');
      }
      if (state.phase !== 'MAIN') return reject(state, 'WRONG_PHASE', 'match is not in the main phase');
      next.phase = 'COMBAT';
      emit('PHASE_CHANGED', { playerId: actorPlayerId, phase: next.phase, turn: next.turn });
      return null;
    }

    function findObject(player: PlayerState, instanceId: string): BattlefieldObjectState | null {
      for (let index = 0; index < player.battlefield.length; index += 1) {
        if (player.battlefield[index]!.instanceId === instanceId) return player.battlefield[index]!;
      }
      return null;
    }

    function removeDeadObjects(player: PlayerState): BattlefieldObjectState[] {
      const deadObjects = player.battlefield.filter(function (object): boolean { return object.health <= 0; });
      deadObjects.sort(function (left, right): number {
        if (left.slotIndex !== right.slotIndex) return left.slotIndex - right.slotIndex;
        return left.instanceId < right.instanceId ? -1 : left.instanceId > right.instanceId ? 1 : 0;
      });
      for (let index = 0; index < deadObjects.length; index += 1) {
        const object = deadObjects[index]!;
        const slots = object.slotKind === 'UNIT' ? player.unitSlots : player.buildingSlots;
        for (let slot = 0; slot < slots.length; slot += 1) {
          if (slots[slot] === object.instanceId) slots[slot] = null;
        }
        const battlefieldIndex = player.battlefield.indexOf(object);
        if (battlefieldIndex < 0) throw new Error('dead object disappeared before settlement: ' + object.instanceId);
        player.battlefield.splice(battlefieldIndex, 1);
        player.discardPile.push(object.cardId);
        emit('OBJECT_DIED', {
          playerId: player.playerId,
          instanceId: object.instanceId,
          cardId: object.cardId,
          slotKind: object.slotKind,
          slotIndex: object.slotIndex,
          occupiedSlots: object.occupiedSlots,
          discardCount: player.discardPile.length
        });
      }
      return deadObjects;
    }

    function settleDeaths(currentPlayer: PlayerState, nonCurrentPlayer: PlayerState): void {
      while (true) {
        const currentDeaths = removeDeadObjects(currentPlayer);
        const nonCurrentDeaths = removeDeadObjects(nonCurrentPlayer);
        if (currentDeaths.length === 0 && nonCurrentDeaths.length === 0) return;
        for (let index = 0; index < currentDeaths.length; index += 1) resolveDeathrattles(currentPlayer, currentDeaths[index]!);
        for (let index = 0; index < nonCurrentDeaths.length; index += 1) resolveDeathrattles(nonCurrentPlayer, nonCurrentDeaths[index]!);
      }
    }

    function resolveDeathrattles(player: PlayerState, object: BattlefieldObjectState): void {
      const definition = getCardDefinition(object.cardId);
      if (definition === null || definition.effectImplementationStatus !== 'IMPLEMENTED') return;
      if (definition.effectIds.indexOf('effect.ed_004.01') >= 0) {
        generateCard(player, 'tk_016', object.cardId, object.instanceId, 'effect.ed_004.01');
      }
      if (definition.effectIds.indexOf('effect.nt_001.01') >= 0) {
        summonUnit(player, 'tk_014', object.cardId, object.instanceId, 'effect.nt_001.01', object.slotIndex);
      }
    }

    function summonUnit(
      player: PlayerState,
      cardId: string,
      sourceCardId: string,
      sourceInstanceId: string,
      effectId: string,
      preferredSlotIndex: number
    ): boolean {
      const definition = getCardDefinition(cardId);
      if (definition === null || definition.cardType !== 'UNIT') throw new Error('summoned unit is not registered: ' + cardId);
      const slotIndex = preferredSlotIndex >= 0 && preferredSlotIndex < player.unitSlots.length && player.unitSlots[preferredSlotIndex] === null
        ? preferredSlotIndex
        : player.unitSlots.indexOf(null);
      if (slotIndex < 0) return false;
      const object: BattlefieldObjectState = {
        instanceId: 'object-' + next.nextInstanceId,
        cardId: cardId,
        cardType: 'UNIT',
        attack: definition.attack,
        health: definition.health,
        maxHealth: definition.health,
        slotKind: 'UNIT',
        slotIndex: slotIndex,
        occupiedSlots: 1,
        summonedTurn: next.turn,
        hasAttacked: false,
        keywords: definition.keywords.slice(),
        temporaryAttackModifier: 0,
        temporaryAttackModifierExpiresOnTurn: 0
      };
      next.nextInstanceId += 1;
      player.unitSlots[slotIndex] = object.instanceId;
      player.battlefield.push(object);
      emit('OBJECT_SUMMONED', {
        playerId: player.playerId,
        sourceCardId: sourceCardId,
        sourceInstanceId: sourceInstanceId,
        effectId: effectId,
        cardId: object.cardId,
        instanceId: object.instanceId,
        cardType: object.cardType,
        slotKind: object.slotKind,
        slotIndex: object.slotIndex,
        occupiedSlots: object.occupiedSlots,
        attack: object.attack,
        health: object.health,
        maxHealth: object.maxHealth,
        summonedTurn: object.summonedTurn,
        keywords: object.keywords.slice(),
        nextInstanceId: next.nextInstanceId
      });
      return true;
    }

    function generateCard(
      player: PlayerState,
      cardId: string,
      sourceCardId: string,
      sourceInstanceId: string,
      effectId: string
    ): void {
      if (getCardDefinition(cardId) === null) throw new Error('generated card is not registered: ' + cardId);
      const destination = player.hand.length >= HAND_LIMIT ? 'DISCARD' : 'HAND';
      if (destination === 'HAND') player.hand.push(cardId);
      else player.discardPile.push(cardId);
      emit('CARD_GENERATED', {
        playerId: player.playerId,
        sourceCardId: sourceCardId,
        sourceInstanceId: sourceInstanceId,
        effectId: effectId,
        cardId: cardId,
        destination: destination,
        handCount: player.hand.length,
        discardCount: player.discardPile.length
      });
    }

    function damageHero(player: PlayerState, amount: number): void {
      const armorDamage = Math.min(player.armor, amount);
      player.armor -= armorDamage;
      player.life = Math.max(0, player.life - (amount - armorDamage));
    }

    function buryCard(player: PlayerState, cardId: string, sourceCardId: string, effectId: string): void {
      if (getCardDefinition(cardId) === null) throw new Error('buried card is not registered: ' + cardId);
      const insertIndex = (seedFromText(next.matchId + ':' + player.playerId + ':bury:' + next.lastEventId + ':' + cardId) >>> 0) %
        (player.deck.length + 1);
      player.deck.splice(insertIndex, 0, cardId);
      player.buriedCardIds.push(cardId);
      emit('CARD_BURIED', {
        playerId: player.playerId,
        sourceCardId: sourceCardId,
        effectId: effectId,
        cardId: cardId,
        deckCount: player.deck.length,
        buriedCount: player.buriedCardIds.length
      });
    }

    function drawCard(player: PlayerState): void {
      while (true) {
        if (player.deck.length === 0) {
          player.fatigueCount += 1;
          player.life = Math.max(0, player.life - player.fatigueCount);
          emit('FATIGUE_DAMAGE', {
            playerId: player.playerId,
            damage: player.fatigueCount,
            fatigueCount: player.fatigueCount,
            life: player.life,
            armor: player.armor,
            handCount: player.hand.length,
            deckCount: 0
          });
          return;
        }
        const cardId = player.deck.pop()!;
        const buriedIndex = player.buriedCardIds.indexOf(cardId);
        if (buriedIndex >= 0) {
          player.buriedCardIds.splice(buriedIndex, 1);
          if (cardId !== 'tk_006') throw new Error('buried effect handler is not registered: ' + cardId);
          const destination = player.hand.length >= HAND_LIMIT ? 'DISCARD' : 'HAND';
          if (destination === 'HAND') player.hand.push(cardId);
          else player.discardPile.push(cardId);
          emit('CARD_EXCAVATED', {
            playerId: player.playerId,
            cardId: cardId,
            effectId: 'effect.tk_006.01',
            destination: destination,
            handCount: player.hand.length,
            deckCount: player.deck.length,
            discardCount: player.discardPile.length,
            buriedCount: player.buriedCardIds.length
          });
          player.armor += 1;
          emit('ARMOR_GAINED', {
            playerId: player.playerId, sourceCardId: cardId, effectId: 'effect.tk_006.01',
            amount: 1, armor: player.armor
          });
          continue;
        }
        if (player.hand.length >= HAND_LIMIT) {
          player.discardPile.push(cardId);
          emit('CARD_BURNED', {
            playerId: player.playerId,
            cardId: cardId,
            handCount: player.hand.length,
            deckCount: player.deck.length,
            discardCount: player.discardPile.length
          });
          return;
        }
        player.hand.push(cardId);
        emit('CARD_DRAWN', {
          playerId: player.playerId,
          cardId: cardId,
          handCount: player.hand.length,
          deckCount: player.deck.length
        });
        return;
      }
    }

    function finishForSelfDefeat(player: PlayerState, reason: string): boolean {
      if (player.life > 0) return false;
      const winner = next.players[0]!.playerId === player.playerId ? next.players[1]! : next.players[0]!;
      next.status = 'FINISHED';
      next.winnerPlayerId = winner.playerId;
      emit('MATCH_ENDED', { winnerPlayerId: next.winnerPlayerId, reason: reason });
      return true;
    }

    function playCard(): CommandRejected | null {
      if (actorIndex !== state.activePlayerIndex) return reject(state, 'NOT_ACTIVE_PLAYER', 'only the active player may play a card');
      if (state.phase !== 'MAIN') return reject(state, 'WRONG_PHASE', 'cards may only be played during the main phase');
      const cardId = command.payload && command.payload.cardId;
      if (typeof cardId !== 'string') return reject(state, 'INVALID_COMMAND', 'PLAY_CARD requires cardId');
      const definition = getCardDefinition(cardId);
      if (definition === null) return reject(state, 'UNKNOWN_CARD', 'card is not registered');
      if (definition.cardType !== 'SPELL' && definition.cardType !== 'MATERIAL') {
        return reject(state, 'INVALID_TARGET', 'PLAY_CARD currently accepts only spells and materials');
      }
      if (definition.effectImplementationStatus !== 'IMPLEMENTED' || definition.effectIds.length !== 1) {
        return reject(state, 'EFFECT_NOT_IMPLEMENTED', 'card effect is registered but not implemented');
      }
      const effectId = definition.effectIds[0]!;
      if (effectId !== 'effect.db_002.01' && effectId !== 'effect.db_006.01' && effectId !== 'effect.nt_006.01' &&
          effectId !== 'effect.si_001.01' && effectId !== 'effect.tk_005.01' &&
          effectId !== 'effect.tk_009.01' && effectId !== 'effect.tk_010.01' &&
          effectId !== 'effect.tk_016.01') {
        return reject(state, 'EFFECT_NOT_IMPLEMENTED', 'effect handler is not registered');
      }
      const player = next.players[actorIndex]!;
      const opponent = next.players[actorIndex === 0 ? 1 : 0]!;
      let targetedObject: BattlefieldObjectState | null = null;
      let targetedPlayer: PlayerState | null = null;
      if (effectId === 'effect.si_001.01' || effectId === 'effect.tk_009.01') {
        if (command.payload.targetType !== 'UNIT' || typeof command.payload.targetInstanceId !== 'string') {
          return reject(state, 'INVALID_TARGET', effectId === 'effect.si_001.01'
            ? 'snowball requires an enemy unit target'
            : 'bone requires a friendly unit target');
        }
        targetedPlayer = effectId === 'effect.si_001.01' ? opponent : player;
        targetedObject = findObject(targetedPlayer, command.payload.targetInstanceId);
        if (targetedObject === null || targetedObject.cardType !== 'UNIT') {
          return reject(state, 'INVALID_TARGET', effectId === 'effect.si_001.01'
            ? 'snowball target must be a living enemy unit'
            : 'bone target must be a living friendly unit');
        }
      } else if (effectId === 'effect.tk_010.01') {
        if (command.payload.targetType !== 'BUILDING' || typeof command.payload.targetInstanceId !== 'string') {
          return reject(state, 'INVALID_TARGET', 'cobblestone requires a friendly building target');
        }
        targetedPlayer = player;
        targetedObject = findObject(player, command.payload.targetInstanceId);
        if (targetedObject === null || (targetedObject.cardType !== 'BUILDING' && targetedObject.cardType !== 'STRUCTURE')) {
          return reject(state, 'INVALID_TARGET', 'cobblestone target must be a living friendly building or structure');
        }
      }
      const handIndex = player.hand.indexOf(cardId);
      if (handIndex < 0) return reject(state, 'CARD_NOT_IN_HAND', 'card is not in the active players hand');
      if (definition.cost > player.redstone) return reject(state, 'INSUFFICIENT_REDSTONE', 'not enough redstone');

      player.hand.splice(handIndex, 1);
      player.redstone -= definition.cost;
      player.discardPile.push(cardId);
      emit('CARD_PLAYED', {
        playerId: player.playerId,
        cardId: cardId,
        cardType: definition.cardType,
        effectId: effectId,
        redstone: player.redstone,
        handCount: player.hand.length,
        discardCount: player.discardPile.length
      });

      switch (effectId) {
        case 'effect.db_002.01':
          buryCard(player, 'tk_006', cardId, effectId);
          player.armor += 1;
          emit('ARMOR_GAINED', {
            playerId: player.playerId, sourceCardId: cardId, effectId: effectId,
            amount: 1, armor: player.armor
          });
          return null;
        case 'effect.db_006.01':
          for (let playerIndex = 0; playerIndex < next.players.length; playerIndex += 1) {
            const damagedPlayer = next.players[playerIndex]!;
            for (let objectIndex = 0; objectIndex < damagedPlayer.battlefield.length; objectIndex += 1) {
              const damagedObject = damagedPlayer.battlefield[objectIndex]!;
              if (damagedObject.cardType !== 'UNIT') continue;
              damagedObject.health = Math.max(0, damagedObject.health - 2);
              emit('OBJECT_STATS_CHANGED', {
                playerId: damagedPlayer.playerId,
                instanceId: damagedObject.instanceId,
                sourceCardId: cardId,
                effectId: effectId,
                reason: 'DAMAGE',
                attack: damagedObject.attack,
                health: damagedObject.health,
                temporaryAttackModifier: damagedObject.temporaryAttackModifier,
                temporaryAttackModifierExpiresOnTurn: damagedObject.temporaryAttackModifierExpiresOnTurn
              });
            }
          }
          settleDeaths(player, opponent);
          return null;
        case 'effect.nt_006.01':
          player.life = Math.max(0, player.life - 2);
          emit('HERO_DAMAGED', {
            playerId: player.playerId, sourceCardId: cardId, effectId: effectId,
            damage: 2, damageType: 'TRUE', life: player.life, armor: player.armor
          });
          if (finishForSelfDefeat(player, 'SELF_DAMAGE')) return null;
          drawCard(player);
          finishForSelfDefeat(player, 'FATIGUE');
          return null;
        case 'effect.si_001.01': {
          if (targetedObject === null || targetedPlayer === null) throw new Error('validated snowball target was not resolved');
          const attackBefore = targetedObject.attack;
          targetedObject.attack = Math.max(0, targetedObject.attack - 1);
          targetedObject.temporaryAttackModifier += targetedObject.attack - attackBefore;
          if (targetedObject.temporaryAttackModifier !== 0) targetedObject.temporaryAttackModifierExpiresOnTurn = state.turn;
          emit('OBJECT_STATS_CHANGED', {
            playerId: targetedPlayer.playerId,
            instanceId: targetedObject.instanceId,
            sourceCardId: cardId,
            effectId: effectId,
            reason: 'TEMPORARY_ATTACK_MODIFIER',
            attack: targetedObject.attack,
            health: targetedObject.health,
            temporaryAttackModifier: targetedObject.temporaryAttackModifier,
            temporaryAttackModifierExpiresOnTurn: targetedObject.temporaryAttackModifierExpiresOnTurn
          });
          return null;
        }
        case 'effect.tk_005.01': {
          const healedLife = Math.min(30, player.life + 2);
          const healing = healedLife - player.life;
          player.life = healedLife;
          emit('HERO_HEALED', {
            playerId: player.playerId, sourceCardId: cardId, effectId: effectId,
            healing: healing, life: player.life
          });
          player.life = Math.max(0, player.life - 1);
          emit('HERO_DAMAGED', {
            playerId: player.playerId, sourceCardId: cardId, effectId: effectId,
            damage: 1, damageType: 'TRUE', life: player.life, armor: player.armor
          });
          finishForSelfDefeat(player, 'SELF_DAMAGE');
          return null;
        }
        case 'effect.tk_009.01': {
          if (targetedObject === null || targetedPlayer === null) throw new Error('validated bone target was not resolved');
          targetedObject.attack += 1;
          targetedObject.temporaryAttackModifier += 1;
          targetedObject.temporaryAttackModifierExpiresOnTurn = state.turn;
          emit('OBJECT_STATS_CHANGED', {
            playerId: targetedPlayer.playerId,
            instanceId: targetedObject.instanceId,
            sourceCardId: cardId,
            effectId: effectId,
            reason: 'TEMPORARY_ATTACK_MODIFIER',
            attack: targetedObject.attack,
            health: targetedObject.health,
            temporaryAttackModifier: targetedObject.temporaryAttackModifier,
            temporaryAttackModifierExpiresOnTurn: targetedObject.temporaryAttackModifierExpiresOnTurn
          });
          return null;
        }
        case 'effect.tk_010.01': {
          if (targetedObject === null || targetedPlayer === null) throw new Error('validated cobblestone target was not resolved');
          targetedObject.health = Math.min(targetedObject.maxHealth, targetedObject.health + 2);
          emit('OBJECT_STATS_CHANGED', {
            playerId: targetedPlayer.playerId,
            instanceId: targetedObject.instanceId,
            sourceCardId: cardId,
            effectId: effectId,
            reason: 'HEAL',
            attack: targetedObject.attack,
            health: targetedObject.health,
            temporaryAttackModifier: targetedObject.temporaryAttackModifier,
            temporaryAttackModifierExpiresOnTurn: targetedObject.temporaryAttackModifierExpiresOnTurn
          });
          return null;
        }
        case 'effect.tk_016.01':
          player.armor += 2;
          emit('ARMOR_GAINED', {
            playerId: player.playerId, sourceCardId: cardId, effectId: effectId,
            amount: 2, armor: player.armor
          });
          return null;
        default:
          throw new Error('validated effect handler was not dispatched');
      }
    }

    function attack(): CommandRejected | null {
      if (actorIndex !== state.activePlayerIndex) {
        return reject(state, 'NOT_ACTIVE_PLAYER', 'only the active player may attack');
      }
      if (state.phase !== 'COMBAT') return reject(state, 'WRONG_PHASE', 'attacks require the combat phase');
      if (!command.payload || typeof command.payload !== 'object') {
        return reject(state, 'INVALID_COMMAND', 'ATTACK requires an object payload');
      }
      const attackerInstanceId = command.payload.attackerInstanceId;
      const targetType = command.payload.targetType;
      const targetInstanceId = command.payload.targetInstanceId;
      if (typeof attackerInstanceId !== 'string' ||
          (targetType !== 'HERO' && targetType !== 'UNIT' && targetType !== 'BUILDING') ||
          (targetType !== 'HERO' && typeof targetInstanceId !== 'string')) {
        return reject(state, 'INVALID_COMMAND', 'ATTACK requires attackerInstanceId and a valid target');
      }

      const attackerPlayer = next.players[actorIndex]!;
      const defenderIndex = actorIndex === 0 ? 1 : 0;
      const defenderPlayer = next.players[defenderIndex]!;
      const attacker = findObject(attackerPlayer, attackerInstanceId);
      if (attacker === null || attacker.cardType !== 'UNIT' || attacker.attack <= 0) {
        return reject(state, 'INVALID_ATTACKER', 'attacker must be a living friendly unit with attack');
      }
      if (attacker.hasAttacked) return reject(state, 'ATTACK_ALREADY_USED', 'unit has already attacked this turn');
      if (attacker.summonedTurn === state.turn && attacker.keywords.indexOf('CHARGE') < 0) {
        return reject(state, 'ATTACKER_NOT_READY', 'unit cannot attack on its summoned turn without CHARGE');
      }

      const tauntTargets = defenderPlayer.battlefield.filter(function (object): boolean {
        return object.health > 0 && object.keywords.indexOf('TAUNT') >= 0;
      });
      if (tauntTargets.length > 0) {
        const targetsTaunt = targetType !== 'HERO' && typeof targetInstanceId === 'string' &&
          tauntTargets.some(function (object): boolean { return object.instanceId === targetInstanceId; });
        if (!targetsTaunt) return reject(state, 'TAUNT_TARGET_REQUIRED', 'a legal enemy TAUNT object must be attacked first');
      }

      attacker.hasAttacked = true;
      if (targetType === 'HERO') {
        damageHero(defenderPlayer, attacker.attack);
        emit('ATTACK_RESOLVED', {
          attackerPlayerId: attackerPlayer.playerId,
          attackerInstanceId: attacker.instanceId,
          targetPlayerId: defenderPlayer.playerId,
          targetType: 'HERO',
          targetInstanceId: null,
          damageToTarget: attacker.attack,
          damageToAttacker: 0,
          attackerHealth: attacker.health,
          targetHealth: defenderPlayer.life,
          targetArmor: defenderPlayer.armor
        });
      } else {
        const target = typeof targetInstanceId === 'string' ? findObject(defenderPlayer, targetInstanceId) : null;
        const expectedSlotKind = targetType === 'UNIT' ? 'UNIT' : 'BUILDING';
        if (target === null || target.slotKind !== expectedSlotKind) {
          return reject(state, 'INVALID_TARGET', 'target is not a living enemy object of the requested type');
        }
        const retaliation = target.cardType === 'UNIT' ? target.attack : 0;
        target.health = Math.max(0, target.health - attacker.attack);
        attacker.health = Math.max(0, attacker.health - retaliation);
        emit('ATTACK_RESOLVED', {
          attackerPlayerId: attackerPlayer.playerId,
          attackerInstanceId: attacker.instanceId,
          targetPlayerId: defenderPlayer.playerId,
          targetType: targetType,
          targetInstanceId: target.instanceId,
          damageToTarget: attacker.attack,
          damageToAttacker: retaliation,
          attackerHealth: attacker.health,
          targetHealth: target.health,
          targetArmor: 0
        });
        settleDeaths(attackerPlayer, defenderPlayer);
      }

      if (defenderPlayer.life <= 0) {
        next.status = 'FINISHED';
        next.winnerPlayerId = attackerPlayer.playerId;
        emit('MATCH_ENDED', { winnerPlayerId: next.winnerPlayerId, reason: 'HERO_DEFEATED' });
      }
      return null;
    }

    switch (command.type) {
      case 'MULLIGAN': {
        const mulliganRejection = completeMulligan();
        if (mulliganRejection !== null) return mulliganRejection;
        break;
      }
      case 'DEPLOY_CARD': {
        if (state.status !== 'ACTIVE') return reject(state, 'MULLIGAN_REQUIRED', 'both players must confirm their opening hands first');
        const deploymentRejection = deployCard();
        if (deploymentRejection !== null) return deploymentRejection;
        break;
      }
      case 'PLAY_CARD': {
        if (state.status !== 'ACTIVE') return reject(state, 'MULLIGAN_REQUIRED', 'both players must confirm their opening hands first');
        const playRejection = playCard();
        if (playRejection !== null) return playRejection;
        break;
      }
      case 'ENTER_COMBAT': {
        if (state.status !== 'ACTIVE') return reject(state, 'MULLIGAN_REQUIRED', 'both players must confirm their opening hands first');
        const phaseRejection = enterCombat();
        if (phaseRejection !== null) return phaseRejection;
        break;
      }
      case 'ATTACK': {
        if (state.status !== 'ACTIVE') return reject(state, 'MULLIGAN_REQUIRED', 'both players must confirm their opening hands first');
        const attackRejection = attack();
        if (attackRejection !== null) return attackRejection;
        break;
      }
      case 'END_TURN': {
        if (state.status !== 'ACTIVE') return reject(state, 'MULLIGAN_REQUIRED', 'both players must confirm their opening hands first');
        if (actorIndex !== state.activePlayerIndex) return reject(state, 'NOT_ACTIVE_PLAYER', 'only the active player may end the turn');
        for (let playerIndex = 0; playerIndex < next.players.length; playerIndex += 1) {
          const effectPlayer = next.players[playerIndex]!;
          for (let objectIndex = 0; objectIndex < effectPlayer.battlefield.length; objectIndex += 1) {
            const object = effectPlayer.battlefield[objectIndex]!;
            if (object.temporaryAttackModifierExpiresOnTurn !== state.turn) continue;
            object.attack -= object.temporaryAttackModifier;
            object.temporaryAttackModifier = 0;
            object.temporaryAttackModifierExpiresOnTurn = 0;
            emit('OBJECT_STATS_CHANGED', {
              playerId: effectPlayer.playerId,
              instanceId: object.instanceId,
              sourceCardId: null,
              effectId: null,
              reason: 'TEMPORARY_EXPIRED',
              attack: object.attack,
              health: object.health,
              temporaryAttackModifier: 0,
              temporaryAttackModifierExpiresOnTurn: 0
            });
          }
        }
        emit('TURN_ENDED', { playerId: actorPlayerId, turn: state.turn });
        next.activePlayerIndex = (state.activePlayerIndex + 1) % state.players.length;
        if (next.activePlayerIndex === 0) next.turn += 1;
        const nextPlayer = next.players[next.activePlayerIndex]!;
        next.phase = 'MAIN';
        if (next.turn > 1) nextPlayer.redstoneCapacity = Math.min(10, nextPlayer.redstoneCapacity + 1);
        nextPlayer.redstone = nextPlayer.redstoneCapacity;
        for (let objectIndex = 0; objectIndex < nextPlayer.battlefield.length; objectIndex += 1) {
          nextPlayer.battlefield[objectIndex]!.hasAttacked = false;
        }
        emit('TURN_STARTED', {
          playerId: nextPlayer.playerId,
          turn: next.turn,
          activePlayerIndex: next.activePlayerIndex,
          redstone: nextPlayer.redstone,
          redstoneCapacity: nextPlayer.redstoneCapacity,
          phase: next.phase
        });
        drawCard(nextPlayer);
        if (nextPlayer.life <= 0) {
          next.status = 'FINISHED';
          next.winnerPlayerId = next.players[actorIndex]!.playerId;
          emit('MATCH_ENDED', { winnerPlayerId: next.winnerPlayerId, reason: 'FATIGUE' });
        }
        break;
      }
      case 'CONCEDE': {
        const winnerIndex = actorIndex === 0 ? 1 : 0;
        next.status = 'FINISHED';
        next.winnerPlayerId = next.players[winnerIndex]!.playerId;
        emit('PLAYER_CONCEDED', { playerId: actorPlayerId });
        emit('MATCH_ENDED', { winnerPlayerId: next.winnerPlayerId, reason: 'CONCEDE' });
        break;
      }
      default:
        return reject(state, 'INVALID_COMMAND', 'unknown command type');
    }

    next.revision += 1;
    next.processedCommandIds.push(command.commandId);
    const nextViolations = validateState(next);
    if (nextViolations.length > 0) return reject(state, 'INVALID_STATE', nextViolations.join('; '));

    return {
      accepted: true,
      state: next,
      batch: {
        protocolVersion: PROTOCOL_VERSION,
        rulesetVersion: next.rulesetVersion,
        revision: next.revision,
        acknowledgedCommandId: command.commandId,
        events: events
      }
    };
  }
}
