import { randomUUID } from 'node:crypto';
import { mkdir, writeFile } from 'node:fs/promises';
import { dirname, resolve } from 'node:path';
import { Client } from '@heroiclabs/nakama-js';
import WebSocket from 'ws';

globalThis.WebSocket = WebSocket;

const host = process.env.BIOME_RIVALS_NAKAMA_HOST || '127.0.0.1';
const port = process.env.BIOME_RIVALS_NAKAMA_PORT || '17350';
const serverKey = process.env.BIOME_RIVALS_NAKAMA_SERVER_KEY || 'local_only_change_me';
const timeoutMs = Number(process.env.BIOME_RIVALS_SMOKE_TIMEOUT_MS || 30000);
const reportPath = resolve(process.env.BIOME_RIVALS_DRAW_PROBE_REPORT ||
  '../artifacts/simultaneous-draw-online-smoke.json');

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
  return { promise: promise.finally(() => clearTimeout(timer)), resolve: resolvePromise, reject: rejectPromise };
}

function stream(label) {
  const queue = [];
  const waiters = [];
  let terminalError = null;
  return {
    push(value) {
      const waiter = waiters.shift();
      if (waiter) waiter.resolve(value);
      else queue.push(value);
    },
    fail(error) {
      terminalError = error;
      while (waiters.length > 0) waiters.shift().reject(error);
    },
    next(itemLabel) {
      if (terminalError) return Promise.reject(terminalError);
      if (queue.length > 0) return Promise.resolve(queue.shift());
      const waiter = deferred(`${label} ${itemLabel}`);
      waiters.push(waiter);
      return waiter.promise;
    }
  };
}

function decode(message) {
  return JSON.parse(new TextDecoder().decode(message.data));
}

async function connect(player, label) {
  const snapshots = stream(`${label} snapshots`);
  const batches = stream(`${label} event batches`);
  const fixtureResults = stream(`${label} fixture results`);
  const socket = player.client.createSocket(false, false);
  socket.onmatchdata = (message) => {
    const payload = decode(message);
    if (message.op_code === 4 || message.op_code === 2) {
      player.trace.push({ opCode: message.op_code, json: JSON.stringify(payload) });
    }
    if (message.op_code === 4) snapshots.push(payload);
    else if (message.op_code === 2) batches.push(payload);
    else if (message.op_code === 5) fixtureResults.push(payload);
    else if (message.op_code === 3) {
      const error = new Error(`server rejected match action: ${JSON.stringify(payload)}`);
      batches.fail(error);
      fixtureResults.fail(error);
    }
  };
  socket.onerror = (event) => {
    const error = new Error(`${label} socket error: ${event?.message || String(event)}`);
    snapshots.fail(error);
    batches.fail(error);
    fixtureResults.fail(error);
  };
  player.socket = socket;
  player.snapshots = snapshots;
  player.batches = batches;
  player.fixtureResults = fixtureResults;
  await socket.connect(player.session, true, timeoutMs);
}

async function createPlayer(index, factionId) {
  const client = new Client(serverKey, host, port, false);
  const session = await client.authenticateDevice(`biome-rivals-draw-${index}-${randomUUID()}`, true);
  const player = { index, factionId, client, session, socket: null, trace: [] };
  await connect(player, `player ${index}`);
  player.matchmaking = deferred(`player ${index} matchmaking`);
  player.socket.onmatchmakermatched = player.matchmaking.resolve;
  return player;
}

function samePublicEvents(left, right) {
  return JSON.stringify(left.events.map((event) => `${event.eventId}:${event.type}`)) ===
    JSON.stringify(right.events.map((event) => `${event.eventId}:${event.type}`));
}

async function disconnect(player) {
  if (!player.socket) return;
  try { await player.socket.disconnect(false); } catch { /* expected while replacing a socket */ }
  player.socket = null;
}

