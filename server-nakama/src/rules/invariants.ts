namespace BiomeRivalsRules {
  export function validateState(state: MatchState): string[] {
    const violations: string[] = [];
    if (!state.matchId) violations.push('matchId is required');
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
    return violations;
  }
}
