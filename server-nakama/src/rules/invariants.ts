namespace BiomeRivalsRules {
  export function validateState(state: MatchState): string[] {
    const violations: string[] = [];
    if (!state.matchId) violations.push('matchId is required');
    if (state.protocolVersion !== PROTOCOL_VERSION) violations.push('protocolVersion is unsupported');
    if (state.players.length !== 2) violations.push('exactly two players are required');
    if (state.players.length === 2 && state.players[0]!.playerId === state.players[1]!.playerId) {
      violations.push('player ids must be unique');
    }
    if (state.activePlayerIndex < 0 || state.activePlayerIndex >= state.players.length) {
      violations.push('activePlayerIndex is out of range');
    }
    if (state.revision < 0 || state.lastEventId < 0) violations.push('counters cannot be negative');
    if (state.turn < 1) violations.push('turn must start at one');
    if (state.status === 'FINISHED' && state.winnerPlayerId === null) {
      violations.push('finished match requires a winner');
    }
    if (state.status !== 'FINISHED' && state.winnerPlayerId !== null) {
      violations.push('unfinished match cannot have a winner');
    }
    for (let playerIndex = 0; playerIndex < state.players.length; playerIndex += 1) {
      const player = state.players[playerIndex]!;
      if (player.life < 0 || player.armor < 0) violations.push('player combat values cannot be negative');
      if (player.redstone < 0 || player.redstone > player.redstoneCapacity || player.redstoneCapacity < 0 || player.redstoneCapacity > 10) {
        violations.push('player redstone is out of range');
      }
      if (player.unitSlots.length !== 4) violations.push('each player requires four unit slots');
      if (player.buildingSlots.length !== 3) violations.push('each player requires three building slots');
      for (let handIndex = 0; handIndex < player.hand.length; handIndex += 1) {
        if (getCardDefinition(player.hand[handIndex]!) === null) violations.push('hand contains an unknown card');
      }
    }
    return violations;
  }
}
