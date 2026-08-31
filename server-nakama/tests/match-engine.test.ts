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
  paymentMethod: BiomeRivalsRules.PaymentMethod = 'REDSTONE',
  targetType?: BiomeRivalsRules.AttackTargetType,
  targetInstanceId?: string
): BiomeRivalsRules.MatchCommand {
  const result = command(id, revision, 'DEPLOY_CARD');
  result.payload = { cardId: cardId, slotKind: slotKind, slotIndex: slotIndex, paymentMethod: paymentMethod };
  if (targetType !== undefined) result.payload.targetType = targetType;
  if (targetInstanceId !== undefined) result.payload.targetInstanceId = targetInstanceId;
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

function resolveChoiceCommand(id: string, revision: number, choiceId: string, selectedOptionIndex: number): BiomeRivalsRules.MatchCommand {
  const result = command(id, revision, 'RESOLVE_CHOICE');
  result.payload = { choiceId: choiceId, selectedOptionIndex: selectedOptionIndex };
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
    adjacencyHealthModifier: 0,
    slotKind: 'UNIT',
    slotIndex: slotIndex,
    occupiedSlots: 1,
    summonedTurn: summonedTurn,
    hasAttacked: false,
    keywords: definition.keywords.slice(),
    temporaryAttackModifier: 0,
    temporaryAttackModifierExpiresOnTurn: 0,
    statuses: []
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
    adjacencyHealthModifier: 0,
    slotKind: 'BUILDING',
    slotIndex: slotIndex,
    occupiedSlots: occupiedSlots,
    summonedTurn: state.turn,
    hasAttacked: false,
    keywords: definition.keywords.slice(),
    temporaryAttackModifier: 0,
    temporaryAttackModifierExpiresOnTurn: 0,
    statuses: []
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

TestHarness.test('Villager Farmer generates private Wheat after deployment', function (): void {
  const state = activeState('match-farmer-wheat', ['alice', 'bob'], ['plains_forest', 'nether']);
  const actorIndex = state.players[0]!.playerId === 'alice' ? 0 : 1;
  const opponentIndex = actorIndex === 0 ? 1 : 0;
  const actor = state.players[actorIndex]!;
  state.activePlayerIndex = actorIndex;
  actor.hand = ['pf_004'];
  actor.redstone = 3;
  actor.redstoneCapacity = 3;

  const result = BiomeRivalsRules.applyCommand(state, actor.playerId,
    deployCommand('farmer-wheat', 0, 'pf_004', 'UNIT', 1));
  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[actorIndex]!.hand.join(','), 'tk_002');
  TestHarness.equal(result.batch.events.length, 2);
  TestHarness.equal(result.batch.events[0]!.type, 'CARD_DEPLOYED');
  TestHarness.equal(result.batch.events[1]!.type, 'CARD_GENERATED');
  TestHarness.equal(result.batch.events[1]!.payload.sourceCardId, 'pf_004');
  TestHarness.equal(result.batch.events[1]!.payload.sourceInstanceId, result.batch.events[0]!.payload.instanceId);
  TestHarness.equal(result.batch.events[1]!.payload.effectId, 'effect.pf_004.01');
  TestHarness.equal(result.batch.events[1]!.payload.cardId, 'tk_002');
  TestHarness.equal(result.batch.events[1]!.payload.destination, 'HAND');
  const ownerEvents = BiomeRivalsRules.createClientEventBatch(result.batch, actor.playerId);
  const opponentEvents = BiomeRivalsRules.createClientEventBatch(
    result.batch, result.state.players[opponentIndex]!.playerId);
  TestHarness.equal(ownerEvents.events[1]!.payload.cardId, 'tk_002');
  TestHarness.equal(opponentEvents.events[1]!.payload.cardId, null);
});

TestHarness.test('Villager Farmer replaces itself with Wheat at the seven-card hand limit', function (): void {
  const state = activeState('match-farmer-hand-limit', ['alice', 'bob'], ['plains_forest', 'nether']);
  const actorIndex = state.players[0]!.playerId === 'alice' ? 0 : 1;
  const actor = state.players[actorIndex]!;
  state.activePlayerIndex = actorIndex;
  actor.hand = ['pf_004', 'tk_003', 'tk_003', 'tk_003', 'tk_003', 'tk_003', 'tk_003'];
  actor.redstone = 3;
  actor.redstoneCapacity = 3;

  const result = BiomeRivalsRules.applyCommand(state, actor.playerId,
    deployCommand('farmer-hand-limit', 0, 'pf_004', 'UNIT', 0));
  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[actorIndex]!.hand.length, 7);
  TestHarness.equal(result.state.players[actorIndex]!.hand.filter(function (cardId): boolean {
    return cardId === 'tk_002';
  }).length, 1);
  TestHarness.equal(result.state.players[actorIndex]!.discardPile.length, 0);
  TestHarness.equal(result.batch.events[1]!.payload.handCount, 7);
  TestHarness.equal(result.batch.events[1]!.payload.destination, 'HAND');
});

TestHarness.test('offers the archaeologists top-three choice privately after deployment', function (): void {
  const state = activeState('match-1', ['alice', 'bob'], ['desert_badlands', 'nether']);
  state.players[0]!.hand = ['db_003'];
  state.players[0]!.deck = ['db_001', 'db_002', 'tk_006', 'db_004'];
  state.players[0]!.buriedCardIds = ['tk_006'];
  state.players[0]!.redstone = 2;
  state.players[0]!.redstoneCapacity = 2;

  const result = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-archaeologist', 0, 'db_003', 'UNIT', 0));

  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(result.batch.events.length, 2);
  TestHarness.equal(result.batch.events[0]!.type, 'CARD_DEPLOYED');
  TestHarness.equal(result.batch.events[1]!.type, 'CHOICE_OFFERED');
  TestHarness.equal(result.state.pendingChoice!.kind, 'ARCHAEOLOGY_TOP_3');
  TestHarness.equal(result.state.pendingChoice!.options.length, 3);
  TestHarness.equal(JSON.stringify(result.state.pendingChoice!.options), JSON.stringify([
    { optionIndex: 0, cardId: 'db_004', slotIndex: -1, selectable: false },
    { optionIndex: 1, cardId: 'tk_006', slotIndex: -1, selectable: true },
    { optionIndex: 2, cardId: 'db_002', slotIndex: -1, selectable: false }
  ]));
  const ownerSnapshot = BiomeRivalsRules.createClientSnapshot(result.state, 'alice');
  const opponentSnapshot = BiomeRivalsRules.createClientSnapshot(result.state, 'bob');
  TestHarness.equal(ownerSnapshot.pendingChoice!.options[1]!.cardId, 'tk_006');
  TestHarness.equal(ownerSnapshot.pendingChoice!.options[1]!.selectable, true);
  TestHarness.equal(opponentSnapshot.pendingChoice!.options[1]!.cardId, null);
  TestHarness.equal(opponentSnapshot.pendingChoice!.options[1]!.selectable, false);
  const opponentBatch = BiomeRivalsRules.createClientEventBatch(result.batch, 'bob');
  const hiddenOptions = opponentBatch.events[1]!.payload.options as BiomeRivalsRules.PendingChoiceOptionSnapshot[];
  TestHarness.equal(hiddenOptions[1]!.cardId, null);
  TestHarness.equal(hiddenOptions[1]!.selectable, false);
  TestHarness.equal(state.pendingChoice, null, 'accepted commands must not mutate input');
});

TestHarness.test('blocks other actions until the archaeology choice resolves', function (): void {
  const state = activeState('match-1', ['alice', 'bob'], ['desert_badlands', 'nether']);
  state.players[0]!.hand = ['db_003'];
  state.players[0]!.deck = ['db_001', 'tk_006'];
  state.players[0]!.buriedCardIds = ['tk_006'];
  state.players[0]!.redstone = 2;
  state.players[0]!.redstoneCapacity = 2;
  const deployed = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-choice-lock', 0, 'db_003', 'UNIT', 0));
  TestHarness.ok(deployed.accepted);
  if (!deployed.accepted) return;

  const blocked = BiomeRivalsRules.applyCommand(deployed.state, 'alice', command('blocked-end-turn', 1, 'END_TURN'));

  TestHarness.equal(blocked.accepted, false);
  if (!blocked.accepted) TestHarness.equal(blocked.code, 'CHOICE_REQUIRED');
  TestHarness.ok(deployed.state.pendingChoice !== null);
  TestHarness.equal(deployed.state.revision, 1);
});

TestHarness.test('resolves an archaeology choice into excavation and a normal draw', function (): void {
  const state = activeState('match-1', ['alice', 'bob'], ['desert_badlands', 'nether']);
  state.players[0]!.hand = ['db_003'];
  state.players[0]!.deck = ['db_001', 'db_002', 'tk_006', 'db_004'];
  state.players[0]!.buriedCardIds = ['tk_006'];
  state.players[0]!.redstone = 2;
  state.players[0]!.redstoneCapacity = 2;
  const deployed = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-choice', 0, 'db_003', 'UNIT', 0));
  TestHarness.ok(deployed.accepted);
  if (!deployed.accepted) return;
  const choiceId = deployed.state.pendingChoice!.choiceId;

  const invalid = BiomeRivalsRules.applyCommand(deployed.state, 'alice', resolveChoiceCommand('invalid-choice', 1, choiceId, 0));
  TestHarness.equal(invalid.accepted, false);
  if (!invalid.accepted) TestHarness.equal(invalid.code, 'INVALID_CHOICE');
  TestHarness.ok(deployed.state.pendingChoice !== null, 'rejected choices must be atomic');

  const result = BiomeRivalsRules.applyCommand(deployed.state, 'alice', resolveChoiceCommand('resolve-choice', 1, choiceId, 1));
  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(result.state.pendingChoice, null);
  TestHarness.equal(JSON.stringify(result.state.players[0]!.hand), JSON.stringify(['tk_006', 'db_004']));
  TestHarness.equal(JSON.stringify(result.state.players[0]!.deck), JSON.stringify(['db_001', 'db_002']));
  TestHarness.equal(result.state.players[0]!.buriedCardIds.length, 0);
  TestHarness.equal(result.state.players[0]!.excavatedThisTurn, true);
  TestHarness.equal(result.state.players[0]!.armor, 1);
  TestHarness.equal(result.batch.events.length, 4);
  TestHarness.equal(result.batch.events[0]!.type, 'CHOICE_RESOLVED');
  TestHarness.equal(result.batch.events[1]!.type, 'CARD_EXCAVATED');
  TestHarness.equal(result.batch.events[2]!.type, 'ARMOR_GAINED');
  TestHarness.equal(result.batch.events[3]!.type, 'CARD_DRAWN');
  TestHarness.equal(BiomeRivalsRules.validateState(result.state).length, 0);
});

