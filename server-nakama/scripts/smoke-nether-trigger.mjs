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
const maximumAttempts = Number(process.env.BIOME_RIVALS_NETHER_PROBE_ATTEMPTS || 12);
const reportPath = resolve(process.env.BIOME_RIVALS_NETHER_PROBE_REPORT ||
  '../artifacts/nether-trigger-online-probe.json');
const protocolVersion = 36;
const rulesetVersion = 'prototype-0.60';

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

async function createPlayer(attempt, index) {
  const client = new Client(serverKey, host, port, false);
  const session = await client.authenticateDevice(
    `biome-rivals-nether-e2-${attempt}-${index}-${randomUUID()}`, true);
  const player = { index, client, session, socket: null, snapshots: null, batches: null, matchmaker: null };
  await attachSocket(player, `attempt ${attempt} player ${index}`);
  player.matchmaker = deferred(`attempt ${attempt} player ${index} matchmaking`);
  player.socket.onmatchmakermatched = player.matchmaker.resolve;
  return player;
}

function ownState(snapshot) {
  return snapshot.players.find((player) => player.playerId === snapshot.viewerPlayerId);
}

function mulliganIndices(hand) {
  let keptPiglin = false;
  let keptSelfDamage = false;
  let keptAnchors = 0;
  const replace = [];
  for (let index = 0; index < hand.length; index += 1) {
    const cardId = hand[index];
    if (cardId === 'nt_002' && !keptPiglin) keptPiglin = true;
    else if (cardId === 'nt_006' && !keptSelfDamage) keptSelfDamage = true;
    else if (cardId === 'nt_007' && keptAnchors < 2) keptAnchors += 1;
    else replace.push(index);
  }
  return replace;
}

