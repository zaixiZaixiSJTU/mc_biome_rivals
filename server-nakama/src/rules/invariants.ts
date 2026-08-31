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
    if (state.phase !== 'MAIN' && state.phase !== 'COMBAT') violations.push('turn phase is invalid');
    if (state.nextInstanceId < 1) violations.push('nextInstanceId must be positive');
    if (state.status === 'FINISHED' && state.winnerPlayerId === null) {
      violations.push('finished match requires a winner');
    }
    if (state.status !== 'FINISHED' && state.winnerPlayerId !== null) {
      violations.push('unfinished match cannot have a winner');
    }
    if (state.pendingChoice !== null) {
      const choice = state.pendingChoice;
      const choicePlayerIndex = state.players.map(function (player): string { return player.playerId; }).indexOf(choice.playerId);
      const riptideMove = choice.kind === 'MOVE_UNIT' && choice.effectId === 'effect.or_006.01' && choice.sourceCardId === 'or_006';
      const salmonMove = choice.kind === 'MOVE_UNIT' && choice.effectId === 'effect.or_001.01' && choice.sourceCardId === 'or_001';
      const prismarineMove = choice.kind === 'MOVE_UNIT' && choice.effectId === 'effect.tk_012.01' && choice.sourceCardId === 'tk_012';
      if (state.status !== 'ACTIVE' || (choice.kind === 'ARCHAEOLOGY_TOP_3' && state.phase !== 'MAIN') ||
          (riptideMove && state.phase !== 'COMBAT') || ((salmonMove || prismarineMove) && state.phase !== 'MAIN')) violations.push('pending choice phase is invalid');
      if (choicePlayerIndex < 0 || choicePlayerIndex !== state.activePlayerIndex) violations.push('pending choice owner must be the active player');
      if (!/^choice-[0-9]+$/.test(choice.choiceId)) violations.push('pending choice id is invalid');
      const archaeologyChoice = choice.kind === 'ARCHAEOLOGY_TOP_3' && choice.effectId === 'effect.db_003.01' && choice.sourceCardId === 'db_003';
      const moveChoice = riptideMove || salmonMove || prismarineMove;
      if (!archaeologyChoice && !moveChoice) violations.push('pending choice kind or source is unsupported');
      if (!Array.isArray(choice.options) || choice.options.length > (moveChoice ? 2 : 3)) violations.push('pending choice options are invalid');
      if (choicePlayerIndex >= 0) {
        const choicePlayer = state.players[choicePlayerIndex]!;
        if (archaeologyChoice) {
          const expectedCount = Math.min(3, choicePlayer.deck.length);
          if (choice.options.length !== expectedCount) violations.push('archaeology choice must inspect the complete top-three range');
          const sourceExists = choicePlayer.battlefield.some(function (object): boolean {
            return object.instanceId === choice.sourceInstanceId && object.cardId === choice.sourceCardId;
          });
          if (!sourceExists) violations.push('pending choice source object is missing');
          for (let optionIndex = 0; optionIndex < choice.options.length; optionIndex += 1) {
            const option = choice.options[optionIndex]!;
            const expectedCardId = choicePlayer.deck[choicePlayer.deck.length - 1 - optionIndex];
            if (option.optionIndex !== optionIndex || option.cardId !== expectedCardId || option.slotIndex !== -1) {
              violations.push('pending choice options no longer match the deck top');
            }
            if (option.selectable !== (choicePlayer.buriedCardIds.indexOf(option.cardId) >= 0)) {
              violations.push('pending choice selectable marker differs from buried state');
            }
          }
        } else if (moveChoice) {
          const targetPlayer = state.players.filter(function (candidate): boolean { return candidate.playerId === choice.targetPlayerId; })[0];
          const target = targetPlayer === undefined ? undefined : targetPlayer.battlefield.filter(function (object): boolean {
            return object.instanceId === choice.targetInstanceId && object.cardType === 'UNIT';
          })[0];
          if (target === undefined) violations.push('pending movement target is missing');
          else {
            const expectedSlots = [target.slotIndex - 1, target.slotIndex + 1].filter(function (slotIndex): boolean {
              return slotIndex >= 0 && slotIndex < targetPlayer!.unitSlots.length && targetPlayer!.unitSlots[slotIndex] === null;
            });
            if (choice.options.length !== expectedSlots.length || expectedSlots.length === 0) {
              violations.push('pending movement options must include every adjacent empty slot');
            }
            for (let optionIndex = 0; optionIndex < choice.options.length; optionIndex += 1) {
              const option = choice.options[optionIndex]!;
              if (option.optionIndex !== optionIndex || option.cardId !== target.cardId || !option.selectable ||
                  option.slotIndex !== expectedSlots[optionIndex]) {
                violations.push('pending movement option is invalid');
              }
            }
            if (salmonMove && (targetPlayer!.playerId !== choice.playerId || target.instanceId !== choice.sourceInstanceId)) {
              violations.push('salmon movement must target its own source unit');
            }
            if (prismarineMove) {
              const targetDefinition = getCardDefinition(target.cardId);
              if (targetPlayer!.playerId !== choice.playerId || !/^effect-[0-9]+$/.test(choice.sourceInstanceId) ||
                  targetDefinition === null || targetDefinition.tags.indexOf('aquatic') < 0) {
                violations.push('prismarine movement source or target is invalid');
              }
            }
          }
        }
      }
    }
    const completedMulligans = state.players.filter(function (player): boolean { return player.mulliganCompleted; }).length;
    if (state.status === 'MULLIGAN' && completedMulligans === state.players.length) {
      violations.push('mulligan state cannot have every player confirmed');
    }
    if (state.status === 'ACTIVE' && completedMulligans !== state.players.length) {
      violations.push('active match requires every opening hand to be confirmed');
    }
    for (let playerIndex = 0; playerIndex < state.players.length; playerIndex += 1) {
      const player = state.players[playerIndex]!;
      if (typeof player.excavatedThisTurn !== 'boolean') violations.push('player excavation turn marker is invalid');
      if (typeof player.heroHasAttacked !== 'boolean') violations.push('player hero attack marker is invalid');
      if (!Array.isArray(player.triggeredEffectKeysThisTurn) ||
          player.triggeredEffectKeysThisTurn.some(function (key): boolean {
            return typeof key !== 'string' || !/^object-[0-9]+:effect\.or_(?:002|004)\.01$/.test(key);
          }) || player.triggeredEffectKeysThisTurn.some(function (key, keyIndex): boolean {
            return player.triggeredEffectKeysThisTurn.indexOf(key) !== keyIndex;
          })) {
        violations.push('player once-per-turn effect markers are invalid');
      }
      if (state.status === 'ACTIVE' && playerIndex !== state.activePlayerIndex && player.excavatedThisTurn) {
        violations.push('inactive player cannot retain an excavation turn marker');
      }
      if (!isFactionId(player.factionId)) violations.push('player faction is unsupported');
      if (typeof player.mulliganCompleted !== 'boolean') violations.push('player mulligan state is invalid');
      if (player.life < 0 || player.armor < 0) violations.push('player combat values cannot be negative');
      if (player.redstone < 0 || player.redstone > player.redstoneCapacity || player.redstoneCapacity < 0 || player.redstoneCapacity > 10) {
        violations.push('player redstone is out of range');
      }
      if (player.unitSlots.length !== 4) violations.push('each player requires four unit slots');
      if (player.buildingSlots.length !== 3) violations.push('each player requires three building slots');
      if (player.hand.length > 7) violations.push('hand cannot exceed seven cards');
      if (player.fatigueCount < 0) violations.push('fatigue count cannot be negative');
      if (player.equipment !== null) {
        const equipmentDefinition = getCardDefinition(player.equipment.cardId);
        if (equipmentDefinition === null || equipmentDefinition.cardType !== 'EQUIPMENT') violations.push('equipment card is invalid');
        if (!/^equipment-[0-9]+$/.test(player.equipment.instanceId) || player.equipment.attack <= 0 ||
            player.equipment.durability <= 0 || player.equipment.durability > player.equipment.maxDurability ||
            player.equipment.maxDurability <= 0) violations.push('equipment state is invalid');
      }
      for (let handIndex = 0; handIndex < player.hand.length; handIndex += 1) {
        if (getCardDefinition(player.hand[handIndex]!) === null) violations.push('hand contains an unknown card');
      }
      for (let deckIndex = 0; deckIndex < player.deck.length; deckIndex += 1) {
        if (getCardDefinition(player.deck[deckIndex]!) === null) violations.push('deck contains an unknown card');
      }
      const remainingDeckCounts: { [cardId: string]: number } = {};
      for (let deckIndex = 0; deckIndex < player.deck.length; deckIndex += 1) {
        const cardId = player.deck[deckIndex]!;
        remainingDeckCounts[cardId] = (remainingDeckCounts[cardId] || 0) + 1;
      }
      for (let buriedIndex = 0; buriedIndex < player.buriedCardIds.length; buriedIndex += 1) {
        const cardId = player.buriedCardIds[buriedIndex]!;
        if (getCardDefinition(cardId) === null) violations.push('buried cards contain an unknown card');
        if (!remainingDeckCounts[cardId]) violations.push('buried card marker is missing from the deck');
        else remainingDeckCounts[cardId] -= 1;
      }
      for (let discardIndex = 0; discardIndex < player.discardPile.length; discardIndex += 1) {
        if (getCardDefinition(player.discardPile[discardIndex]!) === null) violations.push('discard pile contains an unknown card');
      }
      const instances: { [instanceId: string]: BattlefieldObjectState } = {};
      for (let objectIndex = 0; objectIndex < player.battlefield.length; objectIndex += 1) {
        const object = player.battlefield[objectIndex]!;
        if (!object.instanceId || instances[object.instanceId]) violations.push('battlefield instance ids must be unique per player');
        instances[object.instanceId] = object;
        const definition = getCardDefinition(object.cardId);
        if (definition === null) violations.push('battlefield contains an unknown card');
        else {
          if (definition.cardType !== object.cardType) violations.push('battlefield card type differs from its definition');
          if (object.cardType === 'UNIT' && (object.slotKind !== 'UNIT' || object.occupiedSlots !== 1)) {
            violations.push('unit battlefield placement is invalid');
          }
          if ((object.cardType === 'BUILDING' || object.cardType === 'STRUCTURE') &&
              (object.slotKind !== 'BUILDING' || object.occupiedSlots !== Math.max(1, definition.buildingSlots))) {
            violations.push('building battlefield placement is invalid');
          }
        }
        if (!Array.isArray(object.keywords)) violations.push('battlefield keywords must be an array');
        else {
          const seenKeywords: { [keyword: string]: boolean } = {};
          for (let keywordIndex = 0; keywordIndex < object.keywords.length; keywordIndex += 1) {
            const keyword = object.keywords[keywordIndex]!;
            if (keyword !== 'TAUNT' && keyword !== 'CHARGE') violations.push('battlefield keyword is unsupported');
            if (seenKeywords[keyword]) violations.push('battlefield keywords must be unique');
            seenKeywords[keyword] = true;
          }
        }
        if (object.health <= 0 || object.health > object.maxHealth || object.maxHealth <= 0) violations.push('battlefield health is out of range');
        if (object.adjacencyHealthModifier < 0 || object.adjacencyHealthModifier > 2 ||
            object.adjacencyHealthModifier % 1 !== 0) {
          violations.push('adjacency health modifier is invalid');
        }
        const expectedAdjacencyHealthModifier = object.cardType !== 'UNIT' ? 0 : player.battlefield.filter(function (source): boolean {
          if (source.instanceId === object.instanceId || source.cardType !== 'UNIT' || source.health <= 0 ||
              Math.abs(source.slotIndex - object.slotIndex) !== 1) return false;
          const sourceDefinition = getCardDefinition(source.cardId);
          return sourceDefinition !== null && sourceDefinition.effectImplementationStatus === 'IMPLEMENTED' &&
            sourceDefinition.effectIds.indexOf('effect.or_005.01') >= 0;
        }).length;
        if (object.adjacencyHealthModifier !== expectedAdjacencyHealthModifier) {
          violations.push('adjacency health modifier does not match current turtle auras');
        }
        if (object.attack < 0 || object.summonedTurn < 1 || object.summonedTurn > state.turn) violations.push('battlefield combat values are invalid');
        if (object.temporaryAttackModifierExpiresOnTurn < 0) {
          violations.push('temporary attack modifier state is invalid');
        }
        if ((object.temporaryAttackModifier === 0) !== (object.temporaryAttackModifierExpiresOnTurn === 0)) {
          violations.push('temporary attack modifier and expiry must be cleared together');
        }
        if (!Array.isArray(object.statuses)) violations.push('battlefield statuses must be an array');
        else {
          const seenStatuses: { [statusId: string]: boolean } = {};
          for (let statusIndex = 0; statusIndex < object.statuses.length; statusIndex += 1) {
            const status = object.statuses[statusIndex]!;
            if (status.statusId !== 'SLOW') violations.push('battlefield status is unsupported');
            if (seenStatuses[status.statusId]) violations.push('battlefield statuses must be unique');
            seenStatuses[status.statusId] = true;
            if (status.remainingDuration < 1 || status.remainingDuration % 1 !== 0) violations.push('battlefield status duration is invalid');
            if (!status.sourcePlayerId || !status.sourceCardId || !status.effectId) violations.push('battlefield status source is incomplete');
            if (!state.players.some(function (candidate): boolean { return candidate.playerId === status.sourcePlayerId; })) {
              violations.push('battlefield status source player is not in the match');
            }
            const sourceDefinition = getCardDefinition(status.sourceCardId);
            if (sourceDefinition === null || sourceDefinition.effectIds.indexOf(status.effectId) < 0 ||
                !/^effect\.[a-z0-9_]+\.[0-9]{2}$/.test(status.effectId)) {
              violations.push('battlefield status source card or effect is invalid');
            }
            if (status.attackModifier > 0) violations.push('battlefield status attack modifier is invalid');
            if (status.boundAttackModifier > 0 || status.attackModifier < status.boundAttackModifier) {
              violations.push('battlefield status bound attack modifier is invalid');
            }
          }
        }
        if (object.occupiedSlots < 1) violations.push('battlefield object must occupy at least one slot');
        const expectedRow = object.slotKind === 'UNIT' ? player.unitSlots : player.buildingSlots;
        if (object.slotIndex < 0 || object.slotIndex + object.occupiedSlots > expectedRow.length) {
          violations.push('battlefield object slot range is invalid');
        } else {
          for (let slotOffset = 0; slotOffset < object.occupiedSlots; slotOffset += 1) {
            if (expectedRow[object.slotIndex + slotOffset] !== object.instanceId) violations.push('battlefield object does not own its declared slots');
          }
        }
        let referenceCount = 0;
        const objectRows = [player.unitSlots, player.buildingSlots];
        for (let objectRowIndex = 0; objectRowIndex < objectRows.length; objectRowIndex += 1) {
          const objectRow = objectRows[objectRowIndex]!;
          for (let objectSlotIndex = 0; objectSlotIndex < objectRow.length; objectSlotIndex += 1) {
            if (objectRow[objectSlotIndex] === object.instanceId) referenceCount += 1;
          }
        }
        if (referenceCount !== object.occupiedSlots) violations.push('battlefield instance has an unexpected slot reference count');
      }
      const rows = [player.unitSlots, player.buildingSlots];
      for (let rowIndex = 0; rowIndex < rows.length; rowIndex += 1) {
        const row = rows[rowIndex]!;
        for (let slotIndex = 0; slotIndex < row.length; slotIndex += 1) {
          const instanceId = row[slotIndex];
          if (instanceId !== null && instanceId !== undefined && !instances[instanceId]) violations.push('slot references a missing battlefield object');
        }
      }
    }
    return violations;
  }
}
