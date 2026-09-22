import { randomUUID } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { Client } from '@heroiclabs/nakama-js';
import WebSocket from 'ws';

globalThis.WebSocket = WebSocket;

const host = process.env.BIOME_RIVALS_NAKAMA_HOST || '127.0.0.1';
const port = process.env.BIOME_RIVALS_NAKAMA_PORT || '17350';
const serverKey = process.env.BIOME_RIVALS_NAKAMA_SERVER_KEY || 'local_only_change_me';
const timeoutMs = Number(process.env.BIOME_RIVALS_SMOKE_TIMEOUT_MS || 45000);
const maximumAttempts = Number(process.env.BIOME_RIVALS_NETHER_STATUS_PROBE_ATTEMPTS || 24);
const reportPath = resolve(process.env.BIOME_RIVALS_NETHER_STATUS_PROBE_REPORT ||
  '../artifacts/nether-status-summon-online-probe.json');
const protocolVersion = 36;
const rulesetVersion = 'prototype-0.61';

function assert(condition, message) {
  if (!condition) throw new Error(message);
}

function deferred(label) {
  let resolvePromise;
  let rejectPromise;
  const promise = new Promise((resolveValue, rejectValue) => {
    resolvePromise = resolveValue;
    rejectPromise = rejectValue;
  });
  const timer = setTimeout(() => rejectPromise(new Error(`${label} timed out after ${timeoutMs}ms`)), timeoutMs);
  return {
    promise: promise.finally(() => clearTimeout(timer)),
    resolve: resolvePromise,
    reject: rejectPromise
  };
}

function stream(label) {
  const queued = [];
  const waiters = [];
  let terminalError = null;
  return {
    push(value) {
      const waiter = waiters.shift();
      if (waiter) waiter.resolve(value);
      else queued.push(value);
    },
    fail(error) {
      terminalError = error;
      while (waiters.length > 0) waiters.shift().reject(error);
    },
    next(itemLabel) {
      if (terminalError) return Promise.reject(terminalError);
      if (queued.length > 0) return Promise.resolve(queued.shift());
      const waiter = deferred(`${label} ${itemLabel}`);
      waiters.push(waiter);
      return waiter.promise;
    }
  };
}

function decodeMatchData(message) {
  return JSON.parse(new TextDecoder().decode(message.data));
}

async function attachSocket(player, label) {
  const snapshots = stream(`${label} snapshot`);
  const batches = stream(`${label} event batch`);
  const socket = player.client.createSocket(false, false);
  socket.onmatchdata = (message) => {
    const payload = decodeMatchData(message);
    if (message.op_code === 4) snapshots.push(payload);
    else if (message.op_code === 2) batches.push(payload);
    else if (message.op_code === 3) batches.fail(new Error(`command rejected: ${JSON.stringify(payload)}`));
  };
  socket.onerror = (event) => {
    const error = new Error(`${label} socket error: ${event?.message || String(event)}`);
    snapshots.fail(error);
    batches.fail(error);
  };
  await socket.connect(player.session, true, timeoutMs);
  player.socket = socket;
  player.snapshots = snapshots;
  player.batches = batches;
}

async function createPlayer(scenarioName, attempt, index) {
  const client = new Client(serverKey, host, port, false);
  const session = await client.authenticateDevice(
    `biome-rivals-${scenarioName}-${attempt}-${index}-${randomUUID()}`, true);
  const player = { index, client, session, socket: null, snapshots: null, batches: null, matchmaker: null };
  await attachSocket(player, `${scenarioName} attempt ${attempt} player ${index}`);
  player.matchmaker = deferred(`${scenarioName} attempt ${attempt} player ${index} matchmaking`);
  player.socket.onmatchmakermatched = player.matchmaker.resolve;
  return player;
}

function ownState(snapshot) {
  return snapshot.players.find((player) => player.playerId === snapshot.viewerPlayerId);
}

function playerState(snapshot, playerId) {
  return snapshot.players.find((player) => player.playerId === playerId);
}

function requiredMulliganIndices(hand, requiredCardIds) {
  const remaining = new Map();
  for (const cardId of requiredCardIds) remaining.set(cardId, (remaining.get(cardId) || 0) + 1);
  const replace = [];
  for (let index = 0; index < hand.length; index += 1) {
    const cardId = hand[index];
    const count = remaining.get(cardId) || 0;
    if (count > 0) remaining.set(cardId, count - 1);
    else replace.push(index);
  }
  return replace;
}

