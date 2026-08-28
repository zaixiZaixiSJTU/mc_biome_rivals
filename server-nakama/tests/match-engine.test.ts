function command(id: string, revision: number, type: BiomeRivalsRules.CommandType): BiomeRivalsRules.MatchCommand {
  return {
    protocolVersion: BiomeRivalsRules.PROTOCOL_VERSION,
    rulesetVersion: BiomeRivalsRules.RULESET_VERSION,
    commandId: id,
    expectedRevision: revision,
    type: type,
    payload: {}
  };
}

function deployCommand(
  id: string,
  revision: number,
  cardId: string,
  slotKind: BiomeRivalsRules.DeploySlotKind,
  slotIndex: number,
  paymentMethod: BiomeRivalsRules.PaymentMethod = 'REDSTONE'
): BiomeRivalsRules.MatchCommand {
  const result = command(id, revision, 'DEPLOY_CARD');
  result.payload = { cardId: cardId, slotKind: slotKind, slotIndex: slotIndex, paymentMethod: paymentMethod };
  return result;
}

function enterCombatCommand(id: string, revision: number): BiomeRivalsRules.MatchCommand {
  return command(id, revision, 'ENTER_COMBAT');
}

function playCommand(id: string, revision: number, cardId: string, targetType?: BiomeRivalsRules.AttackTargetType, targetInstanceId?: string): BiomeRivalsRules.MatchCommand {
  const result = command(id, revision, 'PLAY_CARD');
  result.payload = { cardId: cardId };
  if (targetType !== undefined) result.payload.targetType = targetType;
  if (targetInstanceId !== undefined) result.payload.targetInstanceId = targetInstanceId;
  return result;
}

function attackCommand(
  id: string,
  revision: number,
  attackerInstanceId: string,
  targetType: BiomeRivalsRules.AttackTargetType,
  targetInstanceId?: string
): BiomeRivalsRules.MatchCommand {
  const result = command(id, revision, 'ATTACK');
  result.payload = {
    attackerInstanceId: attackerInstanceId,
    targetType: targetType,
    targetInstanceId: targetInstanceId
  };
  return result;
}

function mulliganCommand(id: string, revision: number, cardIndices: number[]): BiomeRivalsRules.MatchCommand {
  const result = command(id, revision, 'MULLIGAN');
  result.payload = { cardIndices: cardIndices };
  return result;
}

function activeState(
  matchId: string,
  playerIds: string[],
  factionIds?: BiomeRivalsRules.FactionId[]
): BiomeRivalsRules.MatchState {
  const state = BiomeRivalsRules.createInitialState(matchId, playerIds, factionIds);
  state.players[0]!.mulliganCompleted = true;
  state.players[1]!.mulliganCompleted = true;
  state.status = 'ACTIVE';
  state.players[state.activePlayerIndex]!.hand.push(state.players[state.activePlayerIndex]!.deck.pop()!);
  return state;
}

function placeUnit(
  state: BiomeRivalsRules.MatchState,
  playerIndex: number,
  cardId: string,
  slotIndex: number,
  instanceId: string,
  summonedTurn: number
): void {
  const definition = BiomeRivalsRules.getCardDefinition(cardId)!;
  const player = state.players[playerIndex]!;
  player.unitSlots[slotIndex] = instanceId;
  player.battlefield.push({
    instanceId: instanceId,
    cardId: cardId,
    cardType: 'UNIT',
    attack: definition.attack,
    health: definition.health,
    maxHealth: definition.health,
    slotKind: 'UNIT',
    slotIndex: slotIndex,
    occupiedSlots: 1,
    summonedTurn: summonedTurn,
    hasAttacked: false,
    keywords: definition.keywords.slice(),
    temporaryAttackModifier: 0,
    temporaryAttackModifierExpiresOnTurn: 0
  });
}

function placeBuilding(
  state: BiomeRivalsRules.MatchState,
  playerIndex: number,
  cardId: string,
  slotIndex: number,
  instanceId: string,
  health?: number
): void {
  const definition = BiomeRivalsRules.getCardDefinition(cardId)!;
  const player = state.players[playerIndex]!;
  const occupiedSlots = Math.max(1, definition.buildingSlots);
  for (let index = slotIndex; index < slotIndex + occupiedSlots; index += 1) player.buildingSlots[index] = instanceId;
  player.battlefield.push({
    instanceId: instanceId,
    cardId: cardId,
    cardType: definition.cardType === 'STRUCTURE' ? 'STRUCTURE' : 'BUILDING',
    attack: definition.attack,
    health: health === undefined ? definition.health : health,
    maxHealth: definition.health,
    slotKind: 'BUILDING',
    slotIndex: slotIndex,
    occupiedSlots: occupiedSlots,
    summonedTurn: state.turn,
    hasAttacked: false,
    keywords: definition.keywords.slice(),
    temporaryAttackModifier: 0,
    temporaryAttackModifierExpiresOnTurn: 0
  });
}

TestHarness.test('creates a valid two-player initial state', function (): void {
  const state = BiomeRivalsRules.createInitialState('match-1', ['alice', 'bob']);
  TestHarness.equal(state.revision, 0);
  TestHarness.equal(state.protocolVersion, BiomeRivalsRules.PROTOCOL_VERSION);
  TestHarness.equal(state.status, 'MULLIGAN');
  TestHarness.equal(state.players[0]!.life, 30);
  TestHarness.equal(state.players[0]!.redstone, 1);
  TestHarness.equal(state.players[0]!.hand.length, 3);
  TestHarness.equal(state.players[1]!.hand.length, 4);
  TestHarness.equal(state.players[0]!.deck.length, 27);
  TestHarness.equal(state.players[1]!.deck.length, 26);
  TestHarness.equal(state.players[0]!.mulliganCompleted, false);
  TestHarness.equal(state.players[1]!.mulliganCompleted, false);
  TestHarness.equal(state.players[0]!.unitSlots.length, 4);
  TestHarness.equal(state.players[0]!.buildingSlots.length, 3);
  TestHarness.equal(BiomeRivalsRules.validateState(state).length, 0);
});

TestHarness.test('creates faction-specific decks and exposes both public faction ids', function (): void {
  const state = BiomeRivalsRules.createInitialState('match-factions', ['alice', 'bob'], ['ocean_river', 'end']);
  TestHarness.equal(state.players[0]!.factionId, 'ocean_river');
  TestHarness.equal(state.players[1]!.factionId, 'end');
  TestHarness.ok(state.players[0]!.hand.concat(state.players[0]!.deck).every(function (cardId): boolean { return cardId.indexOf('or_') === 0; }));
  TestHarness.ok(state.players[1]!.hand.concat(state.players[1]!.deck).every(function (cardId): boolean { return cardId.indexOf('ed_') === 0; }));
  const snapshot = BiomeRivalsRules.createClientSnapshot(state, 'alice');
  TestHarness.equal(snapshot.players[0]!.factionId, 'ocean_river');
  TestHarness.equal(snapshot.players[1]!.factionId, 'end');
  TestHarness.equal(snapshot.players[0]!.mulliganCompleted, false);
  TestHarness.equal(snapshot.players[1]!.hand[0], null);
});

