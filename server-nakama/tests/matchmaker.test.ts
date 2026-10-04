function matchmakerResult(userId: string, factionId: string): nkruntime.MatchmakerResult {
  return {
    presence: { userId: userId } as nkruntime.Presence,
    properties: { factionId: factionId }
  } as nkruntime.MatchmakerResult;
}

TestHarness.test('matchmaker creates a two-player authoritative match', function (): void {
  let createdModule = '';
  let createdParams: { [key: string]: unknown } | undefined;
  const fakeNakama = {
    matchCreate: function (module: string, params?: { [key: string]: unknown }): string {
      createdModule = module;
      createdParams = params;
      return 'authoritative-match-id';
    }
  } as unknown as nkruntime.Nakama;
  const fakeLogger = {
    info: function (): void { }
  } as unknown as nkruntime.Logger;

  const matchId = biomeRivalsMatchmakerMatched(
    {} as nkruntime.Context,
    fakeLogger,
    fakeNakama,
    [matchmakerResult('alice', 'ocean_river'), matchmakerResult('bob', 'end')]
  );

  TestHarness.equal(matchId, 'authoritative-match-id');
  TestHarness.equal(createdModule, 'biome_rivals');
  TestHarness.equal(createdParams?.mode, 'prototype');
  TestHarness.equal(createdParams?.matchedPlayerCount, 2);
  const factions = JSON.parse(String(createdParams?.playerFactions)) as Array<{ playerId: string; factionId: string }>;
  TestHarness.equal(factions[0]!.playerId, 'alice');
  TestHarness.equal(factions[0]!.factionId, 'ocean_river');
  TestHarness.equal(factions[1]!.playerId, 'bob');
  TestHarness.equal(factions[1]!.factionId, 'end');
});

TestHarness.test('arena audit mode config is server-only and exact-gated', function (): void {
  const logger = { info: function (): void { } } as unknown as nkruntime.Logger;
  const nk = {} as nkruntime.Nakama;
  const clientParams = { arenaId: 'deep_caverns', mode: 'deep_caverns' };
  for (const gate of [undefined, 'false', 'True', '1']) {
    const ctx = { env: { BIOME_RIVALS_ENABLE_TEST_FIXTURES: gate, BIOME_RIVALS_TEST_ARENA_ID: 'deep_caverns' } } as unknown as nkruntime.Context;
    TestHarness.equal(biomeRivalsMatchInit(ctx, logger, nk, clientParams).state.arenaId, 'standard_meadow');
  }
  TestHarness.equal(biomeRivalsMatchInit({} as nkruntime.Context, logger, nk, clientParams).state.arenaId, 'standard_meadow');
  for (const arenaId of Object.keys(BiomeRivalsRules.ARENA_LAYOUTS)) {
    const ctx = { env: { BIOME_RIVALS_ENABLE_TEST_FIXTURES: 'true', BIOME_RIVALS_TEST_ARENA_ID: arenaId } } as unknown as nkruntime.Context;
    TestHarness.equal(biomeRivalsMatchInit(ctx, logger, nk, clientParams).state.arenaId, arenaId);
  }
  TestHarness.equal(biomeRivalsMatchInit({ env: { BIOME_RIVALS_ENABLE_TEST_FIXTURES: 'true' } } as unknown as nkruntime.Context,
    logger, nk, clientParams).state.arenaId, 'standard_meadow');
  let rejected = false;
  try {
    biomeRivalsMatchInit({ env: { BIOME_RIVALS_ENABLE_TEST_FIXTURES: 'true', BIOME_RIVALS_TEST_ARENA_ID: 'unknown' } } as unknown as nkruntime.Context,
      logger, nk, clientParams);
  } catch (_) { rejected = true; }
  TestHarness.equal(rejected, true);
});

TestHarness.test('match init preserves validated faction selections by player id', function (): void {
  const initialized = biomeRivalsMatchInit(
    {} as nkruntime.Context,
    { info: function (): void { } } as unknown as nkruntime.Logger,
    {} as nkruntime.Nakama,
    { playerFactions: JSON.stringify([
      { playerId: 'alice', factionId: 'snow_ice' },
      { playerId: 'bob', factionId: 'desert_badlands' }
    ]) }
  );
  TestHarness.equal(initialized.state.factionByPlayerId.alice, 'snow_ice');
  TestHarness.equal(initialized.state.factionByPlayerId.bob, 'desert_badlands');
  TestHarness.equal(initialized.state.arenaId, BiomeRivalsRules.DEFAULT_ARENA_ID);
});

