const BIOME_RIVALS_COMMAND_OPCODE = 1;
const BIOME_RIVALS_EVENT_BATCH_OPCODE = 2;
const BIOME_RIVALS_REJECTION_OPCODE = 3;
const BIOME_RIVALS_SNAPSHOT_OPCODE = 4;
const BIOME_RIVALS_TEST_FIXTURE_RESULT_OPCODE = 5;
const BIOME_RIVALS_TEST_FIXTURE_OPCODE = 255;
const BIOME_RIVALS_TICK_RATE = 5;

interface BiomeRivalsMatchState extends nkruntime.MatchState {
  presences: { [sessionId: string]: nkruntime.Presence };
  arenaId: BiomeRivalsRules.ArenaId;
  factionByPlayerId: { [playerId: string]: BiomeRivalsRules.FactionId };
  game: BiomeRivalsRules.MatchState | null;
}

function parseRequestedFactions(params: { [key: string]: unknown }): { [playerId: string]: BiomeRivalsRules.FactionId } {
  const result: { [playerId: string]: BiomeRivalsRules.FactionId } = {};
  if (typeof params.playerFactions !== 'string') return result;
  const entries = JSON.parse(params.playerFactions) as Array<{ playerId?: unknown; factionId?: unknown }>;
  if (!Array.isArray(entries) || entries.length !== 2) throw new Error('match requires exactly two faction selections');
  for (let index = 0; index < entries.length; index += 1) {
    const entry = entries[index]!;
    if (typeof entry.playerId !== 'string' || !entry.playerId || !BiomeRivalsRules.isFactionId(entry.factionId) || result[entry.playerId]) {
      throw new Error('match contains an invalid faction selection');
    }
    result[entry.playerId] = entry.factionId;
  }
  return result;
}

function encodeMatchMessage(value: unknown): string {
  return JSON.stringify(value);
}

function isTestFixtureEnabled(ctx: nkruntime.Context): boolean {
  return !!ctx.env && ctx.env.BIOME_RIVALS_ENABLE_TEST_FIXTURES === 'true';
}

// Only an isolated server's audit configuration may override the frozen prototype mode.
// Neither match params nor player faction/preferences can select a production arena.
function resolveAuditArena(ctx: nkruntime.Context): BiomeRivalsRules.ArenaId {
  const requested = isTestFixtureEnabled(ctx) && ctx.env ? ctx.env.BIOME_RIVALS_TEST_ARENA_ID : undefined;
  if (requested === undefined || requested === '') return BiomeRivalsRules.DEFAULT_ARENA_ID;
  if (!BiomeRivalsRules.isArenaId(requested)) throw new Error('unsupported server audit arena');
  return requested;
}

function broadcastAcceptedMatchResult(
  dispatcher: nkruntime.MatchDispatcher,
  state: BiomeRivalsMatchState,
  game: BiomeRivalsRules.MatchState,
  batch: BiomeRivalsRules.MatchEventBatch,
  sender: nkruntime.Presence | null
): void {
  const recipients = Object.keys(state.presences).map(function (sessionId): nkruntime.Presence {
    return state.presences[sessionId]!;
  });
  for (let recipientIndex = 0; recipientIndex < recipients.length; recipientIndex += 1) {
    const recipient = recipients[recipientIndex]!;
    const isPlayer = game.players.some(function (player): boolean { return player.playerId === recipient.userId; });
    if (!isPlayer) continue;
    dispatcher.broadcastMessage(
      BIOME_RIVALS_EVENT_BATCH_OPCODE,
      encodeMatchMessage(BiomeRivalsRules.createClientEventBatch(batch, recipient.userId, game)),
      [recipient],
      sender,
      true
    );
  }
}

