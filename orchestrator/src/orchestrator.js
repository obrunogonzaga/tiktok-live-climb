import { randomUUID } from 'node:crypto';
import { readFileSync } from 'node:fs';
import { readFile, realpath, stat } from 'node:fs/promises';
import { createServer } from 'node:http';
import { extname, isAbsolute, relative, resolve, sep } from 'node:path';
import { fileURLToPath } from 'node:url';

import Ajv2020 from 'ajv/dist/2020.js';
import addFormats from 'ajv-formats';
import { WebSocket, WebSocketServer } from 'ws';

import { actionForUnity, loadLadder, resolveGift } from './ladder.js';

const MAX_BODY_BYTES = 1_048_576;
const MAX_REMEMBERED_IDS = 10_000;
const TOAST_DURATION_MS = 2_500;
const ROUND_STATUSES = new Set(['idle', 'climbing', 'fell', 'summit']);

function repoPath(relativePath) {
  return fileURLToPath(new URL(`../../${relativePath}`, import.meta.url));
}

function isRecord(value) {
  return value !== null && typeof value === 'object' && !Array.isArray(value);
}

function createValidators(liveEventSchemaPath, overlayStateSchemaPath) {
  const ajv = new Ajv2020({ allErrors: true, strict: false });
  addFormats(ajv);

  const liveEventSchema = JSON.parse(readFileSync(liveEventSchemaPath, 'utf8'));
  const overlayStateSchema = JSON.parse(readFileSync(overlayStateSchemaPath, 'utf8'));

  return {
    validateLiveEvent: ajv.compile(liveEventSchema),
    validateOverlayState: ajv.compile(overlayStateSchema)
  };
}

function readRequestBody(request) {
  return new Promise((resolve, reject) => {
    const chunks = [];
    let length = 0;
    let tooLarge = false;

    request.on('data', (chunk) => {
      if (tooLarge) {
        return;
      }

      length += chunk.length;
      if (length > MAX_BODY_BYTES) {
        tooLarge = true;
        reject(new Error('payload_too_large'));
        request.resume();
        return;
      }

      chunks.push(chunk);
    });
    request.on('end', () => {
      if (!tooLarge) {
        resolve(Buffer.concat(chunks).toString('utf8'));
      }
    });
    request.on('error', reject);
  });
}

function sendJson(response, statusCode, payload) {
  response.writeHead(statusCode, { 'content-type': 'application/json; charset=utf-8' });
  response.end(JSON.stringify(payload));
}

function listen(server, port, host) {
  return new Promise((resolve, reject) => {
    const onError = (error) => {
      server.off('listening', onListening);
      reject(error);
    };
    const onListening = () => {
      server.off('error', onError);
      resolve();
    };

    server.once('error', onError);
    server.once('listening', onListening);
    server.listen(port, host);
  });
}

function closeServer(server) {
  return new Promise((resolve, reject) => {
    server.close((error) => {
      if (error && error.code !== 'ERR_SERVER_NOT_RUNNING') {
        reject(error);
        return;
      }
      resolve();
    });
  });
}

function parseRoundMessage(raw) {
  let value;
  try {
    value = JSON.parse(raw.toString());
  } catch {
    return null;
  }

  if (!isRecord(value) || Object.keys(value).some((key) => !['index', 'status', 'height', 'record'].includes(key))) {
    return null;
  }

  if (!Number.isInteger(value.index) || value.index < 1 || !ROUND_STATUSES.has(value.status)) {
    return null;
  }

  const hasProgress = Object.hasOwn(value, 'height') || Object.hasOwn(value, 'record');
  if (hasProgress && (!Number.isSafeInteger(value.height) || value.height < 0 ||
      !Number.isSafeInteger(value.record) || value.record < value.height)) {
    return null;
  }
  return hasProgress
    ? { index: value.index, status: value.status, height: value.height, record: value.record }
    : { index: value.index, status: value.status };
}

function toDate(clock) {
  const value = clock();
  return value instanceof Date ? value : new Date(value);
}

