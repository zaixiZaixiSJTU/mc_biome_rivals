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
const maximumAttempts = Number(process.env.BIOME_RIVALS_RETURN_PROBE_ATTEMPTS || 12);
const reportPath = resolve(process.env.BIOME_RIVALS_RETURN_PROBE_REPORT ||
  '../artifacts/end-return-online-smoke.json');
const protocolVersion = 40;
const rulesetVersion = 'prototype-0.65';

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
  player.socket = socket;
  player.snapshots = snapshots;
  player.batches = batches;
  await socket.connect(player.session, true, timeoutMs);
}

async function createPlayer(attempt, index) {
  const client = new Client(serverKey, host, port, false);
  const session = await client.authenticateDevice(
    `biome-rivals-end-return-${attempt}-${index}-${randomUUID()}`, true);
  const player = { index, client, session, socket: null, snapshots: null, batches: null, matchmaker: null };
  await attachSocket(player, `attempt ${attempt} player ${index}`);
  player.matchmaker = deferred(`attempt ${attempt} player ${index} matchmaking`);
  player.socket.onmatchmakermatched = player.matchmaker.resolve;
  return player;
}

function ownState(snapshot) {
  return snapshot.players.find((player) => player.playerId === snapshot.viewerPlayerId);
}

function mulliganIndices(hand, returnCardId) {
  const retained = new Set();
  const returnCardIndex = hand.indexOf(returnCardId);
  const unitIndex = hand.findIndex((cardId) => cardId === 'ed_001' || cardId === 'ed_004');
  if (returnCardIndex >= 0) retained.add(returnCardIndex);
  if (unitIndex >= 0) retained.add(unitIndex);
  return hand.map((_, index) => index).filter((index) => !retained.has(index));
}

function samePublicEventOrder(batches, label) {
  assert(batches[0].revision === batches[1].revision, `${label}: clients observed different revisions`);
  const left = batches[0].events.map((event) => `${event.eventId}:${event.type}`);
  const right = batches[1].events.map((event) => `${event.eventId}:${event.type}`);
  assert(JSON.stringify(left) === JSON.stringify(right), `${label}: clients observed different public event order`);
}

function assertPrivateHandProjections(batches, playerIds, label) {
  for (let index = 0; index < batches.length; index += 1) {
    const projection = batches[index].handProjection;
    const opponentIndex = index === 0 ? 1 : 0;
    assert(projection?.ownPlayerId === playerIds[index] &&
      projection.opponentPlayerId === playerIds[opponentIndex],
    `${label}: hand projection identities were misrouted`);
    assert(projection.ownHand.length === projection.ownHandCards.length &&
      projection.opponentHandCount === batches[opponentIndex].handProjection.ownHand.length,
    `${label}: private hand projection counts disagree`);
    const opponentIds = new Set(batches[opponentIndex].handProjection.ownHandCards
      .map((card) => card.handCardInstanceId));
    for (const card of projection.ownHandCards) {
      assert(!opponentIds.has(card.handCardInstanceId),
        `${label}: opponent hand instance ${card.handCardInstanceId} leaked`);
    }
  }
}

function firstEvent(batch, type, predicate = () => true) {
  return batch.events.find((event) => event.type === type && predicate(event.payload));
}

async function disconnectPlayer(player) {
  if (!player.socket) return;
  try {
    await player.socket.disconnect(false);
  } catch {
    // The socket may already be gone during an intentional reconnect or cleanup.
  }
  player.socket = null;
}

async function reconnectPlayer(player, matchId, label) {
  await disconnectPlayer(player);
  await attachSocket(player, label);
  await player.socket.joinMatch(matchId);
  return player.snapshots.next('private recovery');
}