TestHarness.test('replaces selected opening cards before returning them to the shuffled deck', function (): void {
  const state = BiomeRivalsRules.createInitialState('match-mulligan', ['alice', 'bob']);
  const originalHand = state.players[0]!.hand.slice();
  const result = BiomeRivalsRules.applyCommand(state, 'alice', mulliganCommand('mulligan-a', 0, [0, 2]));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.status, 'MULLIGAN');
  TestHarness.equal(result.state.players[0]!.mulliganCompleted, true);
  TestHarness.equal(result.state.players[0]!.hand.length, 3);
  TestHarness.equal(result.state.players[0]!.hand[0], originalHand[1]);
  TestHarness.equal(result.state.players[0]!.deck.length, 27);
  TestHarness.ok(result.state.players[0]!.deck.indexOf(originalHand[0]!) >= 0);
  TestHarness.ok(result.state.players[0]!.deck.indexOf(originalHand[2]!) >= 0);
  TestHarness.equal(result.batch.events[0]!.type, 'MULLIGAN_COMPLETED');
  TestHarness.equal(result.batch.events[0]!.payload.replacedCount, 2);
  const opponentBatch = BiomeRivalsRules.createClientEventBatch(result.batch, 'bob');
  TestHarness.equal((opponentBatch.events[0]!.payload.hand as Array<string | null>)[0], null);
});

TestHarness.test('starts the first turn and draws only after both players confirm', function (): void {
  const state = BiomeRivalsRules.createInitialState('match-start', ['alice', 'bob']);
  TestHarness.equal(state.activePlayerIndex, 0);
  TestHarness.equal(state.players[0]!.playerId, 'bob', 'recorded match seed assigns one input player to the canonical first-player slot');
  TestHarness.equal(state.players[0]!.hand.length, 3);
  TestHarness.equal(state.players[1]!.hand.length, 4);
  const first = BiomeRivalsRules.applyCommand(state, 'alice', mulliganCommand('mulligan-a', 0, []));
  TestHarness.equal(first.accepted, true);
  if (!first.accepted) return;
  const second = BiomeRivalsRules.applyCommand(first.state, 'bob', mulliganCommand('mulligan-b', 0, [1]));
  TestHarness.equal(second.accepted, true, 'independent mulligans may cross on the same client revision');
  if (!second.accepted) return;
  TestHarness.equal(second.state.status, 'ACTIVE');
  TestHarness.equal(second.state.players[0]!.hand.length, 4);
  TestHarness.equal(second.state.players[1]!.hand.length, 4);
  TestHarness.equal(second.state.players[0]!.deck.length, 26);
  TestHarness.equal(second.batch.events[0]!.type, 'MULLIGAN_COMPLETED');
  TestHarness.equal(second.batch.events[1]!.type, 'MATCH_STARTED');
  TestHarness.equal(second.batch.events[2]!.type, 'CARD_DRAWN');
  const nextTurn = BiomeRivalsRules.applyCommand(second.state, 'bob', command('first-player-end', 2, 'END_TURN'));
  TestHarness.equal(nextTurn.accepted, true);
  if (!nextTurn.accepted) return;
  TestHarness.equal(nextTurn.state.turn, 1, 'the second player still takes a first-round turn');
  TestHarness.equal(nextTurn.state.players[1]!.redstoneCapacity, 1, 'the second player does not gain round-two energy early');
});

TestHarness.test('rejects gameplay and invalid or repeated selections during opening hands', function (): void {
  const state = BiomeRivalsRules.createInitialState('match-opening-guards', ['alice', 'bob']);
  const deploy = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('too-early', 0, state.players[0]!.hand[0]!, 'UNIT', 0));
  TestHarness.equal(deploy.accepted, false);
  if (!deploy.accepted) TestHarness.equal(deploy.code, 'MULLIGAN_REQUIRED');
  const invalid = BiomeRivalsRules.applyCommand(state, 'alice', mulliganCommand('bad-selection', 0, [0, 0]));
  TestHarness.equal(invalid.accepted, false);
  const first = BiomeRivalsRules.applyCommand(state, 'alice', mulliganCommand('confirm-once', 0, []));
  TestHarness.equal(first.accepted, true);
  if (!first.accepted) return;
  const repeated = BiomeRivalsRules.applyCommand(first.state, 'alice', mulliganCommand('confirm-twice', 1, []));
  TestHarness.equal(repeated.accepted, false);
  if (!repeated.accepted) TestHarness.equal(repeated.code, 'MULLIGAN_ALREADY_COMPLETED');
});

TestHarness.test('rejects unsupported initial faction selections', function (): void {
  let rejected = false;
  try {
    BiomeRivalsRules.createInitialState('match-factions', ['alice', 'bob'], ['plains_forest', 'invalid'] as BiomeRivalsRules.FactionId[]);
  } catch (error) {
    rejected = String(error).indexOf('supported faction ids') >= 0;
  }
  TestHarness.ok(rejected);
});

TestHarness.test('redacts the opponents hand without mutating authoritative state', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  const snapshot = BiomeRivalsRules.createClientSnapshot(state, 'alice');
  TestHarness.equal(snapshot.viewerPlayerId, 'alice');
  TestHarness.equal(snapshot.players[0]!.hand[0], state.players[0]!.hand[0]);
  TestHarness.equal(snapshot.players[1]!.hand.length, state.players[1]!.hand.length);
  TestHarness.equal(snapshot.players[1]!.hand[0], null);
  TestHarness.equal(snapshot.players[1]!.deckCount, 26);
  TestHarness.ok(state.players[1]!.hand[0] !== null);
  TestHarness.equal(JSON.stringify(snapshot).indexOf('"deck":'), -1);
  TestHarness.equal(JSON.stringify(snapshot).indexOf('processedCommandIds'), -1);
});

TestHarness.test('deploys a registered unit and emits replayable state data', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['tk_003'];
  const result = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-1', 0, 'tk_003', 'UNIT', 2));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.unitSlots[2], 'object-1');
  TestHarness.equal(result.state.players[0]!.battlefield[0]!.cardId, 'tk_003');
  TestHarness.equal(result.state.players[0]!.battlefield[0]!.health, 2);
  TestHarness.equal(result.state.players[0]!.hand.indexOf('tk_003'), -1);
  TestHarness.equal(result.state.players[0]!.redstone, 0);
  TestHarness.equal(result.batch.events.length, 1);
  TestHarness.equal(result.batch.events[0]!.type, 'CARD_DEPLOYED');
  TestHarness.equal(result.batch.events[0]!.payload.slotIndex, 2);
  TestHarness.equal(result.batch.events[0]!.payload.instanceId, 'object-1');
  TestHarness.equal(result.batch.events[0]!.payload.redstone, 0);
  TestHarness.equal(state.players[0]!.unitSlots[2], null, 'accepted commands must not mutate their input state');
});