function isWithinDirectory(directory, candidate) {
  const pathFromDirectory = relative(directory, candidate);
  return pathFromDirectory !== ''
    && pathFromDirectory !== '..'
    && !pathFromDirectory.startsWith(`..${sep}`)
    && !isAbsolute(pathFromDirectory);
}

function staticAssetPath(overlayRoot, requestUrl) {
  let decodedPathname;
  try {
    decodedPathname = decodeURIComponent(
      new URL(requestUrl ?? '/', 'http://127.0.0.1').pathname
    );
  } catch {
    return null;
  }

  const relativePath = decodedPathname === '/' ? 'index.html' : decodedPathname.slice(1);
  if (!relativePath || relativePath.includes('\0')) {
    return null;
  }

  const extension = extname(relativePath).toLowerCase();
  if (relativePath !== 'index.html' && extension !== '.css' && extension !== '.js') {
    return null;
  }

  const candidate = resolve(overlayRoot, relativePath);
  return isWithinDirectory(overlayRoot, candidate) ? candidate : null;
}

function staticContentType(path) {
  switch (extname(path).toLowerCase()) {
    case '.css':
      return 'text/css; charset=utf-8';
    case '.js':
      return 'text/javascript; charset=utf-8';
    default:
      return 'text/html; charset=utf-8';
  }
}

function sendStaticNotFound(response) {
  response.writeHead(404, { 'content-type': 'text/plain; charset=utf-8' });
  response.end('Not found');
}

/**
 * Creates the synthetic-event Orchestrator. Call start() once, then close()
 * when the owning process exits. It also hosts the local static overlay.
 */
