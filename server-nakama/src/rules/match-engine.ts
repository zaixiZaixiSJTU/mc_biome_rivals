namespace BiomeRivalsRules {
  function starterHand(playerIndex: number): string[] {
    const prefix = playerIndex === 0 ? 'pf' : 'nt';
    return [prefix + '_001', prefix + '_002', prefix + '_003', prefix + '_004', prefix + '_005'];
  }

  function makePlayer(playerId: string, playerIndex: number): PlayerState {
    return {
      playerId: playerId,
      life: 30,
      armor: 0,
      redstone: 1,
      redstoneCapacity: 1,
      hand: starterHand(playerIndex),
      unitSlots: [null, null, null, null],
      buildingSlots: [null, null, null]
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
      activePlayerIndex: 0,
      players: [makePlayer(playerIds[0], 0), makePlayer(playerIds[1], 1)],
      winnerPlayerId: null,
      processedCommandIds: []
    };
    const violations = validateState(state);
    if (violations.length > 0) throw new Error(violations.join('; '));
    return state;
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
      activePlayerIndex: state.activePlayerIndex,
      players: state.players.map(function (player): PlayerState {
        return {
          playerId: player.playerId,
          life: player.life,
          armor: player.armor,
          redstone: player.redstone,
          redstoneCapacity: player.redstoneCapacity,
          hand: player.hand.slice(),
          unitSlots: player.unitSlots.slice(),
          buildingSlots: player.buildingSlots.slice()
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
      if (definition.cardType === 'UNIT') {
        if (slotKind !== 'UNIT' || slotIndex < 0 || slotIndex >= player.unitSlots.length) {
          return reject(state, 'INVALID_TARGET', 'unit cards require a valid unit slot');
        }
        if (player.unitSlots[slotIndex] !== null) return reject(state, 'SLOT_OCCUPIED', 'unit slot is occupied');
        player.unitSlots[slotIndex] = cardId;
      } else if (definition.cardType === 'BUILDING' || definition.cardType === 'STRUCTURE') {
        occupiedSlots = Math.max(1, definition.buildingSlots);
        if (slotKind !== 'BUILDING' || slotIndex < 0 || slotIndex + occupiedSlots > player.buildingSlots.length) {
          return reject(state, 'INVALID_TARGET', 'building cards require enough consecutive building slots');
        }
        for (let index = slotIndex; index < slotIndex + occupiedSlots; index += 1) {
          if (player.buildingSlots[index] !== null) {
            return reject(state, 'SLOT_OCCUPIED', 'required building slots are occupied');
          }
        }
        for (let index = slotIndex; index < slotIndex + occupiedSlots; index += 1) {
          player.buildingSlots[index] = cardId;
        }
      } else {
        return reject(state, 'INVALID_TARGET', 'card type cannot be deployed to the battlefield');
      }

      player.hand.splice(handIndex, 1);
      player.redstone -= definition.cost;
      emit('CARD_DEPLOYED', {
        playerId: actorPlayerId,
        cardId: cardId,
        slotKind: slotKind,
        slotIndex: slotIndex,
        occupiedSlots: occupiedSlots,
        redstone: player.redstone
      });
      return null;
    }

    switch (command.type) {
      case 'DEPLOY_CARD': {
        const deploymentRejection = deployCard();
        if (deploymentRejection !== null) return deploymentRejection;
        break;
      }
      case 'END_TURN': {
        if (actorIndex !== state.activePlayerIndex) return reject(state, 'NOT_ACTIVE_PLAYER', 'only the active player may end the turn');
        emit('TURN_ENDED', { playerId: actorPlayerId, turn: state.turn });
        next.activePlayerIndex = (state.activePlayerIndex + 1) % state.players.length;
        if (next.activePlayerIndex === 0) next.turn += 1;
        const nextPlayer = next.players[next.activePlayerIndex]!;
        if (next.activePlayerIndex === 0) nextPlayer.redstoneCapacity = Math.min(10, nextPlayer.redstoneCapacity + 1);
        nextPlayer.redstone = nextPlayer.redstoneCapacity;
        emit('TURN_STARTED', {
          playerId: nextPlayer.playerId,
          turn: next.turn,
          activePlayerIndex: next.activePlayerIndex,
          redstone: nextPlayer.redstone,
          redstoneCapacity: nextPlayer.redstoneCapacity
        });
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