TestHarness.test('assigned match rejects a player outside the validated faction map', function (): void {
  const initialized = biomeRivalsMatchInit(
    {} as nkruntime.Context,
    { info: function (): void { } } as unknown as nkruntime.Logger,
    {} as nkruntime.Nakama,
    { playerFactions: JSON.stringify([
      { playerId: 'alice', factionId: 'snow_ice' },
      { playerId: 'bob', factionId: 'desert_badlands' }
    ]) }
  );
  const attempt = biomeRivalsMatchJoinAttempt(
    {} as nkruntime.Context,
    {} as nkruntime.Logger,
    {} as nkruntime.Nakama,
    {} as nkruntime.MatchDispatcher,
    0,
    initialized.state,
    { userId: 'mallory', sessionId: 'session-mallory' } as nkruntime.Presence,
    {}
  );

  TestHarness.equal(attempt.accept, false);
  TestHarness.equal(attempt.rejectMessage, 'player was not assigned to this match');
});

TestHarness.test('same player session can replace a stale presence and receives a private recovery snapshot', function (): void {
  const logger = { info: function (): void { } } as unknown as nkruntime.Logger;
  const fakeNakama = { uuidv4: function (): string { return 'reconnect-seed'; } } as unknown as nkruntime.Nakama;
  const broadcasts: Array<{ opCode: number; data: string; recipients: nkruntime.Presence[] }> = [];
  const dispatcher = {
    broadcastMessage: function (
      opCode: number,
      data: string,
      recipients: nkruntime.Presence[]
    ): void {
      broadcasts.push({ opCode: opCode, data: data, recipients: recipients });
    }
  } as unknown as nkruntime.MatchDispatcher;
  const initialized = biomeRivalsMatchInit(
    { matchId: 'reconnect-match' } as nkruntime.Context,
    logger,
    fakeNakama,
    { playerFactions: JSON.stringify([
      { playerId: 'alice', factionId: 'cave_dark_forest' },
      { playerId: 'bob', factionId: 'nether' }
    ]) }
  );
  const aliceOld = { userId: 'alice', sessionId: 'alice-old' } as nkruntime.Presence;
  const bob = { userId: 'bob', sessionId: 'bob-session' } as nkruntime.Presence;
  let state = biomeRivalsMatchJoin(
    { matchId: 'reconnect-match' } as nkruntime.Context,
    logger,
    fakeNakama,
    dispatcher,
    0,
    initialized.state,
    [aliceOld, bob]
  ).state;
  if (state.game === null) throw new Error('test match did not initialize');
  state.game.revision = 7;
  state.game.lastEventId = 19;
  state.game.players[0]!.heroLifeLostThisTurn = true;
  state.game.players[state.game.activePlayerIndex]!.temporaryRedstone = 2;
  TestHarness.equal(BiomeRivalsRules.trySpendRedstone(state.game.players[state.game.activePlayerIndex]!, 1), true);
  state.game.pendingChoice = {
    choiceId: 'choice-reconnect',
    playerId: 'alice',
    sourceCardId: 'cd_002',
    sourceInstanceId: 'object-7',
    effectId: 'effect.cd_002.01',
    kind: 'TOP_CARD_SCRY',
    targetPlayerId: 'alice',
    targetInstanceId: '',
    options: [{ optionIndex: 0, cardId: state.game.players[0]!.hand[0]!, slotIndex: -1, selectable: true }]
  };
  broadcasts.length = 0;

  const aliceNew = { userId: 'alice', sessionId: 'alice-new' } as nkruntime.Presence;
  const attempt = biomeRivalsMatchJoinAttempt(
    {} as nkruntime.Context,
    logger,
    fakeNakama,
    dispatcher,
    1,
    state,
    aliceNew,
    {}
  );
  TestHarness.equal(attempt.accept, true);

  state = biomeRivalsMatchJoin(
    { matchId: 'reconnect-match' } as nkruntime.Context,
    logger,
    fakeNakama,
    dispatcher,
    1,
    state,
    [aliceNew]
  ).state;
  TestHarness.equal(Object.keys(state.presences).length, 2);
  TestHarness.equal(state.presences['alice-old'], undefined);
  TestHarness.equal(state.presences['alice-new']!.userId, 'alice');
  TestHarness.equal(broadcasts.length, 1);
  TestHarness.equal(broadcasts[0]!.opCode, 4);
  TestHarness.equal(broadcasts[0]!.recipients[0]!.sessionId, 'alice-new');
  const snapshot = JSON.parse(broadcasts[0]!.data) as BiomeRivalsRules.MatchSnapshot;
  TestHarness.equal(snapshot.viewerPlayerId, 'alice');
  TestHarness.equal(snapshot.revision, 7);
  TestHarness.equal(snapshot.lastEventId, 19);
  TestHarness.equal(snapshot.players[0]!.heroLifeLostThisTurn, true);
  TestHarness.equal(snapshot.players[1]!.heroLifeLostThisTurn, false);
  TestHarness.equal(snapshot.players[state.game!.activePlayerIndex]!.temporaryRedstone, 1);
  TestHarness.equal(snapshot.players[state.game!.activePlayerIndex]!.totalRedstone,
    snapshot.players[state.game!.activePlayerIndex]!.redstone + 1);
  TestHarness.equal(snapshot.pendingChoice!.choiceId, 'choice-reconnect');
  TestHarness.equal(snapshot.pendingChoice!.options[0]!.cardId, state.game!.players[0]!.hand[0]!);
  TestHarness.equal(snapshot.players[1]!.hand[0], null);
  TestHarness.equal(JSON.stringify(snapshot).indexOf('processedCommandIds'), -1);
});

