namespace BiomeRivalsRules {
  const HAND_LIMIT = 7;

  function prototypeDeck(prefix: string, seedText: string): string[] {
    const deck: string[] = [];
    for (let index = 0; index < 30; index += 1) deck.push(prefix + '_' + ('00' + String((index % 8) + 1)).slice(-3));
    let seed = 17;
    for (let index = 0; index < seedText.length; index += 1) {
      seed = ((seed * 31) + seedText.charCodeAt(index)) | 0;
    }
    for (let index = deck.length - 1; index > 0; index -= 1) {
      seed = ((seed * 1664525) + 1013904223) | 0;
      const swapIndex = (seed >>> 0) % (index + 1);
      const value = deck[index]!;
      deck[index] = deck[swapIndex]!;
      deck[swapIndex] = value;
    }
    return deck;
  }

  function makePlayer(playerId: string, playerIndex: number, matchId: string): PlayerState {
    const deck = prototypeDeck(playerIndex === 0 ? 'pf' : 'nt', matchId + ':' + playerId);
    const hand: string[] = [];
    const startingCards = playerIndex === 0 ? 3 : 4;
    for (let index = 0; index < startingCards; index += 1) hand.push(deck.pop()!);
    return {
      playerId: playerId,
      life: 30,
      armor: 0,
      redstone: 1,
      redstoneCapacity: 1,
      hand: hand,
      deck: deck,
      discardPile: [],
      fatigueCount: 0,
      unitSlots: [null, null, null, null],
      buildingSlots: [null, null, null],
      battlefield: []
    };
  }

