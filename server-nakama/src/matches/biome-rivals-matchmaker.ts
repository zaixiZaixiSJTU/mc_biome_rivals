function biomeRivalsMatchmakerMatched(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  matches: nkruntime.MatchmakerResult[]
): string {
  if (matches.length !== 2) {
    throw new Error('Biome Rivals authoritative matches require exactly two players.');
  }
  const matchId = nk.matchCreate('biome_rivals', {
    mode: 'prototype',
    matchedPlayerCount: matches.length
  });
  logger.info('Created authoritative Biome Rivals match %s for %d matched players.', matchId, matches.length);
  return matchId;
}
