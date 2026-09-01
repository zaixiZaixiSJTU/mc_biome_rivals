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

  export function getEffectiveCardCost(player: PlayerState, definition: CardRuleDefinition): number {
    if (definition.id === 'db_005' && player.excavatedThisTurn) return Math.max(0, definition.cost - 1);
    return definition.cost;
  }

  function cardHasTag(cardId: string, tag: string): boolean {
    const definition = getCardDefinition(cardId);
    return definition !== null && definition.tags.indexOf(tag) >= 0;
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
      excavatedThisTurn: false,
      discardPile: [],
      fatigueCount: 0,
      equipment: null,
      heroHasAttacked: false,
      triggeredEffectKeysThisTurn: [],
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
      pendingChoice: null,
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
          excavatedThisTurn: player.excavatedThisTurn,
          discardPile: player.discardPile.slice(),
          fatigueCount: player.fatigueCount,
          equipment: player.equipment === null ? null : {
            instanceId: player.equipment.instanceId, cardId: player.equipment.cardId,
            attack: player.equipment.attack, durability: player.equipment.durability,
            maxDurability: player.equipment.maxDurability
          },
          heroHasAttacked: player.heroHasAttacked,
          triggeredEffectKeysThisTurn: player.triggeredEffectKeysThisTurn.slice(),
          unitSlots: player.unitSlots.slice(),
          buildingSlots: player.buildingSlots.slice(),
          battlefield: player.battlefield.map(function (object): BattlefieldObjectState {
            return {
              instanceId: object.instanceId, cardId: object.cardId, cardType: object.cardType,
              attack: object.attack, health: object.health, maxHealth: object.maxHealth,
              adjacencyHealthModifier: object.adjacencyHealthModifier,
              slotKind: object.slotKind, slotIndex: object.slotIndex, occupiedSlots: object.occupiedSlots,
              summonedTurn: object.summonedTurn, hasAttacked: object.hasAttacked,
              keywords: object.keywords.slice(),
              temporaryAttackModifier: object.temporaryAttackModifier,
              temporaryAttackModifierExpiresOnTurn: object.temporaryAttackModifierExpiresOnTurn,
              statuses: object.statuses.map(function (status): BattlefieldStatusState {
                return {
                  statusId: status.statusId,
                  remainingDuration: status.remainingDuration,
                  sourcePlayerId: status.sourcePlayerId,
                  sourceCardId: status.sourceCardId,
                  sourceInstanceId: status.sourceInstanceId,
                  effectId: status.effectId,
                  attackModifier: status.attackModifier,
                  boundAttackModifier: status.boundAttackModifier
                };
              })
            };
          })
        };
      }),
      pendingChoice: state.pendingChoice === null ? null : {
        choiceId: state.pendingChoice.choiceId,
        playerId: state.pendingChoice.playerId,
        sourceCardId: state.pendingChoice.sourceCardId,
        sourceInstanceId: state.pendingChoice.sourceInstanceId,
        effectId: state.pendingChoice.effectId,
        kind: state.pendingChoice.kind,
        targetPlayerId: state.pendingChoice.targetPlayerId,
        targetInstanceId: state.pendingChoice.targetInstanceId,
        options: state.pendingChoice.options.map(function (option): PendingChoiceOptionSnapshot {
          const ownsChoice = state.pendingChoice !== null && state.pendingChoice.playerId === viewerPlayerId;
          const publicMoveChoice = state.pendingChoice !== null && state.pendingChoice.kind === 'MOVE_UNIT';
          return {
            optionIndex: option.optionIndex,
            cardId: ownsChoice || publicMoveChoice ? option.cardId : null,
            slotIndex: option.slotIndex,
            selectable: ownsChoice && option.selectable
          };
        })
      },
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
        if (event.type === 'CHOICE_OFFERED' && payload.playerId !== viewerPlayerId && payload.kind !== 'MOVE_UNIT' && Array.isArray(payload.options)) {
          payload.options = (payload.options as PendingChoiceOptionState[]).map(function (option): PendingChoiceOptionSnapshot {
            return { optionIndex: option.optionIndex, cardId: null, slotIndex: option.slotIndex, selectable: false };
          });
        }
        if (event.type === 'CHOICE_OFFERED' && payload.playerId !== viewerPlayerId && payload.kind === 'MOVE_UNIT' && Array.isArray(payload.options)) {
          payload.options = (payload.options as PendingChoiceOptionState[]).map(function (option): PendingChoiceOptionState {
            return { optionIndex: option.optionIndex, cardId: option.cardId, slotIndex: option.slotIndex, selectable: false };
          });
        }
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
          excavatedThisTurn: player.excavatedThisTurn,
          discardPile: player.discardPile.slice(),
          fatigueCount: player.fatigueCount,
          equipment: player.equipment === null ? null : {
            instanceId: player.equipment.instanceId, cardId: player.equipment.cardId,
            attack: player.equipment.attack, durability: player.equipment.durability,
            maxDurability: player.equipment.maxDurability
          },
          heroHasAttacked: player.heroHasAttacked,
          triggeredEffectKeysThisTurn: player.triggeredEffectKeysThisTurn.slice(),
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
              adjacencyHealthModifier: object.adjacencyHealthModifier,
              slotKind: object.slotKind,
              slotIndex: object.slotIndex,
              occupiedSlots: object.occupiedSlots,
              summonedTurn: object.summonedTurn,
              hasAttacked: object.hasAttacked,
              keywords: object.keywords.slice(),
              temporaryAttackModifier: object.temporaryAttackModifier,
              temporaryAttackModifierExpiresOnTurn: object.temporaryAttackModifierExpiresOnTurn,
              statuses: object.statuses.map(function (status): BattlefieldStatusState {
                return {
                  statusId: status.statusId,
                  remainingDuration: status.remainingDuration,
                  sourcePlayerId: status.sourcePlayerId,
                  sourceCardId: status.sourceCardId,
                  sourceInstanceId: status.sourceInstanceId,
                  effectId: status.effectId,
                  attackModifier: status.attackModifier,
                  boundAttackModifier: status.boundAttackModifier
                };
              })
            };
          })
        };
      }),
      pendingChoice: state.pendingChoice === null ? null : {
        choiceId: state.pendingChoice.choiceId,
        playerId: state.pendingChoice.playerId,
        sourceCardId: state.pendingChoice.sourceCardId,
        sourceInstanceId: state.pendingChoice.sourceInstanceId,
        effectId: state.pendingChoice.effectId,
        kind: state.pendingChoice.kind,
        targetPlayerId: state.pendingChoice.targetPlayerId,
        targetInstanceId: state.pendingChoice.targetInstanceId,
        options: state.pendingChoice.options.map(function (option): PendingChoiceOptionState {
          return { optionIndex: option.optionIndex, cardId: option.cardId, slotIndex: option.slotIndex, selectable: option.selectable };
        })
      },
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
    if (state.pendingChoice !== null && command.type !== 'RESOLVE_CHOICE' && command.type !== 'CONCEDE') {
      return reject(state, 'CHOICE_REQUIRED', 'the pending card choice must be resolved before another action');
    }

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
      let battlecryTargetPlayer: PlayerState | null = null;
      let battlecryTarget: BattlefieldObjectState | null = null;
      let drownedBattlecryActive = false;
      if (definition.effectImplementationStatus === 'IMPLEMENTED' &&
          definition.effectIds.length === 1 && definition.effectIds[0] === 'effect.si_003.01') {
        if (command.payload.targetType !== 'UNIT' || typeof command.payload.targetInstanceId !== 'string') {
          return reject(state, 'INVALID_TARGET', 'stray requires an enemy unit battlecry target');
        }
        battlecryTargetPlayer = next.players[actorIndex === 0 ? 1 : 0]!;
        battlecryTarget = findObject(battlecryTargetPlayer, command.payload.targetInstanceId);
        if (battlecryTarget === null || battlecryTarget.cardType !== 'UNIT') {
          return reject(state, 'INVALID_TARGET', 'stray battlecry target must be a living enemy unit');
        }
      }

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

      if (definition.effectImplementationStatus === 'IMPLEMENTED' &&
          definition.effectIds.length === 1 && definition.effectIds[0] === 'effect.or_003.01') {
        const adjacentSlots = [slotIndex - 1, slotIndex + 1];
        drownedBattlecryActive = adjacentSlots.some(function (adjacentSlot): boolean {
          if (adjacentSlot < 0 || adjacentSlot >= player.unitSlots.length) return false;
          const adjacentInstanceId = player.unitSlots[adjacentSlot]!;
          if (adjacentInstanceId === null) return false;
          const adjacentObject = findObject(player, adjacentInstanceId);
          return adjacentObject !== null && adjacentObject.cardType === 'UNIT' && adjacentObject.health > 0 &&
            cardHasTag(adjacentObject.cardId, 'aquatic');
        });
        battlecryTargetPlayer = next.players[actorIndex === 0 ? 1 : 0]!;
        const hasEnemyUnit = battlecryTargetPlayer.battlefield.some(function (object): boolean {
          return object.cardType === 'UNIT' && object.health > 0;
        });
        if (drownedBattlecryActive && hasEnemyUnit) {
          if (command.payload.targetType !== 'UNIT' || typeof command.payload.targetInstanceId !== 'string') {
            return reject(state, 'INVALID_TARGET', 'drowned requires an enemy unit target when deployed beside an aquatic unit');
          }
          battlecryTarget = findObject(battlecryTargetPlayer, command.payload.targetInstanceId);
          if (battlecryTarget === null || battlecryTarget.cardType !== 'UNIT' || battlecryTarget.health <= 0) {
            return reject(state, 'INVALID_TARGET', 'drowned battlecry target must be a living enemy unit');
          }
        }
      }

      const materialIndices: number[] = [];
      const consumedMaterials: string[] = [];
      const effectiveCost = getEffectiveCardCost(player, definition);
      if (paymentMethod === 'REDSTONE') {
        if (effectiveCost > player.redstone) return reject(state, 'INSUFFICIENT_REDSTONE', 'not enough redstone');
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
        adjacencyHealthModifier: 0,
        slotKind: slotKind,
        slotIndex: slotIndex,
        occupiedSlots: occupiedSlots,
        summonedTurn: state.turn,
        hasAttacked: false,
        keywords: definition.keywords.slice(),
        temporaryAttackModifier: 0,
        temporaryAttackModifierExpiresOnTurn: 0,
        statuses: []
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
      if (paymentMethod === 'REDSTONE') player.redstone -= effectiveCost;
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
      recalculateAdjacencyHealthAuras();
      triggerWoodlandNurseryGrowth(player, battlefieldObject);
      triggerCoralReefGrowth(player, battlefieldObject);
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
      } else if (definition.effectImplementationStatus === 'IMPLEMENTED' &&
          definition.effectIds.length === 1 && definition.effectIds[0] === 'effect.pf_003.01') {
        triggerTamedWolfBattlecry(player, battlefieldObject);
      } else if (definition.effectImplementationStatus === 'IMPLEMENTED' &&
          definition.effectIds.length === 1 && definition.effectIds[0] === 'effect.pf_004.01') {
        generateCard(player, 'tk_002', cardId, battlefieldObject.instanceId, definition.effectIds[0]);
      } else if (definition.effectImplementationStatus === 'IMPLEMENTED' &&
          definition.effectIds.length === 1 && definition.effectIds[0] === 'effect.db_003.01') {
        offerArchaeologyChoice(player, battlefieldObject, definition.effectIds[0]);
      } else if (definition.effectImplementationStatus === 'IMPLEMENTED' &&
          definition.effectIds.length === 1 && definition.effectIds[0] === 'effect.si_003.01') {
        if (battlecryTargetPlayer === null || battlecryTarget === null) throw new Error('validated stray target was not resolved');
        applySlow(
          battlecryTargetPlayer,
          battlecryTarget,
          player,
          cardId,
          battlefieldObject.instanceId,
          definition.effectIds[0],
          0
        );
      } else if (definition.effectImplementationStatus === 'IMPLEMENTED' &&
          definition.effectIds.length === 1 && definition.effectIds[0] === 'effect.or_003.01') {
        if (drownedBattlecryActive && battlecryTargetPlayer !== null && battlecryTarget !== null) {
          battlecryTarget.health = Math.max(0, battlecryTarget.health - 1);
          emit('OBJECT_STATS_CHANGED', {
            playerId: battlecryTargetPlayer.playerId,
            instanceId: battlecryTarget.instanceId,
            sourceCardId: cardId,
            sourceInstanceId: battlefieldObject.instanceId,
            effectId: definition.effectIds[0],
            reason: 'DAMAGE',
            attack: battlecryTarget.attack,
            health: battlecryTarget.health,
            temporaryAttackModifier: battlecryTarget.temporaryAttackModifier,
            temporaryAttackModifierExpiresOnTurn: battlecryTarget.temporaryAttackModifierExpiresOnTurn
          });
          const killCredits: { [instanceId: string]: string } = {};
          if (battlecryTarget.health === 0) killCredits[battlecryTarget.instanceId] = player.playerId;
          settleDeaths(player, battlecryTargetPlayer, killCredits);
        }
      } else if (definition.effectImplementationStatus === 'IMPLEMENTED' &&
          definition.effectIds.length === 1 && definition.effectIds[0] === 'effect.or_001.01') {
        offerMoveChoice(player, cardId, battlefieldObject.instanceId, player, battlefieldObject, definition.effectIds[0]);
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

    function applySlow(
      targetPlayer: PlayerState,
      target: BattlefieldObjectState,
      sourcePlayer: PlayerState,
      sourceCardId: string,
      sourceInstanceId: string,
      effectId: string,
      attackModifier: number
    ): void {
      let status: BattlefieldStatusState | null = null;
      for (let index = 0; index < target.statuses.length; index += 1) {
        if (target.statuses[index]!.statusId === 'SLOW') status = target.statuses[index]!;
      }
      if (status === null) {
        const attackBefore = target.attack;
        status = {
          statusId: 'SLOW',
          remainingDuration: 1,
          sourcePlayerId: sourcePlayer.playerId,
          sourceCardId: sourceCardId,
          sourceInstanceId: sourceInstanceId,
          effectId: effectId,
          attackModifier: 0,
          boundAttackModifier: Math.min(0, attackModifier)
        };
        target.statuses.push(status);
        target.attack = Math.max(0, target.attack + Math.min(0, attackModifier));
        status.attackModifier = target.attack - attackBefore;
      } else {
        let replacesSource = false;
        if (status.remainingDuration < 1) {
          status.remainingDuration = 1;
          replacesSource = true;
        }
        const requestedModifier = Math.min(0, attackModifier);
        if (requestedModifier < status.boundAttackModifier) {
          const attackBefore = target.attack;
          target.attack = Math.max(0, target.attack + requestedModifier - status.boundAttackModifier);
          status.attackModifier += target.attack - attackBefore;
          status.boundAttackModifier = requestedModifier;
          replacesSource = true;
        }
        if (replacesSource) {
          status.sourcePlayerId = sourcePlayer.playerId;
          status.sourceCardId = sourceCardId;
          status.sourceInstanceId = sourceInstanceId;
          status.effectId = effectId;
        }
      }
      emit('OBJECT_STATUS_APPLIED', {
        playerId: targetPlayer.playerId,
        instanceId: target.instanceId,
        statusId: status.statusId,
        remainingDuration: status.remainingDuration,
        sourcePlayerId: status.sourcePlayerId,
        sourceCardId: status.sourceCardId,
        sourceInstanceId: status.sourceInstanceId,
        effectId: status.effectId,
        statusAttackModifier: status.attackModifier,
        boundAttackModifier: status.boundAttackModifier,
        attack: target.attack,
        health: target.health
      });
    }

    function expireStatuses(player: PlayerState): void {
      for (let objectIndex = 0; objectIndex < player.battlefield.length; objectIndex += 1) {
        const object = player.battlefield[objectIndex]!;
        for (let statusIndex = object.statuses.length - 1; statusIndex >= 0; statusIndex -= 1) {
          const status = object.statuses[statusIndex]!;
          status.remainingDuration -= 1;
          if (status.remainingDuration > 0) continue;
          object.attack = Math.max(0, object.attack - status.attackModifier);
          object.statuses.splice(statusIndex, 1);
          emit('OBJECT_STATUS_REMOVED', {
            playerId: player.playerId,
            instanceId: object.instanceId,
            statusId: status.statusId,
            sourcePlayerId: status.sourcePlayerId,
            sourceCardId: status.sourceCardId,
            sourceInstanceId: status.sourceInstanceId,
            effectId: status.effectId,
            reason: 'DURATION_EXPIRED',
            attack: object.attack,
            health: object.health
          });
        }
      }
    }

    function isImplementedTurtleAuraSource(object: BattlefieldObjectState): boolean {
      if (object.cardType !== 'UNIT' || object.health <= 0) return false;
      const definition = getCardDefinition(object.cardId);
      return definition !== null && definition.effectImplementationStatus === 'IMPLEMENTED' &&
        definition.effectIds.indexOf('effect.or_005.01') >= 0;
    }

    function recalculateAdjacencyHealthAuras(): void {
      for (let playerIndex = 0; playerIndex < next.players.length; playerIndex += 1) {
        const player = next.players[playerIndex]!;
        const sources = player.battlefield.filter(isImplementedTurtleAuraSource).slice();
        const targets = player.battlefield.filter(function (object): boolean {
          return object.cardType === 'UNIT';
        }).slice().sort(function (left, right): number {
          if (left.slotIndex !== right.slotIndex) return left.slotIndex - right.slotIndex;
          return left.instanceId < right.instanceId ? -1 : left.instanceId > right.instanceId ? 1 : 0;
        });
        for (let targetIndex = 0; targetIndex < targets.length; targetIndex += 1) {
          const target = targets[targetIndex]!;
          const desiredModifier = sources.filter(function (source): boolean {
            return source.instanceId !== target.instanceId && Math.abs(source.slotIndex - target.slotIndex) === 1;
          }).length;
          if (desiredModifier === target.adjacencyHealthModifier) continue;
          const delta = desiredModifier - target.adjacencyHealthModifier;
          target.adjacencyHealthModifier = desiredModifier;
          target.maxHealth += delta;
          target.health = Math.max(0, Math.min(target.maxHealth, target.health + delta));
          emit('OBJECT_STATS_CHANGED', {
            playerId: player.playerId,
            instanceId: target.instanceId,
            sourceCardId: 'or_005',
            sourceInstanceId: null,
            effectId: 'effect.or_005.01',
            reason: 'AURA_RECALCULATED',
            attack: target.attack,
            health: target.health,
            maxHealth: target.maxHealth,
            adjacencyHealthModifier: target.adjacencyHealthModifier,
            temporaryAttackModifier: target.temporaryAttackModifier,
            temporaryAttackModifierExpiresOnTurn: target.temporaryAttackModifierExpiresOnTurn
          });
        }
      }
    }

    function triggerWoodlandNurseryGrowth(player: PlayerState, summonedUnit: BattlefieldObjectState): number {
      if (summonedUnit.cardType !== 'UNIT' || summonedUnit.health <= 0 || !cardHasTag(summonedUnit.cardId, 'animal')) return 0;
      const nurseries = player.battlefield.filter(function (object): boolean {
        if (object.cardType !== 'BUILDING' || object.health <= 0) return false;
        const definition = getCardDefinition(object.cardId);
        return definition !== null && definition.effectImplementationStatus === 'IMPLEMENTED' &&
          definition.effectIds.indexOf('effect.pf_005.01') >= 0;
      }).slice().sort(function (left, right): number {
        if (left.slotIndex !== right.slotIndex) return left.slotIndex - right.slotIndex;
        return left.instanceId < right.instanceId ? -1 : left.instanceId > right.instanceId ? 1 : 0;
      });
      let triggered = 0;
      for (let nurseryIndex = 0; nurseryIndex < nurseries.length; nurseryIndex += 1) {
        const nursery = nurseries[nurseryIndex]!;
        const triggerKey = nursery.instanceId + ':effect.pf_005.01';
        if (player.triggeredEffectKeysThisTurn.indexOf(triggerKey) >= 0) continue;
        player.triggeredEffectKeysThisTurn.push(triggerKey);
        summonedUnit.health += 1;
        summonedUnit.maxHealth += 1;
        triggered += 1;
        emit('OBJECT_STATS_CHANGED', {
          playerId: player.playerId,
          instanceId: summonedUnit.instanceId,
          sourceCardId: nursery.cardId,
          sourceInstanceId: nursery.instanceId,
          effectId: 'effect.pf_005.01',
          reason: 'PERMANENT_HEALTH_MODIFIER',
          attack: summonedUnit.attack,
          health: summonedUnit.health,
          maxHealth: summonedUnit.maxHealth,
          temporaryAttackModifier: summonedUnit.temporaryAttackModifier,
          temporaryAttackModifierExpiresOnTurn: summonedUnit.temporaryAttackModifierExpiresOnTurn
        });
      }
      return triggered;
    }

    function triggerCoralReefGrowth(player: PlayerState, summonedUnit: BattlefieldObjectState): number {
      if (summonedUnit.cardType !== 'UNIT' || summonedUnit.health <= 0 || !cardHasTag(summonedUnit.cardId, 'aquatic')) return 0;
      const reefs = player.battlefield.filter(function (object): boolean {
        if (object.cardType !== 'BUILDING' || object.health <= 0) return false;
        const definition = getCardDefinition(object.cardId);
        return definition !== null && definition.effectImplementationStatus === 'IMPLEMENTED' &&
          definition.effectIds.indexOf('effect.or_007.01') >= 0;
      }).slice().sort(function (left, right): number {
        if (left.slotIndex !== right.slotIndex) return left.slotIndex - right.slotIndex;
        return left.instanceId < right.instanceId ? -1 : left.instanceId > right.instanceId ? 1 : 0;
      });
      let triggered = 0;
      for (let reefIndex = 0; reefIndex < reefs.length; reefIndex += 1) {
        const reef = reefs[reefIndex]!;
        const triggerKey = reef.instanceId + ':effect.or_007.01';
        if (player.triggeredEffectKeysThisTurn.indexOf(triggerKey) >= 0) continue;
        player.triggeredEffectKeysThisTurn.push(triggerKey);
        summonedUnit.health += 1;
        summonedUnit.maxHealth += 1;
        triggered += 1;
        emit('OBJECT_STATS_CHANGED', {
          playerId: player.playerId,
          instanceId: summonedUnit.instanceId,
          sourceCardId: reef.cardId,
          sourceInstanceId: reef.instanceId,
          effectId: 'effect.or_007.01',
          reason: 'PERMANENT_HEALTH_MODIFIER',
          attack: summonedUnit.attack,
          health: summonedUnit.health,
          maxHealth: summonedUnit.maxHealth,
          temporaryAttackModifier: summonedUnit.temporaryAttackModifier,
          temporaryAttackModifierExpiresOnTurn: summonedUnit.temporaryAttackModifierExpiresOnTurn
        });
      }
      return triggered;
    }

    function triggerTamedWolfBattlecry(player: PlayerState, wolf: BattlefieldObjectState): boolean {
      if (wolf.cardType !== 'UNIT' || wolf.health <= 0) return false;
      const hasAdjacentAnimal = player.battlefield.some(function (neighbor): boolean {
        return neighbor.instanceId !== wolf.instanceId && neighbor.cardType === 'UNIT' && neighbor.health > 0 &&
          Math.abs(neighbor.slotIndex - wolf.slotIndex) === 1 && cardHasTag(neighbor.cardId, 'animal');
      });
      if (!hasAdjacentAnimal) return false;
      wolf.health += 1;
      wolf.maxHealth += 1;
      emit('OBJECT_STATS_CHANGED', {
        playerId: player.playerId,
        instanceId: wolf.instanceId,
        sourceCardId: wolf.cardId,
        sourceInstanceId: wolf.instanceId,
        effectId: 'effect.pf_003.01',
        reason: 'PERMANENT_HEALTH_MODIFIER',
        attack: wolf.attack,
        health: wolf.health,
        maxHealth: wolf.maxHealth,
        temporaryAttackModifier: wolf.temporaryAttackModifier,
        temporaryAttackModifierExpiresOnTurn: wolf.temporaryAttackModifierExpiresOnTurn
      });
      return true;
    }

    function resolveOceanMonumentEndPhase(player: PlayerState, opponent: PlayerState): number {
      const monuments = player.battlefield.filter(function (object): boolean {
        if (object.cardType !== 'STRUCTURE' || object.health <= 0) return false;
        const definition = getCardDefinition(object.cardId);
        return definition !== null && definition.effectImplementationStatus === 'IMPLEMENTED' &&
          definition.effectIds.indexOf('effect.or_008.01') >= 0;
      }).slice().sort(function (left, right): number {
        if (left.slotIndex !== right.slotIndex) return left.slotIndex - right.slotIndex;
        return left.instanceId < right.instanceId ? -1 : left.instanceId > right.instanceId ? 1 : 0;
      });
      let totalDamage = 0;
      for (let monumentIndex = 0; monumentIndex < monuments.length; monumentIndex += 1) {
        const monument = monuments[monumentIndex]!;
        if (player.battlefield.indexOf(monument) < 0 || monument.health <= 0) continue;
        const targets = opponent.battlefield.filter(function (target): boolean {
          if (target.cardType !== 'UNIT' || target.health <= 0) return false;
          return !opponent.battlefield.some(function (neighbor): boolean {
            return neighbor.instanceId !== target.instanceId && neighbor.cardType === 'UNIT' && neighbor.health > 0 &&
              Math.abs(neighbor.slotIndex - target.slotIndex) === 1;
          });
        }).slice().sort(function (left, right): number {
          if (left.slotIndex !== right.slotIndex) return left.slotIndex - right.slotIndex;
          return left.instanceId < right.instanceId ? -1 : left.instanceId > right.instanceId ? 1 : 0;
        });
        const killCredits: { [instanceId: string]: string } = {};
        for (let targetIndex = 0; targetIndex < targets.length; targetIndex += 1) {
          const target = targets[targetIndex]!;
          target.health = Math.max(0, target.health - 1);
          totalDamage += 1;
          if (target.health === 0) killCredits[target.instanceId] = player.playerId;
          emit('OBJECT_STATS_CHANGED', {
            playerId: opponent.playerId,
            instanceId: target.instanceId,
            sourceCardId: monument.cardId,
            sourceInstanceId: monument.instanceId,
            effectId: 'effect.or_008.01',
            reason: 'DAMAGE',
            attack: target.attack,
            health: target.health,
            temporaryAttackModifier: target.temporaryAttackModifier,
            temporaryAttackModifierExpiresOnTurn: target.temporaryAttackModifierExpiresOnTurn
          });
        }
        if (targets.length > 0) settleDeaths(player, opponent, killCredits);
      }
      return totalDamage;
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

    function settleDeaths(
      currentPlayer: PlayerState,
      nonCurrentPlayer: PlayerState,
      killCredits?: { [instanceId: string]: string }
    ): void {
      const resolvedKillCredits = killCredits || {};
      while (true) {
        const currentDeaths = removeDeadObjects(currentPlayer);
        const nonCurrentDeaths = removeDeadObjects(nonCurrentPlayer);
        if (currentDeaths.length === 0 && nonCurrentDeaths.length === 0) return;
        recalculateAdjacencyHealthAuras();
        for (let index = 0; index < currentDeaths.length; index += 1) {
          const object = currentDeaths[index]!;
          resolveDeathTriggers(currentPlayer, object, resolvedKillCredits);
        }
        for (let index = 0; index < nonCurrentDeaths.length; index += 1) {
          const object = nonCurrentDeaths[index]!;
          resolveDeathTriggers(nonCurrentPlayer, object, resolvedKillCredits);
        }
      }
    }

    function resolveDeathTriggers(
      player: PlayerState,
      object: BattlefieldObjectState,
      killCredits: { [instanceId: string]: string }
    ): void {
      resolveDeathrattles(player, object, killCredits);
      resolveDrops(player, object, killCredits[object.instanceId]);
    }

    function resolveDeathrattles(
      player: PlayerState,
      object: BattlefieldObjectState,
      killCredits: { [instanceId: string]: string }
    ): void {
      const definition = getCardDefinition(object.cardId);
      if (definition === null || definition.effectImplementationStatus !== 'IMPLEMENTED') return;
      if (definition.effectIds.indexOf('effect.ed_004.01') >= 0) {
        generateCard(player, 'tk_016', object.cardId, object.instanceId, 'effect.ed_004.01');
      }
      if (definition.effectIds.indexOf('effect.nt_001.01') >= 0) {
        summonUnit(player, 'tk_014', object.cardId, object.instanceId, 'effect.nt_001.01', object.slotIndex);
      }
      if (definition.effectIds.indexOf('effect.cd_003.01') >= 0) {
        const enemy = next.players.filter(function (candidate): boolean {
          return candidate.playerId !== player.playerId;
        })[0];
        if (!enemy) throw new Error('deathrattle owner has no opposing player: ' + player.playerId);
        const candidates = enemy.battlefield.filter(function (candidate): boolean {
          return candidate.cardType === 'UNIT' && candidate.health > 0;
        });
        candidates.sort(function (left, right): number {
          if (left.slotIndex !== right.slotIndex) return left.slotIndex - right.slotIndex;
          return left.instanceId < right.instanceId ? -1 : left.instanceId > right.instanceId ? 1 : 0;
        });
        if (candidates.length > 0) {
          const randomIndex = (seedFromText(
            next.matchId + ':deathrattle:' + object.instanceId + ':' + next.lastEventId
          ) >>> 0) % candidates.length;
          const target = candidates[randomIndex]!;
          target.health = Math.max(0, target.health - 1);
          if (target.health === 0) killCredits[target.instanceId] = player.playerId;
          emit('OBJECT_STATS_CHANGED', {
            playerId: enemy.playerId,
            instanceId: target.instanceId,
            sourceCardId: object.cardId,
            sourceInstanceId: object.instanceId,
            effectId: 'effect.cd_003.01',
            reason: 'DAMAGE',
            attack: target.attack,
            health: target.health,
            temporaryAttackModifier: target.temporaryAttackModifier,
            temporaryAttackModifierExpiresOnTurn: target.temporaryAttackModifierExpiresOnTurn
          });
        }
      }
    }

    function resolveDrops(player: PlayerState, object: BattlefieldObjectState, killerPlayerId?: string): void {
      if (!killerPlayerId || killerPlayerId === player.playerId) return;
      const definition = getCardDefinition(object.cardId);
      if (definition === null || definition.effectImplementationStatus !== 'IMPLEMENTED') return;
      let dropCardId: string | null = null;
      if (definition.effectIds.indexOf('effect.db_001.01') >= 0) dropCardId = 'tk_005';
      else if (definition.effectIds.indexOf('effect.pf_002.01') >= 0) dropCardId = 'tk_001';
      else if (definition.effectIds.indexOf('effect.cd_003.01') >= 0) dropCardId = 'tk_009';
      else if (definition.effectIds.indexOf('effect.si_003.01') >= 0) dropCardId = 'tk_009';
      else if (definition.effectIds.indexOf('effect.or_004.01') >= 0) dropCardId = 'tk_012';
      if (dropCardId === null) return;
      const killer = next.players.filter(function (candidate): boolean {
        return candidate.playerId === killerPlayerId;
      })[0];
      if (!killer) throw new Error('drop killer is not a match player: ' + killerPlayerId);
      generateCard(killer, dropCardId, object.cardId, object.instanceId, definition.effectIds[0]!);
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
        adjacencyHealthModifier: 0,
        slotKind: 'UNIT',
        slotIndex: slotIndex,
        occupiedSlots: 1,
        summonedTurn: next.turn,
        hasAttacked: false,
        keywords: definition.keywords.slice(),
        temporaryAttackModifier: 0,
        temporaryAttackModifierExpiresOnTurn: 0,
        statuses: []
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
      recalculateAdjacencyHealthAuras();
      triggerWoodlandNurseryGrowth(player, object);
      triggerCoralReefGrowth(player, object);
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

    function resolveExcavatedCard(player: PlayerState, cardId: string): void {
      const buriedIndex = player.buriedCardIds.indexOf(cardId);
      if (buriedIndex < 0) throw new Error('excavated card does not have a buried marker: ' + cardId);
      player.buriedCardIds.splice(buriedIndex, 1);
      if (cardId !== 'tk_006') throw new Error('buried effect handler is not registered: ' + cardId);
      const destination = player.hand.length >= HAND_LIMIT ? 'DISCARD' : 'HAND';
      if (destination === 'HAND') player.hand.push(cardId);
      else player.discardPile.push(cardId);
      player.excavatedThisTurn = true;
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
        if (player.buriedCardIds.indexOf(cardId) >= 0) {
          resolveExcavatedCard(player, cardId);
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

    function offerArchaeologyChoice(player: PlayerState, source: BattlefieldObjectState, effectId: string): void {
      if (next.pendingChoice !== null) throw new Error('cannot offer a second choice while one is pending');
      const options: PendingChoiceOptionState[] = [];
      const optionCount = Math.min(3, player.deck.length);
      for (let optionIndex = 0; optionIndex < optionCount; optionIndex += 1) {
        const cardId = player.deck[player.deck.length - 1 - optionIndex]!;
        options.push({
          optionIndex: optionIndex,
          cardId: cardId,
          slotIndex: -1,
          selectable: player.buriedCardIds.indexOf(cardId) >= 0
        });
      }
      next.pendingChoice = {
        choiceId: 'choice-' + String(next.lastEventId + 1),
        playerId: player.playerId,
        sourceCardId: source.cardId,
        sourceInstanceId: source.instanceId,
        effectId: effectId,
        kind: 'ARCHAEOLOGY_TOP_3',
        targetPlayerId: '',
        targetInstanceId: '',
        options: options
      };
      emit('CHOICE_OFFERED', {
        choiceId: next.pendingChoice.choiceId,
        playerId: player.playerId,
        sourceCardId: source.cardId,
        sourceInstanceId: source.instanceId,
        effectId: effectId,
        kind: next.pendingChoice.kind,
        targetPlayerId: '',
        targetInstanceId: '',
        options: options.map(function (option): PendingChoiceOptionState {
          return { optionIndex: option.optionIndex, cardId: option.cardId, slotIndex: option.slotIndex, selectable: option.selectable };
        })
      });
    }

    function offerMoveChoice(player: PlayerState, sourceCardId: string, sourceInstanceId: string, targetPlayer: PlayerState,
      target: BattlefieldObjectState, effectId: string): void {
      const options: PendingChoiceOptionState[] = [];
      const candidates = [target.slotIndex - 1, target.slotIndex + 1];
      for (let index = 0; index < candidates.length; index += 1) {
        const slotIndex = candidates[index]!;
        if (slotIndex < 0 || slotIndex >= targetPlayer.unitSlots.length || targetPlayer.unitSlots[slotIndex] !== null) continue;
        options.push({ optionIndex: options.length, cardId: target.cardId, slotIndex: slotIndex, selectable: true });
      }
      if (options.length === 0) return;
      next.pendingChoice = {
        choiceId: 'choice-' + String(next.lastEventId + 1),
        playerId: player.playerId,
        sourceCardId: sourceCardId,
        sourceInstanceId: sourceInstanceId,
        effectId: effectId,
        kind: 'MOVE_UNIT',
        targetPlayerId: targetPlayer.playerId,
        targetInstanceId: target.instanceId,
        options: options
      };
      emit('CHOICE_OFFERED', {
        choiceId: next.pendingChoice.choiceId,
        playerId: player.playerId,
        sourceCardId: sourceCardId,
        sourceInstanceId: sourceInstanceId,
        effectId: effectId,
        kind: 'MOVE_UNIT',
        targetPlayerId: targetPlayer.playerId,
        targetInstanceId: target.instanceId,
        options: options.map(function (option): PendingChoiceOptionState {
          return { optionIndex: option.optionIndex, cardId: option.cardId, slotIndex: option.slotIndex, selectable: option.selectable };
        })
      });
    }

    function triggerSuccessfulMovement(targetPlayer: PlayerState, movedUnit: BattlefieldObjectState): void {
      const guides = targetPlayer.battlefield.filter(function (object): boolean {
        return object.cardType === 'UNIT' && object.cardId === 'or_002' && object.instanceId !== movedUnit.instanceId;
      }).slice().sort(function (left, right): number {
        if (left.slotIndex !== right.slotIndex) return left.slotIndex - right.slotIndex;
        return left.instanceId < right.instanceId ? -1 : left.instanceId > right.instanceId ? 1 : 0;
      });
      for (let guideIndex = 0; guideIndex < guides.length; guideIndex += 1) {
        const guide = guides[guideIndex]!;
        const triggerKey = guide.instanceId + ':effect.or_002.01';
        if (targetPlayer.triggeredEffectKeysThisTurn.indexOf(triggerKey) >= 0) continue;
        targetPlayer.triggeredEffectKeysThisTurn.push(triggerKey);
        movedUnit.attack += 1;
        movedUnit.temporaryAttackModifier += 1;
        movedUnit.temporaryAttackModifierExpiresOnTurn = movedUnit.temporaryAttackModifier === 0 ? 0 : next.turn;
        emit('OBJECT_STATS_CHANGED', {
          playerId: targetPlayer.playerId, instanceId: movedUnit.instanceId,
          sourceCardId: guide.cardId, sourceInstanceId: guide.instanceId,
          effectId: 'effect.or_002.01', reason: 'TEMPORARY_ATTACK_MODIFIER',
          attack: movedUnit.attack, health: movedUnit.health,
          temporaryAttackModifier: movedUnit.temporaryAttackModifier,
          temporaryAttackModifierExpiresOnTurn: movedUnit.temporaryAttackModifierExpiresOnTurn
        });
      }

      const guardianPlayer = next.players.filter(function (candidate): boolean {
        return candidate.playerId !== targetPlayer.playerId;
      })[0];
      if (!guardianPlayer) throw new Error('moved unit has no opposing player: ' + targetPlayer.playerId);
      const guardians = guardianPlayer.battlefield.filter(function (object): boolean {
        if (object.cardType !== 'UNIT' || object.health <= 0) return false;
        const definition = getCardDefinition(object.cardId);
        return definition !== null && definition.effectImplementationStatus === 'IMPLEMENTED' &&
          definition.effectIds.indexOf('effect.or_004.01') >= 0;
      }).slice().sort(function (left, right): number {
        if (left.slotIndex !== right.slotIndex) return left.slotIndex - right.slotIndex;
        return left.instanceId < right.instanceId ? -1 : left.instanceId > right.instanceId ? 1 : 0;
      });
      const killCredits: { [instanceId: string]: string } = {};
      for (let guardianIndex = 0; guardianIndex < guardians.length; guardianIndex += 1) {
        if (movedUnit.health <= 0) break;
        const guardian = guardians[guardianIndex]!;
        const triggerKey = guardian.instanceId + ':effect.or_004.01';
        if (guardianPlayer.triggeredEffectKeysThisTurn.indexOf(triggerKey) >= 0) continue;
        guardianPlayer.triggeredEffectKeysThisTurn.push(triggerKey);
        movedUnit.health = Math.max(0, movedUnit.health - 1);
        if (movedUnit.health === 0) killCredits[movedUnit.instanceId] = guardianPlayer.playerId;
        emit('OBJECT_STATS_CHANGED', {
          playerId: targetPlayer.playerId, instanceId: movedUnit.instanceId,
          sourceCardId: guardian.cardId, sourceInstanceId: guardian.instanceId,
          effectId: 'effect.or_004.01', reason: 'DAMAGE',
          attack: movedUnit.attack, health: movedUnit.health,
          temporaryAttackModifier: movedUnit.temporaryAttackModifier,
          temporaryAttackModifierExpiresOnTurn: movedUnit.temporaryAttackModifierExpiresOnTurn
        });
      }
      if (movedUnit.health === 0) {
        const activePlayer = next.players[next.activePlayerIndex]!;
        const nonActivePlayer = next.players[next.activePlayerIndex === 0 ? 1 : 0]!;
        settleDeaths(activePlayer, nonActivePlayer, killCredits);
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
      if (definition.cardType !== 'SPELL' && definition.cardType !== 'MATERIAL' && definition.cardType !== 'EQUIPMENT') {
        return reject(state, 'INVALID_TARGET', 'PLAY_CARD accepts spells, materials, and equipment');
      }
      if (definition.effectImplementationStatus !== 'IMPLEMENTED' || definition.effectIds.length !== 1) {
        return reject(state, 'EFFECT_NOT_IMPLEMENTED', 'card effect is registered but not implemented');
      }
      const effectId = definition.effectIds[0]!;
      if (effectId !== 'effect.db_002.01' && effectId !== 'effect.db_006.01' && effectId !== 'effect.nt_006.01' &&
          effectId !== 'effect.pf_006.01' && effectId !== 'effect.pf_007.01' &&
          effectId !== 'effect.si_001.01' && effectId !== 'effect.si_006.01' && effectId !== 'effect.tk_005.01' &&
          effectId !== 'effect.tk_009.01' && effectId !== 'effect.tk_010.01' && effectId !== 'effect.tk_012.01' && effectId !== 'effect.or_006.01' &&
          effectId !== 'effect.tk_016.01') {
        return reject(state, 'EFFECT_NOT_IMPLEMENTED', 'effect handler is not registered');
      }
      const player = next.players[actorIndex]!;
      const opponent = next.players[actorIndex === 0 ? 1 : 0]!;
      let targetedObject: BattlefieldObjectState | null = null;
      let targetedPlayer: PlayerState | null = null;
      let targetedObjects: BattlefieldObjectState[] = [];
      if (effectId === 'effect.pf_006.01') {
        const targetInstanceIds = command.payload.targetInstanceIds;
        if (command.payload.targetType !== 'UNIT' || !Array.isArray(targetInstanceIds) || targetInstanceIds.length !== 2 ||
            targetInstanceIds.some(function (instanceId): boolean { return typeof instanceId !== 'string'; }) ||
            targetInstanceIds[0] === targetInstanceIds[1]) {
          return reject(state, 'INVALID_TARGET', 'breeding season requires two different friendly Animal targets');
        }
        for (let targetIndex = 0; targetIndex < targetInstanceIds.length; targetIndex += 1) {
          const target = findObject(player, targetInstanceIds[targetIndex] as string);
          if (target === null || target.cardType !== 'UNIT' || target.health <= 0 || !cardHasTag(target.cardId, 'animal')) {
            return reject(state, 'INVALID_TARGET', 'breeding season targets must be living friendly Animals');
          }
          targetedObjects.push(target);
        }
        targetedObjects.sort(function (left, right): number {
          if (left.slotIndex !== right.slotIndex) return left.slotIndex - right.slotIndex;
          return left.instanceId < right.instanceId ? -1 : left.instanceId > right.instanceId ? 1 : 0;
        });
      } else if (effectId === 'effect.si_001.01' || effectId === 'effect.si_006.01' ||
          effectId === 'effect.tk_009.01' || effectId === 'effect.tk_012.01') {
        if (command.payload.targetType !== 'UNIT' || typeof command.payload.targetInstanceId !== 'string') {
          return reject(state, 'INVALID_TARGET', effectId === 'effect.tk_009.01' || effectId === 'effect.tk_012.01'
            ? 'material requires a friendly unit target'
            : 'snow spell requires an enemy unit target');
        }
        targetedPlayer = effectId === 'effect.tk_009.01' || effectId === 'effect.tk_012.01' ? player : opponent;
        targetedObject = findObject(targetedPlayer, command.payload.targetInstanceId);
        if (targetedObject === null || targetedObject.cardType !== 'UNIT') {
          return reject(state, 'INVALID_TARGET', effectId === 'effect.tk_009.01' || effectId === 'effect.tk_012.01'
            ? 'material target must be a living friendly unit'
            : 'snow spell target must be a living enemy unit');
        }
        if (effectId === 'effect.tk_012.01') {
          const hasAdjacentEmptySlot = [targetedObject.slotIndex - 1, targetedObject.slotIndex + 1].some(function (slotIndex): boolean {
            return slotIndex >= 0 && slotIndex < player.unitSlots.length && player.unitSlots[slotIndex] === null;
          });
          if (!cardHasTag(targetedObject.cardId, 'aquatic') || !hasAdjacentEmptySlot) {
            return reject(state, 'INVALID_TARGET', 'prismarine shard requires a friendly aquatic unit with an adjacent empty slot');
          }
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
      if (definition.cardType === 'EQUIPMENT') {
        if (definition.durability <= 0 || definition.attack <= 0) {
          return reject(state, 'INVALID_STATE', 'equipment requires positive attack and durability');
        }
        if (player.equipment !== null) {
          const replaced = player.equipment;
          player.discardPile.push(replaced.cardId);
          emit('EQUIPMENT_DESTROYED', {
            playerId: player.playerId, instanceId: replaced.instanceId, cardId: replaced.cardId,
            reason: 'REPLACED', discardCount: player.discardPile.length
          });
        }
        const equipment: EquipmentState = {
          instanceId: 'equipment-' + String(next.nextInstanceId++), cardId: cardId,
          attack: definition.attack, durability: definition.durability, maxDurability: definition.durability
        };
        player.equipment = equipment;
        emit('CARD_EQUIPPED', {
          playerId: player.playerId, instanceId: equipment.instanceId, cardId: equipment.cardId,
          attack: equipment.attack, durability: equipment.durability, maxDurability: equipment.maxDurability,
          effectId: effectId, redstone: player.redstone, handCount: player.hand.length,
          discardCount: player.discardPile.length, nextInstanceId: next.nextInstanceId
        });
        return null;
      }
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
      const effectSourceInstanceId = 'effect-' + String(next.lastEventId);

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
          const sandstormKillCredits: { [instanceId: string]: string } = {};
          for (let playerIndex = 0; playerIndex < next.players.length; playerIndex += 1) {
            const damagedPlayer = next.players[playerIndex]!;
            for (let objectIndex = 0; objectIndex < damagedPlayer.battlefield.length; objectIndex += 1) {
              const damagedObject = damagedPlayer.battlefield[objectIndex]!;
              if (damagedObject.cardType !== 'UNIT') continue;
              damagedObject.health = Math.max(0, damagedObject.health - 2);
              if (damagedObject.health === 0 && damagedPlayer.playerId !== player.playerId) {
                sandstormKillCredits[damagedObject.instanceId] = player.playerId;
              }
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
          settleDeaths(player, opponent, sandstormKillCredits);
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
        case 'effect.pf_006.01': {
          if (targetedObjects.length !== 2) throw new Error('validated breeding targets were not resolved');
          for (let targetIndex = 0; targetIndex < targetedObjects.length; targetIndex += 1) {
            const target = targetedObjects[targetIndex]!;
            target.health += 1;
            target.maxHealth += 1;
            emit('OBJECT_STATS_CHANGED', {
              playerId: player.playerId,
              instanceId: target.instanceId,
              sourceCardId: cardId,
              sourceInstanceId: effectSourceInstanceId,
              effectId: effectId,
              reason: 'PERMANENT_HEALTH_MODIFIER',
              attack: target.attack,
              health: target.health,
              maxHealth: target.maxHealth,
              temporaryAttackModifier: target.temporaryAttackModifier,
              temporaryAttackModifierExpiresOnTurn: target.temporaryAttackModifierExpiresOnTurn
            });
          }
          summonUnit(player, 'tk_003', cardId, effectSourceInstanceId, effectId, -1);
          return null;
        }
        case 'effect.pf_007.01': {
          for (let rallyStep = 0; rallyStep < 2; rallyStep += 1) {
            if (summonUnit(player, 'tk_004', cardId, effectSourceInstanceId, effectId, -1)) continue;
            drawCard(player);
            if (finishForSelfDefeat(player, 'FATIGUE')) break;
          }
          return null;
        }
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
        case 'effect.si_006.01': {
          if (targetedObject === null || targetedPlayer === null) throw new Error('validated powder snow target was not resolved');
          applySlow(targetedPlayer, targetedObject, player, cardId, '', effectId, -2);
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
        case 'effect.tk_012.01': {
          if (targetedObject === null || targetedPlayer === null) throw new Error('validated prismarine shard target was not resolved');
          offerMoveChoice(player, cardId, 'effect-' + String(next.lastEventId), targetedPlayer, targetedObject, effectId);
          if (next.pendingChoice === null) throw new Error('validated prismarine movement did not create a choice');
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

    function resolveChoice(): CommandRejected | null {
      const pendingChoice = next.pendingChoice;
      if (pendingChoice === null) return reject(state, 'INVALID_CHOICE', 'there is no pending card choice');
      if (pendingChoice.playerId !== actorPlayerId) return reject(state, 'CHOICE_REQUIRED', 'only the choice owner may resolve it');
      const choiceId = command.payload && command.payload.choiceId;
      const selectedOptionIndex = command.payload && command.payload.selectedOptionIndex;
      if (typeof choiceId !== 'string' || choiceId !== pendingChoice.choiceId ||
          typeof selectedOptionIndex !== 'number' || selectedOptionIndex % 1 !== 0) {
        return reject(state, 'INVALID_CHOICE', 'RESOLVE_CHOICE requires the current choiceId and an integer selectedOptionIndex');
      }
      if (pendingChoice.kind === 'MOVE_UNIT') {
        let moveOption: PendingChoiceOptionState | null = null;
        if (selectedOptionIndex === -1 && pendingChoice.effectId === 'effect.tk_012.01') {
          return reject(state, 'INVALID_CHOICE', 'prismarine shard movement requires a destination');
        }
        if (selectedOptionIndex !== -1) {
          for (let index = 0; index < pendingChoice.options.length; index += 1) {
            const option = pendingChoice.options[index]!;
            if (option.optionIndex === selectedOptionIndex && option.selectable) moveOption = option;
          }
          if (moveOption === null) return reject(state, 'INVALID_CHOICE', 'the selected movement destination is not available');
        }
        const targetPlayer = next.players.filter(function (candidate): boolean {
          return candidate.playerId === pendingChoice.targetPlayerId;
        })[0];
        if (targetPlayer === undefined) throw new Error('pending movement target player is missing');
        const target = findObject(targetPlayer, pendingChoice.targetInstanceId);
        if (target === null || target.cardType !== 'UNIT') throw new Error('pending movement target unit is missing');
        const fromSlotIndex = target.slotIndex;
        next.pendingChoice = null;
        emit('CHOICE_RESOLVED', {
          choiceId: pendingChoice.choiceId, playerId: actorPlayerId,
          sourceCardId: pendingChoice.sourceCardId, sourceInstanceId: pendingChoice.sourceInstanceId,
          effectId: pendingChoice.effectId, kind: pendingChoice.kind,
          selectedOptionIndex: selectedOptionIndex, selectedCardId: null,
          selectedSlotIndex: moveOption === null ? -1 : moveOption.slotIndex
        });
        if (moveOption !== null) {
          if (Math.abs(moveOption.slotIndex - fromSlotIndex) !== 1 || targetPlayer.unitSlots[moveOption.slotIndex] !== null) {
            throw new Error('pending movement destination changed before resolution');
          }
          targetPlayer.unitSlots[fromSlotIndex] = null;
          targetPlayer.unitSlots[moveOption.slotIndex] = target.instanceId;
          target.slotIndex = moveOption.slotIndex;
          emit('OBJECT_MOVED', {
            playerId: targetPlayer.playerId, instanceId: target.instanceId, cardId: target.cardId,
            sourcePlayerId: actorPlayerId, sourceCardId: pendingChoice.sourceCardId,
            sourceInstanceId: pendingChoice.sourceInstanceId, effectId: pendingChoice.effectId,
            fromSlotIndex: fromSlotIndex, toSlotIndex: moveOption.slotIndex
          });
          recalculateAdjacencyHealthAuras();
          const activePlayer = next.players[next.activePlayerIndex]!;
          const nonActivePlayer = next.players[next.activePlayerIndex === 0 ? 1 : 0]!;
          settleDeaths(activePlayer, nonActivePlayer);
          if (targetPlayer.battlefield.indexOf(target) < 0) return null;
          if (pendingChoice.effectId === 'effect.or_001.01') {
            target.attack += 1;
            target.temporaryAttackModifier += 1;
            target.temporaryAttackModifierExpiresOnTurn = target.temporaryAttackModifier === 0 ? 0 : next.turn;
            emit('OBJECT_STATS_CHANGED', {
              playerId: targetPlayer.playerId, instanceId: target.instanceId,
              sourceCardId: pendingChoice.sourceCardId, sourceInstanceId: pendingChoice.sourceInstanceId,
              effectId: pendingChoice.effectId, reason: 'TEMPORARY_ATTACK_MODIFIER',
              attack: target.attack, health: target.health,
              temporaryAttackModifier: target.temporaryAttackModifier,
              temporaryAttackModifierExpiresOnTurn: target.temporaryAttackModifierExpiresOnTurn
            });
          } else if (pendingChoice.effectId === 'effect.tk_012.01') {
            target.health = Math.min(target.maxHealth, target.health + 1);
            emit('OBJECT_STATS_CHANGED', {
              playerId: targetPlayer.playerId, instanceId: target.instanceId,
              sourceCardId: pendingChoice.sourceCardId, sourceInstanceId: pendingChoice.sourceInstanceId,
              effectId: pendingChoice.effectId, reason: 'HEAL',
              attack: target.attack, health: target.health,
              temporaryAttackModifier: target.temporaryAttackModifier,
              temporaryAttackModifierExpiresOnTurn: target.temporaryAttackModifierExpiresOnTurn
            });
          }
          triggerSuccessfulMovement(targetPlayer, target);
        }
        return null;
      }
      const selectableOptions = pendingChoice.options.filter(function (option): boolean { return option.selectable; });
      let selectedOption: PendingChoiceOptionState | null = null;
      if (selectableOptions.length === 0) {
        if (selectedOptionIndex !== -1) return reject(state, 'INVALID_CHOICE', 'this inspection has no selectable buried card');
      } else {
        for (let index = 0; index < selectableOptions.length; index += 1) {
          if (selectableOptions[index]!.optionIndex === selectedOptionIndex) selectedOption = selectableOptions[index]!;
        }
        if (selectedOption === null) return reject(state, 'INVALID_CHOICE', 'the selected option is not a buried card');
      }

      const player = next.players[actorIndex]!;
      next.pendingChoice = null;
      emit('CHOICE_RESOLVED', {
        choiceId: pendingChoice.choiceId,
        playerId: player.playerId,
        sourceCardId: pendingChoice.sourceCardId,
        sourceInstanceId: pendingChoice.sourceInstanceId,
        effectId: pendingChoice.effectId,
        kind: pendingChoice.kind,
        selectedOptionIndex: selectedOptionIndex,
        selectedCardId: selectedOption === null ? null : selectedOption.cardId,
        selectedSlotIndex: -1
      });
      if (selectedOption !== null) {
        const deckIndex = player.deck.length - 1 - selectedOption.optionIndex;
        if (deckIndex < 0 || player.deck[deckIndex] !== selectedOption.cardId ||
            player.buriedCardIds.indexOf(selectedOption.cardId) < 0) {
          throw new Error('pending archaeology choice no longer matches the authoritative deck');
        }
        player.deck.splice(deckIndex, 1);
        resolveExcavatedCard(player, selectedOption.cardId);
        drawCard(player);
        finishForSelfDefeat(player, 'FATIGUE');
      }
      return null;
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
      const heroAttack = attackerInstanceId === 'HERO';
      const attackingEquipment = heroAttack ? attackerPlayer.equipment : null;
      if (heroAttack) {
        if (attackingEquipment === null || attackingEquipment.attack <= 0 || attackingEquipment.durability <= 0) {
          return reject(state, 'INVALID_ATTACKER', 'hero requires a usable equipment card to attack');
        }
        if (attackerPlayer.heroHasAttacked) return reject(state, 'ATTACK_ALREADY_USED', 'hero has already attacked this turn');
      } else {
        if (attacker === null || attacker.cardType !== 'UNIT') {
          return reject(state, 'INVALID_ATTACKER', 'attacker must be a living friendly unit or HERO');
        }
        if (attacker.statuses.some(function (status): boolean { return status.statusId === 'SLOW'; })) {
          return reject(state, 'ATTACKER_NOT_READY', 'unit cannot attack while slowed');
        }
        if (attacker.attack <= 0) return reject(state, 'INVALID_ATTACKER', 'attacker must have attack');
        if (attacker.hasAttacked) return reject(state, 'ATTACK_ALREADY_USED', 'unit has already attacked this turn');
        if (attacker.summonedTurn === state.turn && attacker.keywords.indexOf('CHARGE') < 0) {
          return reject(state, 'ATTACKER_NOT_READY', 'unit cannot attack on its summoned turn without CHARGE');
        }
      }

      const tauntTargets = defenderPlayer.battlefield.filter(function (object): boolean {
        return object.health > 0 && object.keywords.indexOf('TAUNT') >= 0;
      });
      if (tauntTargets.length > 0) {
        const targetsTaunt = targetType !== 'HERO' && typeof targetInstanceId === 'string' &&
          tauntTargets.some(function (object): boolean { return object.instanceId === targetInstanceId; });
        if (!targetsTaunt) return reject(state, 'TAUNT_TARGET_REQUIRED', 'a legal enemy TAUNT object must be attacked first');
      }

      const attackValue = heroAttack ? attackingEquipment!.attack : attacker!.attack;
      if (heroAttack) attackerPlayer.heroHasAttacked = true;
      else attacker!.hasAttacked = true;
      if (targetType === 'HERO') {
        damageHero(defenderPlayer, attackValue);
        emit('ATTACK_RESOLVED', {
          attackerPlayerId: attackerPlayer.playerId,
          attackerInstanceId: heroAttack ? 'HERO' : attacker!.instanceId,
          targetPlayerId: defenderPlayer.playerId,
          targetType: 'HERO',
          targetInstanceId: null,
          damageToTarget: attackValue,
          damageToAttacker: 0,
          attackerHealth: heroAttack ? attackerPlayer.life : attacker!.health,
          attackerArmor: heroAttack ? attackerPlayer.armor : 0,
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
        target.health = Math.max(0, target.health - attackValue);
        if (heroAttack) damageHero(attackerPlayer, retaliation);
        else attacker!.health = Math.max(0, attacker!.health - retaliation);
        const combatKillCredits: { [instanceId: string]: string } = {};
        if (target.health === 0) combatKillCredits[target.instanceId] = attackerPlayer.playerId;
        if (!heroAttack && attacker!.health === 0 && retaliation > 0) combatKillCredits[attacker!.instanceId] = defenderPlayer.playerId;
        emit('ATTACK_RESOLVED', {
          attackerPlayerId: attackerPlayer.playerId,
          attackerInstanceId: heroAttack ? 'HERO' : attacker!.instanceId,
          targetPlayerId: defenderPlayer.playerId,
          targetType: targetType,
          targetInstanceId: target.instanceId,
          damageToTarget: attackValue,
          damageToAttacker: retaliation,
          attackerHealth: heroAttack ? attackerPlayer.life : attacker!.health,
          attackerArmor: heroAttack ? attackerPlayer.armor : 0,
          targetHealth: target.health,
          targetArmor: 0
        });
        settleDeaths(attackerPlayer, defenderPlayer, combatKillCredits);
        if (heroAttack && attackingEquipment !== null) {
          attackingEquipment.durability -= 1;
          emit('EQUIPMENT_DURABILITY_CHANGED', {
            playerId: attackerPlayer.playerId, instanceId: attackingEquipment.instanceId,
            cardId: attackingEquipment.cardId, durability: attackingEquipment.durability,
            maxDurability: attackingEquipment.maxDurability
          });
          if (attackingEquipment.durability === 0) {
            attackerPlayer.discardPile.push(attackingEquipment.cardId);
            attackerPlayer.equipment = null;
            emit('EQUIPMENT_DESTROYED', {
              playerId: attackerPlayer.playerId, instanceId: attackingEquipment.instanceId,
              cardId: attackingEquipment.cardId, reason: 'DURABILITY',
              discardCount: attackerPlayer.discardPile.length
            });
          }
          const survivingTarget = findObject(defenderPlayer, target.instanceId);
          if (attackingEquipment.cardId === 'or_006' && survivingTarget !== null && survivingTarget.cardType === 'UNIT') {
            offerMoveChoice(attackerPlayer, attackingEquipment.cardId, attackingEquipment.instanceId,
              defenderPlayer, survivingTarget, 'effect.or_006.01');
          }
        }
      }

      if (heroAttack && targetType === 'HERO' && attackingEquipment !== null) {
        attackingEquipment.durability -= 1;
        emit('EQUIPMENT_DURABILITY_CHANGED', {
          playerId: attackerPlayer.playerId, instanceId: attackingEquipment.instanceId,
          cardId: attackingEquipment.cardId, durability: attackingEquipment.durability,
          maxDurability: attackingEquipment.maxDurability
        });
        if (attackingEquipment.durability === 0) {
          attackerPlayer.discardPile.push(attackingEquipment.cardId);
          attackerPlayer.equipment = null;
          emit('EQUIPMENT_DESTROYED', {
            playerId: attackerPlayer.playerId, instanceId: attackingEquipment.instanceId,
            cardId: attackingEquipment.cardId, reason: 'DURABILITY', discardCount: attackerPlayer.discardPile.length
          });
        }
      }

      if (defenderPlayer.life <= 0) {
        next.status = 'FINISHED';
        next.winnerPlayerId = attackerPlayer.playerId;
        emit('MATCH_ENDED', { winnerPlayerId: next.winnerPlayerId, reason: 'HERO_DEFEATED' });
      }
      if (attackerPlayer.life <= 0 && next.status !== 'FINISHED') {
        next.status = 'FINISHED';
        next.winnerPlayerId = defenderPlayer.playerId;
        next.pendingChoice = null;
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
      case 'RESOLVE_CHOICE': {
        if (state.status !== 'ACTIVE') return reject(state, 'MULLIGAN_REQUIRED', 'both players must confirm their opening hands first');
        const choiceRejection = resolveChoice();
        if (choiceRejection !== null) return choiceRejection;
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
        resolveOceanMonumentEndPhase(next.players[actorIndex]!, next.players[actorIndex === 0 ? 1 : 0]!);
        expireStatuses(next.players[actorIndex]!);
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
        next.players[actorIndex]!.excavatedThisTurn = false;
        for (let playerIndex = 0; playerIndex < next.players.length; playerIndex += 1) {
          next.players[playerIndex]!.triggeredEffectKeysThisTurn = [];
        }
        emit('TURN_ENDED', { playerId: actorPlayerId, turn: state.turn });
        next.activePlayerIndex = (state.activePlayerIndex + 1) % state.players.length;
        if (next.activePlayerIndex === 0) next.turn += 1;
        const nextPlayer = next.players[next.activePlayerIndex]!;
        nextPlayer.excavatedThisTurn = false;
        nextPlayer.heroHasAttacked = false;
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
        next.pendingChoice = null;
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