async function runAttempt(attempt, returnCardId, costModifier, redstoneCost) {
  const players = [];
  try {
    players.push(await createPlayer(attempt, 0));
    players.push(await createPlayer(attempt, 1));
    await Promise.all(players.map((player) =>
      player.socket.addMatchmaker('*', 2, 2, { factionId: 'end' })));
    const matches = await Promise.all(players.map((player) => player.matchmaker.promise));
    const joined = await Promise.all(players.map((player, index) =>
      player.socket.joinMatch(matches[index].match_id, matches[index].token)));
    assert(joined.every((match) => match.authoritative), 'matchmaker returned a non-authoritative match');
    assert(joined[0].match_id === joined[1].match_id, 'players joined different matches');
    const matchId = joined[0].match_id;
    const snapshots = await Promise.all(players.map((player) => player.snapshots.next('initial')));
    for (const snapshot of snapshots) {
      assert(snapshot.protocolVersion === protocolVersion && snapshot.rulesetVersion === rulesetVersion,
        `version mismatch: ${snapshot.protocolVersion}/${snapshot.rulesetVersion}`);
      assert(ownState(snapshot)?.factionId === 'end', 'probe player did not receive the End deck');
    }

    // Seat 1 starts with four cards. Its mulligan and first draw make the randomized smoke practical.
    const scenarioIndex = snapshots.findIndex((snapshot) =>
      snapshot.players.findIndex((player) => player.playerId === snapshot.viewerPlayerId) === 1);
    assert(scenarioIndex >= 0, 'could not identify the four-card scenario seat');
    const scenario = players[scenarioIndex];
    const observer = players[scenarioIndex === 0 ? 1 : 0];
    const scenarioPlayerId = snapshots[scenarioIndex].viewerPlayerId;
    const opponentPlayerId = snapshots[scenarioIndex === 0 ? 1 : 0].viewerPlayerId;
    let revision = snapshots[0].revision;
    let phase = snapshots[0].phase;
    let activePlayerId = snapshots[0].players[snapshots[0].activePlayerIndex].playerId;
    let scenarioHand = ownState(snapshots[scenarioIndex]).hand.slice();
    let scenarioHandCards = ownState(snapshots[scenarioIndex]).handCards.slice();
    let scenarioEnergy = ownState(snapshots[scenarioIndex]).totalRedstone;
    let scenarioUnits = new Map();
    let returnEvidence = null;
    let expiryEvidence = null;
    const commands = [];

    function applyEvents(batch) {
      for (const event of batch.events) {
        const payload = event.payload;
        if (payload.playerId === scenarioPlayerId && Number.isInteger(payload.totalRedstone)) {
          scenarioEnergy = payload.totalRedstone;
        }
        if (event.type === 'TURN_STARTED') {
          activePlayerId = payload.playerId;
          phase = payload.phase;
        } else if (event.type === 'PHASE_CHANGED') {
          phase = payload.phase;
        } else if (event.type === 'CARD_DEPLOYED' && payload.playerId === scenarioPlayerId &&
                   payload.cardType === 'UNIT') {
          scenarioUnits.set(payload.instanceId, {
            instanceId: payload.instanceId,
            cardId: payload.cardId,
            slotIndex: payload.slotIndex,
            health: payload.health
          });
        } else if (event.type === 'OBJECT_RETURNED' && payload.controllerPlayerId === scenarioPlayerId) {
          scenarioUnits.delete(payload.instanceId);
        } else if (event.type === 'OBJECT_DIED' && payload.playerId === scenarioPlayerId) {
          scenarioUnits.delete(payload.instanceId);
        }
        if (event.type === 'HAND_CARD_COST_MODIFIER_EXPIRED' && payload.playerId === scenarioPlayerId) {
          expiryEvidence = payload;
        }
      }
    }

    async function send(actor, type, payload, label) {
      const commandId = `end-return-${attempt}-${commands.length}-${randomUUID()}`;
      const commandPayload = { ...payload };
      if (type === 'DEPLOY_CARD' || type === 'PLAY_CARD') {
        const handCards = actor === scenario ? scenarioHandCards : [];
        const selected = handCards.find((card) => card.handCardInstanceId === commandPayload.handCardInstanceId &&
          card.cardId === commandPayload.cardId);
        assert(selected, `${label}: selected hand instance is missing`);
      }
      await actor.socket.sendMatchState(matchId, 1, JSON.stringify({
        protocolVersion,
        rulesetVersion,
        commandId,
        expectedRevision: revision,
        type,
        payload: commandPayload
      }));
      const batches = await Promise.all(players.map((player) => player.batches.next(label)));
      samePublicEventOrder(batches, label);
      assertPrivateHandProjections(batches, players.map((player) => player.session.user_id), label);
      assert(batches.every((batch) => batch.acknowledgedCommandId === commandId),
        `${label}: command acknowledgement did not converge`);
      revision = batches[0].revision;
      const ownBatch = batches[scenarioIndex];
      scenarioHand = ownBatch.handProjection.ownHand.slice();
      scenarioHandCards = ownBatch.handProjection.ownHandCards.slice();
      applyEvents(ownBatch);
      commands.push({ commandId, type, revision, eventTypes: ownBatch.events.map((event) => event.type) });
      return batches;
    }

    await send(players[scenarioIndex === 0 ? 1 : 0], 'MULLIGAN', { cardIndices: [] }, 'opponent mulligan');
    await send(scenario, 'MULLIGAN', { cardIndices: mulliganIndices(scenarioHand, returnCardId) }, 'scenario mulligan');

    for (let step = 0; step < 120 && returnEvidence === null; step += 1) {
      const actorIndex = players.findIndex((player) => player.session.user_id === activePlayerId);
      assert(actorIndex >= 0, 'active player was not one of the two probe clients');
      const actor = players[actorIndex];
      if (phase === 'MAIN' && actorIndex === scenarioIndex) {
        const openSlot = [0, 1, 2, 3].find((slotIndex) =>
          !Array.from(scenarioUnits.values()).some((unit) => unit.slotIndex === slotIndex));
        const playableUnit = scenarioHandCards.find((card) =>
          card.cardId === 'ed_001' || card.cardId === 'ed_004');
        const unitCost = playableUnit?.cardId === 'ed_001' ? 1 : 3;
        if (playableUnit && openSlot !== undefined && scenarioEnergy >= unitCost &&
            (scenarioUnits.size === 0 || !scenarioHand.includes(returnCardId))) {
          await send(scenario, 'DEPLOY_CARD', {
            cardId: playableUnit.cardId,
            handCardInstanceId: playableUnit.handCardInstanceId,
            slotKind: 'UNIT',
            slotIndex: openSlot,
            paymentMethod: 'REDSTONE'
          }, `deploy return target ${playableUnit.cardId}`);
          continue;
        }

        const returnCard = scenarioHandCards.find((card) => card.cardId === returnCardId);
        const target = scenarioUnits.values().next().value;
        if (returnCard && target && scenarioEnergy >= redstoneCost) {
          const energyBefore = scenarioEnergy;
          const batches = await send(scenario, 'PLAY_CARD', {
            cardId: returnCardId,
            handCardInstanceId: returnCard.handCardInstanceId,
            targetType: 'UNIT',
            targetInstanceId: target.instanceId
          }, `play ${returnCardId}`);
          const ownBatch = batches[scenarioIndex];
          const playedIndex = ownBatch.events.findIndex((event) =>
            event.type === 'CARD_PLAYED' && event.payload.cardId === returnCardId);
          const returnedIndex = ownBatch.events.findIndex((event) =>
            event.type === 'OBJECT_RETURNED' && event.payload.instanceId === target.instanceId);
          assert(playedIndex >= 0 && returnedIndex === playedIndex + 1,
            `${returnCardId} return was not causally adjacent to CARD_PLAYED`);
          const returned = ownBatch.events[returnedIndex].payload;
          assert(returned.destination === 'HAND' && returned.ownerPlayerId === scenarioPlayerId &&
            returned.controllerPlayerId === scenarioPlayerId && returned.sourcePlayerId === scenarioPlayerId &&
            returned.sourceCardId === returnCardId && returned.instanceId === target.instanceId &&
            returned.fromSlotKind === 'UNIT' && returned.fromSlotIndex === target.slotIndex,
          'OBJECT_RETURNED did not identify the owner, controller, and source');
          assert(returned.cardId === target.cardId && returned.returnedHandCardInstanceId &&
            returned.returnedHandCardInstanceId !== returnCard.handCardInstanceId &&
            returned.costModifier === costModifier && returned.expiresAtEndOfTurnPlayerId === scenarioPlayerId,
          `OBJECT_RETURNED did not create the exact returned hand instance with ${costModifier} expiry`);
          assert(scenarioEnergy === energyBefore - redstoneCost,
            `${returnCardId} did not pay exactly ${redstoneCost} redstone`);
          const returnedCard = scenarioHandCards.find((card) =>
            card.handCardInstanceId === returned.returnedHandCardInstanceId);
          assert(returnedCard?.cardId === target.cardId && returnedCard.costModifier === costModifier &&
            returnedCard.expiresAtEndOfTurnPlayerId === scenarioPlayerId,
          'owner private projection did not contain the discounted returned instance');
          const observerBatch = batches[scenarioIndex === 0 ? 1 : 0];
          const observerReturn = firstEvent(observerBatch, 'OBJECT_RETURNED',
            (payload) => payload.instanceId === target.instanceId);
          assert(observerReturn?.payload.returnedHandCardInstanceId === null &&
            observerReturn.payload.costModifier === null &&
            !observerBatch.handProjection.ownHandCards.some((card) =>
              card.handCardInstanceId === returned.returnedHandCardInstanceId),
          'opponent projection leaked the returned hand identity or discount');
          returnEvidence = {
            eventId: ownBatch.events[returnedIndex].eventId,
            targetInstanceId: target.instanceId,
            returnedCardId: returned.cardId,
            returnedHandCardInstanceId: returned.returnedHandCardInstanceId,
            costModifier: returned.costModifier,
            effectiveCost: Math.max(0, (returned.cardId === 'ed_001' ? 1 : 3) + costModifier),
            sourceCardId: returnCardId,
            sourceHandCardInstanceId: returnCard.handCardInstanceId,
            revision
          };
          break;
        }

        await send(scenario, 'ENTER_COMBAT', {}, 'scenario enter combat');
        phase = 'COMBAT';
      } else if (phase === 'MAIN') {
        await send(actor, 'ENTER_COMBAT', {}, 'opponent enter combat');
        phase = 'COMBAT';
      } else {
        await send(actor, 'END_TURN', {}, actorIndex === scenarioIndex ? 'scenario end turn' : 'opponent end turn');
      }
    }

    if (returnEvidence === null) {
      return { ok: false, retry: true, reason: `did not draw ${returnCardId} and a living unit before the probe limit` };
    }

    const returnRevision = revision;
    const recovered = await reconnectPlayer(scenario, matchId, `attempt ${attempt} scenario reconnect`);
    assert(recovered.matchId === matchId && recovered.revision === returnRevision,
      'reconnect snapshot did not resume the same authoritative revision');
    const recoveredObserver = await reconnectPlayer(observer, matchId, `attempt ${attempt} observer reconnect`);
    assert(recoveredObserver.matchId === matchId && recoveredObserver.revision === returnRevision,
      'observer reconnect snapshot did not resume the same authoritative revision');
    const recoveredState = ownState(recovered);
    const recoveredObserverState = ownState(recoveredObserver);
    const recoveredCard = recoveredState.handCards.find((card) =>
      card.handCardInstanceId === returnEvidence.returnedHandCardInstanceId);
    assert(recoveredCard?.cardId === returnEvidence.returnedCardId && recoveredCard.costModifier === costModifier &&
      recoveredCard.expiresAtEndOfTurnPlayerId === scenarioPlayerId,
    'reconnect snapshot did not restore the discounted returned hand instance');
    assert(recoveredState.battlefield.every((object) => object.instanceId !== returnEvidence.targetInstanceId),
      'reconnect snapshot still contained the returned battlefield object');
    const observerScenarioState = recoveredObserver.players.find((player) => player.playerId === scenarioPlayerId);
    assert(recoveredObserverState.playerId === opponentPlayerId && observerScenarioState &&
      observerScenarioState.hand.length === recoveredState.hand.length &&
      observerScenarioState.hand.every((card) => card === null) &&
      observerScenarioState.handCards.length === recoveredState.handCards.length &&
      observerScenarioState.handCards.every((card) => card === null),
    'observer reconnect snapshot did not preserve hand count while hiding card identities');
    const publicBoard = (snapshot) => snapshot.players.map((player) => ({
      playerId: player.playerId,
      battlefield: player.battlefield.map((object) => object.instanceId).sort(),
      unitSlots: player.unitSlots.slice(),
      buildingSlots: player.buildingSlots.slice()
    }));
    assert(JSON.stringify(publicBoard(recovered)) === JSON.stringify(publicBoard(recoveredObserver)),
      'reconnected clients did not recover an identical public battlefield projection');

    await send(scenario, 'ENTER_COMBAT', {}, 'post-reconnect enter combat');
    const expiryBatches = await send(scenario, 'END_TURN', {}, 'post-reconnect end turn');
    const ownExpiryEvents = expiryBatches[scenarioIndex].events.filter((event) =>
      event.type === 'HAND_CARD_COST_MODIFIER_EXPIRED' &&
      event.payload.handCardInstanceId === returnEvidence.returnedHandCardInstanceId);
    assert(ownExpiryEvents.length === 1 && ownExpiryEvents[0].payload.expiredCostModifier === costModifier &&
      ownExpiryEvents[0].payload.costModifier === 0,
    'ending the source turn did not expire the exact returned instance modifier');
    const expiryObserverBatch = expiryBatches[scenarioIndex === 0 ? 1 : 0];
    const redactedExpiry = firstEvent(expiryObserverBatch, 'HAND_CARD_COST_MODIFIER_EXPIRED',
      (payload) => payload.expiredAtEndOfTurnPlayerId === scenarioPlayerId);
    assert(redactedExpiry?.payload.handCardInstanceId === null && redactedExpiry.payload.cardId === null &&
      redactedExpiry.payload.expiredCostModifier === null,
    'opponent projection leaked private hand-cost expiry details');
    expiryEvidence = ownExpiryEvents[0].payload;

    return {
      ok: true,
      matchId,
      attempt,
      scenarioPlayerId,
      opponentPlayerId,
      returnRevision,
      finalRevision: revision,
      returnEvidence,
      expiryEvidence,
      reconnectSnapshot: {
        matchId: recovered.matchId,
        revision: recovered.revision,
        returnedHandCardInstanceId: recoveredCard.handCardInstanceId,
        costModifier: recoveredCard.costModifier,
        observerHandCount: observerScenarioState.hand.length,
        observerHandIdentityRedacted: true,
        publicBattlefieldConverged: true
      },
      publicEventIds: expiryBatches[0].events.map((event) => event.eventId),
      commands
    };
  } finally {
    await Promise.all(players.map(disconnectPlayer));
  }
}