TestHarness.test('resolves the bee battlecry after deployment', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['pf_001'];
  state.players[0]!.life = 28;
  const result = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-bee', 0, 'pf_001', 'UNIT', 0));
  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.life, 29);
  TestHarness.equal(result.batch.events.length, 2);
  TestHarness.equal(result.batch.events[0]!.type, 'CARD_DEPLOYED');
  TestHarness.equal(result.batch.events[1]!.type, 'HERO_HEALED');
  TestHarness.equal(result.batch.events[1]!.payload.effectId, 'effect.pf_001.01');
  TestHarness.equal(result.batch.events[1]!.payload.healing, 1);
});

TestHarness.test('deploys a structure only across consecutive free building slots', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['db_007'];
  state.players[0]!.redstone = 6;
  state.players[0]!.redstoneCapacity = 6;
  const invalid = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-1', 0, 'db_007', 'BUILDING', 2));
  TestHarness.equal(invalid.accepted, false);
  if (!invalid.accepted) TestHarness.equal(invalid.code, 'INVALID_TARGET');

  const accepted = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-2', 0, 'db_007', 'BUILDING', 1));
  TestHarness.equal(accepted.accepted, true);
  if (!accepted.accepted) return;
  TestHarness.equal(accepted.state.players[0]!.buildingSlots[1], 'object-1');
  TestHarness.equal(accepted.state.players[0]!.buildingSlots[2], 'object-1');
  TestHarness.equal(accepted.state.players[0]!.battlefield[0]!.occupiedSlots, 2);
  TestHarness.equal(accepted.batch.events[0]!.payload.occupiedSlots, 2);
});

TestHarness.test('crafts a structure from deterministic hand materials without spending redstone', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['db_002', 'db_007', 'tk_006', 'db_002'];
  state.players[0]!.redstone = 0;
  state.players[0]!.redstoneCapacity = 1;

  const result = BiomeRivalsRules.applyCommand(
    state,
    'alice',
    deployCommand('craft-temple', 0, 'db_007', 'BUILDING', 1, 'CRAFTING')
  );

  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.redstone, 0);
  TestHarness.equal(JSON.stringify(result.state.players[0]!.hand), JSON.stringify(['db_002']));
  TestHarness.equal(JSON.stringify(result.state.players[0]!.discardPile), JSON.stringify(['db_002', 'tk_006']));
  TestHarness.equal(result.state.players[0]!.battlefield[0]!.health, 10);
  TestHarness.equal(result.state.players[0]!.battlefield[0]!.maxHealth, 10);
  TestHarness.equal(result.batch.events.length, 2);
  TestHarness.equal(result.batch.events[0]!.type, 'MATERIALS_CONSUMED');
  TestHarness.equal(result.batch.events[0]!.payload.handCount, 2, 'the product remains in hand during material consumption');
  TestHarness.equal(result.batch.events[0]!.payload.discardCount, 2);
  TestHarness.equal(result.batch.events[1]!.type, 'CARD_DEPLOYED');
  TestHarness.equal(result.batch.events[1]!.payload.paymentMethod, 'CRAFTING');
  TestHarness.equal(result.batch.events[1]!.payload.health, 10);
  const opponentBatch = BiomeRivalsRules.createClientEventBatch(result.batch, 'bob');
  TestHarness.equal((opponentBatch.events[0]!.payload.materials as Array<{ cardId: string }>)[0]!.cardId, 'db_002');
  TestHarness.equal((opponentBatch.events[0]!.payload.materials as Array<{ cardId: string }>)[1]!.cardId, 'tk_006');
  TestHarness.equal(JSON.stringify(state.players[0]!.hand), JSON.stringify(['db_002', 'db_007', 'tk_006', 'db_002']), 'accepted commands must not mutate input');
});

TestHarness.test('rejects incomplete or illegal crafting atomically', function (): void {
  const missingState = activeState('match-1', ['alice', 'bob']);
  missingState.players[0]!.hand = ['db_007', 'tk_006'];
  missingState.players[0]!.redstone = 0;
  const missing = BiomeRivalsRules.applyCommand(
    missingState,
    'alice',
    deployCommand('craft-missing', 0, 'db_007', 'BUILDING', 0, 'CRAFTING')
  );
  TestHarness.equal(missing.accepted, false);
  if (!missing.accepted) TestHarness.equal(missing.code, 'MISSING_MATERIALS');
  TestHarness.equal(JSON.stringify(missingState.players[0]!.hand), JSON.stringify(['db_007', 'tk_006']));
  TestHarness.equal(missingState.players[0]!.discardPile.length, 0);

  const illegalState = activeState('match-1', ['alice', 'bob']);
  illegalState.players[0]!.hand = ['db_007', 'db_002', 'tk_006'];
  illegalState.players[0]!.redstone = 0;
  const illegal = BiomeRivalsRules.applyCommand(
    illegalState,
    'alice',
    deployCommand('craft-illegal-slot', 0, 'db_007', 'BUILDING', 2, 'CRAFTING')
  );
  TestHarness.equal(illegal.accepted, false);
  if (!illegal.accepted) TestHarness.equal(illegal.code, 'INVALID_TARGET');
  TestHarness.equal(JSON.stringify(illegalState.players[0]!.hand), JSON.stringify(['db_007', 'db_002', 'tk_006']));
  TestHarness.equal(illegalState.players[0]!.discardPile.length, 0);
});

TestHarness.test('rejects crafting payment for a card without a recipe', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['pf_001'];
  const result = BiomeRivalsRules.applyCommand(
    state,
    'alice',
    deployCommand('craft-bee', 0, 'pf_001', 'UNIT', 0, 'CRAFTING')
  );
  TestHarness.equal(result.accepted, false);
  if (!result.accepted) TestHarness.equal(result.code, 'INVALID_PAYMENT_METHOD');
});

TestHarness.test('rejects a structure whose declared range overlaps an occupied building slot', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['db_007'];
  state.players[0]!.redstone = 6;
  state.players[0]!.redstoneCapacity = 6;
  placeBuilding(state, 0, 'db_004', 1, 'object-1');
  state.nextInstanceId = 2;

  const result = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-overlap', 0, 'db_007', 'BUILDING', 0));

  TestHarness.equal(result.accepted, false);
  if (!result.accepted) TestHarness.equal(result.code, 'SLOT_OCCUPIED');
  TestHarness.equal(state.players[0]!.hand[0], 'db_007');
  TestHarness.equal(state.players[0]!.redstone, 6);
  TestHarness.equal(state.players[0]!.buildingSlots[0], null);
  TestHarness.equal(state.players[0]!.buildingSlots[1], 'object-1');
});

