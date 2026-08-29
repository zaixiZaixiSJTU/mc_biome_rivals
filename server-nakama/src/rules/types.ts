namespace BiomeRivalsRules {
  export const PROTOCOL_VERSION = 14;
  export const RULESET_VERSION = 'prototype-0.16';

  export type MatchStatus = 'WAITING' | 'MULLIGAN' | 'ACTIVE' | 'FINISHED';
  export type CommandType = 'MULLIGAN' | 'DEPLOY_CARD' | 'PLAY_CARD' | 'RESOLVE_CHOICE' | 'ENTER_COMBAT' | 'ATTACK' | 'END_TURN' | 'CONCEDE';
  export type EventType = 'MULLIGAN_COMPLETED' | 'MATCH_STARTED' | 'MATERIALS_CONSUMED' | 'CARD_DEPLOYED' | 'OBJECT_SUMMONED' | 'CARD_PLAYED' | 'CARD_BURIED' | 'CHOICE_OFFERED' | 'CHOICE_RESOLVED' | 'CARD_EXCAVATED' | 'CARD_DRAWN' | 'CARD_BURNED' | 'CARD_GENERATED' | 'FATIGUE_DAMAGE' | 'HERO_DAMAGED' | 'HERO_HEALED' | 'ARMOR_GAINED' | 'OBJECT_STATS_CHANGED' | 'PHASE_CHANGED' | 'ATTACK_RESOLVED' | 'OBJECT_DIED' | 'TURN_ENDED' | 'TURN_STARTED' | 'PLAYER_CONCEDED' | 'MATCH_ENDED';
  export type DeploySlotKind = 'UNIT' | 'BUILDING';
  export type PaymentMethod = 'REDSTONE' | 'CRAFTING';
  export type TurnPhase = 'MAIN' | 'COMBAT';
  export type AttackTargetType = 'HERO' | 'UNIT' | 'BUILDING';
  export type CardType = 'UNIT' | 'SPELL' | 'BUILDING' | 'STRUCTURE' | 'EQUIPMENT' | 'MATERIAL';
  export type CardKeyword = 'TAUNT' | 'CHARGE';
  export type FactionId = 'plains_forest' | 'desert_badlands' | 'snow_ice' | 'cave_dark_forest' | 'ocean_river' | 'nether' | 'end';

  export const FACTION_CARD_PREFIXES: { [factionId: string]: string } = {
    plains_forest: 'pf',
    desert_badlands: 'db',
    snow_ice: 'si',
    cave_dark_forest: 'cd',
    ocean_river: 'or',
    nether: 'nt',
    end: 'ed'
  };

  export function isFactionId(value: unknown): value is FactionId {
    return typeof value === 'string' && Object.prototype.hasOwnProperty.call(FACTION_CARD_PREFIXES, value);
  }

  export interface CardRuleDefinition {
    id: string;
    cardType: CardType;
    cost: number;
    buildingSlots: number;
    attack: number;
    health: number;
    keywords: CardKeyword[];
    hasCraftingRecipe: boolean;
    recipeId: string;
    craftingRecipe: Array<{ cardId: string; count: number }>;
    craftedAttackBonus: number;
    craftedHealthBonus: number;
    craftedDurabilityBonus: number;
    effectImplementationStatus: 'NONE' | 'PENDING' | 'IMPLEMENTED';
    effectIds: string[];
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
    keywords: CardKeyword[];
    temporaryAttackModifier: number;
    temporaryAttackModifierExpiresOnTurn: number;
  }

  export interface PendingChoiceOptionState {
    optionIndex: number;
    cardId: string;
    selectable: boolean;
  }

  export interface PendingChoiceState {
    choiceId: string;
    playerId: string;
    sourceCardId: string;
    sourceInstanceId: string;
    effectId: string;
    kind: 'ARCHAEOLOGY_TOP_3';
    options: PendingChoiceOptionState[];
  }

  export interface PendingChoiceOptionSnapshot {
    optionIndex: number;
    cardId: string | null;
    selectable: boolean;
  }

  export interface PendingChoiceSnapshot {
    choiceId: string;
    playerId: string;
    sourceCardId: string;
    sourceInstanceId: string;
    effectId: string;
    kind: 'ARCHAEOLOGY_TOP_3';
    options: PendingChoiceOptionSnapshot[];
  }

  export interface PlayerState {
    playerId: string;
    factionId: FactionId;
    mulliganCompleted: boolean;
    life: number;
    armor: number;
    redstone: number;
    redstoneCapacity: number;
    hand: string[];
    deck: string[];
    buriedCardIds: string[];
    excavatedThisTurn: boolean;
    discardPile: string[];
    fatigueCount: number;
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
    pendingChoice: PendingChoiceState | null;
    winnerPlayerId: string | null;
    processedCommandIds: string[];
  }

  export interface PlayerSnapshot {
    playerId: string;
    factionId: FactionId;
    mulliganCompleted: boolean;
    life: number;
    armor: number;
    redstone: number;
    redstoneCapacity: number;
    hand: Array<string | null>;
    deckCount: number;
    buriedCount: number;
    excavatedThisTurn: boolean;
    discardPile: string[];
    fatigueCount: number;
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
    pendingChoice: PendingChoiceSnapshot | null;
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
    | 'MULLIGAN_REQUIRED'
    | 'MULLIGAN_ALREADY_COMPLETED'
    | 'TAUNT_TARGET_REQUIRED'
    | 'NOT_ACTIVE_PLAYER'
    | 'MATCH_FINISHED'
    | 'UNKNOWN_CARD'
    | 'CARD_NOT_IN_HAND'
    | 'INSUFFICIENT_REDSTONE'
    | 'INVALID_PAYMENT_METHOD'
    | 'MISSING_MATERIALS'
    | 'CHOICE_REQUIRED'
    | 'INVALID_CHOICE'
    | 'INVALID_TARGET'
    | 'SLOT_OCCUPIED'
    | 'WRONG_PHASE'
    | 'INVALID_ATTACKER'
    | 'ATTACKER_NOT_READY'
    | 'ATTACK_ALREADY_USED'
    | 'EFFECT_NOT_IMPLEMENTED';

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