const players = [];
let report;
try {
  players.push(await createPlayer(1, 'plains_forest'), await createPlayer(2, 'nether'));
  await Promise.all(players.map((player) =>
    player.socket.addMatchmaker('*', 2, 2, { factionId: player.factionId })));
  const matches = await Promise.all(players.map((player) => player.matchmaking.promise));
  const joined = await Promise.all(players.map((player, index) =>
    player.socket.joinMatch(matches[index].match_id, matches[index].token)));
  assert(joined.every((match) => match.authoritative), 'matchmaker returned a non-authoritative match');
  assert(joined[0].match_id === joined[1].match_id, 'players joined different matches');
  const matchId = joined[0].match_id;
  const snapshots = await Promise.all(players.map((player) => player.snapshots.next('initial')));
  assert(snapshots.every((snapshot) => snapshot.protocolVersion === 40 &&
    snapshot.rulesetVersion === 'prototype-0.65'), 'clients did not receive the current match versions');
  assert(snapshots.every((snapshot) => snapshot.status === 'MULLIGAN'), 'match did not begin in mulligan');

  const mulliganIds = players.map(() => `simultaneous-draw-mulligan-${randomUUID()}`);
  await Promise.all(players.map((player, index) => player.socket.sendMatchState(matchId, 1, JSON.stringify({
    protocolVersion: snapshots[index].protocolVersion,
    rulesetVersion: snapshots[index].rulesetVersion,
    commandId: mulliganIds[index],
    expectedRevision: snapshots[index].revision,
    type: 'MULLIGAN',
    payload: { cardIndices: [] }
  }))));
  const openingBatches = await Promise.all(players.map(async (player) => [
    await player.batches.next('first mulligan'),
    await player.batches.next('match start')
  ]));
  assert(openingBatches.every((pair) => pair[1].events.some((event) => event.type === 'MATCH_STARTED')),
    'both clients did not observe the authoritative match start');

  await players[0].socket.sendMatchState(matchId, 255, JSON.stringify({ fixture: 'simultaneous-defeat' }));
  const fixtureAck = await players[0].fixtureResults.next('fixture acknowledgement');
  assert(fixtureAck.ok === true && fixtureAck.reason === 'SIMULTANEOUS_DEFEAT',
    `fixture acknowledgement mismatch: ${JSON.stringify(fixtureAck)}`);
  const prepared = await Promise.all(players.map((player) => player.snapshots.next('fixture baseline')));
  for (let index = 0; index < prepared.length; index += 1) {
    const snapshot = prepared[index];
    assert(snapshot.matchId === matchId && snapshot.viewerPlayerId === players[index].session.user_id &&
      snapshot.revision === fixtureAck.revision - 1 && snapshot.status === 'ACTIVE' &&
      snapshot.players.every((player) => player.life === 0), 'fixture baseline was not synchronized before replay');
    const opponent = snapshot.players.find((player) => player.playerId !== snapshot.viewerPlayerId);
    assert(opponent.hand.every((card) => card === null) && opponent.handCards.every((card) => card === null),
      'fixture baseline exposed the opponent private hand');
  }
  const terminalBatches = await Promise.all(players.map((player) => player.batches.next('draw result')));
  assert(terminalBatches.every((batch) => batch.revision === fixtureAck.revision),
    'clients observed different draw revisions');
  assert(samePublicEvents(terminalBatches[0], terminalBatches[1]), 'clients observed different public draw events');
  for (const batch of terminalBatches) {
    const ended = batch.events.find((event) => event.type === 'MATCH_ENDED');
    assert(ended?.payload?.reason === 'SIMULTANEOUS_DEFEAT' && ended.payload.winnerPlayerId === null,
      `terminal event was not a winnerless draw: ${JSON.stringify(ended)}`);
  }
  const drawingPlayerId = terminalBatches[0].events.find((event) => event.type === 'CARD_PLAYED')?.payload?.playerId;
  assert(typeof drawingPlayerId === 'string', 'fixture replay omitted the playing player identity');
  for (let index = 0; index < terminalBatches.length; index += 1) {
    const draw = terminalBatches[index].events.find((event) => event.type === 'CARD_DRAWN');
    const viewerIsDrawer = terminalBatches[index].handProjection?.ownPlayerId === drawingPlayerId;
    assert((typeof draw?.payload?.cardId === 'string') === viewerIsDrawer,
      'drawn card identity crossed its private projection boundary');
  }

  await disconnect(players[1]);
  await connect(players[1], 'player 2 reconnect');
  await players[1].socket.joinMatch(matchId);
  const recovered = await players[1].snapshots.next('draw recovery');
  assert(recovered.status === 'FINISHED' && recovered.winnerPlayerId === null &&
    recovered.players.every((player) => player.life === 0) && recovered.matchId === matchId &&
    recovered.revision === fixtureAck.revision && recovered.viewerPlayerId === players[1].session.user_id,
  `reconnected snapshot did not restore the draw: ${JSON.stringify(recovered)}`);
  report = {
    ok: true,
    matchId,
    players: players.map((player) => player.session.user_id),
    protocolVersion: snapshots[0].protocolVersion,
    rulesetVersion: snapshots[0].rulesetVersion,
    mulliganRevision: openingBatches[0][1].revision,
    drawRevision: fixtureAck.revision,
    eventOrder: terminalBatches[0].events.map((event) => `${event.eventId}:${event.type}`),
    reconnectStatus: recovered.status,
    winnerPlayerId: recovered.winnerPlayerId,
    recoveredRevision: recovered.revision,
    traces: players.map((player) => ({ viewerPlayerId: player.session.user_id, messages: player.trace }))
  };
} finally {
  await Promise.all(players.map(disconnect));
}

if (report) {
  await mkdir(dirname(reportPath), { recursive: true });
  await writeFile(reportPath, `${JSON.stringify(report, null, 2)}\n`, { encoding: 'utf8', flag: 'wx' });
  const { traces, ...summary } = report;
  console.log(JSON.stringify({ ...summary, reportPath }, null, 2));
  process.exit(0);
}