TestHarness.test('discounts the Badlands Raider only after an excavation in the active turn', function (): void {
  const withoutExcavation = activeState('match-1', ['alice', 'bob'], ['desert_badlands', 'nether']);
  withoutExcavation.players[0]!.hand = ['db_005'];
  withoutExcavation.players[0]!.redstone = 2;
  withoutExcavation.players[0]!.redstoneCapacity = 2;
  const rejected = BiomeRivalsRules.applyCommand(
    withoutExcavation, 'alice', deployCommand('raider-full-cost', 0, 'db_005', 'UNIT', 0));
  TestHarness.equal(rejected.accepted, false);
  if (!rejected.accepted) TestHarness.equal(rejected.code, 'INSUFFICIENT_REDSTONE');
  TestHarness.equal(withoutExcavation.players[0]!.redstone, 2, 'a rejected dynamic cost must be atomic');

  const afterExcavation = activeState('match-1', ['alice', 'bob'], ['desert_badlands', 'nether']);
  afterExcavation.players[0]!.hand = ['db_005'];
  afterExcavation.players[0]!.redstone = 2;
  afterExcavation.players[0]!.redstoneCapacity = 2;
  afterExcavation.players[0]!.excavatedThisTurn = true;
  const discounted = BiomeRivalsRules.applyCommand(
    afterExcavation, 'alice', deployCommand('raider-discounted', 0, 'db_005', 'UNIT', 0));
  TestHarness.ok(discounted.accepted);
  if (!discounted.accepted) return;
  TestHarness.equal(discounted.state.players[0]!.redstone, 0);
  TestHarness.equal(discounted.batch.events[0]!.payload.redstone, 0);
  TestHarness.equal(BiomeRivalsRules.createClientSnapshot(discounted.state, 'alice').players[0]!.excavatedThisTurn, true);

  const unrelatedCard = activeState('match-1', ['alice', 'bob'], ['desert_badlands', 'nether']);
  unrelatedCard.players[0]!.hand = ['db_004'];
  unrelatedCard.players[0]!.redstone = 1;
  unrelatedCard.players[0]!.redstoneCapacity = 1;
  unrelatedCard.players[0]!.excavatedThisTurn = true;
  const unrelatedRejected = BiomeRivalsRules.applyCommand(
    unrelatedCard, 'alice', deployCommand('fence-not-discounted', 0, 'db_004', 'BUILDING', 0));
  TestHarness.equal(unrelatedRejected.accepted, false);
  if (!unrelatedRejected.accepted) TestHarness.equal(unrelatedRejected.code, 'INSUFFICIENT_REDSTONE');
});

TestHarness.test('requires an explicit no-selection confirmation when no buried card is inspected', function (): void {
  const state = activeState('match-1', ['alice', 'bob'], ['desert_badlands', 'nether']);
  state.players[0]!.hand = ['db_003'];
  state.players[0]!.deck = ['db_001', 'db_002'];
  state.players[0]!.buriedCardIds = [];
  state.players[0]!.redstone = 2;
  state.players[0]!.redstoneCapacity = 2;
  const deployed = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-empty-choice', 0, 'db_003', 'UNIT', 0));
  TestHarness.ok(deployed.accepted);
  if (!deployed.accepted) return;
  const choiceId = deployed.state.pendingChoice!.choiceId;
  TestHarness.equal(deployed.state.pendingChoice!.options.every(function (option): boolean { return !option.selectable; }), true);

  const result = BiomeRivalsRules.applyCommand(deployed.state, 'alice', resolveChoiceCommand('confirm-empty-choice', 1, choiceId, -1));

  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(result.batch.events.length, 1);
  TestHarness.equal(result.batch.events[0]!.type, 'CHOICE_RESOLVED');
  TestHarness.equal(result.batch.events[0]!.payload.selectedCardId, null);
  TestHarness.equal(JSON.stringify(result.state.players[0]!.deck), JSON.stringify(['db_001', 'db_002']));
  TestHarness.equal(result.state.players[0]!.hand.length, 0);
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
  TestHarness.equal(result.state.players[1]!.excavatedThisTurn, true);
  TestHarness.equal(result.state.players[0]!.excavatedThisTurn, false);
  TestHarness.equal(result.state.players[1]!.armor, 1);
  TestHarness.equal(result.batch.events[2]!.type, 'CARD_EXCAVATED');
  TestHarness.equal(result.batch.events[2]!.payload.destination, 'HAND');
  TestHarness.equal(result.batch.events[3]!.type, 'ARMOR_GAINED');
  TestHarness.equal(result.batch.events[4]!.type, 'CARD_DRAWN');
  const opponentProjection = BiomeRivalsRules.createClientEventBatch(result.batch, 'alice');
  TestHarness.equal(opponentProjection.events[2]!.payload.cardId, 'tk_006', 'excavated cards are public');
  TestHarness.equal(opponentProjection.events[4]!.payload.cardId, null, 'the following normal draw remains private');
});

TestHarness.test('clears the excavation discount marker when its owners turn ends', function (): void {
  const state = activeState('match-1', ['alice', 'bob'], ['desert_badlands', 'nether']);
  state.players[0]!.excavatedThisTurn = true;

  const result = BiomeRivalsRules.applyCommand(state, 'alice', command('end-excavation-turn', 0, 'END_TURN'));

  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.excavatedThisTurn, false);
  TestHarness.equal(result.state.players[1]!.excavatedThisTurn, false);
  TestHarness.equal(BiomeRivalsRules.createClientSnapshot(result.state, 'alice').players[0]!.excavatedThisTurn, false);
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

TestHarness.test('powder snow applies one replayable slow with a bound attack penalty', function (): void {
  const state = activeState('match-slow', ['alice', 'bob']);
  state.players[0]!.hand = ['si_006', 'si_006'];
  state.players[0]!.redstone = 4;
  state.players[0]!.redstoneCapacity = 4;
  placeUnit(state, 1, 'pf_001', 1, 'object-1', 1);
  state.players[1]!.battlefield[0]!.keywords.push('CHARGE');

  const first = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('play-powder-snow', 0, 'si_006', 'UNIT', 'object-1'));
  TestHarness.ok(first.accepted);
  if (!first.accepted) return;
  const target = first.state.players[1]!.battlefield[0]!;
  TestHarness.equal(target.attack, 0);
  TestHarness.equal(target.statuses.length, 1);
  TestHarness.equal(target.statuses[0]!.statusId, 'SLOW');
  TestHarness.equal(target.statuses[0]!.remainingDuration, 1);
  TestHarness.equal(target.statuses[0]!.attackModifier, -1);
  TestHarness.equal(target.statuses[0]!.boundAttackModifier, -2);
  TestHarness.equal(first.batch.events[1]!.type, 'OBJECT_STATUS_APPLIED');
  TestHarness.equal(first.batch.events[1]!.payload.sourcePlayerId, 'alice');
  TestHarness.equal(BiomeRivalsRules.createClientSnapshot(first.state, 'alice').players[1]!.battlefield[0]!.statuses[0]!.statusId, 'SLOW');

  const reapplied = BiomeRivalsRules.applyCommand(first.state, 'alice', playCommand('reapply-powder-snow', 1, 'si_006', 'UNIT', 'object-1'));
  TestHarness.ok(reapplied.accepted);
  if (!reapplied.accepted) return;
  TestHarness.equal(reapplied.state.players[1]!.battlefield[0]!.attack, 0);
  TestHarness.equal(reapplied.state.players[1]!.battlefield[0]!.statuses.length, 1);
  TestHarness.equal(reapplied.state.players[1]!.battlefield[0]!.statuses[0]!.attackModifier, -1);
});

TestHarness.test('stray validates its battlecry target before deployment and applies slow without an attack penalty', function (): void {
  const state = activeState('match-stray', ['alice', 'bob']);
  state.players[0]!.hand = ['si_003'];
  state.players[0]!.redstone = 3;
  state.players[0]!.redstoneCapacity = 3;
  placeUnit(state, 1, 'nt_003', 1, 'object-1', 1);
  state.nextInstanceId = 2;

  const missing = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('stray-missing', 0, 'si_003', 'UNIT', 0));
  TestHarness.equal(missing.accepted, false);
  if (!missing.accepted) TestHarness.equal(missing.code, 'INVALID_TARGET');
  TestHarness.equal(state.players[0]!.hand[0], 'si_003');
  TestHarness.equal(state.players[0]!.redstone, 3);
  TestHarness.equal(state.players[0]!.unitSlots[0], null);

  const deployed = BiomeRivalsRules.applyCommand(
    state,
    'alice',
    deployCommand('stray-deploy', 0, 'si_003', 'UNIT', 0, 'REDSTONE', 'UNIT', 'object-1')
  );
  TestHarness.ok(deployed.accepted);
  if (!deployed.accepted) return;
  TestHarness.equal(deployed.state.players[0]!.battlefield[0]!.cardId, 'si_003');
  const slow = deployed.state.players[1]!.battlefield[0]!.statuses[0]!;
  TestHarness.equal(slow.statusId, 'SLOW');
  TestHarness.equal(slow.attackModifier, 0);
  TestHarness.equal(slow.boundAttackModifier, 0);
  TestHarness.equal(slow.sourceInstanceId, 'object-2');
  TestHarness.equal(deployed.batch.events[0]!.type, 'CARD_DEPLOYED');
  TestHarness.equal(deployed.batch.events[1]!.type, 'OBJECT_STATUS_APPLIED');
});

TestHarness.test('powder snow strengthens an existing stray slow exactly once and restores clamped attack', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['si_003', 'si_006', 'si_006'];
  state.players[0]!.redstone = 7;
  state.players[0]!.redstoneCapacity = 7;
  placeUnit(state, 1, 'pf_001', 1, 'object-1', 1);
  state.nextInstanceId = 2;

  const stray = BiomeRivalsRules.applyCommand(
    state,
    'alice',
    deployCommand('stray-first', 0, 'si_003', 'UNIT', 0, 'REDSTONE', 'UNIT', 'object-1')
  );
  TestHarness.ok(stray.accepted, !stray.accepted ? stray.code + ': ' + stray.message : 'stray deployment failed');
  if (!stray.accepted) return;
  const bucket = BiomeRivalsRules.applyCommand(stray.state, 'alice', playCommand('bucket-second', 1, 'si_006', 'UNIT', 'object-1'));
  TestHarness.ok(bucket.accepted, !bucket.accepted ? bucket.code + ': ' + bucket.message : 'first powder snow failed');
  if (!bucket.accepted) return;
  const repeated = BiomeRivalsRules.applyCommand(bucket.state, 'alice', playCommand('bucket-third', 2, 'si_006', 'UNIT', 'object-1'));
  TestHarness.ok(repeated.accepted, !repeated.accepted ? repeated.code + ': ' + repeated.message : 'repeated powder snow failed');
  if (!repeated.accepted) return;
  const slowed = repeated.state.players[1]!.battlefield[0]!;
  TestHarness.equal(slowed.attack, 0);
  TestHarness.equal(slowed.statuses[0]!.attackModifier, -1);
  TestHarness.equal(slowed.statuses[0]!.boundAttackModifier, -2);
  TestHarness.equal(slowed.statuses[0]!.sourceCardId, 'si_006');
  TestHarness.equal(slowed.statuses[0]!.sourceInstanceId, '');

  const bobTurn = BiomeRivalsRules.applyCommand(repeated.state, 'alice', command('stray-bucket-pass', 3, 'END_TURN'));
  TestHarness.ok(bobTurn.accepted, !bobTurn.accepted ? bobTurn.code + ': ' + bobTurn.message : 'turn handoff failed');
  if (!bobTurn.accepted) return;
  const expired = BiomeRivalsRules.applyCommand(bobTurn.state, 'bob', command('stray-bucket-expire', 4, 'END_TURN'));
  TestHarness.ok(expired.accepted, !expired.accepted ? expired.code + ': ' + expired.message : 'status expiry failed');
  if (!expired.accepted) return;
  TestHarness.equal(expired.state.players[1]!.battlefield[0]!.attack, 1);
  TestHarness.equal(expired.state.players[1]!.battlefield[0]!.statuses.length, 0);
});