export function createOrchestrator(options = {}) {
  const host = options.host ?? '127.0.0.1';
  const httpPort = options.httpPort ?? 8765;
  const wsPort = options.wsPort ?? 8766;
  const overlayPort = options.overlayPort ?? 8790;
  const broadcastIntervalMs = options.broadcastIntervalMs ?? 100;
  const clock = options.clock ?? (() => new Date());
  const overlayRoot = resolve(options.overlayRoot ?? repoPath('overlay'));
  const ladder = loadLadder(options.ladderPath ?? repoPath('config/ladder.v1.toml'));
  const { validateLiveEvent, validateOverlayState } = createValidators(
    options.liveEventSchemaPath ?? repoPath('schemas/live-event.v1.schema.json'),
    options.overlayStateSchemaPath ?? repoPath('schemas/overlay-state.v1.schema.json')
  );
  const startedAt = toDate(clock).toISOString();

  const state = {
    schemaVersion: 1,
    session: {
      id: options.sessionId ?? randomUUID(),
      startedAt
    },
    round: {
      index: 1,
      status: 'idle'
    },
    ladder: ladder.entries
      .filter((entry) => entry.showOnOverlay)
      .map((entry) => ({
        slug: entry.slug,
        labelPt: entry.labelPt,
        coins: entry.coins,
        countSession: 0
      })),
    topGifters: [],
    lastDonors: [],
    toast: null,
    bridge: {
      connected: true
    }
  };

  if (!validateOverlayState(state)) {
    throw new Error('initial OverlayState does not match its schema');
  }

  const eventServer = createServer(handleEventRequest);
  const overlayServer = createServer(handleOverlayRequest);
  const socketServer = createServer((_, response) => {
    response.writeHead(404);
    response.end();
  });
  const gameWebSocketServer = new WebSocketServer({ noServer: true, clientTracking: false });
  const overlayWebSocketServer = new WebSocketServer({ noServer: true, clientTracking: false });
  const gameClients = new Set();
  const overlayClients = new Set();
  const rememberedIds = new Set();
  const rememberedIdOrder = [];
  const rememberedGiftKeys = new Set();
  const rememberedGiftKeyOrder = [];
  const combos = new Map();
  const spawnTimes = [];
  const metrics = {
    validGiftEvents: 0,
    validGiftRepeats: 0,
    spawned: 0,
    dropped: 0
  };

  let started = false;
  let publishTimer = null;
  let toastTimer = null;
  let lastBroadcastAt = 0;

  socketServer.on('upgrade', (request, socket, head) => {
    const pathname = new URL(request.url ?? '/', 'http://127.0.0.1').pathname;
    const target = pathname === '/game'
      ? gameWebSocketServer
      : pathname === '/overlay'
        ? overlayWebSocketServer
        : null;

    if (!target) {
      socket.write('HTTP/1.1 404 Not Found\r\nConnection: close\r\n\r\n');
      socket.destroy();
      return;
    }

    target.handleUpgrade(request, socket, head, (client) => {
      target.emit('connection', client, request);
    });
  });

  gameWebSocketServer.on('connection', (client) => {
    gameClients.add(client);
    client.on('message', (message) => {
      const round = parseRoundMessage(message);
      if (!round) {
        return;
      }

      if (state.round.index === round.index && state.round.status === round.status &&
          state.round.height === round.height && state.round.record === round.record) {
        return;
      }

      state.round = round;
      requestOverlayPublish();
    });
    client.on('close', () => gameClients.delete(client));
    client.on('error', () => gameClients.delete(client));
  });

  overlayWebSocketServer.on('connection', (client) => {
    overlayClients.add(client);
    sendOverlaySnapshot(client);
    client.on('close', () => overlayClients.delete(client));
    client.on('error', () => overlayClients.delete(client));
  });

  function now() {
    return toDate(clock);
  }

  function spawnToastText(nickname) {
    const prefix = 'Valeu, ';
    const suffix = '!';
    const normalized = nickname
      .replace(/[\u0000-\u001f\u007f]/g, ' ')
      .replace(/\s+/g, ' ')
      .trim();
    const maximumNicknameLength = 80 - Array.from(prefix).length - Array.from(suffix).length;
    const shortNickname = Array.from(normalized || 'alguém')
      .slice(0, maximumNicknameLength)
      .join('');

    return `${prefix}${shortNickname}${suffix}`;
  }

  function setSpawnToast(event) {
    const expiresAt = now().getTime() + TOAST_DURATION_MS;
    const expiresAtIso = new Date(expiresAt).toISOString();
    state.toast = {
      kind: 'spawn',
      text: spawnToastText(event.user.nickname),
      expiresAt: expiresAtIso
    };

    if (toastTimer) {
      clearTimeout(toastTimer);
    }
    toastTimer = setTimeout(() => {
      toastTimer = null;
      if (state.toast?.expiresAt !== expiresAtIso) {
        return;
      }
      state.toast = null;
      requestOverlayPublish();
    }, Math.max(0, expiresAt - now().getTime()));
    toastTimer.unref?.();
  }

  function rememberId(id) {
    if (rememberedIds.has(id)) {
      return false;
    }

    rememberedIds.add(id);
    rememberedIdOrder.push(id);
    if (rememberedIdOrder.length > MAX_REMEMBERED_IDS) {
      rememberedIds.delete(rememberedIdOrder.shift());
    }
    return true;
  }

  function rememberGiftKey(event) {
    const key = JSON.stringify([
      event.source.bridge,
      event.payload.giftId,
      event.user.id,
      event.occurredAt,
      event.payload.repeat,
      event.payload.comboFinished !== false
    ]);

    if (rememberedGiftKeys.has(key)) {
      return false;
    }

    rememberedGiftKeys.add(key);
    rememberedGiftKeyOrder.push(key);
    if (rememberedGiftKeyOrder.length > MAX_REMEMBERED_IDS) {
      rememberedGiftKeys.delete(rememberedGiftKeyOrder.shift());
    }
    return true;
  }

  function comboUpdate(event) {
    const { payload, source, user } = event;
    const isFinished = payload.comboFinished !== false;

    if (!payload.comboId) {
      return { countDelta: payload.repeat, shouldSpawn: isFinished };
    }

    const key = JSON.stringify([source.bridge, user.id, payload.giftId, payload.comboId]);
    const combo = combos.get(key) ?? { repeat: 0, finished: false };
    combos.set(key, combo);

    if (combo.finished) {
      return { countDelta: 0, shouldSpawn: false };
    }

    const countDelta = Math.max(0, payload.repeat - combo.repeat);
    combo.repeat = Math.max(combo.repeat, payload.repeat);
    if (isFinished) {
      combo.finished = true;
    }

    return { countDelta, shouldSpawn: isFinished };
  }

  function incrementGiftCounter(resolution, countDelta) {
    if (countDelta === 0) {
      return false;
    }

    metrics.validGiftRepeats += countDelta;
    if (!resolution.displayEntry) {
      return false;
    }

    const entry = state.ladder.find((item) => item.slug === resolution.displayEntry.slug);
    if (!entry) {
      return false;
    }

    entry.countSession += countDelta;
    return true;
  }

  function pruneSpawnTimes(currentTime) {
    while (spawnTimes.length > 0 && currentTime - spawnTimes[0] >= 1_000) {
      spawnTimes.shift();
    }
  }

  function sendGameAction(event, resolution) {
    const action = actionForUnity(resolution.action);
    if (!action || !state.bridge.connected) {
      return { spawned: false, dropped: false };
    }

    const currentTime = now().getTime();
    pruneSpawnTimes(currentTime);
    if (spawnTimes.length >= ladder.maxNewObstaclesPerSecond) {
      metrics.dropped += 1;
      return { spawned: false, dropped: true };
    }

    spawnTimes.push(currentTime);
    metrics.spawned += 1;
    const message = JSON.stringify({
      action,
      slug: resolution.slug,
      nickname: event.user.nickname,
      coins: event.payload.coins,
      repeat: event.payload.repeat
    });

    for (const client of gameClients) {
      if (client.readyState === WebSocket.OPEN) {
        client.send(message);
      }
    }

    return { spawned: true, dropped: false };
  }

  function processGift(event) {
    metrics.validGiftEvents += 1;
    const resolution = resolveGift(ladder, event.payload);
    const { countDelta, shouldSpawn } = comboUpdate(event);
    const stateChanged = incrementGiftCounter(resolution, countDelta);
    const spawn = shouldSpawn
      ? sendGameAction(event, resolution)
      : { spawned: false, dropped: false };

    if (shouldSpawn) {
      setSpawnToast(event);
    }

    if (stateChanged || shouldSpawn) {
      requestOverlayPublish();
    }

    return {
      accepted: true,
      duplicate: false,
      spawned: spawn.spawned,
      dropped: spawn.dropped
    };
  }

  function processEvent(event) {
    if (!rememberId(event.id)) {
      return { accepted: true, duplicate: true, spawned: false, dropped: false };
    }

    if (event.type === 'gift') {
      if (!rememberGiftKey(event)) {
        return { accepted: true, duplicate: true, spawned: false, dropped: false };
      }
      return processGift(event);
    }

    if (event.type === 'live.connected' && !state.bridge.connected) {
      state.bridge.connected = true;
      requestOverlayPublish();
    }
    if (event.type === 'live.disconnected' && state.bridge.connected) {
      state.bridge.connected = false;
      requestOverlayPublish();
    }

    return { accepted: true, duplicate: false, spawned: false, dropped: false };
  }

  async function handleEventRequest(request, response) {
    const pathname = new URL(request.url ?? '/', 'http://127.0.0.1').pathname;
    if (request.method !== 'POST' || pathname !== '/v1/events') {
      sendJson(response, 404, { error: 'not_found' });
      return;
    }

    let body;
    try {
      body = await readRequestBody(request);
    } catch (error) {
      const statusCode = error.message === 'payload_too_large' ? 413 : 400;
      sendJson(response, statusCode, { error: statusCode === 413 ? 'payload_too_large' : 'invalid_body' });
      return;
    }

    let event;
    try {
      event = JSON.parse(body);
    } catch {
      sendJson(response, 400, { error: 'invalid_json' });
      return;
    }

    if (!validateLiveEvent(event)) {
      sendJson(response, 400, { error: 'invalid_event' });
      return;
    }

    const result = processEvent(event);
    sendJson(response, 202, { accepted: result.accepted, duplicate: result.duplicate });
  }

  async function handleOverlayRequest(request, response) {
    if (request.method !== 'GET' && request.method !== 'HEAD') {
      sendStaticNotFound(response);
      return;
    }

    const assetPath = staticAssetPath(overlayRoot, request.url);
    if (!assetPath) {
      sendStaticNotFound(response);
      return;
    }

    try {
      const [realOverlayRoot, realAssetPath] = await Promise.all([
        realpath(overlayRoot),
        realpath(assetPath)
      ]);
      if (!isWithinDirectory(realOverlayRoot, realAssetPath)) {
        sendStaticNotFound(response);
        return;
      }

      const assetStat = await stat(realAssetPath);
      if (!assetStat.isFile()) {
        sendStaticNotFound(response);
        return;
      }

      const body = request.method === 'HEAD' ? null : await readFile(realAssetPath);
      response.writeHead(200, {
        'cache-control': 'no-store',
        'content-length': body?.length ?? assetStat.size,
        'content-type': staticContentType(realAssetPath),
        'x-content-type-options': 'nosniff'
      });
      if (request.method === 'HEAD') {
        response.end();
        return;
      }

      response.end(body);
    } catch {
      sendStaticNotFound(response);
    }
  }

  function overlayMessage() {
    if (!validateOverlayState(state)) {
      throw new Error('OverlayState does not match its schema');
    }
    return JSON.stringify(state);
  }

  function sendOverlaySnapshot(client) {
    if (client.readyState === WebSocket.OPEN) {
      client.send(overlayMessage());
    }
  }

  function broadcastOverlay() {
    publishTimer = null;
    const message = overlayMessage();
    lastBroadcastAt = now().getTime();
    for (const client of overlayClients) {
      if (client.readyState === WebSocket.OPEN) {
        client.send(message);
      }
    }
  }

  function requestOverlayPublish() {
    const currentTime = now().getTime();
    const wait = Math.max(0, broadcastIntervalMs - (currentTime - lastBroadcastAt));

    if (wait === 0) {
      if (publishTimer) {
        clearTimeout(publishTimer);
        publishTimer = null;
      }
      broadcastOverlay();
      return;
    }

    if (!publishTimer) {
      publishTimer = setTimeout(broadcastOverlay, wait);
      publishTimer.unref?.();
    }
  }

  async function start() {
    if (started) {
      return;
    }

    await listen(eventServer, httpPort, host);
    try {
      await listen(socketServer, wsPort, host);
      await listen(overlayServer, overlayPort, host);
      started = true;
    } catch (error) {
      await Promise.allSettled([
        closeServer(eventServer),
        closeServer(socketServer),
        closeServer(overlayServer)
      ]);
      throw error;
    }
  }

  async function close() {
    if (publishTimer) {
      clearTimeout(publishTimer);
      publishTimer = null;
    }
    if (toastTimer) {
      clearTimeout(toastTimer);
      toastTimer = null;
    }

    for (const client of gameClients) {
      client.terminate();
    }
    for (const client of overlayClients) {
      client.terminate();
    }
    gameClients.clear();
    overlayClients.clear();

    if (!started) {
      return;
    }

    await Promise.all([
      new Promise((resolve) => gameWebSocketServer.close(resolve)),
      new Promise((resolve) => overlayWebSocketServer.close(resolve)),
      closeServer(eventServer),
      closeServer(socketServer),
      closeServer(overlayServer)
    ]);
    started = false;
  }

  return {
    start,
    close,
    getHttpPort: () => eventServer.address()?.port ?? null,
    getWsPort: () => socketServer.address()?.port ?? null,
    getOverlayPort: () => overlayServer.address()?.port ?? null,
    getOverlayState: () => structuredClone(state),
    getMetrics: () => ({ ...metrics })
  };
}
