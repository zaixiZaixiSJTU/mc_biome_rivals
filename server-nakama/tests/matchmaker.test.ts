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
