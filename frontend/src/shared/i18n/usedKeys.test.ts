import { describe, expect, it } from 'vitest';

import { REPORT_NAMES } from '../../features/reports/api';
import { ENUM_FILTER_LABELS, ENUM_FILTERS } from '../../features/reports/useReportFilter';
import { ROUTE_META } from '../navigation/routeMeta';

import { DEFAULT_NAMESPACE, resources } from '.';

/**
 * Keys the type checker cannot see: keys built at runtime (`translateOr`, route titles, report enum labels) and namespaced
 * keys written as plain strings. Literal `t('…')` calls are already checked by TypeScript (i18next.d.ts).
 */
const SOURCES = import.meta.glob<string>(['/src/**/*.{ts,tsx}', '!/src/**/*.test.{ts,tsx}'], { query: '?raw', import: 'default', eager: true });

const NAMESPACES = Object.keys(resources.tr).join('|');
const KEY_SEPARATOR = ':';
const PATH_SEPARATOR = '.';
/** The route param that names a report (`/reports/:report`). */
const REPORT_PARAM = 'report';
/** A whole key in quotes: 'reports:figure.sample'; a trailing dot marks a prefix ('reports:tabs.'). */
const QUOTED_KEY = new RegExp(`['"\`]((?:${NAMESPACES}):[A-Za-z0-9_.]+)['"\`]`, 'g');
/** The fixed head of a template key: `tickets:statuses.${…}`. */
const TEMPLATE_PREFIX = new RegExp(`\`((?:${NAMESPACES}):[A-Za-z0-9_.]+)\\.\\$\\{`, 'g');

type Node = string | { readonly [key: string]: Node } | undefined;

function lookup(key: string): Node {
  const [namespace, path] = key.includes(KEY_SEPARATOR) ? key.split(KEY_SEPARATOR) : [DEFAULT_NAMESPACE, key];
  let node: Node = (resources.tr as Record<string, Node>)[namespace ?? DEFAULT_NAMESPACE];
  for (const part of (path ?? '').split(PATH_SEPARATOR).filter((p) => p !== '')) {
    node = typeof node === 'object' ? node[part] : undefined;
  }
  return node;
}

const isText = (key: string) => typeof lookup(key) === 'string';
const isGroup = (key: string) => typeof lookup(key) === 'object';

function matches(pattern: RegExp): string[] {
  return Object.entries(SOURCES).flatMap(([file, text]) => [...text.matchAll(pattern)].map((m) => `${file}|${m[1] ?? ''}`));
}

describe('i18n keys used in source', () => {
  it('reads the application source', () => {
    expect(Object.keys(SOURCES).length).toBeGreaterThan(0);
  });

  it('every namespaced key written as a string exists (a trailing dot is a prefix of a group)', () => {
    const found = matches(QUOTED_KEY);
    const missing = found.filter((entry) => {
      const key = entry.split('|')[1] ?? '';
      return key.endsWith(PATH_SEPARATOR) ? !isGroup(key) : !isText(key) && !isGroup(key);
    });
    expect(found.length).toBeGreaterThan(0);
    expect(missing).toEqual([]);
  });

  it('every template key starts at an existing group', () => {
    const found = matches(TEMPLATE_PREFIX);
    expect(found.length).toBeGreaterThan(0);
    expect(found.filter((entry) => !isGroup(entry.split('|')[1] ?? ''))).toEqual([]);
  });

  it('every report filter value has a name', () => {
    const missing = Object.entries(ENUM_FILTERS).flatMap(([filter, values]) =>
      values.map((value) => `${ENUM_FILTER_LABELS[filter as keyof typeof ENUM_FILTERS]}.${value}`).filter((key) => !isText(key)),
    );
    expect(missing).toEqual([]);
  });

  it('every route title and report tab title exists', () => {
    const titles = Object.values(ROUTE_META).map((meta) => meta.titleKey);
    const params = Object.values(ROUTE_META).flatMap((meta) =>
      meta.titleParam?.param === REPORT_PARAM ? REPORT_NAMES.map((name) => `${meta.titleParam?.keyPrefix ?? ''}${name}`) : [],
    );
    expect(params.length).toBeGreaterThan(0);
    expect([...titles, ...params].filter((key) => !isText(key))).toEqual([]);
  });
});