TestHarness.test('damages a three-slot structure once and releases its complete range on death', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  placeUnit(state, 0, 'pf_003', 0, 'object-1', 1);
  placeBuilding(state, 1, 'ed_008', 0, 'object-2', 3);
  state.players[0]!.battlefield[0]!.attack = 4;
  state.nextInstanceId = 3;

  const result = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('attack-structure', 0, 'object-1', 'BUILDING', 'object-2'));

  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.battlefield[0]!.health, 2, 'structures do not retaliate');
  TestHarness.equal(result.state.players[1]!.battlefield.length, 0);
  TestHarness.equal(result.state.players[1]!.buildingSlots[0], null);
  TestHarness.equal(result.state.players[1]!.buildingSlots[1], null);
  TestHarness.equal(result.state.players[1]!.buildingSlots[2], null);
  TestHarness.equal(result.state.players[1]!.discardPile.filter(function (cardId): boolean { return cardId === 'ed_008'; }).length, 1);
  TestHarness.equal(result.batch.events[0]!.type, 'ATTACK_RESOLVED');
  TestHarness.equal(result.batch.events[0]!.payload.damageToAttacker, 0);
  TestHarness.equal(result.batch.events[1]!.type, 'OBJECT_DIED');
  TestHarness.equal(result.batch.events[1]!.payload.instanceId, 'object-2');
  TestHarness.equal(result.batch.events[1]!.payload.occupiedSlots, 3);
  TestHarness.equal(BiomeRivalsRules.validateState(result.state).length, 0);
});

TestHarness.test('plays implemented armor material through its stable effect id', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['tk_016'];
  state.players[0]!.redstone = 1;
  const result = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('play-armor', 0, 'tk_016'));
  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.armor, 2);
  TestHarness.equal(result.state.players[0]!.redstone, 0);
  TestHarness.equal(result.state.players[0]!.hand.length, 0);
  TestHarness.equal(result.state.players[0]!.discardPile[0], 'tk_016');
  TestHarness.equal(result.batch.events[0]!.type, 'CARD_PLAYED');
  TestHarness.equal(result.batch.events[0]!.payload.effectId, 'effect.tk_016.01');
  TestHarness.equal(result.batch.events[1]!.type, 'ARMOR_GAINED');
  TestHarness.equal(state.players[0]!.armor, 0);
});

TestHarness.test('suspicious sand buries a pottery sherd and grants immediate armor', function (): void {
  const state = activeState('match-1', ['alice', 'bob'], ['desert_badlands', 'nether']);
  state.players[0]!.hand = ['db_002'];
  state.players[0]!.redstone = 1;
  const deckCount = state.players[0]!.deck.length;

  const result = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('bury-sherd', 0, 'db_002'));

  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.armor, 1);
  TestHarness.equal(result.state.players[0]!.deck.length, deckCount + 1);
  TestHarness.equal(JSON.stringify(result.state.players[0]!.buriedCardIds), JSON.stringify(['tk_006']));
  TestHarness.equal(result.state.players[0]!.deck.filter(function (cardId): boolean { return cardId === 'tk_006'; }).length, 1);
  TestHarness.equal(result.batch.events[0]!.type, 'CARD_PLAYED');
  TestHarness.equal(result.batch.events[1]!.type, 'CARD_BURIED');
  TestHarness.equal(result.batch.events[1]!.payload.deckCount, deckCount + 1);
  TestHarness.equal(result.batch.events[1]!.payload.buriedCount, 1);
  TestHarness.equal(result.batch.events[2]!.type, 'ARMOR_GAINED');
  TestHarness.equal(BiomeRivalsRules.createClientSnapshot(result.state, 'bob').players[0]!.buriedCount, 1);
  TestHarness.equal(state.players[0]!.buriedCardIds.length, 0, 'accepted commands must not mutate input');
});

TestHarness.test('excavates a public pottery sherd before the normal turn draw', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[1]!.deck = ['nt_001', 'tk_006'];
  state.players[1]!.buriedCardIds = ['tk_006'];
  const handCount = state.players[1]!.hand.length;

  const result = BiomeRivalsRules.applyCommand(state, 'alice', command('turn-excavate', 0, 'END_TURN'));

  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(JSON.stringify(result.state.players[1]!.hand.slice(-2)), JSON.stringify(['tk_006', 'nt_001']));
  TestHarness.equal(result.state.players[1]!.hand.length, handCount + 2);
  TestHarness.equal(result.state.players[1]!.deck.length, 0);
  TestHarness.equal(result.state.players[1]!.buriedCardIds.length, 0);
  TestHarness.equal(result.state.players[1]!.armor, 1);
  TestHarness.equal(result.batch.events[2]!.type, 'CARD_EXCAVATED');
  TestHarness.equal(result.batch.events[2]!.payload.destination, 'HAND');
  TestHarness.equal(result.batch.events[3]!.type, 'ARMOR_GAINED');
  TestHarness.equal(result.batch.events[4]!.type, 'CARD_DRAWN');
  const opponentProjection = BiomeRivalsRules.createClientEventBatch(result.batch, 'alice');
  TestHarness.equal(opponentProjection.events[2]!.payload.cardId, 'tk_006', 'excavated cards are public');
  TestHarness.equal(opponentProjection.events[4]!.payload.cardId, null, 'the following normal draw remains private');
});

TestHarness.test('excavation and its following draw both enter discard when the hand is full', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[1]!.hand = ['nt_001', 'nt_002', 'nt_003', 'nt_004', 'nt_005', 'nt_006', 'nt_007'];
  state.players[1]!.deck = ['nt_008', 'tk_006'];
  state.players[1]!.buriedCardIds = ['tk_006'];

  const result = BiomeRivalsRules.applyCommand(state, 'alice', command('turn-excavate-full', 0, 'END_TURN'));

  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[1]!.hand.length, 7);
  TestHarness.equal(JSON.stringify(result.state.players[1]!.discardPile), JSON.stringify(['tk_006', 'nt_008']));
  TestHarness.equal(result.batch.events[2]!.type, 'CARD_EXCAVATED');
  TestHarness.equal(result.batch.events[2]!.payload.destination, 'DISCARD');
  TestHarness.equal(result.batch.events[4]!.type, 'CARD_BURNED');
  TestHarness.equal(result.state.players[1]!.armor, 1);
});