let result = null;
let lastRetryReason = null;
try {
  const scenarios = [];
  for (const testCase of [
    { cardId: 'ed_002', costModifier: -1, redstoneCost: 1 },
    { cardId: 'ed_005', costModifier: -2, redstoneCost: 2 }
  ]) {
    let scenarioResult = null;
    lastRetryReason = null;
    for (let attempt = 1; attempt <= maximumAttempts; attempt += 1) {
      const attemptResult = await runAttempt(attempt, testCase.cardId,
        testCase.costModifier, testCase.redstoneCost);
      if (attemptResult.ok) {
        scenarioResult = attemptResult;
        break;
      }
      lastRetryReason = attemptResult.reason;
      console.warn(`${testCase.cardId} smoke attempt ${attempt} will retry: ${attemptResult.reason}`);
    }
    if (!scenarioResult) {
      throw new Error(`${testCase.cardId} smoke exhausted ${maximumAttempts} attempts: ${lastRetryReason}`);
    }
    scenarios.push(scenarioResult);
  }
  result = { ok: true, protocolVersion, rulesetVersion, scenarios };
  await mkdir(dirname(reportPath), { recursive: true });
  await writeFile(reportPath, `${JSON.stringify(result, null, 2)}\n`, 'utf8');
  console.log(JSON.stringify(result, null, 2));
} catch (error) {
  const failure = { ok: false, error: error instanceof Error ? error.message : String(error) };
  await mkdir(dirname(reportPath), { recursive: true });
  await writeFile(reportPath, `${JSON.stringify(failure, null, 2)}\n`, 'utf8');
  console.error(error);
  process.exitCode = 1;
}

// ws leaves a close-handshake timer alive after a one-shot validation; exit after the report is durable.
process.exit(process.exitCode ?? 0);
