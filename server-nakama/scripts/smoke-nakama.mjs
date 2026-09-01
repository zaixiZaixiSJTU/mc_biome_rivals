import { randomUUID } from 'node:crypto';
import { Client } from '@heroiclabs/nakama-js';
import WebSocket from 'ws';

globalThis.WebSocket = WebSocket;

const host = process.env.BIOME_RIVALS_NAKAMA_HOST || '127.0.0.1';
const port = process.env.BIOME_RIVALS_NAKAMA_PORT || '17350';
const serverKey = process.env.BIOME_RIVALS_NAKAMA_SERVER_KEY || 'local_only_change_me';
const timeoutMs = Number(process.env.BIOME_RIVALS_SMOKE_TIMEOUT_MS || 30000);

function deferred(label) {
  let resolve;
  let reject;
  const promise = new Promise((resolvePromise, rejectPromise) => {
    resolve = resolvePromise;
    reject = rejectPromise;
  });
  const timer = setTimeout(() => reject(new Error(`${label} timed out after ${timeoutMs}ms`)), timeoutMs);
  return {
    promise: promise.finally(() => clearTimeout(timer)),
    resolve,
    reject
  };
}

function eventStream(playerIndex) {
  const queued = [];
  const waiters = [];
  let terminalError = null;
  return {
    push(payload) {
      const waiter = waiters.shift();
      if (waiter) waiter.resolve(payload);
      else queued.push(payload);
    },
    fail(error) {
      terminalError = error;
      while (waiters.length > 0) waiters.shift().reject(error);
    },
    next(label) {
      if (terminalError) return Promise.reject(terminalError);
      if (queued.length > 0) return Promise.resolve(queued.shift());
      const waiter = deferred(`player ${playerIndex} ${label}`);
      waiters.push(waiter);
      return waiter.promise;
    }
  };
}

async function createPlayer(index, factionId) {
  const client = new Client(serverKey, host, port, false);
  const session = await client.authenticateDevice(`biome-rivals-smoke-${index}-${randomUUID()}`, true);
  const socket = client.createSocket(false, false);
  const matched = deferred(`player ${index} matchmaking`);
  const snapshot = deferred(`player ${index} snapshot`);
  const batches = eventStream(index);
  socket.onmatchmakermatched = matched.resolve;
  socket.onmatchdata = (message) => {
    const payload = JSON.parse(new TextDecoder().decode(message.data));
    if (message.op_code === 4) snapshot.resolve(payload);
    if (message.op_code === 2) batches.push(payload);
    if (message.op_code === 3) batches.fail(new Error(`command rejected: ${JSON.stringify(payload)}`));
  };
  socket.onerror = (event) => {
    const error = new Error(`player ${index} socket error: ${event?.message || String(event)}`);
    matched.reject(error);
    snapshot.reject(error);
    batches.fail(error);
  };
  await socket.connect(session, true, timeoutMs);
  return { index, factionId, session, socket, matched, snapshot, nextEventBatch: batches.next };
}

const players = [];
try {
  players.push(await createPlayer(1, 'ocean_river'), await createPlayer(2, 'end'));
  await Promise.all(players.map((player) =>
    player.socket.addMatchmaker('*', 2, 2, { factionId: player.factionId })));
  const matches = await Promise.all(players.map((player) => player.matched.promise));
  const joined = await Promise.all(players.map((player, index) =>
    player.socket.joinMatch(matches[index].match_id, matches[index].token)));
  if (!joined.every((match) => match.authoritative)) throw new Error('matchmaker returned a non-authoritative match');
  if (joined[0].match_id !== joined[1].match_id) throw new Error('players joined different matches');

  const snapshots = await Promise.all(players.map((player) => player.snapshot.promise));
  for (let index = 0; index < players.length; index += 1) {
    const snapshot = snapshots[index];
    if (snapshot.protocolVersion !== 22 || snapshot.rulesetVersion !== 'prototype-0.33') {
      throw new Error(`snapshot ${index + 1} version mismatch: ${snapshot.protocolVersion}/${snapshot.rulesetVersion}`);
    }
    const ownPlayer = snapshot.players.find((entry) => entry.playerId === snapshot.viewerPlayerId);
    if (ownPlayer?.factionId !== players[index].factionId) {
      throw new Error(`player ${index + 1} faction mismatch: expected ${players[index].factionId}, got ${ownPlayer?.factionId}`);
    }
    const publicFactions = new Set(snapshot.players.map((entry) => entry.factionId));
    if (!publicFactions.has('ocean_river') || !publicFactions.has('end')) {
      throw new Error(`snapshot ${index + 1} did not expose both authoritative factions`);
    }
    if (snapshot.status !== 'MULLIGAN' || ownPlayer.mulliganCompleted) {
      throw new Error(`snapshot ${index + 1} did not begin in an unconfirmed mulligan state`);
    }
  }


  const mulliganCommandIds = players.map(() => `smoke-mulligan-${randomUUID()}`);
  await Promise.all(players.map((player, index) => player.socket.sendMatchState(joined[index].match_id, 1, JSON.stringify({
    protocolVersion: snapshots[index].protocolVersion,
    rulesetVersion: snapshots[index].rulesetVersion,
    commandId: mulliganCommandIds[index],
    expectedRevision: snapshots[index].revision,
    type: 'MULLIGAN',
    payload: { cardIndices: index === 0 ? [0] : [] }
  }))));
  const openingBatches = await Promise.all(players.map(async (player) => [
    await player.nextEventBatch('first mulligan batch'),
    await player.nextEventBatch('second mulligan batch')
  ]));
  if (!openingBatches.every((viewerBatches) =>
    viewerBatches[1].revision === 2 && viewerBatches[1].events.some((event) => event.type === 'MATCH_STARTED'))) {
    throw new Error('both clients did not observe the authoritative opening hand transition');
  }
  const activePlayerId = snapshots[0].players[snapshots[0].activePlayerIndex].playerId;
  const activeIndex = snapshots.findIndex((snapshot) => snapshot.viewerPlayerId === activePlayerId);
  if (activeIndex < 0) throw new Error('active player was not present in the two private snapshots');

  const commandId = `smoke-end-turn-${randomUUID()}`;
  await players[activeIndex].socket.sendMatchState(joined[activeIndex].match_id, 1, JSON.stringify({
    protocolVersion: snapshots[activeIndex].protocolVersion,
    rulesetVersion: snapshots[activeIndex].rulesetVersion,
    commandId,
    expectedRevision: 2,
    type: 'END_TURN',
    payload: {}
  }));
  const turnBatches = await Promise.all(players.map((player) => player.nextEventBatch('end turn batch')));
  if (!turnBatches.every((batch) => batch.acknowledgedCommandId === commandId)) {
    throw new Error('authoritative event batch did not acknowledge the submitted command');
  }

  console.log(JSON.stringify({
    ok: true,
    matchId: joined[0].match_id,
    players: players.map((player) => player.session.user_id),
    factions: players.map((player) => player.factionId),
    initialRevision: snapshots[0].revision,
    openingRevision: openingBatches[0][1].revision,
    acknowledgedCommandId: commandId,
    resultingRevision: turnBatches[0].revision
  }, null, 2));
} finally {
  for (const player of players) player.socket.disconnect(false);
}

// ws may keep its close handshake timer alive after the assertions have passed.
// This is a one-shot CLI check, so finish immediately once every socket is closed.
process.exit(0);