function applySimultaneousDefeatTestFixture(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  dispatcher: nkruntime.MatchDispatcher,
  state: BiomeRivalsMatchState,
  sender: nkruntime.Presence,
  fixtureName: unknown
): string | null {
  if (!isTestFixtureEnabled(ctx)) return 'test fixtures are disabled';
  const readable = fixtureName === 'simultaneous-defeat-readable';
  if (fixtureName !== 'simultaneous-defeat' && !readable) return 'unsupported test fixture';
  if (state.game === null || state.game.status !== 'ACTIVE' || state.game.pendingChoice !== null) {
    return 'test fixture requires an active match without a pending choice';
  }
  if (!state.game.players.some(function (player): boolean { return player.playerId === sender.userId; })) {
    return 'test fixture sender is not a match participant';
  }
  if (Object.keys(state.presences).length < 2) return 'test fixture requires both players to be connected';

  const fixtureState = JSON.parse(JSON.stringify(state.game)) as BiomeRivalsRules.MatchState;
  const activePlayer = fixtureState.players[fixtureState.activePlayerIndex]!;
  const inactivePlayer = fixtureState.players[fixtureState.activePlayerIndex === 0 ? 1 : 0]!;
  if (fixtureState.phase !== 'MAIN' || activePlayer.handCards.length === 0) {
    return 'test fixture could not prepare its deterministic command';
  }

  // Route the fixture through the same ordinary PLAY_CARD command path and terminal settlement as
  // the generic lethal rules test. The injected 0-life precondition is only reachable when the
  // explicit test-fixture environment gate is enabled.
  activePlayer.life = 0;
  inactivePlayer.life = 0;
  activePlayer.hand = ['cd_006'];
  activePlayer.handCards = [{
    handCardInstanceId: activePlayer.handCards[0]!.handCardInstanceId,
    cardId: 'cd_006',
    costModifier: 0,
    expiresAtEndOfTurnPlayerId: null
  }];
  activePlayer.redstone = Math.max(2, activePlayer.redstone);
  activePlayer.redstoneCapacity = Math.max(2, activePlayer.redstoneCapacity);
  if (readable) {
    const hand = ['ed_002', 'ed_003', 'ed_004', 'ed_005', 'ed_006', 'ed_007', 'ed_008'];
    fixtureState.players.forEach(function (player): void {
      player.hand = hand.slice();
      player.handCards = hand.map(function (cardId): BiomeRivalsRules.HandCardState {
        return { handCardInstanceId: 'hand-' + String(fixtureState.nextHandCardInstanceId++),
          cardId: cardId, costModifier: 0, expiresAtEndOfTurnPlayerId: null };
      });
    });
    // The existing ordinary CD-006 play consumes one card and draws one, retaining seven.
    activePlayer.hand[0] = 'cd_006';
    activePlayer.handCards[0]!.cardId = 'cd_006';
  }

  const command: BiomeRivalsRules.MatchCommand = {
    protocolVersion: BiomeRivalsRules.PROTOCOL_VERSION,
    rulesetVersion: fixtureState.rulesetVersion,
    commandId: 'test-fixture-simultaneous-defeat-' + String(fixtureState.revision),
    expectedRevision: fixtureState.revision,
    type: 'PLAY_CARD',
    payload: { cardId: 'cd_006', handCardInstanceId: activePlayer.handCards[0]!.handCardInstanceId }
  };
  const result = BiomeRivalsRules.applyCommand(fixtureState, activePlayer.playerId, command);
  if (!result.accepted) {
    logger.warn('Simultaneous defeat test fixture command was rejected: %s', result.message);
    return 'test fixture command was rejected: ' + result.code;
  }
  if (result.state.status !== 'FINISHED' || result.state.winnerPlayerId !== null ||
      !result.batch.events.some(function (event): boolean {
        return event.type === 'MATCH_ENDED' && event.payload.reason === 'SIMULTANEOUS_DEFEAT';
      })) {
    logger.warn('Simultaneous defeat test fixture did not produce the expected terminal result.');
    return 'test fixture did not produce a simultaneous defeat';
  }

  // The fixture injects life/hand preconditions outside normal gameplay. Real clients must receive
  // that private baseline before replaying the ordinary command, otherwise hand counts and the
  // defeated opponent cannot be reconstructed. Publish nothing if validation above failed.
  const recipients = Object.keys(state.presences).map(function (sessionId): nkruntime.Presence {
    return state.presences[sessionId]!;
  });
  for (let index = 0; index < recipients.length; index += 1) {
    const recipient = recipients[index]!;
    if (!fixtureState.players.some(function (player): boolean { return player.playerId === recipient.userId; })) continue;
    dispatcher.broadcastMessage(
      BIOME_RIVALS_SNAPSHOT_OPCODE,
      encodeMatchMessage(BiomeRivalsRules.createClientSnapshot(fixtureState, recipient.userId)),
      [recipient],
      null,
      true
    );
  }
  state.game = result.state;
  broadcastAcceptedMatchResult(dispatcher, state, result.state, result.batch, sender);
  return null;
}