TestHarness.test('resolves healing before rotten flesh true damage', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['tk_005'];
  state.players[0]!.life = 25;
  const result = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('play-flesh', 0, 'tk_005'));
  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.life, 26);
  TestHarness.equal(result.batch.events[1]!.type, 'HERO_HEALED');
  TestHarness.equal(result.batch.events[1]!.payload.life, 27);
  TestHarness.equal(result.batch.events[2]!.type, 'HERO_DAMAGED');
  TestHarness.equal(result.batch.events[2]!.payload.damageType, 'TRUE');
});

TestHarness.test('resolves lava sacrifice self damage then a private draw', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['nt_006'];
  state.players[0]!.deck = ['nt_001'];
  const result = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('play-sacrifice', 0, 'nt_006'));
  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.life, 28);
  TestHarness.equal(result.state.players[0]!.hand[0], 'nt_001');
  TestHarness.equal(result.state.players[0]!.discardPile[0], 'nt_006');
  TestHarness.equal(result.batch.events[1]!.type, 'HERO_DAMAGED');
  TestHarness.equal(result.batch.events[2]!.type, 'CARD_DRAWN');
  const opponentProjection = BiomeRivalsRules.createClientEventBatch(result.batch, 'bob');
  TestHarness.equal(opponentProjection.events[2]!.payload.cardId, null);
});

TestHarness.test('applies a targeted snowball debuff and restores it when the caster turn ends', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['si_001'];
  placeUnit(state, 1, 'nt_003', 1, 'object-1', 1);
  const played = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('play-snowball', 0, 'si_001', 'UNIT', 'object-1'));
  TestHarness.ok(played.accepted);
  if (!played.accepted) return;
  const target = played.state.players[1]!.battlefield[0]!;
  TestHarness.equal(target.attack, 2);
  TestHarness.equal(target.temporaryAttackModifier, -1);
  TestHarness.equal(target.temporaryAttackModifierExpiresOnTurn, 1);
  TestHarness.equal(played.batch.events[1]!.type, 'OBJECT_STATS_CHANGED');
  TestHarness.equal(played.batch.events[1]!.payload.instanceId, 'object-1');

  const ended = BiomeRivalsRules.applyCommand(played.state, 'alice', command('end-after-snowball', 1, 'END_TURN'));
  TestHarness.ok(ended.accepted);
  if (!ended.accepted) return;
  const restored = ended.state.players[1]!.battlefield[0]!;
  TestHarness.equal(restored.attack, 3);
  TestHarness.equal(restored.temporaryAttackModifier, 0);
  TestHarness.equal(ended.batch.events[0]!.type, 'OBJECT_STATS_CHANGED');
  TestHarness.equal(ended.batch.events[0]!.payload.reason, 'TEMPORARY_EXPIRED');
  TestHarness.equal(ended.batch.events[1]!.type, 'TURN_ENDED');
});

TestHarness.test('sandstorm damages every unit and removes deaths in event order', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['db_006'];
  state.players[0]!.redstone = 3;
  state.players[0]!.redstoneCapacity = 3;
  placeUnit(state, 0, 'pf_001', 0, 'object-1', 1);
  placeUnit(state, 1, 'nt_003', 1, 'object-2', 1);
  state.nextInstanceId = 3;

  const result = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('play-sandstorm', 0, 'db_006'));
  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.battlefield.length, 0);
  TestHarness.equal(result.state.players[0]!.unitSlots[0], null);
  TestHarness.equal(result.state.players[1]!.battlefield[0]!.health, 1);
  TestHarness.equal(result.batch.events[0]!.type, 'CARD_PLAYED');
  TestHarness.equal(result.batch.events[1]!.type, 'OBJECT_STATS_CHANGED');
  TestHarness.equal(result.batch.events[1]!.payload.health, 0);
  TestHarness.equal(result.batch.events[2]!.type, 'OBJECT_STATS_CHANGED');
  TestHarness.equal(result.batch.events[3]!.type, 'OBJECT_DIED');
});

TestHarness.test('bone buffs a friendly unit for the current turn only', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['tk_009'];
  placeUnit(state, 0, 'pf_001', 0, 'object-1', 1);
  state.nextInstanceId = 2;

  const played = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('play-bone', 0, 'tk_009', 'UNIT', 'object-1'));
  TestHarness.ok(played.accepted);
  if (!played.accepted) return;
  TestHarness.equal(played.state.players[0]!.battlefield[0]!.attack, 2);
  TestHarness.equal(played.state.players[0]!.battlefield[0]!.temporaryAttackModifier, 1);
  TestHarness.equal(played.batch.events[1]!.payload.playerId, 'alice');

  const ended = BiomeRivalsRules.applyCommand(played.state, 'alice', command('end-bone', 1, 'END_TURN'));
  TestHarness.ok(ended.accepted);
  if (!ended.accepted) return;
  TestHarness.equal(ended.state.players[0]!.battlefield[0]!.attack, 1);
  TestHarness.equal(ended.state.players[0]!.battlefield[0]!.temporaryAttackModifier, 0);
});

TestHarness.test('cobblestone heals only a friendly building or structure', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['tk_010'];
  placeBuilding(state, 0, 'db_004', 0, 'object-1', 1);
  placeUnit(state, 0, 'pf_001', 0, 'object-2', 1);
  state.nextInstanceId = 3;

  const invalid = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('bad-cobble', 0, 'tk_010', 'BUILDING', 'object-2'));
  TestHarness.equal(invalid.accepted, false);
  if (!invalid.accepted) TestHarness.equal(invalid.code, 'INVALID_TARGET');
  TestHarness.equal(state.players[0]!.hand[0], 'tk_010');

  const played = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('play-cobble', 0, 'tk_010', 'BUILDING', 'object-1'));
  TestHarness.ok(played.accepted);
  if (!played.accepted) return;
  TestHarness.equal(played.state.players[0]!.battlefield[0]!.health, 3);
  TestHarness.equal(played.batch.events[1]!.type, 'OBJECT_STATS_CHANGED');
  TestHarness.equal(played.batch.events[1]!.payload.reason, 'HEAL');
});

TestHarness.test('rejects a snowball without a living enemy unit target before payment', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['si_001'];
  const result = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('play-snowball', 0, 'si_001'));
  TestHarness.equal(result.accepted, false);
  if (!result.accepted) TestHarness.equal(result.code, 'INVALID_TARGET');
  TestHarness.equal(state.players[0]!.hand[0], 'si_001');
  TestHarness.equal(state.players[0]!.discardPile.length, 0);
});

TestHarness.test('rejects pending card effects without paying or discarding', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['pf_006'];
  state.players[0]!.redstone = 6;
  state.players[0]!.redstoneCapacity = 6;
  const result = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('play-pending', 0, 'pf_006'));
  TestHarness.equal(result.accepted, false);
  if (!result.accepted) TestHarness.equal(result.code, 'EFFECT_NOT_IMPLEMENTED');
  TestHarness.equal(state.players[0]!.redstone, 6);
  TestHarness.equal(state.players[0]!.hand[0], 'pf_006');
  TestHarness.equal(state.players[0]!.discardPile.length, 0);
});

