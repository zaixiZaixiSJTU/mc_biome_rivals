namespace BiomeRivalsRules {
  export const PROTOCOL_VERSION = 2;
  export const RULESET_VERSION = 'prototype-0.2';

  export type MatchStatus = 'WAITING' | 'ACTIVE' | 'FINISHED';
  export type CommandType = 'DEPLOY_CARD' | 'ENTER_COMBAT' | 'ATTACK' | 'END_TURN' | 'CONCEDE';
  export type EventType = 'CARD_DEPLOYED' | 'PHASE_CHANGED' | 'ATTACK_RESOLVED' | 'OBJECT_DIED' | 'TURN_ENDED' | 'TURN_STARTED' | 'PLAYER_CONCEDED' | 'MATCH_ENDED';
  export type DeploySlotKind = 'UNIT' | 'BUILDING';
  export type TurnPhase = 'MAIN' | 'COMBAT';
  export type AttackTargetType = 'HERO' | 'UNIT' | 'BUILDING';
  export type CardType = 'UNIT' | 'SPELL' | 'BUILDING' | 'STRUCTURE' | 'EQUIPMENT' | 'MATERIAL';

  export interface CardRuleDefinition {
    id: string;
    cardType: CardType;
    cost: number;
    buildingSlots: number;
    attack: number;
    health: number;
  }

  export interface BattlefieldObjectState {
    instanceId: string;
    cardId: string;
    cardType: 'UNIT' | 'BUILDING' | 'STRUCTURE';
    attack: number;
    health: number;
    maxHealth: number;
    slotKind: DeploySlotKind;
    slotIndex: number;
    occupiedSlots: number;
    summonedTurn: number;
    hasAttacked: boolean;
  }

  export interface PlayerState {
    playerId: string;
    life: number;
    armor: number;
    redstone: number;
    redstoneCapacity: number;
    hand: string[];
    unitSlots: Array<string | null>;
    buildingSlots: Array<string | null>;
    battlefield: BattlefieldObjectState[];
  }

  export interface MatchState {
    matchId: string;
    protocolVersion: number;
    rulesetVersion: string;
    revision: number;
    lastEventId: number;
    status: MatchStatus;
    turn: number;
    phase: TurnPhase;
    activePlayerIndex: number;
    nextInstanceId: number;
    players: PlayerState[];
    winnerPlayerId: string | null;
    processedCommandIds: string[];
  }

  export interface PlayerSnapshot {
    playerId: string;
    life: number;
    armor: number;
    redstone: number;
    redstoneCapacity: number;
    hand: Array<string | null>;
    unitSlots: Array<string | null>;
    buildingSlots: Array<string | null>;
    battlefield: BattlefieldObjectState[];
  }

  export interface MatchSnapshot {
    matchId: string;
    viewerPlayerId: string;
    protocolVersion: number;
    rulesetVersion: string;
    revision: number;
    lastEventId: number;
    status: MatchStatus;
    turn: number;
    phase: TurnPhase;
    activePlayerIndex: number;
    nextInstanceId: number;
    players: PlayerSnapshot[];
    winnerPlayerId: string | null;
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
    | 'MATCH_FINISHED'
    | 'UNKNOWN_CARD'
    | 'CARD_NOT_IN_HAND'
    | 'INSUFFICIENT_REDSTONE'
    | 'INVALID_TARGET'
    | 'SLOT_OCCUPIED'
    | 'WRONG_PHASE'
    | 'INVALID_ATTACKER'
    | 'ATTACKER_NOT_READY'
    | 'ATTACK_ALREADY_USED';

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