TestHarness.test('slow blocks its controllers attack and expires at that controllers end phase', function (): void {
  const state = activeState('match-slow-expiry', ['alice', 'bob']);
  state.players[0]!.hand = ['si_006'];
  state.players[0]!.redstone = 2;
  state.players[0]!.redstoneCapacity = 2;
  placeUnit(state, 1, 'nt_003', 1, 'object-1', 1);
  state.players[1]!.battlefield[0]!.keywords.push('CHARGE');

  const played = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('slow-before-turn', 0, 'si_006', 'UNIT', 'object-1'));
  TestHarness.ok(played.accepted);
  if (!played.accepted) return;
  const bobTurn = BiomeRivalsRules.applyCommand(played.state, 'alice', command('pass-to-bob', 1, 'END_TURN'));
  TestHarness.ok(bobTurn.accepted);
  if (!bobTurn.accepted) return;
  const combat = BiomeRivalsRules.applyCommand(bobTurn.state, 'bob', enterCombatCommand('bob-combat', 2));
  TestHarness.ok(combat.accepted);
  if (!combat.accepted) return;
  const blocked = BiomeRivalsRules.applyCommand(combat.state, 'bob', attackCommand('slow-attack', 3, 'object-1', 'HERO'));
  TestHarness.equal(blocked.accepted, false);
  if (!blocked.accepted) TestHarness.equal(blocked.code, 'ATTACKER_NOT_READY');

  const expired = BiomeRivalsRules.applyCommand(combat.state, 'bob', command('bob-end', 3, 'END_TURN'));
  TestHarness.ok(expired.accepted);
  if (!expired.accepted) return;
  TestHarness.equal(expired.state.players[1]!.battlefield[0]!.attack, 3);
  TestHarness.equal(expired.state.players[1]!.battlefield[0]!.statuses.length, 0);
  TestHarness.equal(expired.batch.events[0]!.type, 'OBJECT_STATUS_REMOVED');
  TestHarness.equal(expired.batch.events[0]!.payload.reason, 'DURATION_EXPIRED');
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

TestHarness.test('awards Rotten Flesh to the enemy unit that kills a Husk', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  state.nextInstanceId = 3;
  state.players[0]!.hand = [];
  placeUnit(state, 0, 'pf_008', 0, 'object-1', 1);
  placeUnit(state, 1, 'db_001', 0, 'object-2', 1);

  const result = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('husk-drop', 0, 'object-1', 'UNIT', 'object-2'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.hand[0], 'tk_005');
  TestHarness.equal(result.state.players[1]!.discardPile[0], 'db_001');
  TestHarness.equal(result.batch.events[0]!.type, 'ATTACK_RESOLVED');
  TestHarness.equal(result.batch.events[1]!.type, 'OBJECT_DIED');
  TestHarness.equal(result.batch.events[2]!.type, 'CARD_GENERATED');
  TestHarness.equal(result.batch.events[2]!.payload.playerId, 'alice');
  TestHarness.equal(result.batch.events[2]!.payload.sourceCardId, 'db_001');
  TestHarness.equal(result.batch.events[2]!.payload.sourceInstanceId, 'object-2');
  TestHarness.equal(result.batch.events[2]!.payload.effectId, 'effect.db_001.01');
  TestHarness.equal(result.batch.events[2]!.payload.destination, 'HAND');
  TestHarness.equal(result.batch.events[2]!.payload.cardId, 'tk_005');

  const killerProjection = BiomeRivalsRules.createClientEventBatch(result.batch, 'alice');
  const ownerProjection = BiomeRivalsRules.createClientEventBatch(result.batch, 'bob');
  TestHarness.equal(killerProjection.events[2]!.payload.cardId, 'tk_005');
  TestHarness.equal(ownerProjection.events[2]!.payload.cardId, null);
});

TestHarness.test('credits a retaliation kill to the Husk defenders opponent', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  state.nextInstanceId = 3;
  state.players[1]!.hand = [];
  placeUnit(state, 0, 'db_001', 0, 'object-1', 1);
  placeUnit(state, 1, 'nt_003', 0, 'object-2', 1);

  const result = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('husk-retaliation-drop', 0, 'object-1', 'UNIT', 'object-2'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.battlefield.length, 0);
  TestHarness.equal(result.state.players[1]!.hand[0], 'tk_005');
  const generated = result.batch.events.filter(function (event): boolean { return event.type === 'CARD_GENERATED'; });
  TestHarness.equal(generated.length, 1);
  TestHarness.equal(generated[0]!.payload.playerId, 'bob');
  TestHarness.equal(generated[0]!.payload.effectId, 'effect.db_001.01');
});

TestHarness.test('Sandstorm drops only the enemy Husk loot to its caster', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['db_006'];
  state.players[0]!.redstone = 3;
  state.players[0]!.redstoneCapacity = 3;
  placeUnit(state, 0, 'db_001', 0, 'object-1', 1);
  placeUnit(state, 1, 'db_001', 0, 'object-2', 1);
  state.nextInstanceId = 3;

  const result = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('sandstorm-husk-drop', 0, 'db_006'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.hand.length, 1);
  TestHarness.equal(result.state.players[0]!.hand[0], 'tk_005');
  TestHarness.equal(result.state.players[0]!.discardPile[0], 'db_006');
  TestHarness.equal(result.state.players[0]!.discardPile[1], 'db_001');
  TestHarness.equal(result.state.players[1]!.discardPile[0], 'db_001');
  const generated = result.batch.events.filter(function (event): boolean { return event.type === 'CARD_GENERATED'; });
  TestHarness.equal(generated.length, 1);
  TestHarness.equal(generated[0]!.payload.playerId, 'alice');
  TestHarness.equal(generated[0]!.payload.cardId, 'tk_005');
});

TestHarness.test('sends Husk loot to the killers public discard when its hand is full', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  state.nextInstanceId = 3;
  state.players[0]!.hand = ['pf_001', 'pf_001', 'pf_001', 'pf_001', 'pf_001', 'pf_001', 'pf_001'];
  placeUnit(state, 0, 'pf_008', 0, 'object-1', 1);
  placeUnit(state, 1, 'db_001', 0, 'object-2', 1);

  const result = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('husk-full-hand-drop', 0, 'object-1', 'UNIT', 'object-2'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.hand.length, 7);
  TestHarness.equal(result.state.players[0]!.discardPile[0], 'tk_005');
  const generated = result.batch.events.filter(function (event): boolean { return event.type === 'CARD_GENERATED'; })[0]!;
  TestHarness.equal(generated.payload.destination, 'DISCARD');
  TestHarness.equal(generated.payload.cardId, 'tk_005');
  const ownerProjection = BiomeRivalsRules.createClientEventBatch(result.batch, 'bob');
  const projected = ownerProjection.events.filter(function (event): boolean { return event.type === 'CARD_GENERATED'; })[0]!;
  TestHarness.equal(projected.payload.cardId, 'tk_005');
});

TestHarness.test('resolves Dungeon Skeleton deathrattle damage before its Bone drop', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  state.nextInstanceId = 3;
  state.players[0]!.hand = [];
  state.players[1]!.hand = [];
  placeUnit(state, 0, 'pf_008', 0, 'object-1', 1);
  placeUnit(state, 1, 'cd_003', 0, 'object-2', 1);

  const result = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('dungeon-skeleton-death', 0, 'object-1', 'UNIT', 'object-2'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.battlefield[0]!.health, 3);
  TestHarness.equal(result.state.players[0]!.hand[0], 'tk_009');
  TestHarness.equal(result.state.players[1]!.discardPile[0], 'cd_003');
  TestHarness.equal(result.batch.events[0]!.type, 'ATTACK_RESOLVED');
  TestHarness.equal(result.batch.events[1]!.type, 'OBJECT_DIED');
  TestHarness.equal(result.batch.events[2]!.type, 'OBJECT_STATS_CHANGED');
  TestHarness.equal(result.batch.events[2]!.payload.sourceCardId, 'cd_003');
  TestHarness.equal(result.batch.events[2]!.payload.sourceInstanceId, 'object-2');
  TestHarness.equal(result.batch.events[2]!.payload.effectId, 'effect.cd_003.01');
  TestHarness.equal(result.batch.events[2]!.payload.instanceId, 'object-1');
  TestHarness.equal(result.batch.events[2]!.payload.health, 3);
  TestHarness.equal(result.batch.events[3]!.type, 'CARD_GENERATED');
  TestHarness.equal(result.batch.events[3]!.payload.playerId, 'alice');
  TestHarness.equal(result.batch.events[3]!.payload.cardId, 'tk_009');
});

TestHarness.test('skips Dungeon Skeleton deathrattle when simultaneous combat leaves no legal enemy unit', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  state.nextInstanceId = 3;
  state.players[0]!.hand = [];
  placeUnit(state, 0, 'pf_003', 0, 'object-1', 1);
  placeUnit(state, 1, 'cd_003', 0, 'object-2', 1);

  const result = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('dungeon-skeleton-no-target', 0, 'object-1', 'UNIT', 'object-2'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.battlefield.length, 0);
  TestHarness.equal(result.state.players[1]!.battlefield.length, 0);
  TestHarness.equal(result.state.players[0]!.hand[0], 'tk_009');
  TestHarness.equal(result.batch.events.some(function (event): boolean {
    return event.type === 'OBJECT_STATS_CHANGED' && event.payload.effectId === 'effect.cd_003.01';
  }), false);
});

TestHarness.test('records a repeatable legal random target for Dungeon Skeleton deathrattle', function (): void {
  const initial = activeState('repeatable-random-match', ['alice', 'bob']);
  initial.turn = 2;
  initial.phase = 'COMBAT';
  initial.nextInstanceId = 4;
  initial.players[0]!.hand = [];
  initial.players[1]!.hand = [];
  placeUnit(initial, 0, 'pf_008', 0, 'object-1', 1);
  placeUnit(initial, 0, 'pf_001', 3, 'object-2', 1);
  placeUnit(initial, 1, 'cd_003', 0, 'object-3', 1);
  const leftState = JSON.parse(JSON.stringify(initial)) as BiomeRivalsRules.MatchState;
  const rightState = JSON.parse(JSON.stringify(initial)) as BiomeRivalsRules.MatchState;

  const left = BiomeRivalsRules.applyCommand(leftState, 'alice', attackCommand('repeatable-random', 0, 'object-1', 'UNIT', 'object-3'));
  const right = BiomeRivalsRules.applyCommand(rightState, 'alice', attackCommand('repeatable-random', 0, 'object-1', 'UNIT', 'object-3'));
  TestHarness.equal(left.accepted, true);
  TestHarness.equal(right.accepted, true);
  if (!left.accepted || !right.accepted) return;
  const leftDamage = left.batch.events.filter(function (event): boolean {
    return event.type === 'OBJECT_STATS_CHANGED' && event.payload.effectId === 'effect.cd_003.01';
  })[0]!;
  const rightDamage = right.batch.events.filter(function (event): boolean {
    return event.type === 'OBJECT_STATS_CHANGED' && event.payload.effectId === 'effect.cd_003.01';
  })[0]!;
  TestHarness.equal(leftDamage.payload.instanceId, rightDamage.payload.instanceId);
  TestHarness.equal(leftDamage.payload.instanceId === 'object-1' || leftDamage.payload.instanceId === 'object-2', true);
});