TestHarness.test('enters combat and blocks main phase deployments', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  const entered = BiomeRivalsRules.applyCommand(state, 'alice', enterCombatCommand('phase-1', 0));
  TestHarness.equal(entered.accepted, true);
  if (!entered.accepted) return;
  TestHarness.equal(entered.state.phase, 'COMBAT');
  TestHarness.equal(entered.batch.events[0]!.type, 'PHASE_CHANGED');
  const deploy = BiomeRivalsRules.applyCommand(entered.state, 'alice', deployCommand('deploy-1', 1, 'pf_001', 'UNIT', 0));
  TestHarness.equal(deploy.accepted, false);
  if (!deploy.accepted) TestHarness.equal(deploy.code, 'WRONG_PHASE');
});

TestHarness.test('resolves simultaneous unit combat and releases dead object slots', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  state.nextInstanceId = 3;
  placeUnit(state, 0, 'pf_003', 1, 'object-1', 1);
  placeUnit(state, 1, 'pf_001', 2, 'object-2', 1);

  const result = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('attack-1', 0, 'object-1', 'UNIT', 'object-2'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.battlefield[0]!.health, 1);
  TestHarness.equal(result.state.players[0]!.battlefield[0]!.hasAttacked, true);
  TestHarness.equal(result.state.players[1]!.battlefield.length, 0);
  TestHarness.equal(result.state.players[1]!.discardPile[0], 'pf_001');
  TestHarness.equal(result.state.players[1]!.unitSlots[2], null);
  TestHarness.equal(result.batch.events[0]!.type, 'ATTACK_RESOLVED');
  TestHarness.equal(result.batch.events[1]!.type, 'OBJECT_DIED');
});

TestHarness.test('rejects summoning sickness and duplicate attacks', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  state.nextInstanceId = 2;
  placeUnit(state, 0, 'pf_001', 0, 'object-1', 2);
  const notReady = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('attack-1', 0, 'object-1', 'HERO'));
  TestHarness.equal(notReady.accepted, false);
  if (!notReady.accepted) TestHarness.equal(notReady.code, 'ATTACKER_NOT_READY');

  state.players[0]!.battlefield[0]!.summonedTurn = 1;
  state.players[0]!.battlefield[0]!.hasAttacked = true;
  const used = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('attack-2', 0, 'object-1', 'HERO'));
  TestHarness.equal(used.accepted, false);
  if (!used.accepted) TestHarness.equal(used.code, 'ATTACK_ALREADY_USED');
});

TestHarness.test('allows a unit with CHARGE to attack on its summoned turn', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  state.nextInstanceId = 2;
  placeUnit(state, 0, 'pf_001', 0, 'object-1', 2);
  state.players[0]!.battlefield[0]!.keywords.push('CHARGE');

  const result = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('charge-attack', 0, 'object-1', 'HERO'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.battlefield[0]!.hasAttacked, true);
  TestHarness.equal(result.state.players[1]!.life, 29);
});

TestHarness.test('requires attacking one of multiple living TAUNT targets first', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  state.nextInstanceId = 5;
  placeUnit(state, 0, 'pf_003', 0, 'object-1', 1);
  placeUnit(state, 1, 'pf_008', 0, 'object-2', 1);
  placeUnit(state, 1, 'pf_001', 1, 'object-3', 1);
  placeUnit(state, 1, 'or_005', 2, 'object-4', 1);

  const hero = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('taunt-hero', 0, 'object-1', 'HERO'));
  TestHarness.equal(hero.accepted, false);
  if (!hero.accepted) TestHarness.equal(hero.code, 'TAUNT_TARGET_REQUIRED');

  const nonTaunt = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('taunt-bypass', 0, 'object-1', 'UNIT', 'object-3'));
  TestHarness.equal(nonTaunt.accepted, false);
  if (!nonTaunt.accepted) TestHarness.equal(nonTaunt.code, 'TAUNT_TARGET_REQUIRED');
  TestHarness.equal(state.players[0]!.battlefield[0]!.hasAttacked, false);

  const taunt = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('taunt-legal', 0, 'object-1', 'UNIT', 'object-4'));
  TestHarness.equal(taunt.accepted, true);
  if (!taunt.accepted) return;
  TestHarness.equal(taunt.batch.events[0]!.type, 'ATTACK_RESOLVED');
  TestHarness.equal(taunt.state.players[1]!.battlefield.some(function (object): boolean { return object.instanceId === 'object-3'; }), true);
});

TestHarness.test('publishes registered battlefield keywords on deployment', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['pf_008'];
  state.players[0]!.redstone = 6;
  state.players[0]!.redstoneCapacity = 6;
  const result = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-taunt', 0, 'pf_008', 'UNIT', 0));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.battlefield[0]!.keywords[0], 'TAUNT');
  TestHarness.equal((result.batch.events[0]!.payload.keywords as string[])[0], 'TAUNT');
});

TestHarness.test('resolves the Shulker deathrattle into its owners private hand', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  state.nextInstanceId = 3;
  state.players[1]!.hand = [];
  placeUnit(state, 0, 'pf_003', 0, 'object-1', 1);
  placeUnit(state, 1, 'ed_004', 0, 'object-2', 1);
  state.players[1]!.battlefield[0]!.health = 2;

  const result = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('shulker-death', 0, 'object-1', 'UNIT', 'object-2'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[1]!.hand[0], 'tk_016');
  TestHarness.equal(result.state.players[1]!.discardPile[0], 'ed_004');
  const generatedIndex = result.batch.events.findIndex(function (event): boolean { return event.type === 'CARD_GENERATED'; });
  TestHarness.ok(generatedIndex > 0);
  TestHarness.equal(result.batch.events[generatedIndex - 1]!.type, 'OBJECT_DIED');
  TestHarness.equal(result.batch.events[generatedIndex]!.payload.destination, 'HAND');
  TestHarness.equal(result.batch.events[generatedIndex]!.payload.cardId, 'tk_016');

  const attackerProjection = BiomeRivalsRules.createClientEventBatch(result.batch, 'alice');
  const ownerProjection = BiomeRivalsRules.createClientEventBatch(result.batch, 'bob');
  TestHarness.equal(attackerProjection.events[generatedIndex]!.payload.cardId, null);
  TestHarness.equal(ownerProjection.events[generatedIndex]!.payload.cardId, 'tk_016');
});

