namespace BiomeRivalsRules {
  export const PROTOCOL_VERSION = 1;
  export const RULESET_VERSION = 'prototype-0.1';

  export type MatchStatus = 'WAITING' | 'ACTIVE' | 'FINISHED';
  export type CommandType = 'END_TURN' | 'CONCEDE';
  export type EventType = 'TURN_ENDED' | 'TURN_STARTED' | 'PLAYER_CONCEDED' | 'MATCH_ENDED';

  export interface PlayerState {
    playerId: string;
    life: number;
    armor: number;
    redstone: number;
    redstoneCapacity: number;
  }

  export interface MatchState {
    matchId: string;
    rulesetVersion: string;
    revision: number;
    lastEventId: number;
    status: MatchStatus;
    turn: number;
    activePlayerIndex: number;
    players: PlayerState[];
    winnerPlayerId: string | null;
    processedCommandIds: string[];
  }

  export interface MatchCommand {
    protocolVersion: number;
    rulesetVersion: string;
    commandId: string;
    expectedRevision: number;
    type: CommandType;
    payload: { [key: string]: unknown };
  }

  export interface MatchEvent {
    eventId: number;
    type: EventType;
    payload: { [key: string]: unknown };
  }

  export interface MatchEventBatch {
    protocolVersion: number;
    rulesetVersion: string;
    revision: number;
    acknowledgedCommandId: string;
    events: MatchEvent[];
  }

  export type RejectionCode =
    | 'INVALID_STATE'
    | 'INVALID_COMMAND'
    | 'PROTOCOL_MISMATCH'
    | 'RULESET_MISMATCH'
    | 'REVISION_MISMATCH'
    | 'DUPLICATE_COMMAND'
    | 'NOT_A_PLAYER'
    | 'NOT_ACTIVE_PLAYER'
    | 'MATCH_FINISHED';

  export interface CommandAccepted {
    accepted: true;
    state: MatchState;
    batch: MatchEventBatch;
  }

  export interface CommandRejected {
    accepted: false;
    state: MatchState;
    code: RejectionCode;
    message: string;
  }

  export type CommandResult = CommandAccepted | CommandRejected;
}
