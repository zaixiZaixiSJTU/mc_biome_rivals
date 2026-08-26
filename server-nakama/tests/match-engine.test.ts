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
  slotIndex: number
): BiomeRivalsRules.MatchCommand {
  const result = command(id, revision, 'DEPLOY_CARD');
  result.payload = { cardId: cardId, slotKind: slotKind, slotIndex: slotIndex };
  return result;
}

TestHarness.test('creates a valid two-player initial state', function (): void {
  const state = BiomeRivalsRules.createInitialState('match-1', ['alice', 'bob']);
  TestHarness.equal(state.revision, 0);
  TestHarness.equal(state.protocolVersion, BiomeRivalsRules.PROTOCOL_VERSION);
  TestHarness.equal(state.players[0]!.life, 30);
  TestHarness.equal(state.players[0]!.redstone, 6);
  TestHarness.equal(state.players[0]!.hand.length, 5);
  TestHarness.equal(state.players[0]!.unitSlots.length, 4);
  TestHarness.equal(state.players[0]!.buildingSlots.length, 3);
  TestHarness.equal(BiomeRivalsRules.validateState(state).length, 0);
});

TestHarness.test('deploys a registered unit and emits replayable state data', function (): void {
  const state = BiomeRivalsRules.createInitialState('match-1', ['alice', 'bob']);
  const result = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-1', 0, 'pf_001', 'UNIT', 2));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.players[0]!.unitSlots[2], 'pf_001');
  TestHarness.equal(result.state.players[0]!.hand.indexOf('pf_001'), -1);
  TestHarness.equal(result.state.players[0]!.redstone, 5);
  TestHarness.equal(result.batch.events.length, 1);
  TestHarness.equal(result.batch.events[0]!.type, 'CARD_DEPLOYED');
  TestHarness.equal(result.batch.events[0]!.payload.slotIndex, 2);
  TestHarness.equal(result.batch.events[0]!.payload.redstone, 5);
  TestHarness.equal(state.players[0]!.unitSlots[2], null, 'accepted commands must not mutate their input state');
});

TestHarness.test('deploys a structure only across consecutive free building slots', function (): void {
  const state = BiomeRivalsRules.createInitialState('match-1', ['alice', 'bob']);
  state.players[0]!.hand = ['db_007'];
  const invalid = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-1', 0, 'db_007', 'BUILDING', 2));
  TestHarness.equal(invalid.accepted, false);
  if (!invalid.accepted) TestHarness.equal(invalid.code, 'INVALID_TARGET');

  const accepted = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-2', 0, 'db_007', 'BUILDING', 1));
  TestHarness.equal(accepted.accepted, true);
  if (!accepted.accepted) return;
  TestHarness.equal(accepted.state.players[0]!.buildingSlots[1], 'db_007');
  TestHarness.equal(accepted.state.players[0]!.buildingSlots[2], 'db_007');
  TestHarness.equal(accepted.batch.events[0]!.payload.occupiedSlots, 2);
});

TestHarness.test('rejects invalid deploys without spending redstone or moving cards', function (): void {
  const state = BiomeRivalsRules.createInitialState('match-1', ['alice', 'bob']);
  const wrongRow = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-1', 0, 'pf_001', 'BUILDING', 0));
  TestHarness.equal(wrongRow.accepted, false);
  if (!wrongRow.accepted) TestHarness.equal(wrongRow.code, 'INVALID_TARGET');
  TestHarness.equal(state.players[0]!.redstone, 6);
  TestHarness.ok(state.players[0]!.hand.indexOf('pf_001') >= 0);

  state.players[0]!.redstone = 0;
  const unaffordable = BiomeRivalsRules.applyCommand(state, 'alice', deployCommand('deploy-2', 0, 'pf_001', 'UNIT', 0));
  TestHarness.equal(unaffordable.accepted, false);
  if (!unaffordable.accepted) TestHarness.equal(unaffordable.code, 'INSUFFICIENT_REDSTONE');
});

TestHarness.test('rejects a command from the inactive player without mutation', function (): void {
  const state = BiomeRivalsRules.createInitialState('match-1', ['alice', 'bob']);
  const result = BiomeRivalsRules.applyCommand(state, 'bob', command('cmd-1', 0, 'END_TURN'));
  TestHarness.equal(result.accepted, false);
  if (!result.accepted) TestHarness.equal(result.code, 'NOT_ACTIVE_PLAYER');
  TestHarness.equal(state.revision, 0);
});

TestHarness.test('ends a turn and emits an ordered event batch', function (): void {
  const state = BiomeRivalsRules.createInitialState('match-1', ['alice', 'bob']);
  const result = BiomeRivalsRules.applyCommand(state, 'alice', command('cmd-1', 0, 'END_TURN'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.revision, 1);
  TestHarness.equal(result.state.activePlayerIndex, 1);
  TestHarness.equal(result.batch.events.length, 2);
  TestHarness.equal(result.batch.events[0]!.type, 'TURN_ENDED');
  TestHarness.equal(result.batch.events[1]!.type, 'TURN_STARTED');
  TestHarness.equal(result.batch.events[1]!.eventId, result.batch.events[0]!.eventId + 1);
  TestHarness.equal(result.state.players[1]!.redstoneCapacity, 6);
  TestHarness.equal(result.state.players[1]!.redstone, 6);
  TestHarness.equal(result.batch.events[1]!.payload.activePlayerIndex, 1);
  TestHarness.equal(result.batch.events[1]!.payload.redstoneCapacity, 6);
});

TestHarness.test('rejects stale revisions', function (): void {
  const state = BiomeRivalsRules.createInitialState('match-1', ['alice', 'bob']);
  const result = BiomeRivalsRules.applyCommand(state, 'alice', command('cmd-1', 99, 'END_TURN'));
  TestHarness.equal(result.accepted, false);
  if (!result.accepted) TestHarness.equal(result.code, 'REVISION_MISMATCH');
});

TestHarness.test('records a concession and winner', function (): void {
  const state = BiomeRivalsRules.createInitialState('match-1', ['alice', 'bob']);
  const result = BiomeRivalsRules.applyCommand(state, 'alice', command('cmd-1', 0, 'CONCEDE'));
  TestHarness.equal(result.accepted, true);
  if (!result.accepted) return;
  TestHarness.equal(result.state.status, 'FINISHED');
  TestHarness.equal(result.state.winnerPlayerId, 'bob');
  TestHarness.equal(result.batch.events[1]!.type, 'MATCH_ENDED');
});

TestHarness.test('rejects duplicate command ids after acceptance', function (): void {
  const initial = BiomeRivalsRules.createInitialState('match-1', ['alice', 'bob']);
  const first = BiomeRivalsRules.applyCommand(initial, 'alice', command('cmd-1', 0, 'END_TURN'));
  TestHarness.ok(first.accepted);
  if (!first.accepted) return;
  const duplicate = BiomeRivalsRules.applyCommand(first.state, 'bob', command('cmd-1', 1, 'END_TURN'));
  TestHarness.equal(duplicate.accepted, false);
  if (!duplicate.accepted) TestHarness.equal(duplicate.code, 'DUPLICATE_COMMAND');
});

TestHarness.finish();
