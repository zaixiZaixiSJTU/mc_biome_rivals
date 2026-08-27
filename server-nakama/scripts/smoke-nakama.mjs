import { randomUUID } from 'node:crypto';
import { Client } from '@heroiclabs/nakama-js';
import WebSocket from 'ws';

globalThis.WebSocket = WebSocket;

const host = process.env.BIOME_RIVALS_NAKAMA_HOST || '127.0.0.1';
const port = process.env.BIOME_RIVALS_NAKAMA_PORT || '7350';
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

async function createPlayer(index) {
  const client = new Client(serverKey, host, port, false);
  const session = await client.authenticateDevice(`biome-rivals-smoke-${index}-${randomUUID()}`, true);
  const socket = client.createSocket(false, false);
  const matched = deferred(`player ${index} matchmaking`);
  const snapshot = deferred(`player ${index} snapshot`);
  const eventBatch = deferred(`player ${index} event batch`);
  socket.onmatchmakermatched = matched.resolve;
  socket.onmatchdata = (message) => {
    const payload = JSON.parse(new TextDecoder().decode(message.data));
    if (message.op_code === 4) snapshot.resolve(payload);
    if (message.op_code === 2) eventBatch.resolve(payload);
    if (message.op_code === 3) eventBatch.reject(new Error(`command rejected: ${JSON.stringify(payload)}`));
  };
  socket.onerror = (event) => {
    const error = new Error(`player ${index} socket error: ${event?.message || String(event)}`);
    matched.reject(error);
    snapshot.reject(error);
    eventBatch.reject(error);
  };
  await socket.connect(session, true, timeoutMs);
  return { index, session, socket, matched, snapshot, eventBatch };
}

const players = [];
try {
  players.push(await createPlayer(1), await createPlayer(2));
  await Promise.all(players.map((player) => player.socket.addMatchmaker('*', 2, 2)));
  const matches = await Promise.all(players.map((player) => player.matched.promise));
  const joined = await Promise.all(players.map((player, index) =>
    player.socket.joinMatch(matches[index].match_id, matches[index].token)));
  if (!joined.every((match) => match.authoritative)) throw new Error('matchmaker returned a non-authoritative match');
  if (joined[0].match_id !== joined[1].match_id) throw new Error('players joined different matches');

  const snapshots = await Promise.all(players.map((player) => player.snapshot.promise));
  const activePlayerId = snapshots[0].players[snapshots[0].activePlayerIndex].playerId;
  const activeIndex = snapshots.findIndex((snapshot) => snapshot.viewerPlayerId === activePlayerId);
  if (activeIndex < 0) throw new Error('active player was not present in the two private snapshots');

  const commandId = `smoke-end-turn-${randomUUID()}`;
  await players[activeIndex].socket.sendMatchState(joined[activeIndex].match_id, 1, JSON.stringify({
    protocolVersion: snapshots[activeIndex].protocolVersion,
    rulesetVersion: snapshots[activeIndex].rulesetVersion,
    commandId,
    expectedRevision: snapshots[activeIndex].revision,
    type: 'END_TURN',
    payload: {}
  }));
  const batches = await Promise.all(players.map((player) => player.eventBatch.promise));
  if (!batches.every((batch) => batch.acknowledgedCommandId === commandId)) {
    throw new Error('authoritative event batch did not acknowledge the submitted command');
  }

  console.log(JSON.stringify({
    ok: true,
    matchId: joined[0].match_id,
    players: players.map((player) => player.session.user_id),
    initialRevision: snapshots[0].revision,
    acknowledgedCommandId: commandId,
    resultingRevision: batches[0].revision
  }, null, 2));
} finally {
  for (const player of players) player.socket.disconnect(false);
}

// ws may keep its close handshake timer alive after the assertions have passed.
// This is a one-shot CLI check, so finish immediately once every socket is closed.
process.exit(0);