function includesCards(hand, requiredCardIds) {
  const copy = hand.slice();
  return requiredCardIds.every((cardId) => {
    const index = copy.indexOf(cardId);
    if (index < 0) return false;
    copy.splice(index, 1);
    return true;
  });
}

function samePublicEventOrder(batches, label) {
  assert(batches[0].revision === batches[1].revision, `${label}: clients observed different revisions`);
  const left = batches[0].events.map((event) => `${event.eventId}:${event.type}`);
  const right = batches[1].events.map((event) => `${event.eventId}:${event.type}`);
  assert(JSON.stringify(left) === JSON.stringify(right), `${label}: clients observed different public event order`);
}

function assertProjectedEventEqual(batches, index, label) {
  assert(index >= 0 && index < batches[0].events.length, `${label}: event index is out of range`);
  const left = batches[0].events[index];
  const right = batches[1].events[index];
  assert(left.eventId === right.eventId && left.type === right.type,
    `${label}: projected event identity differs`);
  assert(JSON.stringify(left.payload) === JSON.stringify(right.payload),
    `${label}: projected event payload differs`);
}

function eventOf(batch, type, predicate = () => true) {
  return batch.events.find((event) => event.type === type && predicate(event.payload));
}

function eventIndex(batch, type, predicate = () => true) {
  return batch.events.findIndex((event) => event.type === type && predicate(event.payload));
}

function eventEvidence(batch, predicate) {
  return batch.events.filter(predicate).map((event) => ({
    eventId: event.eventId,
    type: event.type,
    payload: event.payload
  }));
}

async function disconnectPlayer(player) {
  if (!player.socket) return;
  try {
    await player.socket.disconnect(false);
  } catch {
    // A deliberate reconnect may race the WebSocket close handshake.
  }
  player.socket = null;
}

async function reconnectPlayer(player, matchId, label) {
  await disconnectPlayer(player);
  await attachSocket(player, label);
  await player.socket.joinMatch(matchId);
  return player.snapshots.next('private recovery');
}

function publicCheckpoint(snapshot) {
  return {
    matchId: snapshot.matchId,
    revision: snapshot.revision,
    turn: snapshot.turn,
    phase: snapshot.phase,
    activePlayerIndex: snapshot.activePlayerIndex,
    nextInstanceId: snapshot.nextInstanceId,
    players: snapshot.players.map((player) => ({
      playerId: player.playerId,
      factionId: player.factionId,
      life: player.life,
      armor: player.armor,
      redstone: player.redstone,
      temporaryRedstone: player.temporaryRedstone,
      totalRedstone: player.totalRedstone,
      redstoneCapacity: player.redstoneCapacity,
      unitSlots: player.unitSlots,
      buildingSlots: player.buildingSlots,
      battlefield: player.battlefield
    }))
  };
}