TestHarness.test('sends a generated deathrattle card to discard when the hand is full', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  state.nextInstanceId = 3;
  state.players[1]!.hand = ['ed_001', 'ed_001', 'ed_001', 'ed_001', 'ed_001', 'ed_001', 'ed_001'];
  placeUnit(state, 0, 'pf_003', 0, 'object-1', 1);
  placeUnit(state, 1, 'ed_004', 0, 'object-2', 1);
  state.players[1]!.battlefield[0]!.health = 2;

  const result = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('shulker-full-hand', 0, 'object-1', 'UNIT', 'object-2'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[1]!.hand.length, 7);
  TestHarness.equal(result.state.players[1]!.discardPile[1], 'tk_016');
  const generatedIndex = result.batch.events.findIndex(function (event): boolean { return event.type === 'CARD_GENERATED'; });
  TestHarness.ok(generatedIndex > 0);
  TestHarness.equal(result.batch.events[generatedIndex]!.payload.destination, 'DISCARD');
  TestHarness.equal(result.batch.events[generatedIndex]!.payload.cardId, 'tk_016');
  const opponentProjection = BiomeRivalsRules.createClientEventBatch(result.batch, 'alice');
  TestHarness.equal(opponentProjection.events[generatedIndex]!.payload.cardId, 'tk_016');
});

TestHarness.test('resolves simultaneous Shulker deathrattles for the active player first', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['db_006'];
  state.players[0]!.redstone = 3;
  state.players[0]!.redstoneCapacity = 3;
  state.players[1]!.hand = [];
  state.nextInstanceId = 3;
  placeUnit(state, 0, 'ed_004', 0, 'object-1', 1);
  placeUnit(state, 1, 'ed_004', 0, 'object-2', 1);
  state.players[0]!.battlefield[0]!.health = 2;
  state.players[1]!.battlefield[0]!.health = 2;

  const result = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('double-shulker', 0, 'db_006'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  const generated = result.batch.events.filter(function (event): boolean { return event.type === 'CARD_GENERATED'; });
  TestHarness.equal(generated.length, 2);
  TestHarness.equal(generated[0]!.payload.playerId, 'alice');
  TestHarness.equal(generated[1]!.payload.playerId, 'bob');
  TestHarness.equal(result.state.players[0]!.hand[0], 'tk_016');
  TestHarness.equal(result.state.players[1]!.hand[0], 'tk_016');
});

TestHarness.test('summons a Small Magma Cube into the Magma Cubes released slot', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  placeUnit(state, 0, 'pf_008', 0, 'object-1', 1);
  placeUnit(state, 0, 'pf_008', 1, 'object-2', 1);
  placeUnit(state, 0, 'nt_001', 2, 'object-3', 1);
  placeUnit(state, 0, 'pf_008', 3, 'object-4', 1);
  placeUnit(state, 1, 'pf_008', 0, 'object-5', 1);
  state.nextInstanceId = 6;

  const result = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('magma-death', 0, 'object-3', 'UNIT', 'object-5'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  const summoned = result.state.players[0]!.battlefield.filter(function (object): boolean { return object.cardId === 'tk_014'; })[0]!;
  TestHarness.equal(result.state.players[0]!.discardPile[0], 'nt_001');
  TestHarness.equal(result.state.players[0]!.unitSlots[2], 'object-6');
  TestHarness.equal(summoned.instanceId, 'object-6');
  TestHarness.equal(summoned.slotIndex, 2);
  TestHarness.equal(summoned.attack, 1);
  TestHarness.equal(summoned.health, 1);
  TestHarness.equal(summoned.summonedTurn, 2);
  TestHarness.equal(result.state.nextInstanceId, 7);
  TestHarness.equal(result.batch.events[1]!.type, 'OBJECT_DIED');
  TestHarness.equal(result.batch.events[2]!.type, 'OBJECT_SUMMONED');
  TestHarness.equal(result.batch.events[2]!.payload.sourceInstanceId, 'object-3');
  TestHarness.equal(result.batch.events[2]!.payload.instanceId, 'object-6');
  TestHarness.equal(result.batch.events[2]!.payload.slotIndex, 2);

  const immediateAttack = BiomeRivalsRules.applyCommand(result.state, 'alice', attackCommand('small-magma-attack', 1, 'object-6', 'HERO'));
  TestHarness.equal(immediateAttack.accepted, false);
  if (!immediateAttack.accepted) TestHarness.equal(immediateAttack.code, 'ATTACKER_NOT_READY');
});

TestHarness.test('removes every simultaneous death before active-player summon deathrattles resolve', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['db_006'];
  state.players[0]!.redstone = 3;
  state.players[0]!.redstoneCapacity = 3;
  placeUnit(state, 0, 'nt_001', 1, 'object-1', 1);
  placeUnit(state, 1, 'nt_001', 3, 'object-2', 1);
  state.nextInstanceId = 3;

  const result = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('double-magma', 0, 'db_006'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  const orderedTypes = result.batch.events.map(function (event): string { return event.type; });
  TestHarness.equal(orderedTypes.join(','), 'CARD_PLAYED,OBJECT_STATS_CHANGED,OBJECT_STATS_CHANGED,OBJECT_DIED,OBJECT_DIED,OBJECT_SUMMONED,OBJECT_SUMMONED');
  TestHarness.equal(result.batch.events[3]!.payload.playerId, 'alice');
  TestHarness.equal(result.batch.events[4]!.payload.playerId, 'bob');
  TestHarness.equal(result.batch.events[5]!.payload.playerId, 'alice');
  TestHarness.equal(result.batch.events[6]!.payload.playerId, 'bob');
  TestHarness.equal(result.state.players[0]!.unitSlots[1], 'object-3');
  TestHarness.equal(result.state.players[1]!.unitSlots[3], 'object-4');
});

TestHarness.test('applies armor before life and ends the match on lethal hero damage', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  state.nextInstanceId = 2;
  state.players[1]!.life = 2;
  state.players[1]!.armor = 1;
  placeUnit(state, 0, 'pf_003', 0, 'object-1', 1);

  const result = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('attack-1', 0, 'object-1', 'HERO'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[1]!.armor, 0);
  TestHarness.equal(result.state.players[1]!.life, 0);
  TestHarness.equal(result.state.status, 'FINISHED');
  TestHarness.equal(result.state.winnerPlayerId, 'alice');
  TestHarness.equal(result.batch.events[1]!.type, 'MATCH_ENDED');
});

TestHarness.test('rejects invalid deploys without spending redstone or moving cards', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['pf_001'];
  const wrongRow = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-1', 0, 'pf_001', 'BUILDING', 0));
  TestHarness.equal(wrongRow.accepted, false);
  if (!wrongRow.accepted) TestHarness.equal(wrongRow.code, 'INVALID_TARGET');
  TestHarness.equal(state.players[0]!.redstone, 1);
  TestHarness.ok(state.players[0]!.hand.indexOf('pf_001') >= 0);

  state.players[0]!.redstone = 0;
  const unaffordable = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-2', 0, 'pf_001', 'UNIT', 0));
  TestHarness.equal(unaffordable.accepted, false);
  if (!unaffordable.accepted) TestHarness.equal(unaffordable.code, 'INSUFFICIENT_REDSTONE');
});

