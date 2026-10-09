import { describe, expect, it } from 'vitest';

import indexHtml from '../../index.html?raw';
import packageJson from '../../package.json';
import { COLOR_MODE_STORAGE_KEY } from '../shared/ui/colorMode';

import layoutScss from './_layout.scss?raw';

const allDependencies: Record<string, string> = { ...packageJson.dependencies, ...packageJson.devDependencies };

describe('styling stack', () => {
  it('uses CoreUI and no longer ships Tailwind or Radix', () => {
    for (const removed of ['tailwindcss', '@tailwindcss/vite', 'radix-ui']) {
      expect(allDependencies).not.toHaveProperty(removed);
    }
    expect(allDependencies['@coreui/react']).toMatch(/^[~^]?5\.13(\.|$)/);
    expect(allDependencies['@coreui/coreui']).toMatch(/^[~^]?5\.9(\.|$)/);
    expect(allDependencies['@coreui/react-chartjs']).toMatch(/^[~^]?3\.0(\.|$)/);
    expect(allDependencies['chart.js']).toMatch(/^[~^]?4\.5(\.|$)/);
  });

  it('ships the CoreUI icon packages', () => {
    expect(allDependencies['@coreui/icons']).toMatch(/^[~^]?3\.0(\.|$)/);
    expect(allDependencies['@coreui/icons-react']).toMatch(/^[~^]?2\.3(\.|$)/);
  });

  it('applies the stored color mode before the first paint with the shared storage key', () => {
    expect(indexHtml).toContain(`localStorage.getItem('${COLOR_MODE_STORAGE_KEY}')`);
    expect(indexHtml).toContain('data-coreui-theme');
  });
});

/** Every source file under src/, read as text by Vite; tests and test helpers are left out. */
const SOURCES = Object.entries(import.meta.glob<string>('../**/*.{ts,tsx}', { query: '?raw', import: 'default', eager: true })).filter(
  ([file]) => !/\.test\.tsx?$/.test(file) && !file.startsWith('../test/'),
);
const FEATURE_SOURCES = SOURCES.filter(([file]) => file.startsWith('../features/'));
/** The one place hex colors may live: the chart palette fallback (CSS variables are read first). */
const CHART_PALETTE_FILE = '../shared/ui/chartTheme.ts';
/** The only max-width media query: the stacked table below 992 px (spec Global Constraints). */
const ALLOWED_MAX_WIDTH_QUERY = '@media (max-width: 991.98px)';

/** Class strings: className="…", the string parts of className={`…`} and class constants ending in _CLASS/_CLASSES. */
function classStrings(source: string): string[] {
  const quoted = [...source.matchAll(/className="([^"]*)"/g)].map((m) => m[1] ?? '');
  const templates = [...source.matchAll(/className=\{`([^`]*)`\}/g)].map((m) => m[1] ?? '');
  const constants = [...source.matchAll(/_CLASS(?:ES)?\s*(?::[^=]*)?=\s*\{?([^;]*)/g)].map((m) => m[1] ?? '');
  return [...quoted, ...templates, ...constants];
}

function lineOffenders(files: [string, string][], pattern: RegExp): string[] {
  return files.flatMap(([file, source]) => source.split('\n').flatMap((line, index) => (pattern.test(line) ? [`${file}:${index + 1}`] : [])));
}

function classOffenders(files: [string, string][], pattern: RegExp): string[] {
  return files.flatMap(([file, source]) => classStrings(source).filter((classes) => pattern.test(classes)).map((classes) => `${file}: ${classes}`));
}

describe('UI cohesion guards (ADR-0026)', () => {
  it('features use form controls and tables only through shared/ui', () => {
    expect(lineOffenders(FEATURE_SOURCES, /<(input|select|textarea|table)[\s>]|<(input|select|textarea|table)$/)).toEqual([]);
  });

  it('features do not build their own cards', () => {
    expect(classOffenders(FEATURE_SOURCES, /(^|\s|')card(\s|'|$)/)).toEqual([]);
  });

  it('uses no hard-coded colors that break dark mode', () => {
    expect(classOffenders(SOURCES, /(^|\s|')(bg-white|bg-light|bg-dark|text-dark|text-black|text-white-50|text-bg-[a-z]+)(\s|'|$)/)).toEqual([]);
    const outsidePalette = SOURCES.filter(([file]) => file !== CHART_PALETTE_FILE);
    expect(lineOffenders(outsidePalette, /['"`]#[0-9a-fA-F]{3,8}['"`]/)).toEqual([]);
  });

  it('keeps styles mobile first: no new max-width media queries', () => {
    expect(layoutScss.match(/@media\s*\(max-width:[^)]*\)/g) ?? []).toEqual([ALLOWED_MAX_WIDTH_QUERY]);
  });
});