async function createScenario(scenarioName, attempt, factions) {
  const players = [];
  try {
    players.push(
      await createPlayer(scenarioName, attempt, 0),
      await createPlayer(scenarioName, attempt, 1)
    );
    await Promise.all(players.map((player, index) =>
      player.socket.addMatchmaker('*', 2, 2, { factionId: factions[index] })));
    const matches = await Promise.all(players.map((player) => player.matchmaker.promise));
    const joined = await Promise.all(players.map((player, index) =>
      player.socket.joinMatch(matches[index].match_id, matches[index].token)));
    assert(joined.every((match) => match.authoritative), `${scenarioName}: non-authoritative match`);
    assert(joined[0].match_id === joined[1].match_id, `${scenarioName}: clients joined different matches`);
    const matchId = joined[0].match_id;
    const snapshots = await Promise.all(players.map((player) => player.snapshots.next('initial')));
    const playerIds = snapshots.map((snapshot) => snapshot.viewerPlayerId);
    const hands = snapshots.map((snapshot) => ownState(snapshot).hand.slice());
    const energy = new Map();
    for (const snapshot of snapshots) {
      assert(snapshot.matchId === matchId, `${scenarioName}: initial snapshot match ID mismatch`);
      assert(snapshot.protocolVersion === protocolVersion && snapshot.rulesetVersion === rulesetVersion,
        `${scenarioName}: version mismatch ${snapshot.protocolVersion}/${snapshot.rulesetVersion}`);
      const own = ownState(snapshot);
      const ownIndex = playerIds.indexOf(snapshot.viewerPlayerId);
      assert(own?.factionId === factions[ownIndex],
        `${scenarioName}: requested ${factions[ownIndex]} but received ${own?.factionId}`);
    }
    for (const state of snapshots[0].players) energy.set(state.playerId, state.totalRedstone);

    const scenario = {
      scenarioName,
      players,
      matchId,
      snapshots,
      playerIds,
      hands,
      energy,
      revision: snapshots[0].revision,
      phase: snapshots[0].phase,
      turn: snapshots[0].turn,
      activePlayerId: snapshots[0].players[snapshots[0].activePlayerIndex].playerId,
      commands: []
    };

    scenario.send = async (actorIndex, type, payload, label) => {
      const commandId = `${scenarioName}-${attempt}-${scenario.commands.length}-${randomUUID()}`;
      await players[actorIndex].socket.sendMatchState(matchId, 1, JSON.stringify({
        protocolVersion,
        rulesetVersion,
        commandId,
        expectedRevision: scenario.revision,
        type,
        payload
      }));
      const batches = await Promise.all(players.map((player) => player.batches.next(label)));
      samePublicEventOrder(batches, `${scenarioName} ${label}`);
      assert(batches.every((batch) => batch.acknowledgedCommandId === commandId),
        `${scenarioName} ${label}: command acknowledgement did not converge`);
      scenario.revision = batches[0].revision;
      for (const event of batches[0].events) {
        const eventPayload = event.payload;
        if (event.type === 'PHASE_CHANGED') scenario.phase = eventPayload.phase;
        if (event.type === 'TURN_STARTED') {
          scenario.turn = eventPayload.turn;
          scenario.phase = eventPayload.phase;
          scenario.activePlayerId = eventPayload.playerId;
        }
        if (typeof eventPayload.playerId === 'string' && Number.isInteger(eventPayload.totalRedstone)) {
          scenario.energy.set(eventPayload.playerId, eventPayload.totalRedstone);
        }
      }
      for (let index = 0; index < players.length; index += 1) {
        for (const event of batches[index].events) {
          const eventPayload = event.payload;
          if (event.type === 'MULLIGAN_COMPLETED' && eventPayload.playerId === playerIds[index]) {
            scenario.hands[index] = eventPayload.hand.slice();
          } else if (event.type === 'CARD_DRAWN' && eventPayload.playerId === playerIds[index]) {
            scenario.hands[index].push(eventPayload.cardId);
          } else if ((event.type === 'CARD_DEPLOYED' || event.type === 'CARD_PLAYED') &&
                     eventPayload.playerId === playerIds[index]) {
            const handIndex = scenario.hands[index].indexOf(eventPayload.cardId);
            if (handIndex >= 0) scenario.hands[index].splice(handIndex, 1);
          }
        }
      }
      scenario.commands.push({
        commandId,
        actorPlayerId: playerIds[actorIndex],
        type,
        revision: scenario.revision,
        eventIds: batches[0].events.map((event) => event.eventId),
        eventTypes: batches[0].events.map((event) => event.type)
      });
      return batches;
    };

    scenario.mulligan = async (requiredByIndex) => {
      for (let index = 0; index < players.length; index += 1) {
        await scenario.send(index, 'MULLIGAN', {
          cardIndices: requiredMulliganIndices(scenario.hands[index], requiredByIndex[index])
        }, `player ${index} mulligan`);
      }
      return requiredByIndex.every((required, index) => includesCards(scenario.hands[index], required));
    };

    return scenario;
  } catch (error) {
    await Promise.all(players.map(disconnectPlayer));
    throw error;
  }
}

async function closeScenario(scenario) {
  if (!scenario) return;
  await Promise.all(scenario.players.map(disconnectPlayer));
}

function activeIndex(scenario) {
  const index = scenario.playerIds.indexOf(scenario.activePlayerId);
  assert(index >= 0, `${scenario.scenarioName}: active player is not a probe client`);
  return index;
}