TestHarness.test('propagates Dungeon Skeleton deathrattle kill credit into a chained Husk drop', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  state.nextInstanceId = 4;
  state.players[0]!.hand = [];
  state.players[1]!.hand = [];
  placeUnit(state, 0, 'pf_003', 0, 'object-1', 1);
  placeUnit(state, 0, 'db_001', 1, 'object-2', 1);
  placeUnit(state, 1, 'cd_003', 0, 'object-3', 1);

  const result = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('chained-loot', 0, 'object-1', 'UNIT', 'object-3'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.battlefield.length, 0);
  TestHarness.equal(result.state.players[1]!.battlefield.length, 0);
  TestHarness.equal(result.state.players[0]!.hand[0], 'tk_009');
  TestHarness.equal(result.state.players[1]!.hand[0], 'tk_005');
  const eventTypes = result.batch.events.map(function (event): string { return event.type; });
  TestHarness.equal(eventTypes.join(','), 'ATTACK_RESOLVED,OBJECT_DIED,OBJECT_DIED,OBJECT_STATS_CHANGED,CARD_GENERATED,OBJECT_DIED,CARD_GENERATED');
  TestHarness.equal(result.batch.events[3]!.payload.instanceId, 'object-2');
  TestHarness.equal(result.batch.events[4]!.payload.cardId, 'tk_009');
  TestHarness.equal(result.batch.events[6]!.payload.cardId, 'tk_005');
  TestHarness.equal(result.batch.events[6]!.payload.playerId, 'bob');
});

TestHarness.test('awards Wool when an enemy kills a Grazing Sheep', function (): void {
  const state = activeState('match-1', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  state.nextInstanceId = 3;
  state.players[0]!.hand = [];
  placeUnit(state, 0, 'pf_008', 0, 'object-1', 1);
  placeUnit(state, 1, 'pf_002', 0, 'object-2', 1);

  const result = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('sheep-drop', 0, 'object-1', 'UNIT', 'object-2'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.hand[0], 'tk_001');
  const generated = result.batch.events.filter(function (event): boolean { return event.type === 'CARD_GENERATED'; })[0]!;
  TestHarness.equal(generated.payload.sourceCardId, 'pf_002');
  TestHarness.equal(generated.payload.effectId, 'effect.pf_002.01');
  TestHarness.equal(generated.payload.cardId, 'tk_001');
});

TestHarness.test('awards Bone when an enemy kills a Stray', function (): void {
  const state = activeState('match-stray-drop', ['alice', 'bob']);
  state.turn = 2;
  state.phase = 'COMBAT';
  state.nextInstanceId = 3;
  state.players[0]!.hand = [];
  placeUnit(state, 0, 'pf_008', 0, 'object-1', 1);
  placeUnit(state, 1, 'si_003', 0, 'object-2', 1);

  const result = BiomeRivalsRules.applyCommand(state, 'alice', attackCommand('stray-drop', 0, 'object-1', 'UNIT', 'object-2'));
  TestHarness.ok(result.accepted);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.hand[0], 'tk_009');
  const generated = result.batch.events.filter(function (event): boolean { return event.type === 'CARD_GENERATED'; })[0]!;
  TestHarness.equal(generated.payload.sourceCardId, 'si_003');
  TestHarness.equal(generated.payload.effectId, 'effect.si_003.01');
  TestHarness.equal(generated.payload.cardId, 'tk_009');
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
  state.players[1]!.battlefield[1]!.health += 1;
  state.players[1]!.battlefield[1]!.maxHealth += 1;
  state.players[1]!.battlefield[1]!.adjacencyHealthModifier = 1;

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

TestHarness.test('equips and replaces Riptide Trident through the public equipment slot', function (): void {
  const state = activeState('match-1', ['alice', 'bob'], ['ocean_river', 'nether']);
  state.players[0]!.hand = ['or_006', 'or_006'];
  state.players[0]!.redstone = 6;
  state.players[0]!.redstoneCapacity = 6;
  const first = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('equip-1', 0, 'or_006'));
  TestHarness.ok(first.accepted);
  if (!first.accepted) return;
  TestHarness.equal(first.batch.events[0]!.type, 'CARD_EQUIPPED');
  TestHarness.equal(first.state.players[0]!.equipment!.durability, 3);
  const second = BiomeRivalsRules.applyCommand(first.state, 'alice', playCommand('equip-2', 1, 'or_006'));
  TestHarness.ok(second.accepted);
  if (!second.accepted) return;
  TestHarness.equal(second.batch.events[0]!.type, 'EQUIPMENT_DESTROYED');
  TestHarness.equal(second.batch.events[1]!.type, 'CARD_EQUIPPED');
  TestHarness.equal(second.state.players[0]!.discardPile[0], 'or_006');
  TestHarness.equal(second.state.players[0]!.equipment!.instanceId, 'equipment-2');
});

TestHarness.test('hero attack consumes durability, takes retaliation, and offers Trident movement in-world', function (): void {
  const state = activeState('match-1', ['alice', 'bob'], ['ocean_river', 'snow_ice']);
  state.players[0]!.hand = ['or_006'];
  state.players[0]!.redstone = 6;
  state.players[0]!.redstoneCapacity = 6;
  state.players[0]!.armor = 1;
  placeUnit(state, 1, 'or_002', 0, 'object-21', 1);
  placeUnit(state, 1, 'si_005', 2, 'object-20', 1);
  const equipped = BiomeRivalsRules.applyCommand(state, 'alice', playCommand('equip', 0, 'or_006'));
  TestHarness.ok(equipped.accepted);
  if (!equipped.accepted) return;
  const combat = BiomeRivalsRules.applyCommand(equipped.state, 'alice', enterCombatCommand('combat', 1));
  TestHarness.ok(combat.accepted);
  if (!combat.accepted) return;
  const attacked = BiomeRivalsRules.applyCommand(combat.state, 'alice', attackCommand('hero-hit', 2, 'HERO', 'UNIT', 'object-20'));
  TestHarness.ok(attacked.accepted);
  if (!attacked.accepted) return;
  TestHarness.equal(attacked.state.players[0]!.heroHasAttacked, true);
  TestHarness.equal(attacked.state.players[0]!.equipment!.durability, 2);
  TestHarness.equal(attacked.state.players[0]!.armor, 0);
  TestHarness.equal(attacked.state.players[0]!.life, 28);
  TestHarness.equal(attacked.state.pendingChoice!.kind, 'MOVE_UNIT');
  TestHarness.equal(attacked.state.pendingChoice!.options.length, 2);
  TestHarness.equal(attacked.state.pendingChoice!.options[0]!.slotIndex, 1);
  TestHarness.equal(attacked.state.pendingChoice!.options[1]!.slotIndex, 3);
  const opponentSnapshot = BiomeRivalsRules.createClientSnapshot(attacked.state, 'bob');
  TestHarness.equal(opponentSnapshot.pendingChoice!.targetInstanceId, 'object-20');
  TestHarness.equal(opponentSnapshot.pendingChoice!.options[0]!.cardId, 'si_005');
  TestHarness.equal(opponentSnapshot.pendingChoice!.options[0]!.selectable, false);
  const moved = BiomeRivalsRules.applyCommand(attacked.state, 'alice',
    resolveChoiceCommand('move', 3, attacked.state.pendingChoice!.choiceId, 0));
  TestHarness.ok(moved.accepted);
  if (!moved.accepted) return;
  TestHarness.equal(moved.state.players[1]!.unitSlots[1], 'object-20');
  TestHarness.equal(moved.state.players[1]!.unitSlots[2], null);
  TestHarness.equal(moved.batch.events[1]!.type, 'OBJECT_MOVED');
  TestHarness.equal(moved.batch.events[2]!.type, 'OBJECT_STATS_CHANGED');
  TestHarness.equal(moved.batch.events[2]!.payload.effectId, 'effect.or_002.01');
  TestHarness.equal(moved.state.players[1]!.battlefield.filter(function (object): boolean {
    return object.instanceId === 'object-20';
  })[0]!.temporaryAttackModifier, 1);
});

TestHarness.test('Salmon School offers friendly water-current movement and grants a temporary attack bonus', function (): void {
  const state = activeState('match-1', ['alice', 'bob'], ['ocean_river', 'nether']);
  state.players[0]!.hand = ['or_001'];
  state.players[0]!.redstone = 1;
  state.players[0]!.redstoneCapacity = 1;
  const deployed = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('salmon-deploy', 0, 'or_001', 'UNIT', 1));
  TestHarness.ok(deployed.accepted);
  if (!deployed.accepted) return;
  TestHarness.equal(deployed.state.pendingChoice!.kind, 'MOVE_UNIT');
  TestHarness.equal(deployed.state.pendingChoice!.effectId, 'effect.or_001.01');
  TestHarness.equal(deployed.state.pendingChoice!.targetPlayerId, 'alice');
  TestHarness.equal(deployed.state.pendingChoice!.targetInstanceId, 'object-1');
  TestHarness.equal(deployed.state.pendingChoice!.options[0]!.slotIndex, 0);
  TestHarness.equal(deployed.state.pendingChoice!.options[1]!.slotIndex, 2);
  const moved = BiomeRivalsRules.applyCommand(deployed.state, 'alice',
    resolveChoiceCommand('salmon-move', 1, deployed.state.pendingChoice!.choiceId, 1));
  TestHarness.ok(moved.accepted);
  if (!moved.accepted) return;
  const salmon = moved.state.players[0]!.battlefield[0]!;
  TestHarness.equal(salmon.slotIndex, 2);
  TestHarness.equal(salmon.attack, 2);
  TestHarness.equal(salmon.temporaryAttackModifier, 1);
  TestHarness.equal(salmon.temporaryAttackModifierExpiresOnTurn, 1);
  TestHarness.equal(moved.batch.events[1]!.type, 'OBJECT_MOVED');
  TestHarness.equal(moved.batch.events[2]!.type, 'OBJECT_STATS_CHANGED');
  const ended = BiomeRivalsRules.applyCommand(moved.state, 'alice', command('salmon-end', 2, 'END_TURN'));
  TestHarness.ok(ended.accepted);
  if (!ended.accepted) return;
  TestHarness.equal(ended.state.players[0]!.battlefield[0]!.attack, 1);
  TestHarness.equal(ended.state.players[0]!.battlefield[0]!.temporaryAttackModifier, 0);
});

TestHarness.test('Salmon School may keep its deployment position without receiving the move bonus', function (): void {
  const state = activeState('match-1', ['alice', 'bob'], ['ocean_river', 'nether']);
  state.players[0]!.hand = ['or_001'];
  const deployed = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('salmon-stay-deploy', 0, 'or_001', 'UNIT', 1));
  TestHarness.ok(deployed.accepted);
  if (!deployed.accepted) return;
  const stayed = BiomeRivalsRules.applyCommand(deployed.state, 'alice',
    resolveChoiceCommand('salmon-stay', 1, deployed.state.pendingChoice!.choiceId, -1));
  TestHarness.ok(stayed.accepted);
  if (!stayed.accepted) return;
  TestHarness.equal(stayed.state.players[0]!.battlefield[0]!.slotIndex, 1);
  TestHarness.equal(stayed.state.players[0]!.battlefield[0]!.attack, 1);
  TestHarness.equal(stayed.batch.events.length, 1);
});