function biomeRivalsMatchInit(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  params: { [key: string]: unknown }
): { state: BiomeRivalsMatchState; tickRate: number; label: string } {
  logger.info('Biome Rivals match created: %s', ctx.matchId || 'pending');
  return {
    state: {
      presences: {}, arenaId: resolveAuditArena(ctx),
      factionByPlayerId: parseRequestedFactions(params), game: null
    },
    tickRate: BIOME_RIVALS_TICK_RATE,
    label: JSON.stringify({ mode: 'prototype', open: true })
  };
}

function biomeRivalsMatchJoinAttempt(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  dispatcher: nkruntime.MatchDispatcher,
  tick: number,
  state: BiomeRivalsMatchState,
  presence: nkruntime.Presence,
  metadata: { [key: string]: unknown }
): { state: BiomeRivalsMatchState; accept: boolean; rejectMessage?: string } {
  const assignedPlayerIds = Object.keys(state.factionByPlayerId);
  if (assignedPlayerIds.length > 0 && !state.factionByPlayerId[presence.userId]) {
    return { state: state, accept: false, rejectMessage: 'player was not assigned to this match' };
  }
  const sessionIds = Object.keys(state.presences);
  const alreadyConnectedAsPlayer = sessionIds.some(function (sessionId): boolean {
    return state.presences[sessionId]!.userId === presence.userId;
  });
  if (!state.presences[presence.sessionId] && !alreadyConnectedAsPlayer && sessionIds.length >= 2) {
    return { state: state, accept: false, rejectMessage: 'match is full' };
  }
  return { state: state, accept: true };
}

function biomeRivalsMatchJoin(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  dispatcher: nkruntime.MatchDispatcher,
  tick: number,
  state: BiomeRivalsMatchState,
  presences: nkruntime.Presence[]
): { state: BiomeRivalsMatchState } {
  for (let i = 0; i < presences.length; i += 1) {
    const presence = presences[i]!;
    const existingSessionIds = Object.keys(state.presences);
    for (let sessionIndex = 0; sessionIndex < existingSessionIds.length; sessionIndex += 1) {
      const existingSessionId = existingSessionIds[sessionIndex]!;
      if (existingSessionId !== presence.sessionId && state.presences[existingSessionId]!.userId === presence.userId) {
        delete state.presences[existingSessionId];
      }
    }
    state.presences[presence.sessionId] = presence;
  }
  const connected = Object.keys(state.presences).map(function (sessionId): nkruntime.Presence {
    return state.presences[sessionId]!;
  });
  let snapshotRecipients = presences;
  if (state.game === null && connected.length === 2) {
    const playerIds = [connected[0]!.userId, connected[1]!.userId];
    const factionIds = playerIds.map(function (playerId, index): BiomeRivalsRules.FactionId {
      return state.factionByPlayerId[playerId] || (index === 0 ? 'plains_forest' : 'nether');
    });
    state.game = BiomeRivalsRules.createInitialState(
      ctx.matchId || 'unknown', playerIds, factionIds, nk.uuidv4(), state.arenaId
    );
    snapshotRecipients = connected;
  }
  if (state.game !== null) {
    for (let index = 0; index < snapshotRecipients.length; index += 1) {
      const recipient = snapshotRecipients[index]!;
      const isPlayer = state.game.players.some(function (player): boolean { return player.playerId === recipient.userId; });
      if (!isPlayer) continue;
      dispatcher.broadcastMessage(
        BIOME_RIVALS_SNAPSHOT_OPCODE,
        encodeMatchMessage(BiomeRivalsRules.createClientSnapshot(state.game, recipient.userId)),
        [recipient],
        null,
        true
      );
    }
  }
  return { state: state };
}

function biomeRivalsMatchLeave(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  dispatcher: nkruntime.MatchDispatcher,
  tick: number,
  state: BiomeRivalsMatchState,
  presences: nkruntime.Presence[]
): { state: BiomeRivalsMatchState } {
  for (let i = 0; i < presences.length; i += 1) delete state.presences[presences[i]!.sessionId];
  return { state: state };
}