async function runStriderAttempt(attempt) {
  let scenario = null;
  try {
    scenario = await createScenario('strider', attempt, ['nether', 'nether']);
    const seatIndexes = scenario.snapshots.map((snapshot) =>
      snapshot.players.findIndex((player) => player.playerId === snapshot.viewerPlayerId));
    const healerIndex = seatIndexes.indexOf(1);
    assert(healerIndex >= 0, 'strider: could not identify the four-card seat');
    const blazeIndex = healerIndex === 0 ? 1 : 0;
    const hasOpening = await scenario.mulligan([
      blazeIndex === 0 ? ['nt_003'] : ['nt_004', 'nt_005'],
      blazeIndex === 1 ? ['nt_003'] : ['nt_004', 'nt_005']
    ]);
    if (!hasOpening) return { ok: false, retry: true, reason: 'opening hand omitted Blaze, Strider, or target' };

    let blazeInstanceId = null;
    let targetInstanceId = null;
    let blazeSummonedTurn = null;
    let fireBatch = null;
    let cleanseBatch = null;
    for (let step = 0; step < 48 && !cleanseBatch; step += 1) {
      const actorIndex = activeIndex(scenario);
      if (scenario.phase === 'MAIN') {
        const actorEnergy = scenario.energy.get(scenario.playerIds[actorIndex]) || 0;
        if (actorIndex === healerIndex && targetInstanceId === null && actorEnergy >= 4) {
          const batches = await scenario.send(healerIndex, 'DEPLOY_CARD', {
            cardId: 'nt_005', slotKind: 'UNIT', slotIndex: 0, paymentMethod: 'REDSTONE'
          }, 'deploy friendly Wither Skeleton target');
          const deployed = eventOf(batches[0], 'CARD_DEPLOYED',
            (payload) => payload.playerId === scenario.playerIds[healerIndex] && payload.cardId === 'nt_005');
          assert(deployed, 'strider: target deployment event missing');
          targetInstanceId = deployed.payload.instanceId;
          continue;
        }
        if (actorIndex === blazeIndex && blazeInstanceId === null && actorEnergy >= 3) {
          const batches = await scenario.send(blazeIndex, 'DEPLOY_CARD', {
            cardId: 'nt_003', slotKind: 'UNIT', slotIndex: 0, paymentMethod: 'REDSTONE'
          }, 'deploy Blaze');
          const deployed = eventOf(batches[0], 'CARD_DEPLOYED',
            (payload) => payload.playerId === scenario.playerIds[blazeIndex] && payload.cardId === 'nt_003');
          assert(deployed, 'strider: Blaze deployment event missing');
          blazeInstanceId = deployed.payload.instanceId;
          blazeSummonedTurn = deployed.payload.summonedTurn;
          continue;
        }
        if (actorIndex === healerIndex && fireBatch && actorEnergy >= 2) {
          const batches = await scenario.send(healerIndex, 'DEPLOY_CARD', {
            cardId: 'nt_004',
            slotKind: 'UNIT',
            slotIndex: 1,
            paymentMethod: 'REDSTONE',
            targetType: 'UNIT',
            targetInstanceId
          }, 'deploy Strider and cleanse Fire');
          const ownBatch = batches[0];
          const deployedIndex = eventIndex(ownBatch, 'CARD_DEPLOYED',
            (payload) => payload.cardId === 'nt_004');
          const removedIndex = eventIndex(ownBatch, 'OBJECT_STATUS_REMOVED',
            (payload) => payload.instanceId === targetInstanceId && payload.statusId === 'FIRE');
          const healedIndex = eventIndex(ownBatch, 'OBJECT_STATS_CHANGED',
            (payload) => payload.instanceId === targetInstanceId && payload.reason === 'HEAL');
          assert(deployedIndex >= 0 && removedIndex === deployedIndex + 1 && healedIndex === removedIndex + 1,
            'strider: deploy, Fire removal, and heal were not adjacent and ordered');
          assertProjectedEventEqual(batches, deployedIndex, 'strider deployment');
          assertProjectedEventEqual(batches, removedIndex, 'strider Fire removal');
          assertProjectedEventEqual(batches, healedIndex, 'strider heal');
          const removed = ownBatch.events[removedIndex];
          const healed = ownBatch.events[healedIndex];
          assert(removed.payload.reason === 'EFFECT_REMOVED' &&
            removed.payload.effectId === 'effect.nt_004.01' &&
            healed.payload.effectId === 'effect.nt_004.01' &&
            healed.payload.sourceInstanceId === removed.payload.sourceInstanceId,
          'strider: cleanse/heal source evidence did not converge');
          cleanseBatch = batches;
          break;
        }
        await scenario.send(actorIndex, 'ENTER_COMBAT', {}, `player ${actorIndex} enter combat`);
        continue;
      }

      if (actorIndex === blazeIndex && blazeInstanceId && targetInstanceId &&
          scenario.turn > blazeSummonedTurn && !fireBatch) {
        const batches = await scenario.send(blazeIndex, 'ATTACK', {
          attackerInstanceId: blazeInstanceId,
          targetType: 'UNIT',
          targetInstanceId
        }, 'Blaze attacks friendly target owner');
        const attackIndex = eventIndex(batches[0], 'ATTACK_RESOLVED',
          (payload) => payload.attackerInstanceId === blazeInstanceId && payload.targetInstanceId === targetInstanceId);
        const fireIndex = eventIndex(batches[0], 'OBJECT_STATUS_APPLIED',
          (payload) => payload.instanceId === targetInstanceId && payload.statusId === 'FIRE');
        assert(attackIndex >= 0 && fireIndex === attackIndex + 1,
          'strider: Blaze damage was not immediately followed by Fire');
        assertProjectedEventEqual(batches, attackIndex, 'strider attack');
        assertProjectedEventEqual(batches, fireIndex, 'strider Fire');
        assert(batches[0].events[fireIndex].payload.remainingDuration === 2 &&
          batches[0].events[fireIndex].payload.sourceCardId === 'nt_003' &&
          batches[0].events[fireIndex].payload.sourceInstanceId === blazeInstanceId,
        'strider: Fire source or duration is incorrect');
        fireBatch = batches;
        continue;
      }
      await scenario.send(actorIndex, 'END_TURN', {}, `player ${actorIndex} end turn`);
    }

    assert(fireBatch && cleanseBatch, 'strider: scenario did not reach cleanse within the command bound');
    return {
      ok: true,
      matchId: scenario.matchId,
      attempt,
      blazePlayerId: scenario.playerIds[blazeIndex],
      striderPlayerId: scenario.playerIds[healerIndex],
      blazeInstanceId,
      targetInstanceId,
      fireRevision: fireBatch[0].revision,
      cleanseRevision: cleanseBatch[0].revision,
      fireEvents: eventEvidence(fireBatch[0], (event) =>
        event.type === 'ATTACK_RESOLVED' || event.type === 'OBJECT_STATUS_APPLIED'),
      cleanseEvents: eventEvidence(cleanseBatch[0], (event) =>
        event.type === 'CARD_DEPLOYED' || event.type === 'OBJECT_STATUS_REMOVED' ||
        event.type === 'OBJECT_STATS_CHANGED'),
      commands: scenario.commands
    };
  } finally {
    await closeScenario(scenario);
  }
}

