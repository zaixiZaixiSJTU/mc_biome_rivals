function InitModule(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  initializer: nkruntime.Initializer
): void {
  initializer.registerMatch<BiomeRivalsMatchState>('biome_rivals', {
    matchInit: biomeRivalsMatchInit,
    matchJoinAttempt: biomeRivalsMatchJoinAttempt,
    matchJoin: biomeRivalsMatchJoin,
    matchLeave: biomeRivalsMatchLeave,
    matchLoop: biomeRivalsMatchLoop,
    matchTerminate: biomeRivalsMatchTerminate,
    matchSignal: biomeRivalsMatchSignal
  });
  initializer.registerMatchmakerMatched(biomeRivalsMatchmakerMatched);
  initializer.registerRpc('biome_rivals_health', rpcHealth);
  logger.info(
    'Biome Rivals module loaded (protocol=%d ruleset=%s)',
    BiomeRivalsRules.PROTOCOL_VERSION,
    BiomeRivalsRules.RULESET_VERSION
  );
}