TestHarness.test('Wool temporary health survives the private snapshot after session replacement', function (): void {
  const logger = { info: function (): void { } } as unknown as nkruntime.Logger;
  const fakeNakama = { uuidv4: function (): string { return 'wool-reconnect-seed'; } } as unknown as nkruntime.Nakama;
  const broadcasts: Array<{ opCode: number; data: string; recipients: nkruntime.Presence[] }> = [];
  const dispatcher = {
    broadcastMessage: function (opCode: number, data: string, recipients: nkruntime.Presence[]): void {
      broadcasts.push({ opCode: opCode, data: data, recipients: recipients });
    }
  } as unknown as nkruntime.MatchDispatcher;
  const ctx = { matchId: 'wool-reconnect-match' } as nkruntime.Context;
  const initialized = biomeRivalsMatchInit(ctx, logger, fakeNakama, {
    playerFactions: JSON.stringify([
      { playerId: 'alice', factionId: 'snow_ice' },
      { playerId: 'bob', factionId: 'plains_forest' }
    ])
  });
  const aliceOld = { userId: 'alice', sessionId: 'alice-old' } as nkruntime.Presence;
  const bob = { userId: 'bob', sessionId: 'bob-session' } as nkruntime.Presence;
  let state = biomeRivalsMatchJoin(ctx, logger, fakeNakama, dispatcher, 0, initialized.state, [aliceOld, bob]).state;
  if (state.game === null) throw new Error('test match did not initialize');
  const game = state.game;
  const sheep = BiomeRivalsRules.getCardDefinition('pf_002')!;
  game.status = 'ACTIVE';
  game.players[0]!.mulliganCompleted = true;
  game.players[1]!.mulliganCompleted = true;
  game.players[0]!.hand = ['tk_001'];
  game.players[0]!.unitSlots[0] = 'object-1';
  game.players[0]!.battlefield.push({
    instanceId: 'object-1', ownerPlayerId: 'alice', cardId: sheep.id, cardType: 'UNIT',
    attack: sheep.attack, health: sheep.health, maxHealth: sheep.health,
    adjacencyHealthModifier: 0, slotKind: 'UNIT', slotIndex: 0, occupiedSlots: 1,
    summonedTurn: game.turn, hasAttacked: false, keywords: sheep.keywords.slice(),
    temporaryAttackModifier: 0, temporaryAttackModifierExpiresOnTurn: 0,
    temporaryHealthModifier: 0, temporaryHealthModifierExpiresOnTurn: 0, statuses: []
  });
  const played = BiomeRivalsRules.applyCommand(game, 'alice', {
    protocolVersion: BiomeRivalsRules.PROTOCOL_VERSION,
    rulesetVersion: BiomeRivalsRules.RULESET_VERSION,
    commandId: 'play-wool-before-reconnect', expectedRevision: game.revision,
    type: 'PLAY_CARD', payload: { cardId: 'tk_001', targetType: 'UNIT', targetInstanceId: 'object-1' }
  });
  TestHarness.ok(played.accepted, JSON.stringify(played));
  if (!played.accepted) return;
  state.game = played.state;
  TestHarness.equal(played.batch.events[1]!.payload.reason, 'TEMPORARY_HEALTH_MODIFIER');
  broadcasts.length = 0;

  const aliceNew = { userId: 'alice', sessionId: 'alice-new' } as nkruntime.Presence;
  const attempt = biomeRivalsMatchJoinAttempt(ctx, logger, fakeNakama, dispatcher, 1, state, aliceNew, {});
  TestHarness.equal(attempt.accept, true);
  state = biomeRivalsMatchJoin(ctx, logger, fakeNakama, dispatcher, 1, state, [aliceNew]).state;
  TestHarness.equal(state.presences['alice-old'], undefined);
  TestHarness.equal(broadcasts.length, 1);
  TestHarness.equal(broadcasts[0]!.opCode, 4);
  TestHarness.equal(broadcasts[0]!.recipients[0]!.sessionId, 'alice-new');
  const snapshot = JSON.parse(broadcasts[0]!.data) as BiomeRivalsRules.MatchSnapshot;
  const recovered = snapshot.players[0]!.battlefield[0]!;
  TestHarness.equal(snapshot.viewerPlayerId, 'alice');
  TestHarness.equal(snapshot.revision, played.state.revision);
  TestHarness.equal(snapshot.lastEventId, played.state.lastEventId);
  TestHarness.equal(recovered.instanceId, 'object-1');
  TestHarness.equal(recovered.health, sheep.health + 1);
  TestHarness.equal(recovered.maxHealth, sheep.health + 1);
  TestHarness.equal(recovered.temporaryHealthModifier, 1);
  TestHarness.equal(recovered.temporaryHealthModifierExpiresOnTurn, played.state.turn);
  TestHarness.equal(snapshot.players[0]!.hand.length, 0);
  TestHarness.equal(snapshot.players[1]!.hand[0], null);
  TestHarness.equal(JSON.stringify(snapshot).indexOf('processedCommandIds'), -1);
  TestHarness.equal(BiomeRivalsRules.validateState(state.game!).length, 0);
});

