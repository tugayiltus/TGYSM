import { describe, expect, it } from 'vitest';

import admin from './locales/tr/admin.json';
import assets from './locales/tr/assets.json';
import auth from './locales/tr/auth.json';
import common from './locales/tr/common.json';
import errors from './locales/tr/errors.json';
import kb from './locales/tr/kb.json';
import reports from './locales/tr/reports.json';
import tickets from './locales/tr/tickets.json';

/**
 * One word per thing in the UI (UX-15, docs/superpowers/specs/2026-10-09-ui-glossary.md): "Lokasyon", "IT" and
 * "Bilgi bankası". "Nasıl Yapılır?" is only the home-page card that leads to the knowledge base.
 */
const BANNED: readonly { pattern: RegExp; use: string }[] = [
  { pattern: /\bKonum|\bkonum/u, use: 'Lokasyon' },
  { pattern: /\bBT\b/u, use: 'IT' },
  { pattern: /Bilgi Bankası/u, use: 'Bilgi bankası' },
];
const ALLOWED: readonly string[] = ['kb.home.card'];

function texts(namespace: string, value: unknown, path = namespace): [string, string][] {
  if (typeof value === 'string') {
    return [[path, value]];
  }
  if (value !== null && typeof value === 'object') {
    return Object.entries(value).flatMap(([key, child]) => texts(namespace, child, `${path}.${key}`));
  }
  return [];
}

const ALL = Object.entries({ admin, assets, auth, common, errors, kb, reports, tickets }).flatMap(([name, value]) => texts(name, value));

describe('UI glossary', () => {
  it('uses one word per thing', () => {
    const offenders = ALL.filter(([path, text]) => !ALLOWED.includes(path) && BANNED.some((rule) => rule.pattern.test(text))).map(([path, text]) => `${path}: ${text}`);
    expect(offenders).toEqual([]);
  });

  it('keeps "Nasıl Yapılır?" to the home card', () => {
    expect(ALL.filter(([, text]) => text.includes('Nasıl Yapılır')).map(([path]) => path)).toEqual(['kb.home.card']);
  });
});
