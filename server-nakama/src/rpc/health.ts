function rpcHealth(
  ctx: nkruntime.Context,
  logger: nkruntime.Logger,
  nk: nkruntime.Nakama,
  payload: string
): string {
  return JSON.stringify({
    ok: true,
    protocolVersion: BiomeRivalsRules.PROTOCOL_VERSION,
    rulesetVersion: BiomeRivalsRules.RULESET_VERSION,
    cardContentVersion: BiomeRivalsRules.CARD_CATALOG_CONTENT_VERSION,
    implementedEffectRegistryVersion: BiomeRivalsRules.IMPLEMENTED_EFFECT_REGISTRY_CONTENT_VERSION
  });
}