TestHarness.test('matchmaker rejects unsupported faction properties', function (): void {
  let rejected = false;
  try {
    biomeRivalsMatchmakerMatched(
      {} as nkruntime.Context,
      {} as nkruntime.Logger,
      {} as nkruntime.Nakama,
      [matchmakerResult('alice', 'unknown'), matchmakerResult('bob', 'end')]
    );
  } catch (error) {
    rejected = String(error).indexOf('supported factionId') >= 0;
  }
  TestHarness.ok(rejected);
});

TestHarness.test('matchmaker rejects a non-two-player result', function (): void {
  let rejected = false;
  try {
    biomeRivalsMatchmakerMatched(
      {} as nkruntime.Context,
      {} as nkruntime.Logger,
      {} as nkruntime.Nakama,
      [matchmakerResult('alice', 'plains_forest')]
    );
  } catch (error) {
    rejected = String(error).indexOf('exactly two players') >= 0;
  }
  TestHarness.ok(rejected);
});

TestHarness.test('simultaneous defeat match fixture is unavailable unless explicitly enabled', function (): void {
  const game = BiomeRivalsRules.createInitialState(
    'fixture-disabled-match', ['alice', 'bob'], ['plains_forest', 'nether'], 'fixture-seed'
  );
  game.status = 'ACTIVE';
  game.players[0]!.mulliganCompleted = true;
  game.players[1]!.mulliganCompleted = true;
  const alice = { userId: 'alice', sessionId: 'alice-session' } as nkruntime.Presence;
  const bob = { userId: 'bob', sessionId: 'bob-session' } as nkruntime.Presence;
  const state: BiomeRivalsMatchState = {
    presences: { [alice.sessionId]: alice, [bob.sessionId]: bob },
    arenaId: BiomeRivalsRules.DEFAULT_ARENA_ID,
    factionByPlayerId: { alice: 'plains_forest', bob: 'nether' },
    game: game
  };
  const broadcasts: Array<{ opCode: number; data: string; recipients: nkruntime.Presence[] }> = [];
  const dispatcher = {
    broadcastMessage: function (
      opCode: number,
      data: string,
      recipients: nkruntime.Presence[]
    ): void { broadcasts.push({ opCode: opCode, data: data, recipients: recipients }); }
  } as unknown as nkruntime.MatchDispatcher;
  const nk = {
    binaryToString: function (data: ArrayBuffer): string { return new TextDecoder().decode(data); }
  } as unknown as nkruntime.Nakama;
  const payload = new TextEncoder().encode(JSON.stringify({ fixture: 'simultaneous-defeat' })).buffer;

  const result = biomeRivalsMatchLoop(
    { env: {}, matchId: game.matchId } as nkruntime.Context,
    { warn: function (): void { } } as unknown as nkruntime.Logger,
    nk,
    dispatcher,
    1,
    state,
    [{ sender: alice, opCode: 255, data: payload } as nkruntime.MatchMessage]
  );

  TestHarness.equal(result.state.game!.status, 'ACTIVE');
  TestHarness.ok(result.state.game!.players.every(function (player): boolean { return player.life > 0; }));
  TestHarness.equal(broadcasts.length, 1);
  TestHarness.equal(broadcasts[0]!.opCode, 3);
  TestHarness.ok(broadcasts[0]!.data.indexOf('test fixtures are disabled') >= 0);
  const readablePayload = new TextEncoder().encode(JSON.stringify({ fixture: 'simultaneous-defeat-readable' })).buffer;
  biomeRivalsMatchLoop(
    { env: {}, matchId: game.matchId } as nkruntime.Context,
    { warn: function (): void { } } as unknown as nkruntime.Logger, nk, dispatcher, 2, state,
    [{ sender: alice, opCode: 255, data: readablePayload } as nkruntime.MatchMessage]
  );
  TestHarness.equal(broadcasts.length, 2);
  TestHarness.equal(broadcasts[1]!.opCode, 3);
  TestHarness.equal(JSON.stringify(result.state.game), JSON.stringify(game), 'disabled readable fixture must not mutate state');
});

