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
    [{}, {}] as nkruntime.MatchmakerResult[]
  );

  TestHarness.equal(matchId, 'authoritative-match-id');
  TestHarness.equal(createdModule, 'biome_rivals');
  TestHarness.equal(createdParams?.mode, 'prototype');
  TestHarness.equal(createdParams?.matchedPlayerCount, 2);
});

TestHarness.test('matchmaker rejects a non-two-player result', function (): void {
  let rejected = false;
  try {
    biomeRivalsMatchmakerMatched(
      {} as nkruntime.Context,
      {} as nkruntime.Logger,
      {} as nkruntime.Nakama,
      [{}] as nkruntime.MatchmakerResult[]
    );
  } catch (error) {
    rejected = String(error).indexOf('exactly two players') >= 0;
  }
  TestHarness.ok(rejected);
});

TestHarness.finish();
