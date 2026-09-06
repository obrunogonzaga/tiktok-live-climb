import { readFile } from 'node:fs/promises';
import assert from 'node:assert/strict';
import test from 'node:test';
import { fileURLToPath } from 'node:url';

import WebSocket from 'ws';

import { createOrchestrator } from '../src/orchestrator.js';

const inboxes = new WeakMap();
const overlayFixtureRoot = fileURLToPath(new URL('../fixtures/overlay', import.meta.url));

async function fixture(name) {
  return JSON.parse(
    await readFile(new URL(`../../fixtures/events/${name}`, import.meta.url), 'utf8')
  );
}

async function startApp(t, options = {}) {
  const app = createOrchestrator({ httpPort: 0, wsPort: 0, overlayPort: 0, ...options });
  await app.start();
  t.after(async () => app.close());
  return app;
}

function connectWebSocket(url) {
  return new Promise((resolve, reject) => {
    const client = new WebSocket(url);
    const inbox = { messages: [], waiters: [] };
    inboxes.set(client, inbox);
    client.on('message', (message) => {
      const waiter = inbox.waiters.shift();
      if (waiter) {
        waiter.resolve(message);
        return;
      }
      inbox.messages.push(message);
    });
    client.on('error', (error) => {
      while (inbox.waiters.length > 0) {
        inbox.waiters.shift().reject(error);
      }
    });

    const timeout = setTimeout(() => reject(new Error(`Timed out connecting to ${url}`)), 1_000);
    const onInitialError = (error) => {
      clearTimeout(timeout);
      reject(error);
    };

    client.once('error', onInitialError);
    client.once('open', () => {
      clearTimeout(timeout);
      client.off('error', onInitialError);
      resolve(client);
    });
  });
}

function nextMessage(client, timeoutMs = 1_000) {
  const inbox = inboxes.get(client);
  if (inbox.messages.length > 0) {
    return Promise.resolve(inbox.messages.shift());
  }

  return new Promise((resolve, reject) => {
    const timeout = setTimeout(() => {
      const index = inbox.waiters.indexOf(waiter);
      if (index >= 0) {
        inbox.waiters.splice(index, 1);
      }
      reject(new Error('Timed out waiting for a WebSocket message'));
    }, timeoutMs);
    const waiter = {
      resolve: (message) => {
        clearTimeout(timeout);
        resolve(message);
      },
      reject: (error) => {
        clearTimeout(timeout);
        reject(error);
      }
    };

    inbox.waiters.push(waiter);
  });
}

async function nextJson(client, timeoutMs = 1_000) {
  return JSON.parse((await nextMessage(client, timeoutMs)).toString());
}

async function collectJson(client, count, timeoutMs = 1_000) {
  return Promise.all(Array.from({ length: count }, () => nextJson(client, timeoutMs)));
}

function expectNoMessage(client, durationMs = 150) {
  const inbox = inboxes.get(client);
  if (inbox.messages.length > 0) {
    return Promise.reject(new Error('Unexpected WebSocket message'));
  }

  return new Promise((resolve, reject) => {
    const waiter = {
      resolve: () => {
        clearTimeout(timeout);
        reject(new Error('Unexpected WebSocket message'));
      },
      reject: (error) => {
        clearTimeout(timeout);
        reject(error);
      }
    };
    const timeout = setTimeout(() => {
      const index = inbox.waiters.indexOf(waiter);
      if (index >= 0) {
        inbox.waiters.splice(index, 1);
      }
      resolve();
    }, durationMs);

    inbox.waiters.push(waiter);
  });
}

async function postJson(app, payload) {
  return fetch(`http://127.0.0.1:${app.getHttpPort()}/v1/events`, {
    method: 'POST',
    headers: { 'content-type': 'application/json' },
    body: JSON.stringify(payload)
  });
}

test('postEvents_validRose_sendsSmallActionAndInitialOverlayState', async (t) => {
  const app = await startApp(t);
  const overlay = await connectWebSocket(`ws://127.0.0.1:${app.getWsPort()}/overlay`);
  const initialState = await nextJson(overlay);

  assert.equal(initialState.schemaVersion, 1);
  assert.equal(initialState.round.index, 1);
  assert.equal(initialState.round.status, 'idle');
  assert.equal(initialState.ladder.length, 7);
  assert.ok(initialState.ladder.every((entry) => entry.countSession === 0));
  assert.deepEqual(initialState.topGifters, []);
  assert.deepEqual(initialState.lastDonors, []);
  assert.equal(initialState.toast, null);
  assert.equal(initialState.bridge.connected, true);

  const game = await connectWebSocket(`ws://127.0.0.1:${app.getWsPort()}/game`);
  const action = nextJson(game);
  const response = await postJson(app, await fixture('gift-rose.json'));

  assert.equal(response.status, 202);
  assert.deepEqual(await action, {
    action: 'SpawnObstacle.Small',
    slug: 'rose',
    nickname: 'Ana',
    coins: 1,
    repeat: 1
  });
  assert.equal(
    app.getOverlayState().ladder.find((entry) => entry.slug === 'rose').countSession,
    1
  );
});

