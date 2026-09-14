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
  TestHarness.equal(snapshot.pendingChoice!.choiceId, 'choice-reconnect');
  TestHarness.equal(snapshot.pendingChoice!.options[0]!.cardId, state.game!.players[0]!.hand[0]!);
  TestHarness.equal(snapshot.players[1]!.hand[0], null);
  TestHarness.equal(JSON.stringify(snapshot).indexOf('processedCommandIds'), -1);
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

TestHarness.finish();