function samePublicEventOrder(batches, label) {
  assert(batches[0].revision === batches[1].revision, `${label}: clients observed different revisions`);
  const left = batches[0].events.map((event) => `${event.eventId}:${event.type}`);
  const right = batches[1].events.map((event) => `${event.eventId}:${event.type}`);
  assert(JSON.stringify(left) === JSON.stringify(right), `${label}: clients observed different public event order`);
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

async function runAttempt(attempt) {
  const players = [];
  try {
    players.push(await createPlayer(attempt, 0), await createPlayer(attempt, 1));
    await Promise.all(players.map((player) =>
      player.socket.addMatchmaker('*', 2, 2, { factionId: 'nether' })));
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
      assert(ownState(snapshot)?.factionId === 'nether', 'probe player did not receive the Nether deck');
    }

    // Seat 1 starts with four cards while seat 0 starts with three and receives the opening draw.
    // Keeping seat 1 as the scenario actor gives the mulligan one additional deterministic sample.
    const scenarioIndex = snapshots.findIndex((snapshot) =>
      snapshot.players.findIndex((player) => player.playerId === snapshot.viewerPlayerId) === 1);
    assert(scenarioIndex >= 0, 'could not identify the four-card scenario seat');
    const scenario = players[scenarioIndex];
    const observer = players[scenarioIndex === 0 ? 1 : 0];
    const scenarioPlayerId = snapshots[scenarioIndex].viewerPlayerId;
    const observerPlayerId = snapshots[scenarioIndex === 0 ? 1 : 0].viewerPlayerId;
    let revision = snapshots[0].revision;
    let phase = snapshots[0].phase;
    let activePlayerId = snapshots[0].players[snapshots[0].activePlayerIndex].playerId;
    let scenarioHand = ownState(snapshots[scenarioIndex]).hand.slice();
    let scenarioEnergy = ownState(snapshots[scenarioIndex]).totalRedstone;
    let scenarioLife = ownState(snapshots[scenarioIndex]).life;
    let observerLife = ownState(snapshots[scenarioIndex === 0 ? 1 : 0]).life;
    let anchorCount = 0;
    let piglinInstanceId = null;
    let unitFillers = 0;
    let scenarioLifeLossUsedThisTurn = false;
    let triggeredBatch = null;
    let finalBatch = null;
    const commands = [];

    function applyScenarioProjection(batch) {
      for (const event of batch.events) {
        const payload = event.payload;
        if (event.type === 'MULLIGAN_COMPLETED' && payload.playerId === scenarioPlayerId) {
          scenarioHand = payload.hand.slice();
        } else if (event.type === 'CARD_DRAWN' && payload.playerId === scenarioPlayerId) {
          scenarioHand.push(payload.cardId);
        } else if ((event.type === 'CARD_DEPLOYED' || event.type === 'CARD_PLAYED') &&
                   payload.playerId === scenarioPlayerId) {
          const index = scenarioHand.indexOf(payload.cardId);
          assert(index >= 0, `scenario hand did not contain played card ${payload.cardId}`);
          scenarioHand.splice(index, 1);
        }
        if (payload.playerId === scenarioPlayerId && Number.isInteger(payload.totalRedstone)) {
          scenarioEnergy = payload.totalRedstone;
        }
        if ((event.type === 'HERO_DAMAGED' || event.type === 'FATIGUE_DAMAGE') &&
            payload.playerId === scenarioPlayerId) scenarioLife = payload.life;
        if ((event.type === 'HERO_DAMAGED' || event.type === 'FATIGUE_DAMAGE') &&
            payload.playerId === observerPlayerId) observerLife = payload.life;
        if (event.type === 'PHASE_CHANGED') phase = payload.phase;
        if (event.type === 'TURN_STARTED') {
          activePlayerId = payload.playerId;
          phase = payload.phase;
        }
      }
    }

    async function send(actor, type, payload, label) {
      const commandId = `nether-e2-${attempt}-${commands.length}-${randomUUID()}`;
      await actor.socket.sendMatchState(matchId, 1, JSON.stringify({
        protocolVersion,
        rulesetVersion,
        commandId,
        expectedRevision: revision,
        type,
        payload
      }));
      const batches = await Promise.all(players.map((player) => player.batches.next(label)));
      samePublicEventOrder(batches, label);
      assert(batches.every((batch) => batch.acknowledgedCommandId === commandId),
        `${label}: command acknowledgement did not converge`);
      revision = batches[0].revision;
      applyScenarioProjection(batches[scenarioIndex]);
      commands.push({ commandId, type, revision, eventTypes: batches[0].events.map((event) => event.type) });
      return batches;
    }

    await send(observer, 'MULLIGAN', { cardIndices: [] }, 'observer mulligan');
    await send(scenario, 'MULLIGAN', { cardIndices: mulliganIndices(scenarioHand) }, 'scenario mulligan');

    let scenarioOwnTurns = 0;
    let scenarioEnteredThisTurn = false;
    let stalledOwnTurns = 0;
    for (let step = 0; step < 120 && !triggeredBatch; step += 1) {
      const actorIndex = players.findIndex((player) => player.session.user_id === activePlayerId);
      assert(actorIndex >= 0, 'active player was not one of the two probe clients');
      const actor = players[actorIndex];
      if (phase === 'MAIN') {
        if (actorIndex === scenarioIndex) {
          if (!scenarioEnteredThisTurn) {
            scenarioOwnTurns += 1;
            scenarioEnteredThisTurn = true;
          }
          let progressed = false;
          if (anchorCount < 2 && scenarioHand.includes('nt_007') && scenarioEnergy >= 3) {
            const slotIndex = anchorCount === 0 ? 0 : 2;
            await send(scenario, 'DEPLOY_CARD', {
              cardId: 'nt_007', slotKind: 'BUILDING', slotIndex, paymentMethod: 'REDSTONE'
            }, `deploy anchor ${anchorCount + 1}`);
            anchorCount += 1;
            progressed = true;
          } else if (anchorCount >= 2 && piglinInstanceId === null &&
                     scenarioHand.includes('nt_002') && scenarioEnergy >= 2) {
            const batches = await send(scenario, 'DEPLOY_CARD', {
              cardId: 'nt_002', slotKind: 'UNIT', slotIndex: 0, paymentMethod: 'REDSTONE'
            }, 'deploy piglin');
            piglinInstanceId = firstEvent(batches[scenarioIndex], 'CARD_DEPLOYED',
              (payload) => payload.playerId === scenarioPlayerId && payload.cardId === 'nt_002').payload.instanceId;
            progressed = true;
          } else if (anchorCount >= 2 && piglinInstanceId !== null && !scenarioLifeLossUsedThisTurn &&
                     scenarioHand.includes('nt_006') && scenarioEnergy >= 1) {
            const batches = await send(scenario, 'PLAY_CARD', { cardId: 'nt_006' }, 'trigger self damage');
            const ownBatch = batches[scenarioIndex];
            const marker = firstEvent(ownBatch, 'HERO_LIFE_LOSS_MARKED',
              (payload) => payload.playerId === scenarioPlayerId);
            const growth = firstEvent(ownBatch, 'OBJECT_STATS_CHANGED',
              (payload) => payload.instanceId === piglinInstanceId && payload.effectId === 'effect.nt_002.01');
            const grants = ownBatch.events.filter((event) => event.type === 'REDSTONE_CHANGED' &&
              event.payload.playerId === scenarioPlayerId && event.payload.effectId === 'effect.nt_007.01');
            assert(marker, 'self-damage batch omitted the first-life-loss marker');
            assert(growth?.payload.attack === 3 && growth?.payload.health === 3,
              'self-damage batch did not grow the piglin to 3/3');
            assert(grants.length === anchorCount && grants.every((event, index) =>
              event.payload.temporaryRedstone === index + 1),
              `${anchorCount} anchors did not grant temporary redstone in stable order`);
            triggeredBatch = batches;
            break;
          } else if (anchorCount === 2 && scenarioHand.includes('nt_007') && scenarioEnergy >= 3 &&
                     (piglinInstanceId === null || !scenarioHand.includes('nt_006'))) {
            await send(scenario, 'DEPLOY_CARD', {
              cardId: 'nt_007', slotKind: 'BUILDING', slotIndex: 1, paymentMethod: 'REDSTONE'
            }, 'deploy optional third anchor');
            anchorCount += 1;
            progressed = true;
          } else if (!scenarioLifeLossUsedThisTurn && scenarioLife > 8 &&
                     scenarioHand.filter((cardId) => cardId === 'nt_006').length > 1 && scenarioEnergy >= 1) {
            await send(scenario, 'PLAY_CARD', { cardId: 'nt_006' }, 'cycle extra self-damage card');
            scenarioLifeLossUsedThisTurn = true;
            progressed = true;
          } else {
            const fillerCosts = { nt_001: 1, nt_003: 3, nt_004: 2, nt_005: 4 };
            const filler = Object.keys(fillerCosts).find((cardId) => scenarioHand.includes(cardId) &&
              scenarioEnergy >= fillerCosts[cardId]);
            if (filler && unitFillers < 3) {
              await send(scenario, 'DEPLOY_CARD', {
                cardId: filler, slotKind: 'UNIT', slotIndex: unitFillers + 1, paymentMethod: 'REDSTONE'
              }, `deploy filler ${unitFillers + 1}`);
              unitFillers += 1;
              progressed = true;
            }
          }
          if (progressed) {
            stalledOwnTurns = 0;
            continue;
          }
          stalledOwnTurns += 1;
          if (stalledOwnTurns >= 7 && scenarioHand.length >= 7 &&
              (!scenarioHand.includes('nt_007') || anchorCount < 2 ||
               !scenarioHand.includes('nt_002') || !scenarioHand.includes('nt_006'))) {
            return { ok: false, retry: true, reason: 'scenario hand stalled before collecting the required cards' };
          }
        }
        await send(actor, 'ENTER_COMBAT', {}, actorIndex === scenarioIndex ? 'scenario enter combat' : 'observer enter combat');
      } else {
        await send(actor, 'END_TURN', {}, actorIndex === scenarioIndex ? 'scenario end turn' : 'observer end turn');
        if (actorIndex === scenarioIndex) {
          scenarioEnteredThisTurn = false;
          scenarioLifeLossUsedThisTurn = false;
        }
      }
    }

    if (!triggeredBatch) {
      return { ok: false, retry: true, reason: 'required cards were not collected before the attempt limit' };
    }

    const triggerRevision = revision;
    const recovered = await reconnectPlayer(scenario, matchId, `attempt ${attempt} scenario reconnect`);
    assert(recovered.matchId === matchId && recovered.revision === triggerRevision,
      'reconnect snapshot did not resume the same authoritative revision');
    const recoveredPlayer = ownState(recovered);
    const recoveredPiglin = recoveredPlayer.battlefield.find((object) => object.instanceId === piglinInstanceId);
    assert(recoveredPlayer.temporaryRedstone === anchorCount &&
      recoveredPlayer.totalRedstone === recoveredPlayer.redstone + anchorCount,
      `reconnect snapshot did not preserve ${anchorCount} temporary redstone points`);
    assert(recoveredPiglin?.attack === 3 && recoveredPiglin?.health === 3,
      'reconnect snapshot did not preserve piglin permanent growth');
    assert(recoveredPlayer.battlefield.filter((object) => object.cardId === 'nt_007').length === anchorCount,
      'reconnect snapshot did not preserve every respawn anchor');

    await send(scenario, 'ENTER_COMBAT', {}, 'post-reconnect enter combat');
    const observerLifeBeforeMagma = observerLife;
    finalBatch = await send(scenario, 'END_TURN', {}, 'post-reconnect end turn');
    const finalOwnBatch = finalBatch[scenarioIndex];
    const automaticPaymentIndex = finalOwnBatch.events.findIndex((event) => event.type === 'REDSTONE_CHANGED' &&
      event.payload.effectId === 'effect.nt_002.01' && event.payload.reason === 'AUTOMATIC_PAYMENT');
    const magmaDamageIndex = finalOwnBatch.events.findIndex((event) => event.type === 'HERO_DAMAGED' &&
      event.payload.effectId === 'effect.nt_002.01');
    const expiryIndex = finalOwnBatch.events.findIndex((event) => event.type === 'REDSTONE_CHANGED' &&
      event.payload.reason === 'TEMPORARY_EXPIRED');
    assert(automaticPaymentIndex >= 0 && magmaDamageIndex === automaticPaymentIndex + 1 && expiryIndex > magmaDamageIndex,
      'end-turn batch did not order automatic payment, magma damage, and temporary expiry');
    assert(finalOwnBatch.events[automaticPaymentIndex].payload.temporaryRedstone === anchorCount - 1,
      'piglin magma did not spend one temporary redstone first');
    assert(finalOwnBatch.events[expiryIndex].payload.temporaryRedstone === 0,
      'remaining temporary redstone did not expire');
    assert(observerLife === observerLifeBeforeMagma - 1,
      'piglin magma did not reduce the opposing hero by one life');

    return {
      ok: true,
      matchId,
      attempt,
      scenarioPlayerId,
      observerPlayerId,
      scenarioOwnTurns,
      anchorCount,
      triggerRevision,
      reconnectRevision: recovered.revision,
      finalRevision: revision,
      scenarioLife,
      observerLife,
      triggerEventTypes: triggeredBatch[0].events.map((event) => event.type),
      finalEventTypes: finalBatch[0].events.map((event) => event.type),
      triggerEventIds: triggeredBatch[0].events.map((event) => event.eventId),
      finalEventIds: finalBatch[0].events.map((event) => event.eventId),
      recoveredTemporaryRedstone: recoveredPlayer.temporaryRedstone,
      recoveredPiglinAttack: recoveredPiglin.attack,
      recoveredPiglinHealth: recoveredPiglin.health,
      commands
    };
  } finally {
    await Promise.all(players.map(disconnectPlayer));
  }
}

let result = null;
let lastRetryReason = null;
try {
  for (let attempt = 1; attempt <= maximumAttempts; attempt += 1) {
    const attemptResult = await runAttempt(attempt);
    if (attemptResult.ok) {
      result = attemptResult;
      break;
    }
    lastRetryReason = attemptResult.reason;
    console.warn(`Nether trigger probe attempt ${attempt} will retry: ${attemptResult.reason}`);
  }
  if (!result) throw new Error(`Nether trigger probe exhausted ${maximumAttempts} attempts: ${lastRetryReason}`);
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

// ws keeps a close-handshake timer alive after the assertions have completed.
// This is a one-shot validation process, so terminate once the report is durable.
process.exit(process.exitCode ?? 0);