test('postEvents_invalidFixture_returns400AndDoesNotSendGameAction', async (t) => {
  const app = await startApp(t);
  const game = await connectWebSocket(`ws://127.0.0.1:${app.getWsPort()}/game`);
  const noAction = expectNoMessage(game);
  const response = await postJson(app, await fixture('invalid-missing-type.json'));

  assert.equal(response.status, 400);
  assert.deepEqual(await response.json(), { error: 'invalid_event' });
  await noAction;
  assert.equal(app.getMetrics().validGiftEvents, 0);
});

test('overlayHttp_getAndHead_servesAssetsAndRejectsTraversal', async (t) => {
  const app = await startApp(t, { overlayRoot: overlayFixtureRoot });
  const origin = `http://127.0.0.1:${app.getOverlayPort()}`;

  const index = await fetch(`${origin}/`);
  assert.equal(index.status, 200);
  assert.match(index.headers.get('content-type'), /^text\/html/);
  assert.match(await index.text(), /Overlay fixture/);

  const head = await fetch(`${origin}/`, { method: 'HEAD' });
  assert.equal(head.status, 200);
  assert.match(head.headers.get('content-type'), /^text\/html/);
  assert.equal(await head.text(), '');

  const stylesheet = await fetch(`${origin}/style.css`);
  assert.equal(stylesheet.status, 200);
  assert.match(stylesheet.headers.get('content-type'), /^text\/css/);
  assert.match(await stylesheet.text(), /fixture/);

  const script = await fetch(`${origin}/overlay.js`);
  assert.equal(script.status, 200);
  assert.match(script.headers.get('content-type'), /^text\/javascript/);

  const traversal = await fetch(`${origin}/%2e%2e%2fpackage.json`);
  assert.equal(traversal.status, 404);
});

test('postEvents_comboTickAndDone_countsRepeatOnceAndSpawnsOnce', async (t) => {
  const app = await startApp(t);
  const game = await connectWebSocket(`ws://127.0.0.1:${app.getWsPort()}/game`);
  const tick = await fixture('gift-combo-tick.json');
  const done = await fixture('gift-combo-done.json');

  const noTickAction = expectNoMessage(game);
  assert.equal((await postJson(app, tick)).status, 202);
  await noTickAction;
  assert.equal(
    app.getOverlayState().ladder.find((entry) => entry.slug === 'rose').countSession,
    4
  );

  const completedAction = nextJson(game);
  assert.equal((await postJson(app, done)).status, 202);
  assert.equal((await completedAction).action, 'SpawnObstacle.Small');
  assert.equal(
    app.getOverlayState().ladder.find((entry) => entry.slug === 'rose').countSession,
    4
  );

  const noRepeatedAction = expectNoMessage(game);
  const repeated = await postJson(app, done);
  assert.equal((await repeated.json()).duplicate, true);
  await noRepeatedAction;

  const sameFinishedCombo = structuredClone(done);
  sameFinishedCombo.id = '01TEST00000000000000000006';
  sameFinishedCombo.occurredAt = '2026-09-06T00:00:03.500Z';
  const noSecondFinishedAction = expectNoMessage(game);
  assert.equal((await postJson(app, sameFinishedCombo)).status, 202);
  await noSecondFinishedAction;
});

test('postEvents_finishedGift_setsAndExpiresToastWithoutTickOrDuplicate', async (t) => {
  const app = await startApp(t);
  const overlay = await connectWebSocket(`ws://127.0.0.1:${app.getWsPort()}/overlay`);
  await nextJson(overlay);
  const tick = await fixture('gift-combo-tick.json');
  const done = await fixture('gift-combo-done.json');

  assert.equal((await postJson(app, tick)).status, 202);
  assert.equal((await nextJson(overlay)).toast, null);
  assert.equal(app.getOverlayState().toast, null);

  const toastState = nextJson(overlay);
  assert.equal((await postJson(app, done)).status, 202);
  const stateWithToast = await toastState;
  assert.equal(stateWithToast.toast.kind, 'spawn');
  assert.equal(stateWithToast.toast.text, 'Valeu, Ana!');
  assert.ok(Array.from(stateWithToast.toast.text).length <= 80);
  const expiresAt = stateWithToast.toast.expiresAt;

  const duplicate = await postJson(app, done);
  assert.deepEqual(await duplicate.json(), { accepted: true, duplicate: true });
  assert.equal(app.getOverlayState().toast.expiresAt, expiresAt);

  const clearedState = nextJson(overlay, 3_500);
  await new Promise((resolve) => setTimeout(resolve, 2_600));
  assert.equal(app.getOverlayState().toast, null);
  assert.equal((await clearedState).toast, null);
});

