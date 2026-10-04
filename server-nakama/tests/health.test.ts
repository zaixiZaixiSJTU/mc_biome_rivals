TestHarness.test('health RPC advertises the deployed gameplay and card catalog versions', () => {
  const response = JSON.parse(rpcHealth(
    null as unknown as nkruntime.Context,
    null as unknown as nkruntime.Logger,
    null as unknown as nkruntime.Nakama,
    ''
  )) as {
    ok: boolean;
    protocolVersion: number;
    rulesetVersion: string;
    cardContentVersion: number;
    implementedEffectRegistryVersion: number;
  };

  TestHarness.equal(response.ok, true);
  TestHarness.equal(response.protocolVersion, BiomeRivalsRules.PROTOCOL_VERSION);
  TestHarness.equal(response.rulesetVersion, BiomeRivalsRules.RULESET_VERSION);
  TestHarness.equal(response.cardContentVersion, BiomeRivalsRules.CARD_CATALOG_CONTENT_VERSION);
  TestHarness.equal(
    response.implementedEffectRegistryVersion,
    BiomeRivalsRules.IMPLEMENTED_EFFECT_REGISTRY_CONTENT_VERSION
  );
});
