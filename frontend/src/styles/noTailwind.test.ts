import { describe, expect, it } from 'vitest';

/** Every component source file, read as text by Vite (tests excluded below). */
const SOURCES = import.meta.glob<string>('../**/*.tsx', { query: '?raw', import: 'default', eager: true });

/** Utility classes that exist only in Tailwind (ADR-0021 removed it); CoreUI/Bootstrap names differ. */
const TAILWIND_ONLY = [
  /(^|\s)(sm|md|lg|xl|2xl):[\w-[\]]+/,
  /(^|\s)(text|bg|border|ring)-(slate|brand|emerald|amber|red|sky|violet|indigo|green|orange)-\d+/,
  /(^|\s)rounded-(lg|xl|2xl)(\s|$)/,
  /(^|\s)(flex-col|items-center|items-start|items-end|justify-between|font-medium|font-semibold|text-sm|text-xs|text-lg|text-xl|text-2xl|w-full|break-words|whitespace-nowrap|min-h-11|space-y-\d|gap-x-\d|gap-y-\d|grid-cols-\d|shrink-0)(\s|$)/,
  /(^|\s)aria-\[/,
  // Left over after ADR-0021 and found in the UI cohesion work (ADR-0026): Bootstrap uses border-start, ps-*, ms-*, rounded.
  /(^|\s)(border-[lrtb]-\d|p[lr]-\d|m[lr]-\d|rounded-md)(\s|$)/,
];

/** Class strings: className="…" plus the string parts of className={`…`} and class constants ending in _CLASS. */
function classStrings(source: string): string[] {
  const quoted = [...source.matchAll(/className="([^"]*)"/g)].map((m) => m[1] ?? '');
  const templates = [...source.matchAll(/className=\{`([^`]*)`\}/g)].map((m) => (m[1] ?? '').replace(/\$\{[^}]*\}/g, ' '));
  const constants = [...source.matchAll(/_CLASS(?:ES)?\s*=\s*'([^']*)'/g)].map((m) => m[1] ?? '');
  return [...quoted, ...templates, ...constants];
}

describe('styling', () => {
  it('has no Tailwind-only utility classes left in components', () => {
    const offenders = Object.entries(SOURCES)
      .filter(([file]) => !file.endsWith('.test.tsx'))
      .flatMap(([file, source]) =>
        classStrings(source)
          .filter((classes) => TAILWIND_ONLY.some((pattern) => pattern.test(classes)))
          .map((classes) => `${file}: ${classes}`),
      );
    expect(offenders).toEqual([]);
  });
});