test('postEvents_newIdSameSemanticGift_isDeduplicated', async (t) => {
  const app = await startApp(t);
  const game = await connectWebSocket(`ws://127.0.0.1:${app.getWsPort()}/game`);
  const rose = await fixture('gift-rose.json');
  const duplicate = structuredClone(rose);
  duplicate.id = '01TEST00000000000000000007';

  const firstAction = nextJson(game);
  await postJson(app, rose);
  await firstAction;

  const noSecondAction = expectNoMessage(game);
  const response = await postJson(app, duplicate);
  assert.deepEqual(await response.json(), { accepted: true, duplicate: true });
  await noSecondAction;
  assert.equal(app.getMetrics().validGiftEvents, 1);
});

test('postEvents_namedAndFallbackFixtures_sendOnlySupportedActions', async (t) => {
  const app = await startApp(t);
  const game = await connectWebSocket(`ws://127.0.0.1:${app.getWsPort()}/game`);

  const perfumeAction = nextJson(game);
  assert.equal((await postJson(app, await fixture('gift-perfume.json'))).status, 202);
  assert.deepEqual(await perfumeAction, {
    action: 'SpawnSmoke',
    slug: 'perfume',
    nickname: 'Lucas',
    coins: 20,
    repeat: 1
  });

  const confettiAction = nextJson(game);
  assert.equal((await postJson(app, await fixture('gift-confetti.json'))).status, 202);
  assert.deepEqual(await confettiAction, {
    action: 'SpawnObstacle.Medium',
    slug: 'confetti',
    nickname: 'Bia',
    coins: 100,
    repeat: 1
  });

  const fallback = {
    ...await fixture('gift-rose.json'),
    id: '01TEST00000000000000000008',
    occurredAt: '2026-09-06T00:00:08.000Z',
    receivedAt: '2026-09-06T00:00:08.050Z',
    payload: {
      giftId: 'fallback-smoke',
      giftName: 'Unknown Gift',
      coins: 20,
      repeat: 1,
      comboFinished: true
    }
  };
  const fallbackAction = nextJson(game);
  assert.equal((await postJson(app, fallback)).status, 202);
  assert.deepEqual(await fallbackAction, {
    action: 'SpawnSmoke',
    slug: 'fallback-20-99',
    nickname: 'Ana',
    coins: 20,
    repeat: 1
  });
  assert.equal(
    app.getOverlayState().ladder.find((entry) => entry.slug === 'perfume').countSession,
    2
  );

  const unsupported = {
    ...fallback,
    id: '01TEST00000000000000000009',
    occurredAt: '2026-09-06T00:00:09.000Z',
    receivedAt: '2026-09-06T00:00:09.050Z',
    payload: {
      giftId: 'fallback-small-knockback',
      giftName: 'Unknown Knockback',
      coins: 5,
      repeat: 1,
      comboFinished: true
    }
  };
  const noUnsupportedAction = expectNoMessage(game);
  assert.equal((await postJson(app, unsupported)).status, 202);
  await noUnsupportedAction;
  assert.equal(
    app.getOverlayState().ladder.find((entry) => entry.slug === 'finger_heart').countSession,
    1
  );
});

test('postEvents_spawnCap_dropsNinthSpawnButCountsGift', async (t) => {
  const app = await startApp(t);
  const game = await connectWebSocket(`ws://127.0.0.1:${app.getWsPort()}/game`);
  const rose = await fixture('gift-rose.json');
  const actions = collectJson(game, 8);

  for (let index = 0; index < 9; index += 1) {
    const event = structuredClone(rose);
    event.id = `01TEST00000000000000000${String(20 + index).padStart(3, '0')}`;
    event.occurredAt = `2026-09-06T00:01:${String(index).padStart(2, '0')}.000Z`;
    event.receivedAt = `2026-09-06T00:01:${String(index).padStart(2, '0')}.050Z`;
    event.user.nickname = index === 8 ? 'Nove' : 'Ana';
    assert.equal((await postJson(app, event)).status, 202);
  }

  assert.equal((await actions).length, 8);
  assert.deepEqual(app.getMetrics(), {
    validGiftEvents: 9,
    validGiftRepeats: 9,
    spawned: 8,
    dropped: 1
  });
  assert.equal(
    app.getOverlayState().ladder.find((entry) => entry.slug === 'rose').countSession,
    9
  );
  assert.equal(app.getOverlayState().toast.kind, 'spawn');
  assert.equal(app.getOverlayState().toast.text, 'Valeu, Nove!');
});

test('gameRound_validMessageUpdatesOverlayAndInvalidMessageIsIgnored', async (t) => {
  const app = await startApp(t);
  const overlay = await connectWebSocket(`ws://127.0.0.1:${app.getWsPort()}/overlay`);
  await nextJson(overlay);
  const game = await connectWebSocket(`ws://127.0.0.1:${app.getWsPort()}/game`);

  game.send(JSON.stringify({ index: 0, status: 'summit' }));
  await new Promise((resolve) => setTimeout(resolve, 20));
  assert.deepEqual(app.getOverlayState().round, { index: 1, status: 'idle' });

  const roundState = nextJson(overlay);
  game.send(JSON.stringify({ index: 2, status: 'climbing' }));
  assert.deepEqual((await roundState).round, { index: 2, status: 'climbing' });
});
