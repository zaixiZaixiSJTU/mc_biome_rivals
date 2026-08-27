function biomeRivalsMatchmakerMatched(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  matches: nkruntime.MatchmakerResult[]
): string {
  if (matches.length !== 2) {
    throw new Error('Biome Rivals authoritative matches require exactly two players.');
  }
  const playerFactions = matches.map(function (match): { playerId: string; factionId: BiomeRivalsRules.FactionId } {
    const factionId = match.properties && match.properties.factionId;
    if (!match.presence || !match.presence.userId || !BiomeRivalsRules.isFactionId(factionId)) {
      throw new Error('Every matched player must provide a supported factionId.');
    }
    return { playerId: match.presence.userId, factionId: factionId };
  });
  const matchId = nk.matchCreate('biome_rivals', {
    mode: 'prototype',
    matchedPlayerCount: matches.length,
    playerFactions: JSON.stringify(playerFactions)
  });
  logger.info('Created authoritative Biome Rivals match %s for %d matched players.', matchId, matches.length);
  return matchId;
}