TestHarness.test('fixture runtime gate accepts only the exact true string', function (): void {
  TestHarness.equal(isTestFixtureEnabled({} as nkruntime.Context), false);
  for (const value of ['false', 'TRUE', 'True', '1', 'true ', '']) {
    TestHarness.equal(isTestFixtureEnabled({
      env: { BIOME_RIVALS_ENABLE_TEST_FIXTURES: value }
    } as unknown as nkruntime.Context), false);
  }
  TestHarness.equal(isTestFixtureEnabled({
    env: { BIOME_RIVALS_ENABLE_TEST_FIXTURES: 'true' }
  } as unknown as nkruntime.Context), true);
});

['simultaneous-defeat', 'simultaneous-defeat-readable'].forEach(function (fixtureName): void {
TestHarness.test('enabled ' + fixtureName + ' fixture uses the authoritative command and broadcasts draw replay', function (): void {
  const readable = fixtureName === 'simultaneous-defeat-readable';
  const game = BiomeRivalsRules.createInitialState(
    'fixture-enabled-match', ['alice', 'bob'], ['plains_forest', 'nether'], 'fixture-seed'
  );
  game.status = 'ACTIVE';
  game.phase = 'MAIN';
  game.players[0]!.mulliganCompleted = true;
  game.players[1]!.mulliganCompleted = true;
  const alice = { userId: 'alice', sessionId: 'alice-session' } as nkruntime.Presence;
  const bob = { userId: 'bob', sessionId: 'bob-session' } as nkruntime.Presence;
  const state: BiomeRivalsMatchState = {
    presences: { [alice.sessionId]: alice, [bob.sessionId]: bob },
    arenaId: BiomeRivalsRules.DEFAULT_ARENA_ID,
    factionByPlayerId: { alice: 'plains_forest', bob: 'nether' },
    game: game
  };
  const broadcasts: Array<{ opCode: number; data: string; recipients: nkruntime.Presence[] }> = [];
  const dispatcher = {
    broadcastMessage: function (
      opCode: number,
      data: string,
      recipients: nkruntime.Presence[]
    ): void { broadcasts.push({ opCode: opCode, data: data, recipients: recipients }); }
  } as unknown as nkruntime.MatchDispatcher;
  const nk = {
    binaryToString: function (data: ArrayBuffer): string { return new TextDecoder().decode(data); }
  } as unknown as nkruntime.Nakama;
  const payload = new TextEncoder().encode(JSON.stringify({ fixture: fixtureName })).buffer;
  const revisionBefore = game.revision;
  const result = biomeRivalsMatchLoop(
    { env: { BIOME_RIVALS_ENABLE_TEST_FIXTURES: 'true' }, matchId: game.matchId } as unknown as nkruntime.Context,
    { warn: function (): void { } } as unknown as nkruntime.Logger,
    nk,
    dispatcher,
    1,
    state,
    [{ sender: alice, opCode: 255, data: payload } as nkruntime.MatchMessage]
  );

  TestHarness.equal(result.state.game!.status, 'FINISHED');
  TestHarness.equal(result.state.game!.winnerPlayerId, null);
  TestHarness.equal(result.state.game!.revision, revisionBefore + 1);
  TestHarness.ok(result.state.game!.players.every(function (player): boolean { return player.life === 0; }));
  TestHarness.ok(game.players.every(function (player): boolean { return player.life > 0; }),
    'test fixture must not mutate the source state before its accepted authoritative result');
  TestHarness.equal(broadcasts.length, 5, 'fixture must synchronize injected preconditions before ordinary replay');
  const preparedAlice = JSON.parse(broadcasts[0]!.data) as BiomeRivalsRules.MatchSnapshot;
  const preparedBob = JSON.parse(broadcasts[1]!.data) as BiomeRivalsRules.MatchSnapshot;
  TestHarness.equal(broadcasts[0]!.opCode, 4);
  TestHarness.equal(broadcasts[1]!.opCode, 4);
  TestHarness.equal(preparedAlice.revision, revisionBefore);
  TestHarness.equal(preparedBob.revision, revisionBefore);
  TestHarness.equal(preparedAlice.status, 'ACTIVE');
  TestHarness.ok(preparedAlice.players.every(function (player): boolean { return player.life === 0; }));
  TestHarness.equal(preparedAlice.players[0]!.hand[0], 'cd_006');
  TestHarness.equal(preparedAlice.players[0]!.hand.length, readable ? 7 : 1);
  TestHarness.ok(preparedBob.players[0]!.hand.every(function (card): boolean { return card === null; }));
  TestHarness.ok(preparedBob.players[0]!.handCards.every(function (card): boolean { return card === null; }));
  const aliceBatch = JSON.parse(broadcasts[2]!.data) as BiomeRivalsRules.MatchEventBatch;
  const bobBatch = JSON.parse(broadcasts[3]!.data) as BiomeRivalsRules.MatchEventBatch;
  TestHarness.equal(broadcasts[2]!.opCode, 2);
  TestHarness.equal(broadcasts[3]!.opCode, 2);
  TestHarness.equal(broadcasts[0]!.recipients[0]!.userId, 'alice');
  TestHarness.equal(broadcasts[1]!.recipients[0]!.userId, 'bob');
  TestHarness.equal(aliceBatch.revision, bobBatch.revision);
  TestHarness.equal(aliceBatch.events[aliceBatch.events.length - 1]!.type, 'MATCH_ENDED');
  TestHarness.equal(bobBatch.events[bobBatch.events.length - 1]!.type, 'MATCH_ENDED');
  TestHarness.equal(aliceBatch.events[aliceBatch.events.length - 1]!.payload.reason, 'SIMULTANEOUS_DEFEAT');
  TestHarness.equal(bobBatch.events[bobBatch.events.length - 1]!.payload.winnerPlayerId, null);
  TestHarness.equal(JSON.stringify(aliceBatch.events.map(function (event): string {
    return event.eventId + ':' + event.type;
  })), JSON.stringify(bobBatch.events.map(function (event): string {
    return event.eventId + ':' + event.type;
  })), 'both clients must receive the same public event order');
  if (readable) {
    TestHarness.ok(result.state.game!.players.every(function (player): boolean { return player.handCards.length === 7 && player.hand.length === 7; }));
    TestHarness.equal(aliceBatch.handProjection!.ownHand.length, 7);
    TestHarness.equal(bobBatch.handProjection!.ownHand.length, 7);
    TestHarness.equal(preparedBob.players[1]!.hand.length, 7);
  } else {
  const aliceDraw = aliceBatch.events.filter(function (event): boolean { return event.type === 'CARD_DRAWN'; })[0]!;
  const bobDraw = bobBatch.events.filter(function (event): boolean { return event.type === 'CARD_DRAWN'; })[0]!;
  const drawingPlayerId = aliceBatch.events.filter(function (event): boolean { return event.type === 'CARD_PLAYED'; })[0]!.payload.playerId;
  TestHarness.equal(typeof aliceDraw.payload.cardId === 'string', aliceBatch.handProjection!.ownPlayerId === drawingPlayerId,
    'only the drawing player may receive the private drawn card id');
  TestHarness.equal(typeof bobDraw.payload.cardId === 'string', bobBatch.handProjection!.ownPlayerId === drawingPlayerId,
    'opponent must not receive the drawn card id');
  }
  TestHarness.equal(broadcasts[4]!.opCode, 5);
  TestHarness.ok(broadcasts[4]!.data.indexOf('SIMULTANEOUS_DEFEAT') >= 0);
});
});