  export function createInitialState(matchId: string, playerIds: string[]): MatchState {
    if (!matchId) throw new Error('matchId is required');
    if (playerIds.length !== 2 || !playerIds[0] || !playerIds[1] || playerIds[0] === playerIds[1]) {
      throw new Error('exactly two unique player ids are required');
    }

    const state: MatchState = {
      matchId: matchId,
      protocolVersion: PROTOCOL_VERSION,
      rulesetVersion: RULESET_VERSION,
      revision: 0,
      lastEventId: 0,
      status: 'ACTIVE',
      turn: 1,
      phase: 'MAIN',
      activePlayerIndex: 0,
      nextInstanceId: 1,
      players: [makePlayer(playerIds[0], 0, matchId), makePlayer(playerIds[1], 1, matchId)],
      winnerPlayerId: null,
      processedCommandIds: []
    };
    state.players[0]!.hand.push(state.players[0]!.deck.pop()!);
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
          life: player.life,
          armor: player.armor,
          redstone: player.redstone,
          redstoneCapacity: player.redstoneCapacity,
          hand: playerIndex === viewerIndex
            ? player.hand.slice()
            : player.hand.map(function (): null { return null; }),
          deckCount: player.deck.length,
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
          life: player.life,
          armor: player.armor,
          redstone: player.redstone,
          redstoneCapacity: player.redstoneCapacity,
          hand: player.hand.slice(),
          deck: player.deck.slice(),
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
    if (command.expectedRevision !== state.revision) return reject(state, 'REVISION_MISMATCH', 'client state is stale');
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
      if (typeof cardId !== 'string' || (slotKind !== 'UNIT' && slotKind !== 'BUILDING') ||
          typeof slotIndex !== 'number' || slotIndex % 1 !== 0) {
        return reject(state, 'INVALID_COMMAND', 'DEPLOY_CARD requires cardId, slotKind and integer slotIndex');
      }
      const definition = getCardDefinition(cardId);
      if (definition === null) return reject(state, 'UNKNOWN_CARD', 'card definition is not registered');
      const player = next.players[actorIndex]!;
      const handIndex = player.hand.indexOf(cardId);
      if (handIndex < 0) return reject(state, 'CARD_NOT_IN_HAND', 'card is not in the actor hand');
      if (definition.cost > player.redstone) return reject(state, 'INSUFFICIENT_REDSTONE', 'not enough redstone');

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

      const instanceId = 'object-' + String(next.nextInstanceId);
      next.nextInstanceId += 1;
      const battlefieldObject: BattlefieldObjectState = {
        instanceId: instanceId,
        cardId: cardId,
        cardType: objectCardType,
        attack: definition.attack,
        health: definition.health,
        maxHealth: definition.health,
        slotKind: slotKind,
        slotIndex: slotIndex,
        occupiedSlots: occupiedSlots,
        summonedTurn: state.turn,
        hasAttacked: false,
        temporaryAttackModifier: 0,
        temporaryAttackModifierExpiresOnTurn: 0
      };
      player.battlefield.push(battlefieldObject);
      const occupiedRow = slotKind === 'UNIT' ? player.unitSlots : player.buildingSlots;
      for (let index = slotIndex; index < slotIndex + occupiedSlots; index += 1) occupiedRow[index] = instanceId;
      player.hand.splice(handIndex, 1);
      player.redstone -= definition.cost;
      emit('CARD_DEPLOYED', {
        playerId: actorPlayerId,
        instanceId: instanceId,
        cardId: cardId,
        cardType: battlefieldObject.cardType,
        slotKind: slotKind,
        slotIndex: slotIndex,
        occupiedSlots: occupiedSlots,
        redstone: player.redstone,
        attack: battlefieldObject.attack,
        health: battlefieldObject.health,
        maxHealth: battlefieldObject.maxHealth,
        summonedTurn: battlefieldObject.summonedTurn,
        nextInstanceId: next.nextInstanceId
      });
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

    function removeDeadObjects(player: PlayerState): void {
      for (let index = player.battlefield.length - 1; index >= 0; index -= 1) {
        const object = player.battlefield[index]!;
        if (object.health > 0) continue;
        const slots = object.slotKind === 'UNIT' ? player.unitSlots : player.buildingSlots;
        for (let slot = 0; slot < slots.length; slot += 1) {
          if (slots[slot] === object.instanceId) slots[slot] = null;
        }
        player.battlefield.splice(index, 1);
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
    }

    function damageHero(player: PlayerState, amount: number): void {
      const armorDamage = Math.min(player.armor, amount);
      player.armor -= armorDamage;
      player.life = Math.max(0, player.life - (amount - armorDamage));
    }

    function drawCard(player: PlayerState): void {
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
      if (effectId !== 'effect.nt_006.01' && effectId !== 'effect.si_001.01' && effectId !== 'effect.tk_005.01' && effectId !== 'effect.tk_016.01') {
        return reject(state, 'EFFECT_NOT_IMPLEMENTED', 'effect handler is not registered');
      }
      const player = next.players[actorIndex]!;
      const opponent = next.players[actorIndex === 0 ? 1 : 0]!;
      let targetedObject: BattlefieldObjectState | null = null;
      if (effectId === 'effect.si_001.01') {
        if (command.payload.targetType !== 'UNIT' || typeof command.payload.targetInstanceId !== 'string') {
          return reject(state, 'INVALID_TARGET', 'snowball requires an enemy unit target');
        }
        targetedObject = findObject(opponent, command.payload.targetInstanceId);
        if (targetedObject === null || targetedObject.cardType !== 'UNIT') {
          return reject(state, 'INVALID_TARGET', 'snowball target must be a living enemy unit');
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
          if (targetedObject === null) throw new Error('validated snowball target was not resolved');
          const attackBefore = targetedObject.attack;
          targetedObject.attack = Math.max(0, targetedObject.attack - 1);
          targetedObject.temporaryAttackModifier += targetedObject.attack - attackBefore;
          if (targetedObject.temporaryAttackModifier !== 0) targetedObject.temporaryAttackModifierExpiresOnTurn = state.turn;
          emit('OBJECT_STATS_CHANGED', {
            playerId: opponent.playerId,
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
      if (attacker.summonedTurn === state.turn) return reject(state, 'ATTACKER_NOT_READY', 'unit cannot attack on its summoned turn');

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
        removeDeadObjects(attackerPlayer);
        removeDeadObjects(defenderPlayer);
      }

      if (defenderPlayer.life <= 0) {
        next.status = 'FINISHED';
        next.winnerPlayerId = attackerPlayer.playerId;
        emit('MATCH_ENDED', { winnerPlayerId: next.winnerPlayerId, reason: 'HERO_DEFEATED' });
      }
      return null;
    }

    switch (command.type) {
      case 'DEPLOY_CARD': {
        const deploymentRejection = deployCard();
        if (deploymentRejection !== null) return deploymentRejection;
        break;
      }
      case 'PLAY_CARD': {
        const playRejection = playCard();
        if (playRejection !== null) return playRejection;
        break;
      }
      case 'ENTER_COMBAT': {
        const phaseRejection = enterCombat();
        if (phaseRejection !== null) return phaseRejection;
        break;
      }
      case 'ATTACK': {
        const attackRejection = attack();
        if (attackRejection !== null) return attackRejection;
        break;
      }
      case 'END_TURN': {
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