async function runWitherFortressAttempt(attempt) {
  let scenario = null;
  try {
    scenario = await createScenario('wither-fortress', attempt, ['nether', 'ocean_river']);
    const netherIndex = 0;
    const oceanIndex = 1;
    const hasOpening = await scenario.mulligan([['nt_005', 'nt_008'], ['or_005']]);
    if (!hasOpening) return { ok: false, retry: true, reason: 'opening hand omitted Wither Skeleton, Fortress, or Turtle' };

    let skeletonInstanceId = null;
    let fortressInstanceId = null;
    let fortressSummonedTurn = null;
    let turtleInstanceId = null;
    let skeletonSummonedTurn = null;
    let witherBatch = null;
    let fortressBatch = null;
    for (let step = 0; step < 72 && !fortressBatch; step += 1) {
      const actorIndex = activeIndex(scenario);
      if (scenario.phase === 'MAIN') {
        const actorEnergy = scenario.energy.get(scenario.playerIds[actorIndex]) || 0;
        if (actorIndex === oceanIndex && turtleInstanceId === null && actorEnergy >= 4) {
          const batches = await scenario.send(oceanIndex, 'DEPLOY_CARD', {
            cardId: 'or_005', slotKind: 'UNIT', slotIndex: 2, paymentMethod: 'REDSTONE'
          }, 'deploy Turtle target');
          const deployed = eventOf(batches[0], 'CARD_DEPLOYED',
            (payload) => payload.playerId === scenario.playerIds[oceanIndex] && payload.cardId === 'or_005');
          assert(deployed, 'wither-fortress: Turtle deployment event missing');
          turtleInstanceId = deployed.payload.instanceId;
          continue;
        }
        if (actorIndex === netherIndex && skeletonInstanceId === null && actorEnergy >= 4) {
          const batches = await scenario.send(netherIndex, 'DEPLOY_CARD', {
            cardId: 'nt_005', slotKind: 'UNIT', slotIndex: 1, paymentMethod: 'REDSTONE'
          }, 'deploy Wither Skeleton');
          const deployed = eventOf(batches[0], 'CARD_DEPLOYED',
            (payload) => payload.playerId === scenario.playerIds[netherIndex] && payload.cardId === 'nt_005');
          assert(deployed, 'wither-fortress: Wither Skeleton deployment event missing');
          skeletonInstanceId = deployed.payload.instanceId;
          skeletonSummonedTurn = deployed.payload.summonedTurn;
          continue;
        }
        if (actorIndex === netherIndex && fortressInstanceId === null &&
            (scenario.energy.get(scenario.playerIds[netherIndex]) || 0) >= 7) {
          const batches = await scenario.send(netherIndex, 'DEPLOY_CARD', {
            cardId: 'nt_008', slotKind: 'BUILDING', slotIndex: 0, paymentMethod: 'REDSTONE'
          }, 'deploy Nether Fortress');
          const deployed = eventOf(batches[0], 'CARD_DEPLOYED',
            (payload) => payload.playerId === scenario.playerIds[netherIndex] && payload.cardId === 'nt_008');
          assert(deployed?.payload.occupiedSlots === 3, 'wither-fortress: Fortress did not occupy three slots');
          fortressInstanceId = deployed.payload.instanceId;
          fortressSummonedTurn = deployed.payload.summonedTurn;
          continue;
        }
        await scenario.send(actorIndex, 'ENTER_COMBAT', {}, `player ${actorIndex} enter combat`);
        continue;
      }

      if (actorIndex === netherIndex && skeletonInstanceId && fortressInstanceId && turtleInstanceId &&
          scenario.turn > skeletonSummonedTurn && scenario.turn > fortressSummonedTurn && !witherBatch) {
        const batches = await scenario.send(netherIndex, 'ATTACK', {
          attackerInstanceId: skeletonInstanceId,
          targetType: 'UNIT',
          targetInstanceId: turtleInstanceId
        }, 'Wither Skeleton attacks Turtle');
        const attackIndex = eventIndex(batches[0], 'ATTACK_RESOLVED',
          (payload) => payload.attackerInstanceId === skeletonInstanceId && payload.targetInstanceId === turtleInstanceId);
        const witherIndex = eventIndex(batches[0], 'OBJECT_STATUS_APPLIED',
          (payload) => payload.instanceId === turtleInstanceId && payload.statusId === 'WITHER');
        assert(attackIndex >= 0 && witherIndex === attackIndex + 1,
          'wither-fortress: attack was not immediately followed by WITHER');
        assertProjectedEventEqual(batches, attackIndex, 'wither-fortress attack');
        assertProjectedEventEqual(batches, witherIndex, 'wither-fortress WITHER application');
        const applied = batches[0].events[witherIndex].payload;
        assert(applied.remainingDuration === 2 && applied.sourcePlayerId === scenario.playerIds[netherIndex] &&
          applied.sourceCardId === 'nt_005' && applied.sourceInstanceId === skeletonInstanceId &&
          applied.effectId === 'effect.nt_005.01',
        'wither-fortress: WITHER source or duration is incorrect');
        witherBatch = batches;
        continue;
      }

      const batches = await scenario.send(actorIndex, 'END_TURN', {}, `player ${actorIndex} end turn`);
      if (actorIndex === netherIndex && witherBatch) {
        const paymentIndex = eventIndex(batches[0], 'REDSTONE_CHANGED',
          (payload) => payload.effectId === 'effect.nt_008.01' && payload.reason === 'AUTOMATIC_PAYMENT');
        const summonIndex = eventIndex(batches[0], 'OBJECT_SUMMONED',
          (payload) => payload.effectId === 'effect.nt_008.01' && payload.cardId === 'tk_015');
        assert(paymentIndex >= 0 && summonIndex === paymentIndex + 1,
          'wither-fortress: Fortress payment and summon were not adjacent');
        assertProjectedEventEqual(batches, paymentIndex, 'wither-fortress payment');
        assertProjectedEventEqual(batches, summonIndex, 'wither-fortress summon');
        const payment = batches[0].events[paymentIndex].payload;
        const summon = batches[0].events[summonIndex].payload;
        assert(payment.sourceInstanceId === fortressInstanceId && summon.sourceInstanceId === fortressInstanceId &&
          summon.slotIndex === 0 && summon.attack === 3 && summon.health === 3,
        'wither-fortress: Fortress summon source, slot, or stats are incorrect');
        fortressBatch = batches;
      }
    }

    assert(witherBatch && fortressBatch, 'wither-fortress: scenario did not reach the checkpoint');
    const checkpointRevision = scenario.revision;
    const reconnectSnapshots = [];
    for (let index = 0; index < scenario.players.length; index += 1) {
      const recovered = await reconnectPlayer(
        scenario.players[index], scenario.matchId, `wither-fortress attempt ${attempt} player ${index} reconnect`);
      assert(recovered.matchId === scenario.matchId && recovered.revision === checkpointRevision,
        `wither-fortress: player ${index} recovered a different revision`);
      reconnectSnapshots.push(recovered);
    }

    const leftPublic = publicCheckpoint(reconnectSnapshots[0]);
    const rightPublic = publicCheckpoint(reconnectSnapshots[1]);
    assert(JSON.stringify(leftPublic) === JSON.stringify(rightPublic),
      'wither-fortress: reconnect public projections differ');
    const recoveredNether = playerState(reconnectSnapshots[0], scenario.playerIds[netherIndex]);
    const recoveredOcean = playerState(reconnectSnapshots[0], scenario.playerIds[oceanIndex]);
    const recoveredFortress = recoveredNether.battlefield.find((object) => object.instanceId === fortressInstanceId);
    const recoveredToken = recoveredNether.battlefield.find((object) => object.cardId === 'tk_015');
    const recoveredTurtle = recoveredOcean.battlefield.find((object) => object.instanceId === turtleInstanceId);
    const recoveredWither = recoveredTurtle?.statuses.find((status) => status.statusId === 'WITHER');
    assert(recoveredFortress?.cardId === 'nt_008' && recoveredFortress.occupiedSlots === 3,
      'wither-fortress: reconnect lost the Fortress');
    assert(recoveredToken?.slotIndex === 0 && recoveredToken.attack === 3 && recoveredToken.health === 3,
      'wither-fortress: reconnect lost the 3/3 token in slot 0');
    assert(recoveredNether.unitSlots[0] === recoveredToken.instanceId &&
      recoveredNether.buildingSlots.every((instanceId) => instanceId === fortressInstanceId),
    'wither-fortress: reconnect slot projection is incorrect');
    assert(recoveredWither?.remainingDuration === 2 &&
      recoveredWither.sourcePlayerId === scenario.playerIds[netherIndex] &&
      recoveredWither.sourceCardId === 'nt_005' &&
      recoveredWither.sourceInstanceId === skeletonInstanceId &&
      recoveredWither.effectId === 'effect.nt_005.01',
    'wither-fortress: reconnect lost WITHER duration or source');
    assert(Number.isInteger(reconnectSnapshots[0].nextInstanceId) &&
      reconnectSnapshots[0].nextInstanceId === reconnectSnapshots[1].nextInstanceId,
    'wither-fortress: reconnect nextInstanceId did not converge');

    assert(activeIndex(scenario) === oceanIndex && scenario.phase === 'MAIN',
      'wither-fortress: checkpoint should hand the turn to the Turtle controller');
    await scenario.send(oceanIndex, 'ENTER_COMBAT', {}, 'post-reconnect Ocean enter combat');
    const tickBatches = await scenario.send(oceanIndex, 'END_TURN', {}, 'post-reconnect Ocean end turn');
    const damageIndex = eventIndex(tickBatches[0], 'OBJECT_STATS_CHANGED',
      (payload) => payload.instanceId === turtleInstanceId && payload.effectId === 'effect.nt_005.01');
    const tickIndex = eventIndex(tickBatches[0], 'OBJECT_STATUS_TICKED',
      (payload) => payload.instanceId === turtleInstanceId && payload.statusId === 'WITHER');
    assert(damageIndex >= 0 && tickIndex === damageIndex + 1,
      'wither-fortress: post-reconnect WITHER damage and tick were not adjacent');
    assertProjectedEventEqual(tickBatches, damageIndex, 'wither-fortress post-reconnect damage');
    assertProjectedEventEqual(tickBatches, tickIndex, 'wither-fortress post-reconnect tick');
    assert(tickBatches[0].events[damageIndex].payload.damageType === 'TRUE' &&
      tickBatches[0].events[tickIndex].payload.remainingDuration === 1 &&
      tickBatches[0].events[tickIndex].payload.sourceInstanceId === skeletonInstanceId,
    'wither-fortress: post-reconnect WITHER tick payload is incorrect');

    return {
      ok: true,
      matchId: scenario.matchId,
      attempt,
      netherPlayerId: scenario.playerIds[netherIndex],
      oceanPlayerId: scenario.playerIds[oceanIndex],
      skeletonInstanceId,
      fortressInstanceId,
      turtleInstanceId,
      tokenInstanceId: recoveredToken.instanceId,
      witherRevision: witherBatch[0].revision,
      checkpointRevision,
      reconnectRevisions: reconnectSnapshots.map((snapshot) => snapshot.revision),
      tickRevision: tickBatches[0].revision,
      recoveredEnergy: {
        redstone: recoveredNether.redstone,
        temporaryRedstone: recoveredNether.temporaryRedstone,
        totalRedstone: recoveredNether.totalRedstone,
        redstoneCapacity: recoveredNether.redstoneCapacity
      },
      recoveredNextInstanceId: reconnectSnapshots[0].nextInstanceId,
      recoveredWither,
      witherEvents: eventEvidence(witherBatch[0], (event) =>
        event.type === 'ATTACK_RESOLVED' || event.type === 'OBJECT_STATUS_APPLIED'),
      fortressEvents: eventEvidence(fortressBatch[0], (event) =>
        event.payload.effectId === 'effect.nt_008.01'),
      tickEvents: eventEvidence(tickBatches[0], (event) =>
        event.payload.effectId === 'effect.nt_005.01'),
      publicCheckpoint: leftPublic,
      commands: scenario.commands
    };
  } finally {
    await closeScenario(scenario);
  }
}