TestHarness.test('Dolphin Guide buffs only the first other friendly unit that moves each turn', function (): void {
  const state = activeState('match-1', ['alice', 'bob'], ['ocean_river', 'nether']);
  state.players[0]!.hand = ['or_002', 'or_001', 'or_001'];
  state.players[0]!.redstone = 4;
  state.players[0]!.redstoneCapacity = 4;
  const guideDeployed = BiomeRivalsRules.applyCommand(state, 'alice',
    deployCommand('guide-deploy', 0, 'or_002', 'UNIT', 0));
  TestHarness.ok(guideDeployed.accepted);
  if (!guideDeployed.accepted) return;
  const firstDeployed = BiomeRivalsRules.applyCommand(guideDeployed.state, 'alice',
    deployCommand('first-salmon-deploy', 1, 'or_001', 'UNIT', 2));
  TestHarness.ok(firstDeployed.accepted);
  if (!firstDeployed.accepted) return;
  const firstMoved = BiomeRivalsRules.applyCommand(firstDeployed.state, 'alice',
    resolveChoiceCommand('first-salmon-move', 2, firstDeployed.state.pendingChoice!.choiceId, 1));
  TestHarness.ok(firstMoved.accepted);
  if (!firstMoved.accepted) return;
  const firstSalmon = firstMoved.state.players[0]!.battlefield.filter(function (object): boolean {
    return object.cardId === 'or_001';
  })[0]!;
  TestHarness.equal(firstSalmon.slotIndex, 3);
  TestHarness.equal(firstSalmon.attack, 3);
  TestHarness.equal(firstSalmon.temporaryAttackModifier, 2);
  TestHarness.equal(firstMoved.batch.events.length, 4);
  TestHarness.equal(firstMoved.batch.events[3]!.payload.effectId, 'effect.or_002.01');
  TestHarness.equal(firstMoved.batch.events[3]!.payload.sourceInstanceId, 'object-1');
  TestHarness.equal(firstMoved.state.players[0]!.triggeredEffectKeysThisTurn[0], 'object-1:effect.or_002.01');

  const secondDeployed = BiomeRivalsRules.applyCommand(firstMoved.state, 'alice',
    deployCommand('second-salmon-deploy', 3, 'or_001', 'UNIT', 2));
  TestHarness.ok(secondDeployed.accepted);
  if (!secondDeployed.accepted) return;
  const secondMoved = BiomeRivalsRules.applyCommand(secondDeployed.state, 'alice',
    resolveChoiceCommand('second-salmon-move', 4, secondDeployed.state.pendingChoice!.choiceId, 0));
  TestHarness.ok(secondMoved.accepted);
  if (!secondMoved.accepted) return;
  const secondSalmon = secondMoved.state.players[0]!.battlefield.filter(function (object): boolean {
    return object.cardId === 'or_001' && object.instanceId !== firstSalmon.instanceId;
  })[0]!;
  TestHarness.equal(secondSalmon.slotIndex, 1);
  TestHarness.equal(secondSalmon.attack, 2);
  TestHarness.equal(secondMoved.batch.events.length, 3);
  const ended = BiomeRivalsRules.applyCommand(secondMoved.state, 'alice', command('guide-turn-end', 5, 'END_TURN'));
  TestHarness.ok(ended.accepted);
  if (!ended.accepted) return;
  TestHarness.equal(ended.state.players[0]!.triggeredEffectKeysThisTurn.length, 0);
});

TestHarness.test('Drowned requires a target beside an aquatic ally and resolves damage after deployment', function (): void {
  const state = activeState('match-1', ['alice', 'bob'], ['ocean_river', 'plains_forest']);
  state.players[0]!.hand = ['or_003'];
  state.players[0]!.redstone = 2;
  state.players[0]!.redstoneCapacity = 2;
  placeUnit(state, 0, 'or_001', 1, 'object-10', 1);
  placeUnit(state, 1, 'pf_001', 0, 'object-20', 1);
  state.players[1]!.battlefield[0]!.health = 1;

  const missingTarget = BiomeRivalsRules.applyCommand(state, 'alice',
    deployCommand('drowned-missing-target', 0, 'or_003', 'UNIT', 2));
  TestHarness.equal(missingTarget.accepted, false);
  if (!missingTarget.accepted) TestHarness.equal(missingTarget.code, 'INVALID_TARGET');
  TestHarness.equal(state.players[0]!.redstone, 2);
  TestHarness.equal(state.players[0]!.hand[0], 'or_003');

  const deployed = BiomeRivalsRules.applyCommand(state, 'alice',
    deployCommand('drowned-deploy', 0, 'or_003', 'UNIT', 2, 'REDSTONE', 'UNIT', 'object-20'));
  TestHarness.ok(deployed.accepted);
  if (!deployed.accepted) return;
  TestHarness.equal(deployed.batch.events[0]!.type, 'CARD_DEPLOYED');
  TestHarness.equal(deployed.batch.events[1]!.type, 'OBJECT_STATS_CHANGED');
  TestHarness.equal(deployed.batch.events[1]!.payload.effectId, 'effect.or_003.01');
  TestHarness.equal(deployed.batch.events[1]!.payload.health, 0);
  TestHarness.equal(deployed.batch.events[2]!.type, 'OBJECT_DIED');
  TestHarness.equal(deployed.state.players[1]!.unitSlots[0], null);
  TestHarness.equal(deployed.state.players[1]!.discardPile[0], 'pf_001');
});

TestHarness.test('Drowned deploys without a target when no adjacent aquatic ally activates its battlecry', function (): void {
  const state = activeState('match-1', ['alice', 'bob'], ['ocean_river', 'plains_forest']);
  state.players[0]!.hand = ['or_003'];
  state.players[0]!.redstone = 2;
  state.players[0]!.redstoneCapacity = 2;
  placeUnit(state, 0, 'pf_001', 1, 'object-10', 1);
  placeUnit(state, 1, 'pf_002', 0, 'object-20', 1);
  const targetHealth = state.players[1]!.battlefield[0]!.health;

  const deployed = BiomeRivalsRules.applyCommand(state, 'alice',
    deployCommand('inactive-drowned-deploy', 0, 'or_003', 'UNIT', 2));
  TestHarness.ok(deployed.accepted);
  if (!deployed.accepted) return;
  TestHarness.equal(deployed.batch.events.length, 1);
  TestHarness.equal(deployed.state.players[1]!.battlefield[0]!.health, targetHealth);
  TestHarness.equal(deployed.state.players[0]!.unitSlots[2], 'object-1');
});

TestHarness.test('Guardian damages only the first enemy unit that actually moves each turn', function (): void {
  const state = activeState('match-guardian-movement', ['alice', 'bob'], ['ocean_river', 'ocean_river']);
  state.players[0]!.hand = ['or_001', 'or_001', 'or_001'];
  state.players[0]!.redstone = 3;
  state.players[0]!.redstoneCapacity = 3;
  placeUnit(state, 1, 'or_004', 1, 'object-20', 1);

  const stayedDeployment = BiomeRivalsRules.applyCommand(state, 'alice',
    deployCommand('guardian-stay-deploy', 0, 'or_001', 'UNIT', 0));
  if (!stayedDeployment.accepted) throw new Error('guardian stay deployment rejected: ' + stayedDeployment.code + ' ' + stayedDeployment.message);
  if (!stayedDeployment.accepted) return;
  const stayed = BiomeRivalsRules.applyCommand(stayedDeployment.state, 'alice',
    resolveChoiceCommand('guardian-stay', 1, stayedDeployment.state.pendingChoice!.choiceId, -1));
  if (!stayed.accepted) throw new Error('guardian stay choice rejected: ' + stayed.code + ' ' + stayed.message);
  if (!stayed.accepted) return;
  TestHarness.equal(stayed.state.players[1]!.triggeredEffectKeysThisTurn.length, 0);

  const firstDeployment = BiomeRivalsRules.applyCommand(stayed.state, 'alice',
    deployCommand('guardian-first-deploy', 2, 'or_001', 'UNIT', 2));
  if (!firstDeployment.accepted) throw new Error('guardian first deployment rejected: ' + firstDeployment.code + ' ' + firstDeployment.message);
  if (!firstDeployment.accepted) return;
  const firstMoved = BiomeRivalsRules.applyCommand(firstDeployment.state, 'alice',
    resolveChoiceCommand('guardian-first-move', 3, firstDeployment.state.pendingChoice!.choiceId, 1));
  if (!firstMoved.accepted) throw new Error('guardian first move rejected: ' + firstMoved.code + ' ' + firstMoved.message);
  if (!firstMoved.accepted) return;
  const firstSalmon = firstMoved.state.players[0]!.battlefield.filter(function (object): boolean {
    return object.slotIndex === 3;
  })[0]!;
  TestHarness.equal(firstSalmon.health, 1);
  TestHarness.equal(firstMoved.batch.events[3]!.type, 'OBJECT_STATS_CHANGED');
  TestHarness.equal(firstMoved.batch.events[3]!.payload.effectId, 'effect.or_004.01');
  TestHarness.equal(firstMoved.batch.events[3]!.payload.sourceInstanceId, 'object-20');
  TestHarness.equal(firstMoved.state.players[1]!.triggeredEffectKeysThisTurn[0], 'object-20:effect.or_004.01');

  const secondDeployment = BiomeRivalsRules.applyCommand(firstMoved.state, 'alice',
    deployCommand('guardian-second-deploy', 4, 'or_001', 'UNIT', 2));
  if (!secondDeployment.accepted) throw new Error('guardian second deployment rejected: ' + secondDeployment.code + ' ' + secondDeployment.message);
  if (!secondDeployment.accepted) return;
  const secondMoved = BiomeRivalsRules.applyCommand(secondDeployment.state, 'alice',
    resolveChoiceCommand('guardian-second-move', 5, secondDeployment.state.pendingChoice!.choiceId, 0));
  if (!secondMoved.accepted) throw new Error('guardian second move rejected: ' + secondMoved.code + ' ' + secondMoved.message);
  if (!secondMoved.accepted) return;
  const secondSalmon = secondMoved.state.players[0]!.battlefield.filter(function (object): boolean {
    return object.slotIndex === 1;
  })[0]!;
  TestHarness.equal(secondSalmon.health, 2);
  TestHarness.equal(secondMoved.batch.events.length, 3);
});

TestHarness.test('Guardian drops a Prismarine Shard to the enemy that kills it', function (): void {
  const state = activeState('match-guardian-drop', ['alice', 'bob'], ['plains_forest', 'ocean_river']);
  state.turn = 2;
  state.players[0]!.hand = [];
  placeUnit(state, 0, 'pf_008', 0, 'object-10', 1);
  placeUnit(state, 1, 'or_004', 0, 'object-20', 1);
  state.players[0]!.battlefield[0]!.attack = 4;
  state.players[0]!.battlefield[0]!.health = 8;
  state.players[0]!.battlefield[0]!.maxHealth = 8;
  const combat = BiomeRivalsRules.applyCommand(state, 'alice', enterCombatCommand('guardian-drop-combat', 0));
  if (!combat.accepted) throw new Error('guardian drop combat rejected: ' + combat.code + ' ' + combat.message);
  if (!combat.accepted) return;
  const attacked = BiomeRivalsRules.applyCommand(combat.state, 'alice',
    attackCommand('guardian-drop-attack', 1, 'object-10', 'UNIT', 'object-20'));
  if (!attacked.accepted) throw new Error('guardian drop attack rejected: ' + attacked.code + ' ' + attacked.message);
  if (!attacked.accepted) return;
  const generated = attacked.batch.events.filter(function (event): boolean { return event.type === 'CARD_GENERATED'; })[0]!;
  TestHarness.equal(generated.payload.cardId, 'tk_012');
  TestHarness.equal(generated.payload.effectId, 'effect.or_004.01');
  TestHarness.equal(attacked.state.players[0]!.hand[0], 'tk_012');
});

