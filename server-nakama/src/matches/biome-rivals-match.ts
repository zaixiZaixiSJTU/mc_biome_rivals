const BIOME_RIVALS_COMMAND_OPCODE = 1;
const BIOME_RIVALS_EVENT_BATCH_OPCODE = 2;
const BIOME_RIVALS_REJECTION_OPCODE = 3;
const BIOME_RIVALS_SNAPSHOT_OPCODE = 4;
const BIOME_RIVALS_TICK_RATE = 5;

interface BiomeRivalsMatchState extends nkruntime.MatchState {
  presences: { [sessionId: string]: nkruntime.Presence };
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

function biomeRivalsMatchInit(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  params: { [key: string]: unknown }
): { state: BiomeRivalsMatchState; tickRate: number; label: string } {
  logger.info('Biome Rivals match created: %s', ctx.matchId || 'pending');
  return {
    state: { presences: {}, factionByPlayerId: parseRequestedFactions(params), game: null },
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
  const count = Object.keys(state.presences).length;
  if (!state.presences[presence.sessionId] && count >= 2) {
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
    state.game = BiomeRivalsRules.createInitialState(ctx.matchId || 'unknown', playerIds, factionIds);
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
    if (message.opCode !== BIOME_RIVALS_COMMAND_OPCODE) continue;
    try {
      const command = JSON.parse(nk.binaryToString(message.data)) as BiomeRivalsRules.MatchCommand;
      const result = BiomeRivalsRules.applyCommand(state.game, message.sender.userId, command);
      if (result.accepted) {
        state.game = result.state;
        const recipients = Object.keys(state.presences).map(function (sessionId): nkruntime.Presence {
          return state.presences[sessionId]!;
        });
        for (let recipientIndex = 0; recipientIndex < recipients.length; recipientIndex += 1) {
          const recipient = recipients[recipientIndex]!;
          const isPlayer = state.game.players.some(function (player): boolean { return player.playerId === recipient.userId; });
          if (!isPlayer) continue;
          dispatcher.broadcastMessage(
            BIOME_RIVALS_EVENT_BATCH_OPCODE,
            encodeMatchMessage(BiomeRivalsRules.createClientEventBatch(result.batch, recipient.userId)),
            [recipient],
            message.sender,
            true
          );
        }
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