async function retryScenario(name, runner) {
  let lastRetryReason = null;
  for (let attempt = 1; attempt <= maximumAttempts; attempt += 1) {
    const result = await runner(attempt);
    if (result.ok) return result;
    assert(result.retry, `${name}: non-retry result without success`);
    lastRetryReason = result.reason;
    console.warn(`${name} attempt ${attempt} will retry: ${result.reason}`);
  }
  throw new Error(`${name} exhausted ${maximumAttempts} attempts: ${lastRetryReason}`);
}

try {
  const strider = await retryScenario('Strider scenario', runStriderAttempt);
  const witherFortress = await retryScenario('WITHER/Fortress scenario', runWitherFortressAttempt);
  const result = {
    ok: true,
    protocolVersion,
    rulesetVersion,
    generatedAt: new Date().toISOString(),
    strider,
    witherFortress
  };
  await mkdir(dirname(reportPath), { recursive: true });
  await writeFile(reportPath, `${JSON.stringify(result, null, 2)}\n`, 'utf8');
  console.log(JSON.stringify(result, null, 2));
} catch (error) {
  const failure = {
    ok: false,
    protocolVersion,
    rulesetVersion,
    generatedAt: new Date().toISOString(),
    error: error instanceof Error ? error.message : String(error)
  };
  await mkdir(dirname(reportPath), { recursive: true });
  await writeFile(reportPath, `${JSON.stringify(failure, null, 2)}\n`, 'utf8');
  console.error(error);
  process.exitCode = 1;
}

// ws keeps a close-handshake timer alive after the report is durable.
process.exit(process.exitCode ?? 0);