TestHarness.test('Guardian reactions stop after lethal damage without creating hidden trigger markers', function (): void {
  const state = activeState('match-guardian-lethal-order', ['alice', 'bob'], ['ocean_river', 'ocean_river']);
  state.players[0]!.hand = ['or_001'];
  state.players[0]!.redstone = 1;
  state.players[0]!.redstoneCapacity = 1;
  placeUnit(state, 1, 'or_004', 0, 'object-20', 1);
  placeUnit(state, 1, 'or_004', 1, 'object-21', 1);
  placeUnit(state, 1, 'or_004', 2, 'object-22', 1);
  const deployed = BiomeRivalsRules.applyCommand(state, 'alice',
    deployCommand('guardian-lethal-deploy', 0, 'or_001', 'UNIT', 1));
  TestHarness.ok(deployed.accepted);
  if (!deployed.accepted) return;
  const moved = BiomeRivalsRules.applyCommand(deployed.state, 'alice',
    resolveChoiceCommand('guardian-lethal-move', 1, deployed.state.pendingChoice!.choiceId, 0));
  TestHarness.ok(moved.accepted);
  if (!moved.accepted) return;
  const guardianEvents = moved.batch.events.filter(function (event): boolean {
    return event.type === 'OBJECT_STATS_CHANGED' && event.payload.effectId === 'effect.or_004.01';
  });
  TestHarness.equal(guardianEvents.length, 2);
  TestHarness.equal(guardianEvents[0]!.payload.sourceInstanceId, 'object-20');
  TestHarness.equal(guardianEvents[1]!.payload.sourceInstanceId, 'object-21');
  TestHarness.equal(moved.state.players[1]!.triggeredEffectKeysThisTurn.length, 2);
  TestHarness.equal(moved.state.players[1]!.triggeredEffectKeysThisTurn.indexOf('object-22:effect.or_004.01'), -1);
  TestHarness.equal(moved.state.players[0]!.battlefield.length, 0);
});

TestHarness.test('Prismarine Shard validates an aquatic unit with movement space before payment', function (): void {
  const state = activeState('match-prismarine-invalid', ['alice', 'bob'], ['ocean_river', 'nether']);
  const actorIndex = state.players[0]!.playerId === 'alice' ? 0 : 1;
  state.activePlayerIndex = actorIndex;
  state.players[actorIndex]!.hand = ['tk_012'];
  state.players[actorIndex]!.redstone = 0;
  state.players[actorIndex]!.redstoneCapacity = 1;
  placeUnit(state, actorIndex, 'pf_001', 0, 'object-10', 1);
  const wrongTag = BiomeRivalsRules.applyCommand(state, 'alice',
    playCommand('prismarine-wrong-tag', 0, 'tk_012', 'UNIT', 'object-10'));
  TestHarness.equal(wrongTag.accepted, false);
  if (!wrongTag.accepted) TestHarness.equal(wrongTag.code, 'INVALID_TARGET');
  TestHarness.equal(state.players[actorIndex]!.hand[0], 'tk_012');
  TestHarness.equal(state.players[actorIndex]!.discardPile.length, 0);

  const blocked = activeState('match-prismarine-blocked', ['alice', 'bob'], ['ocean_river', 'nether']);
  const blockedActorIndex = blocked.players[0]!.playerId === 'alice' ? 0 : 1;
  blocked.activePlayerIndex = blockedActorIndex;
  blocked.players[blockedActorIndex]!.hand = ['tk_012'];
  placeUnit(blocked, blockedActorIndex, 'pf_001', 0, 'object-20', 1);
  placeUnit(blocked, blockedActorIndex, 'or_001', 1, 'object-21', 1);
  placeUnit(blocked, blockedActorIndex, 'pf_001', 2, 'object-22', 1);
  const noSpace = BiomeRivalsRules.applyCommand(blocked, 'alice',
    playCommand('prismarine-no-space', 0, 'tk_012', 'UNIT', 'object-21'));
  TestHarness.equal(noSpace.accepted, false);
  if (!noSpace.accepted) TestHarness.equal(noSpace.code, 'INVALID_TARGET');
  TestHarness.equal(blocked.players[blockedActorIndex]!.hand[0], 'tk_012');
  TestHarness.equal(blocked.players[blockedActorIndex]!.discardPile.length, 0);
});

TestHarness.test('Prismarine Shard forces movement then heals before Dolphin and Guardian reactions', function (): void {
  const state = activeState('match-prismarine-order', ['alice', 'bob'], ['ocean_river', 'ocean_river']);
  state.players[0]!.hand = ['tk_012'];
  state.players[0]!.redstone = 0;
  state.players[0]!.redstoneCapacity = 1;
  placeUnit(state, 0, 'or_002', 0, 'object-10', 1);
  placeUnit(state, 0, 'or_001', 1, 'object-11', 1);
  state.players[0]!.battlefield[1]!.health = 1;
  placeUnit(state, 1, 'or_004', 0, 'object-20', 1);

  const played = BiomeRivalsRules.applyCommand(state, 'alice',
    playCommand('prismarine-play', 0, 'tk_012', 'UNIT', 'object-11'));
  TestHarness.ok(played.accepted);
  if (!played.accepted) return;
  TestHarness.equal(played.batch.events[0]!.type, 'CARD_PLAYED');
  TestHarness.equal(played.batch.events[1]!.type, 'CHOICE_OFFERED');
  TestHarness.equal(played.state.pendingChoice!.sourceCardId, 'tk_012');
  TestHarness.equal(played.state.pendingChoice!.sourceInstanceId, 'effect-1');
  TestHarness.equal(played.state.pendingChoice!.effectId, 'effect.tk_012.01');
  TestHarness.equal(played.state.pendingChoice!.options.length, 1);
  TestHarness.equal(played.state.pendingChoice!.options[0]!.slotIndex, 2);
  TestHarness.equal(played.state.players[0]!.discardPile[0], 'tk_012');

  const stayed = BiomeRivalsRules.applyCommand(played.state, 'alice',
    resolveChoiceCommand('prismarine-stay', 1, played.state.pendingChoice!.choiceId, -1));
  TestHarness.equal(stayed.accepted, false);
  if (!stayed.accepted) TestHarness.equal(stayed.code, 'INVALID_CHOICE');
  TestHarness.equal(played.state.pendingChoice!.targetInstanceId, 'object-11');

  const moved = BiomeRivalsRules.applyCommand(played.state, 'alice',
    resolveChoiceCommand('prismarine-move', 1, played.state.pendingChoice!.choiceId, 0));
  TestHarness.ok(moved.accepted);
  if (!moved.accepted) return;
  TestHarness.equal(moved.batch.events[0]!.type, 'CHOICE_RESOLVED');
  TestHarness.equal(moved.batch.events[1]!.type, 'OBJECT_MOVED');
  TestHarness.equal(moved.batch.events[2]!.payload.effectId, 'effect.tk_012.01');
  TestHarness.equal(moved.batch.events[2]!.payload.reason, 'HEAL');
  TestHarness.equal(moved.batch.events[2]!.payload.health, 2);
  TestHarness.equal(moved.batch.events[3]!.payload.effectId, 'effect.or_002.01');
  TestHarness.equal(moved.batch.events[4]!.payload.effectId, 'effect.or_004.01');
  const salmon = moved.state.players[0]!.battlefield.filter(function (object): boolean {
    return object.instanceId === 'object-11';
  })[0]!;
  TestHarness.equal(salmon.slotIndex, 2);
  TestHarness.equal(salmon.health, 1);
  TestHarness.equal(salmon.attack, 2);
});

TestHarness.test('Turtle grants a live adjacent health aura without buffing itself', function (): void {
  const state = activeState('match-turtle-aura', ['alice', 'bob'], ['ocean_river', 'nether']);
  const actorIndex = state.players[0]!.playerId === 'alice' ? 0 : 1;
  const actor = state.players[actorIndex]!;
  state.activePlayerIndex = actorIndex;
  actor.hand = ['or_005'];
  actor.redstone = 4;
  actor.redstoneCapacity = 4;
  placeUnit(state, actorIndex, 'or_001', 0, 'object-10', 1);
  placeUnit(state, actorIndex, 'or_003', 2, 'object-11', 1);

  const deployed = BiomeRivalsRules.applyCommand(state, actor.playerId,
    deployCommand('turtle-aura-deploy', 0, 'or_005', 'UNIT', 1));
  TestHarness.ok(deployed.accepted);
  if (!deployed.accepted) return;
  TestHarness.equal(deployed.batch.events[0]!.type, 'CARD_DEPLOYED');
  const auraEvents = deployed.batch.events.filter(function (event): boolean {
    return event.type === 'OBJECT_STATS_CHANGED' && event.payload.effectId === 'effect.or_005.01';
  });
  TestHarness.equal(auraEvents.length, 2);
  TestHarness.equal(auraEvents[0]!.payload.instanceId, 'object-10');
  TestHarness.equal(auraEvents[1]!.payload.instanceId, 'object-11');
  const salmon = deployed.state.players[actorIndex]!.battlefield.filter(function (value): boolean {
    return value.instanceId === 'object-10';
  })[0]!;
  const drowned = deployed.state.players[actorIndex]!.battlefield.filter(function (value): boolean {
    return value.instanceId === 'object-11';
  })[0]!;
  const turtle = deployed.state.players[actorIndex]!.battlefield.filter(function (value): boolean {
    return value.cardId === 'or_005';
  })[0]!;
  TestHarness.equal(salmon.health, 3);
  TestHarness.equal(salmon.maxHealth, 3);
  TestHarness.equal(salmon.adjacencyHealthModifier, 1);
  TestHarness.equal(drowned.health, 3);
  TestHarness.equal(drowned.adjacencyHealthModifier, 1);
  TestHarness.equal(turtle.health, 6);
  TestHarness.equal(turtle.adjacencyHealthModifier, 0);
});

TestHarness.test('Losing Turtle aura during movement can kill before Prismarine healing', function (): void {
  const state = activeState('match-turtle-aura-loss', ['alice', 'bob'], ['ocean_river', 'nether']);
  const actorIndex = state.players[0]!.playerId === 'alice' ? 0 : 1;
  const actor = state.players[actorIndex]!;
  state.activePlayerIndex = actorIndex;
  actor.hand = ['or_005'];
  actor.redstone = 4;
  actor.redstoneCapacity = 4;
  placeUnit(state, actorIndex, 'or_001', 2, 'object-10', 1);
  const deployed = BiomeRivalsRules.applyCommand(state, actor.playerId,
    deployCommand('turtle-loss-deploy', 0, 'or_005', 'UNIT', 1));
  TestHarness.ok(deployed.accepted);
  if (!deployed.accepted) return;
  const salmon = deployed.state.players[actorIndex]!.battlefield.filter(function (value): boolean {
    return value.instanceId === 'object-10';
  })[0]!;
  salmon.health = 1;
  deployed.state.players[actorIndex]!.hand.push('tk_012');
  const played = BiomeRivalsRules.applyCommand(deployed.state, actor.playerId,
    playCommand('turtle-loss-shard', 1, 'tk_012', 'UNIT', salmon.instanceId));
  TestHarness.ok(played.accepted);
  if (!played.accepted) return;
  const moved = BiomeRivalsRules.applyCommand(played.state, actor.playerId,
    resolveChoiceCommand('turtle-loss-move', 2, played.state.pendingChoice!.choiceId, 0));
  TestHarness.ok(moved.accepted);
  if (!moved.accepted) return;
  TestHarness.equal(moved.batch.events[0]!.type, 'CHOICE_RESOLVED');
  TestHarness.equal(moved.batch.events[1]!.type, 'OBJECT_MOVED');
  TestHarness.equal(moved.batch.events[2]!.type, 'OBJECT_STATS_CHANGED');
  TestHarness.equal(moved.batch.events[2]!.payload.reason, 'AURA_RECALCULATED');
  TestHarness.equal(moved.batch.events[2]!.payload.health, 0);
  TestHarness.equal(moved.batch.events[3]!.type, 'OBJECT_DIED');
  TestHarness.equal(moved.batch.events.some(function (event): boolean {
    return event.type === 'OBJECT_STATS_CHANGED' && event.payload.effectId === 'effect.tk_012.01';
  }), false);
  TestHarness.equal(moved.state.players[actorIndex]!.battlefield.some(function (value): boolean {
    return value.instanceId === salmon.instanceId;
  }), false);
});

