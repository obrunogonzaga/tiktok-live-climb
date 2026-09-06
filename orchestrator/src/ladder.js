import { readFileSync } from 'node:fs';

import TOML from '@iarna/toml';

const supportedActions = new Set([
  'SpawnObstacle.Small',
  'SpawnObstacle.Medium',
  'SpawnSmoke'
]);

function requireInteger(value, label, minimum = 0) {
  if (!Number.isInteger(value) || value < minimum) {
    throw new Error(`${label} must be an integer >= ${minimum}`);
  }

  return value;
}

function requireString(value, label) {
  if (typeof value !== 'string' || value.length === 0) {
    throw new Error(`${label} must be a non-empty string`);
  }

  return value;
}

function normalizeGiftName(value) {
  return value.trim().toLocaleLowerCase('en-US');
}

function parseGift(entry, index) {
  const label = `gifts[${index}]`;
  const giftNames = entry.gift_names;

  if (!Array.isArray(giftNames) || giftNames.length === 0) {
    throw new Error(`${label}.gift_names must be a non-empty array`);
  }

  return {
    slug: requireString(entry.slug, `${label}.slug`),
    giftNames: giftNames.map((name, nameIndex) =>
      requireString(name, `${label}.gift_names[${nameIndex}]`)
    ),
    coins: requireInteger(entry.coins, `${label}.coins`),
    action: requireString(entry.action, `${label}.action`),
    showOnOverlay: entry.show_on_overlay === true,
    labelPt: requireString(entry.label_pt, `${label}.label_pt`)
  };
}

function parseFallback(entry, index) {
  const label = `fallback[${index}]`;
  const minCoins = requireInteger(entry.min_coins, `${label}.min_coins`);
  const maxCoins = requireInteger(entry.max_coins, `${label}.max_coins`, minCoins);

  return {
    minCoins,
    maxCoins,
    action: requireString(entry.action, `${label}.action`)
  };
}

/** Load and validate the startup-only Ladder v1 TOML configuration. */
export function loadLadder(path) {
  const parsed = TOML.parse(readFileSync(path, 'utf8'));

  if (parsed.version !== 1) {
    throw new Error('ladder.version must be 1');
  }

  const spawn = parsed.spawn;
  if (spawn === null || typeof spawn !== 'object' || Array.isArray(spawn)) {
    throw new Error('ladder.spawn must be an object');
  }

  const gifts = parsed.gifts;
  const fallback = parsed.fallback;
  if (!Array.isArray(gifts) || !Array.isArray(fallback)) {
    throw new Error('ladder.gifts and ladder.fallback must be arrays');
  }

  const entries = gifts.map(parseGift);
  const fallbackEntries = fallback.map(parseFallback);
  const names = new Map();

  for (const entry of entries) {
    for (const name of entry.giftNames) {
      names.set(normalizeGiftName(name), entry);
    }
  }

  return {
    maxNewObstaclesPerSecond: requireInteger(
      spawn.max_new_obstacles_per_second,
      'spawn.max_new_obstacles_per_second',
      1
    ),
    entries,
    fallbackEntries,
    names
  };
}

/** Resolve a gift name first, then a coins * repeat fallback bucket. */
export function resolveGift(ladder, payload) {
  const namedEntry = ladder.names.get(normalizeGiftName(payload.giftName));
  if (namedEntry) {
    return {
      action: namedEntry.action,
      displayEntry: namedEntry,
      slug: namedEntry.slug
    };
  }

  const totalCoins = payload.coins * payload.repeat;
  const fallback = ladder.fallbackEntries.find(
    (entry) => totalCoins >= entry.minCoins && totalCoins <= entry.maxCoins
  );

  if (!fallback) {
    return {
      action: null,
      displayEntry: null,
      slug: 'fallback'
    };
  }

  return {
    action: fallback.action,
    displayEntry: ladder.entries.find((entry) => entry.action === fallback.action) ?? null,
    slug: `fallback-${fallback.minCoins}-${fallback.maxCoins}`
  };
}

/** Return the Unity v1 equivalent, or null for an action outside this issue. */
export function actionForUnity(action) {
  if (supportedActions.has(action)) {
    return action;
  }
  return null;
}