function biomeRivalsMatchLoop(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  dispatcher: nkruntime.MatchDispatcher,
  tick: number,
  state: BiomeRivalsMatchState,
  messages: nkruntime.MatchMessage[]
): { state: BiomeRivalsMatchState } {
  if (state.game === null) return { state: state };

  for (let i = 0; i < messages.length; i += 1) {
    const message = messages[i]!;
    if (message.opCode === BIOME_RIVALS_TEST_FIXTURE_OPCODE) {
      let fixtureName: unknown = null;
      try {
        if (isTestFixtureEnabled(ctx)) {
          const request = JSON.parse(nk.binaryToString(message.data)) as { fixture?: unknown };
          fixtureName = request && request.fixture;
        }
        const response = applySimultaneousDefeatTestFixture(
          ctx, logger, dispatcher, state, message.sender, fixtureName
        );
        dispatcher.broadcastMessage(
          response === null ? BIOME_RIVALS_TEST_FIXTURE_RESULT_OPCODE : BIOME_RIVALS_REJECTION_OPCODE,
          encodeMatchMessage(response === null ? {
            ok: true,
            revision: state.game.revision,
            winnerPlayerId: state.game.winnerPlayerId,
            reason: 'SIMULTANEOUS_DEFEAT'
          } : { code: 'TEST_FIXTURE_REJECTED', message: response }),
          [message.sender],
          null,
          true
        );
      } catch (error) {
        logger.warn('Rejected malformed test fixture request from %s: %s', message.sender.userId, String(error));
        dispatcher.broadcastMessage(
          BIOME_RIVALS_REJECTION_OPCODE,
          encodeMatchMessage({ code: 'INVALID_COMMAND', message: 'malformed test fixture request' }),
          [message.sender],
          null,
          true
        );
      }
      continue;
    }
    if (message.opCode !== BIOME_RIVALS_COMMAND_OPCODE) continue;
    try {
      const command = JSON.parse(nk.binaryToString(message.data)) as BiomeRivalsRules.MatchCommand;
      if ((command.type === 'DEPLOY_CARD' || command.type === 'PLAY_CARD') &&
          (!command.payload || typeof command.payload.handCardInstanceId !== 'string' ||
           !/^hand-[0-9]+$/.test(command.payload.handCardInstanceId))) {
        dispatcher.broadcastMessage(
          BIOME_RIVALS_REJECTION_OPCODE,
          encodeMatchMessage({
            commandId: command.commandId,
            code: 'INVALID_COMMAND',
            message: 'DEPLOY_CARD and PLAY_CARD require a valid handCardInstanceId',
            revision: state.game.revision
          }),
          [message.sender],
          null,
          true
        );
        continue;
      }
      const result = BiomeRivalsRules.applyCommand(state.game, message.sender.userId, command);
      if (result.accepted) {
        state.game = result.state;
        broadcastAcceptedMatchResult(dispatcher, state, result.state, result.batch, message.sender);
      } else {
        dispatcher.broadcastMessage(
          BIOME_RIVALS_REJECTION_OPCODE,
          encodeMatchMessage({
            commandId: command.commandId,
            code: result.code,
            message: result.message,
            revision: state.game.revision
          }),
          [message.sender],
          null,
          true
        );
      }
    } catch (error) {
      logger.warn('Rejected malformed command from %s: %s', message.sender.userId, String(error));
      dispatcher.broadcastMessage(
        BIOME_RIVALS_REJECTION_OPCODE,
        encodeMatchMessage({ code: 'INVALID_COMMAND', message: 'malformed JSON command' }),
        [message.sender],
        null,
        true
      );
    }
  }
  return { state: state };
}

function biomeRivalsMatchTerminate(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  dispatcher: nkruntime.MatchDispatcher,
  tick: number,
  state: BiomeRivalsMatchState,
  graceSeconds: number
): { state: BiomeRivalsMatchState } | null {
  logger.info('Biome Rivals match terminating with %s grace seconds', graceSeconds);
  return null;
}

function biomeRivalsMatchSignal(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  dispatcher: nkruntime.MatchDispatcher,
  tick: number,
  state: BiomeRivalsMatchState,
  data: string
): { state: BiomeRivalsMatchState; data: string } {
  return { state: state, data: data };
}