TestHarness.test('Multiple Turtle auras stack on the shared adjacent unit', function (): void {
  const state = activeState('match-turtle-aura-stack', ['alice', 'bob'], ['ocean_river', 'nether']);
  const actorIndex = state.players[0]!.playerId === 'alice' ? 0 : 1;
  const actor = state.players[actorIndex]!;
  state.activePlayerIndex = actorIndex;
  actor.hand = ['or_005'];
  actor.redstone = 4;
  actor.redstoneCapacity = 4;
  placeUnit(state, actorIndex, 'or_005', 0, 'object-10', 1);
  placeUnit(state, actorIndex, 'or_001', 1, 'object-11', 1);
  actor.battlefield[1]!.health += 1;
  actor.battlefield[1]!.maxHealth += 1;
  actor.battlefield[1]!.adjacencyHealthModifier = 1;

  const deployed = BiomeRivalsRules.applyCommand(state, actor.playerId,
    deployCommand('turtle-stack-deploy', 0, 'or_005', 'UNIT', 2));
  TestHarness.ok(deployed.accepted);
  if (!deployed.accepted) return;
  const salmon = deployed.state.players[actorIndex]!.battlefield.filter(function (value): boolean {
    return value.instanceId === 'object-11';
  })[0]!;
  TestHarness.equal(salmon.health, 4);
  TestHarness.equal(salmon.maxHealth, 4);
  TestHarness.equal(salmon.adjacencyHealthModifier, 2);
});

TestHarness.test('Woodland Nursery grows only the first friendly Animal each turn', function (): void {
  const state = activeState('match-nursery-growth', ['alice', 'bob'], ['plains_forest', 'nether']);
  const actorIndex = state.players[0]!.playerId === 'alice' ? 0 : 1;
  const opponentIndex = actorIndex === 0 ? 1 : 0;
  const actor = state.players[actorIndex]!;
  state.activePlayerIndex = actorIndex;
  actor.hand = ['db_001', 'pf_002', 'pf_001', 'pf_002'];
  actor.redstone = 10;
  actor.redstoneCapacity = 10;
  placeBuilding(state, actorIndex, 'pf_005', 0, 'object-10');

  const nonAnimal = BiomeRivalsRules.applyCommand(state, actor.playerId,
    deployCommand('nursery-non-animal', 0, 'db_001', 'UNIT', 0));
  TestHarness.equal(nonAnimal.accepted, true, JSON.stringify(nonAnimal));
  if (!nonAnimal.accepted) return;
  TestHarness.equal(nonAnimal.state.players[actorIndex]!.triggeredEffectKeysThisTurn.length, 0);

  const first = BiomeRivalsRules.applyCommand(nonAnimal.state, actor.playerId,
    deployCommand('nursery-first-animal', 1, 'pf_002', 'UNIT', 1));
  TestHarness.equal(first.accepted, true, JSON.stringify(first));
  if (!first.accepted) return;
  const sheep = first.state.players[actorIndex]!.battlefield.filter(function (value): boolean {
    return value.cardId === 'pf_002';
  })[0]!;
  TestHarness.equal(sheep.health, 4);
  TestHarness.equal(sheep.maxHealth, 4);
  TestHarness.equal(first.batch.events[0]!.type, 'CARD_DEPLOYED');
  TestHarness.equal(first.batch.events[1]!.type, 'OBJECT_STATS_CHANGED');
  TestHarness.equal(first.batch.events[1]!.payload.sourceCardId, 'pf_005');
  TestHarness.equal(first.batch.events[1]!.payload.sourceInstanceId, 'object-10');
  TestHarness.equal(first.batch.events[1]!.payload.effectId, 'effect.pf_005.01');
  TestHarness.equal(first.batch.events[1]!.payload.reason, 'PERMANENT_HEALTH_MODIFIER');
  TestHarness.equal(first.state.players[actorIndex]!.triggeredEffectKeysThisTurn[0],
    'object-10:effect.pf_005.01');

  const second = BiomeRivalsRules.applyCommand(first.state, actor.playerId,
    deployCommand('nursery-second-animal', 2, 'pf_001', 'UNIT', 2));
  TestHarness.equal(second.accepted, true, JSON.stringify(second));
  if (!second.accepted) return;
  const bee = second.state.players[actorIndex]!.battlefield.filter(function (value): boolean {
    return value.cardId === 'pf_001';
  })[0]!;
  TestHarness.equal(bee.maxHealth, 2);
  TestHarness.equal(second.batch.events.filter(function (event): boolean {
    return event.payload.effectId === 'effect.pf_005.01';
  }).length, 0);

  const ended = BiomeRivalsRules.applyCommand(second.state, actor.playerId,
    command('nursery-end-owner', 3, 'END_TURN'));
  TestHarness.ok(ended.accepted);
  if (!ended.accepted) return;
  const returned = BiomeRivalsRules.applyCommand(ended.state,
    ended.state.players[opponentIndex]!.playerId, command('nursery-end-opponent', 4, 'END_TURN'));
  TestHarness.ok(returned.accepted);
  if (!returned.accepted) return;
  const returnedActor = returned.state.players[actorIndex]!;
  TestHarness.equal(returnedActor.triggeredEffectKeysThisTurn.length, 0);
  returnedActor.redstone = 10;
  const nextTurn = BiomeRivalsRules.applyCommand(returned.state, returnedActor.playerId,
    deployCommand('nursery-next-turn-animal', 5, 'pf_002', 'UNIT', 3));
  TestHarness.equal(nextTurn.accepted, true, JSON.stringify(nextTurn));
  if (!nextTurn.accepted) return;
  const nextSheep = nextTurn.state.players[actorIndex]!.battlefield.filter(function (value): boolean {
    return value.cardId === 'pf_002' && value.slotIndex === 3;
  })[0]!;
  TestHarness.equal(nextSheep.maxHealth, 4);
  TestHarness.equal(nextTurn.batch.events.filter(function (event): boolean {
    return event.payload.effectId === 'effect.pf_005.01';
  }).length, 1);
});

TestHarness.test('Woodland Nurseries stack before Coral Reef in stable source order', function (): void {
  const state = activeState('match-nursery-coral-stack', ['alice', 'bob'], ['plains_forest', 'nether']);
  const actorIndex = state.players[0]!.playerId === 'alice' ? 0 : 1;
  const actor = state.players[actorIndex]!;
  state.activePlayerIndex = actorIndex;
  actor.hand = ['or_005'];
  actor.redstone = 10;
  actor.redstoneCapacity = 10;
  placeBuilding(state, actorIndex, 'pf_005', 1, 'object-11');
  placeBuilding(state, actorIndex, 'or_007', 2, 'object-12');
  placeBuilding(state, actorIndex, 'pf_005', 0, 'object-10');

  const result = BiomeRivalsRules.applyCommand(state, actor.playerId,
    deployCommand('nursery-coral-animal', 0, 'or_005', 'UNIT', 0));
  TestHarness.equal(result.accepted, true, JSON.stringify(result));
  if (!result.accepted) return;
  const turtle = result.state.players[actorIndex]!.battlefield.filter(function (value): boolean {
    return value.cardId === 'or_005';
  })[0]!;
  TestHarness.equal(turtle.health, 9);
  TestHarness.equal(turtle.maxHealth, 9);
  const growthEvents = result.batch.events.filter(function (event): boolean {
    return event.type === 'OBJECT_STATS_CHANGED' && event.payload.reason === 'PERMANENT_HEALTH_MODIFIER';
  });
  TestHarness.equal(growthEvents.length, 3);
  TestHarness.equal(growthEvents[0]!.payload.effectId, 'effect.pf_005.01');
  TestHarness.equal(growthEvents[0]!.payload.sourceInstanceId, 'object-10');
  TestHarness.equal(growthEvents[1]!.payload.effectId, 'effect.pf_005.01');
  TestHarness.equal(growthEvents[1]!.payload.sourceInstanceId, 'object-11');
  TestHarness.equal(growthEvents[2]!.payload.effectId, 'effect.or_007.01');
  TestHarness.equal(growthEvents[2]!.payload.sourceInstanceId, 'object-12');
  TestHarness.equal(growthEvents[2]!.payload.maxHealth, 9);
});

TestHarness.test('Coral Reef permanently grows only the first friendly aquatic unit each turn', function (): void {
  const state = activeState('match-coral-growth', ['alice', 'bob'], ['ocean_river', 'nether']);
  const actorIndex = state.players[0]!.playerId === 'alice' ? 0 : 1;
  const actor = state.players[actorIndex]!;
  state.activePlayerIndex = actorIndex;
  actor.hand = ['or_003', 'or_003'];
  actor.redstone = 10;
  actor.redstoneCapacity = 10;
  placeBuilding(state, actorIndex, 'or_007', 0, 'object-10');

  const first = BiomeRivalsRules.applyCommand(state, actor.playerId,
    deployCommand('coral-first-aquatic', 0, 'or_003', 'UNIT', 0));
  TestHarness.ok(first.accepted);
  if (!first.accepted) return;
  const firstDrowned = first.state.players[actorIndex]!.battlefield.filter(function (value): boolean {
    return value.cardId === 'or_003';
  })[0]!;
  TestHarness.equal(firstDrowned.health, 3);
  TestHarness.equal(firstDrowned.maxHealth, 3);
  TestHarness.equal(first.batch.events[0]!.type, 'CARD_DEPLOYED');
  TestHarness.equal(first.batch.events[1]!.type, 'OBJECT_STATS_CHANGED');
  TestHarness.equal(first.batch.events[1]!.payload.reason, 'PERMANENT_HEALTH_MODIFIER');
  TestHarness.equal(first.batch.events[1]!.payload.sourceInstanceId, 'object-10');
  TestHarness.equal(first.state.players[actorIndex]!.triggeredEffectKeysThisTurn[0],
    'object-10:effect.or_007.01');
  const snapshot = BiomeRivalsRules.createClientSnapshot(first.state, actor.playerId);
  TestHarness.equal(snapshot.players[actorIndex]!.triggeredEffectKeysThisTurn[0],
    'object-10:effect.or_007.01');

  const second = BiomeRivalsRules.applyCommand(first.state, actor.playerId,
    deployCommand('coral-second-aquatic', 1, 'or_003', 'UNIT', 1));
  TestHarness.ok(second.accepted);
  if (!second.accepted) return;
  const drowned = second.state.players[actorIndex]!.battlefield.filter(function (value): boolean {
    return value.cardId === 'or_003';
  }).sort(function (left, right): number { return left.slotIndex - right.slotIndex; });
  TestHarness.equal(drowned[0]!.maxHealth, 3);
  TestHarness.equal(drowned[1]!.maxHealth, 2);
  TestHarness.equal(second.batch.events.filter(function (event): boolean {
    return event.payload.effectId === 'effect.or_007.01';
  }).length, 0);
});