TestHarness.test('rejects a command from the inactive player without mutation', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  const result = BiomeRivalsRules.applyCommand(state, 'bob', command('cmd-1', 0, 'END_TURN'));
  TestHarness.equal(result.accepted, false);
  if (!result.accepted) TestHarness.equal(result.code, 'NOT_ACTIVE_PLAYER');
  TestHarness.equal(state.revision, 0);
});

TestHarness.test('ends a turn and emits an ordered event batch', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  const result = BiomeRivalsRules.applyCommand(state, 'alice', command('cmd-1', 0, 'END_TURN'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.revision, 1);
  TestHarness.equal(result.state.activePlayerIndex, 1);
  TestHarness.equal(result.batch.events.length, 3);
  TestHarness.equal(result.batch.events[0]!.type, 'TURN_ENDED');
  TestHarness.equal(result.batch.events[1]!.type, 'TURN_STARTED');
  TestHarness.equal(result.batch.events[1]!.eventId, result.batch.events[0]!.eventId + 1);
  TestHarness.equal(result.state.players[1]!.redstoneCapacity, 1);
  TestHarness.equal(result.state.players[1]!.redstone, 1);
  TestHarness.equal(result.batch.events[1]!.payload.activePlayerIndex, 1);
  TestHarness.equal(result.batch.events[1]!.payload.redstoneCapacity, 1);
  TestHarness.equal(result.batch.events[2]!.type, 'CARD_DRAWN');
  TestHarness.equal(result.state.players[1]!.hand.length, 5);
  TestHarness.equal(result.state.players[1]!.deck.length, 25);
});

TestHarness.test('burns a public card when drawing with a full hand', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[1]!.hand = ['nt_001', 'nt_002', 'nt_003', 'nt_004', 'nt_005', 'nt_006', 'nt_007'];
  state.players[1]!.deck = ['nt_008'];
  const result = BiomeRivalsRules.applyCommand(state, 'alice', command('turn-burn', 0, 'END_TURN'));
  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[1]!.hand.length, 7);
  TestHarness.equal(result.state.players[1]!.deck.length, 0);
  TestHarness.equal(result.state.players[1]!.discardPile[0], 'nt_008');
  TestHarness.equal(result.batch.events[2]!.type, 'CARD_BURNED');
  const opponentProjection = BiomeRivalsRules.createClientEventBatch(result.batch, 'alice');
  TestHarness.equal(opponentProjection.events[2]!.payload.cardId, 'nt_008', 'burned cards are public');
});

TestHarness.test('applies escalating true fatigue damage and ends the match on lethal', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[1]!.deck = [];
  state.players[1]!.fatigueCount = 2;
  state.players[1]!.life = 3;
  state.players[1]!.armor = 5;
  const result = BiomeRivalsRules.applyCommand(state, 'alice', command('turn-fatigue', 0, 'END_TURN'));
  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[1]!.life, 0);
  TestHarness.equal(result.state.players[1]!.armor, 5);
  TestHarness.equal(result.state.players[1]!.fatigueCount, 3);
  TestHarness.equal(result.batch.events[2]!.payload.damage, 3);
  TestHarness.equal(result.batch.events[2]!.type, 'FATIGUE_DAMAGE');
  TestHarness.equal(result.batch.events[3]!.type, 'MATCH_ENDED');
  TestHarness.equal(result.state.winnerPlayerId, 'alice');
});

TestHarness.test('redacts drawn card identity only for the opponent event projection', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[1]!.deck = ['nt_008'];
  const result = BiomeRivalsRules.applyCommand(state, 'alice', command('turn-draw', 0, 'END_TURN'));
  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  const bobBatch = BiomeRivalsRules.createClientEventBatch(result.batch, 'bob');
  const aliceBatch = BiomeRivalsRules.createClientEventBatch(result.batch, 'alice');
  TestHarness.equal(bobBatch.events[2]!.payload.cardId, 'nt_008');
  TestHarness.equal(aliceBatch.events[2]!.payload.cardId, null);
  TestHarness.equal(result.batch.events[2]!.payload.cardId, 'nt_008');
});

TestHarness.test('increases each players redstone from their second personal turn', function (): void {
  const initial = activeState('match-1', ['alice', 'bob']);
  const bobFirst = BiomeRivalsRules.applyCommand(initial, 'alice', command('turn-1', 0, 'END_TURN'));
  TestHarness.ok(bobFirst.accepted);
  if (!bobFirst.accepted) return;
  const aliceSecond = BiomeRivalsRules.applyCommand(bobFirst.state, 'bob', command('turn-2', 1, 'END_TURN'));
  TestHarness.ok(aliceSecond.accepted);
  if (!aliceSecond.accepted) return;
  TestHarness.equal(aliceSecond.state.turn, 2);
  TestHarness.equal(aliceSecond.state.players[0]!.redstoneCapacity, 2);
  const bobSecond = BiomeRivalsRules.applyCommand(aliceSecond.state, 'alice', command('turn-3', 2, 'END_TURN'));
  TestHarness.ok(bobSecond.accepted);
  if (!bobSecond.accepted) return;
  TestHarness.equal(bobSecond.state.players[1]!.redstoneCapacity, 2);
});

TestHarness.test('rejects stale revisions', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  const result = BiomeRivalsRules.applyCommand(state, 'alice', command('cmd-1', 99, 'END_TURN'));
  TestHarness.equal(result.accepted, false);
  if (!result.accepted) TestHarness.equal(result.code, 'REVISION_MISMATCH');
});

TestHarness.test('records a concession and winner', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  const result = BiomeRivalsRules.applyCommand(state, 'alice', command('cmd-1', 0, 'CONCEDE'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.status, 'FINISHED');
  TestHarness.equal(result.state.winnerPlayerId, 'bob');
  TestHarness.equal(result.batch.events[1]!.type, 'MATCH_ENDED');
});

TestHarness.test('rejects duplicate command ids after acceptance', function (): void {
  const initial = activeState('match-1', ['alice', 'bob']);
  const first = BiomeRivalsRules.applyCommand(initial, 'alice', command('cmd-1', 0, 'END_TURN'));
  TestHarness.ok(first.accepted);
  if (!first.accepted) return;
  const duplicate = BiomeRivalsRules.applyCommand(first.state, 'bob', command('cmd-1', 1, 'END_TURN'));
  TestHarness.equal(duplicate.accepted, false);
  if (!duplicate.accepted) TestHarness.equal(duplicate.code, 'DUPLICATE_COMMAND');
});