TestHarness.test('Multiple Coral Reefs stack in stable order and become ready next turn', function (): void {
  const state = activeState('match-coral-stack', ['alice', 'bob'], ['ocean_river', 'nether']);
  const actorIndex = state.players[0]!.playerId === 'alice' ? 0 : 1;
  const opponentIndex = actorIndex === 0 ? 1 : 0;
  const actor = state.players[actorIndex]!;
  state.activePlayerIndex = actorIndex;
  actor.hand = ['or_003'];
  actor.redstone = 10;
  actor.redstoneCapacity = 10;
  placeBuilding(state, actorIndex, 'or_007', 1, 'object-11');
  placeBuilding(state, actorIndex, 'or_007', 0, 'object-10');

  const first = BiomeRivalsRules.applyCommand(state, actor.playerId,
    deployCommand('coral-stack-first', 0, 'or_003', 'UNIT', 0));
  TestHarness.ok(first.accepted);
  if (!first.accepted) return;
  const growthEvents = first.batch.events.filter(function (event): boolean {
    return event.type === 'OBJECT_STATS_CHANGED' && event.payload.effectId === 'effect.or_007.01';
  });
  TestHarness.equal(growthEvents.length, 2);
  TestHarness.equal(growthEvents[0]!.payload.sourceInstanceId, 'object-10');
  TestHarness.equal(growthEvents[1]!.payload.sourceInstanceId, 'object-11');
  TestHarness.equal(growthEvents[1]!.payload.maxHealth, 4);

  const ended = BiomeRivalsRules.applyCommand(first.state, actor.playerId, command('coral-end-owner', 1, 'END_TURN'));
  TestHarness.ok(ended.accepted);
  if (!ended.accepted) return;
  const returned = BiomeRivalsRules.applyCommand(ended.state,
    ended.state.players[opponentIndex]!.playerId, command('coral-end-opponent', 2, 'END_TURN'));
  TestHarness.ok(returned.accepted);
  if (!returned.accepted) return;
  const returnedActor = returned.state.players[actorIndex]!;
  TestHarness.equal(returnedActor.triggeredEffectKeysThisTurn.length, 0);
  returnedActor.hand = ['or_003'];
  returnedActor.redstone = 10;
  const second = BiomeRivalsRules.applyCommand(returned.state, returnedActor.playerId,
    deployCommand('coral-stack-next-turn', 3, 'or_003', 'UNIT', 1));
  TestHarness.ok(second.accepted);
  if (!second.accepted) return;
  const nextTurnGrowth = second.batch.events.filter(function (event): boolean {
    return event.type === 'OBJECT_STATS_CHANGED' && event.payload.effectId === 'effect.or_007.01';
  });
  TestHarness.equal(nextTurnGrowth.length, 2);
  TestHarness.equal(nextTurnGrowth[1]!.payload.maxHealth, 4);
});

TestHarness.test('Tamed Wolf permanently gains health beside a friendly Animal', function (): void {
  const state = activeState('match-tamed-wolf-adjacent', ['alice', 'bob'], ['plains_forest', 'nether']);
  const actorIndex = state.players[0]!.playerId === 'alice' ? 0 : 1;
  const actor = state.players[actorIndex]!;
  state.activePlayerIndex = actorIndex;
  actor.hand = ['pf_003'];
  actor.redstone = 10;
  actor.redstoneCapacity = 10;
  placeUnit(state, actorIndex, 'pf_002', 1, 'object-10', 1);

  const deployed = BiomeRivalsRules.applyCommand(state, actor.playerId,
    deployCommand('tamed-wolf-adjacent', 0, 'pf_003', 'UNIT', 2));
  TestHarness.ok(deployed.accepted);
  if (!deployed.accepted) return;
  const wolf = deployed.state.players[actorIndex]!.battlefield.filter(function (value): boolean {
    return value.cardId === 'pf_003';
  })[0]!;
  TestHarness.equal(wolf.health, 3);
  TestHarness.equal(wolf.maxHealth, 3);
  TestHarness.equal(deployed.batch.events.length, 2);
  TestHarness.equal(deployed.batch.events[0]!.type, 'CARD_DEPLOYED');
  TestHarness.equal(deployed.batch.events[1]!.type, 'OBJECT_STATS_CHANGED');
  TestHarness.equal(deployed.batch.events[1]!.payload.effectId, 'effect.pf_003.01');
  TestHarness.equal(deployed.batch.events[1]!.payload.sourceInstanceId, wolf.instanceId);
  TestHarness.equal(deployed.batch.events[1]!.payload.reason, 'PERMANENT_HEALTH_MODIFIER');
  TestHarness.equal(deployed.batch.events[1]!.payload.maxHealth, 3);
  const snapshot = BiomeRivalsRules.createClientSnapshot(deployed.state, actor.playerId);
  TestHarness.equal(snapshot.players[actorIndex]!.battlefield.filter(function (value): boolean {
    return value.cardId === 'pf_003';
  })[0]!.maxHealth, 3);
});

TestHarness.test('Tamed Wolf triggers once with two Animals and ignores nonadjacent or enemy Animals', function (): void {
  const doubleState = activeState('match-tamed-wolf-double', ['alice', 'bob'], ['plains_forest', 'nether']);
  const doubleActorIndex = doubleState.players[0]!.playerId === 'alice' ? 0 : 1;
  const doubleActor = doubleState.players[doubleActorIndex]!;
  doubleState.activePlayerIndex = doubleActorIndex;
  doubleActor.hand = ['pf_003'];
  doubleActor.redstone = 10;
  doubleActor.redstoneCapacity = 10;
  placeUnit(doubleState, doubleActorIndex, 'pf_001', 0, 'object-10', 1);
  placeUnit(doubleState, doubleActorIndex, 'pf_002', 2, 'object-11', 1);
  const doubleResult = BiomeRivalsRules.applyCommand(doubleState, doubleActor.playerId,
    deployCommand('tamed-wolf-double', 0, 'pf_003', 'UNIT', 1));
  TestHarness.equal(doubleResult.accepted, true, JSON.stringify(doubleResult));
  if (!doubleResult.accepted) return;
  const doubleWolf = doubleResult.state.players[doubleActorIndex]!.battlefield.filter(function (value): boolean {
    return value.cardId === 'pf_003';
  })[0]!;
  TestHarness.equal(doubleWolf.maxHealth, 3);
  TestHarness.equal(doubleResult.batch.events.filter(function (event): boolean {
    return event.payload.effectId === 'effect.pf_003.01';
  }).length, 1);

  const ignoredState = activeState('match-tamed-wolf-ignored', ['alice', 'bob'], ['plains_forest', 'nether']);
  const ignoredActorIndex = ignoredState.players[0]!.playerId === 'alice' ? 0 : 1;
  const ignoredOpponentIndex = ignoredActorIndex === 0 ? 1 : 0;
  const ignoredActor = ignoredState.players[ignoredActorIndex]!;
  ignoredState.activePlayerIndex = ignoredActorIndex;
  ignoredActor.hand = ['pf_003'];
  ignoredActor.redstone = 10;
  ignoredActor.redstoneCapacity = 10;
  placeUnit(ignoredState, ignoredActorIndex, 'pf_002', 0, 'object-20', 1);
  placeUnit(ignoredState, ignoredActorIndex, 'pf_004', 1, 'object-21', 1);
  placeUnit(ignoredState, ignoredOpponentIndex, 'pf_002', 2, 'object-22', 1);
  const ignoredResult = BiomeRivalsRules.applyCommand(ignoredState, ignoredActor.playerId,
    deployCommand('tamed-wolf-ignored', 0, 'pf_003', 'UNIT', 2));
  TestHarness.equal(ignoredResult.accepted, true, JSON.stringify(ignoredResult));
  if (!ignoredResult.accepted) return;
  const ignoredWolf = ignoredResult.state.players[ignoredActorIndex]!.battlefield.filter(function (value): boolean {
    return value.cardId === 'pf_003';
  })[0]!;
  TestHarness.equal(ignoredWolf.maxHealth, 2);
  TestHarness.equal(ignoredResult.batch.events.filter(function (event): boolean {
    return event.payload.effectId === 'effect.pf_003.01';
  }).length, 0);
});

TestHarness.test('Ocean Monument damages only isolated enemy units before turn end', function (): void {
  const state = activeState('match-monument-isolation', ['alice', 'bob'], ['ocean_river', 'nether']);
  const actorIndex = state.players[0]!.playerId === 'alice' ? 0 : 1;
  const opponentIndex = actorIndex === 0 ? 1 : 0;
  const actor = state.players[actorIndex]!;
  state.activePlayerIndex = actorIndex;
  placeBuilding(state, actorIndex, 'or_008', 0, 'object-10');
  placeUnit(state, opponentIndex, 'pf_001', 0, 'object-20', 1);
  placeUnit(state, opponentIndex, 'pf_001', 1, 'object-21', 1);
  placeUnit(state, opponentIndex, 'or_003', 3, 'object-22', 1);

  const ended = BiomeRivalsRules.applyCommand(state, actor.playerId, command('monument-isolation-end', 0, 'END_TURN'));
  TestHarness.ok(ended.accepted);
  if (!ended.accepted) return;
  const opponent = ended.state.players[opponentIndex]!;
  TestHarness.equal(opponent.battlefield.filter(function (value): boolean { return value.instanceId === 'object-20'; })[0]!.health, 2);
  TestHarness.equal(opponent.battlefield.filter(function (value): boolean { return value.instanceId === 'object-21'; })[0]!.health, 2);
  TestHarness.equal(opponent.battlefield.filter(function (value): boolean { return value.instanceId === 'object-22'; })[0]!.health, 1);
  TestHarness.equal(ended.batch.events[0]!.type, 'OBJECT_STATS_CHANGED');
  TestHarness.equal(ended.batch.events[0]!.payload.instanceId, 'object-22');
  TestHarness.equal(ended.batch.events[0]!.payload.sourceInstanceId, 'object-10');
  TestHarness.equal(ended.batch.events[0]!.payload.effectId, 'effect.or_008.01');
  TestHarness.equal(ended.batch.events[1]!.type, 'TURN_ENDED');
});

TestHarness.test('Ocean Monument lethal damage settles enemy loot before TURN_ENDED', function (): void {
  const state = activeState('match-monument-lethal', ['alice', 'bob'], ['ocean_river', 'desert_badlands']);
  const actorIndex = state.players[0]!.playerId === 'alice' ? 0 : 1;
  const opponentIndex = actorIndex === 0 ? 1 : 0;
  const actor = state.players[actorIndex]!;
  state.activePlayerIndex = actorIndex;
  placeBuilding(state, actorIndex, 'or_008', 0, 'object-10');
  placeUnit(state, opponentIndex, 'db_001', 3, 'object-20', 1);

  const ended = BiomeRivalsRules.applyCommand(state, actor.playerId, command('monument-lethal-end', 0, 'END_TURN'));
  TestHarness.ok(ended.accepted);
  if (!ended.accepted) return;
  const eventTypes = ended.batch.events.map(function (event): string { return event.type; });
  TestHarness.equal(eventTypes.slice(0, 4).join(','),
    'OBJECT_STATS_CHANGED,OBJECT_DIED,CARD_GENERATED,TURN_ENDED');
  TestHarness.equal(ended.batch.events[0]!.payload.health, 0);
  TestHarness.equal(ended.batch.events[1]!.payload.instanceId, 'object-20');
  TestHarness.equal(ended.batch.events[2]!.payload.cardId, 'tk_005');
  TestHarness.equal(ended.state.players[actorIndex]!.hand.indexOf('tk_005') >= 0, true);
  TestHarness.equal(ended.state.players[opponentIndex]!.battlefield.length, 0);
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
