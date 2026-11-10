/**
 * @input Nothing.
 * @output A `matchMedia` stub that reports a phone-width viewport and a light system scheme.
 * @position Test-only. jsdom has no layout, so width queries must be answered by hand.
 */
import { vi } from 'vitest';

/** Width queries Astryx and the app use for "narrower than a tablet". */
const NARROW_QUERY = /\(width < |\(max-width:/;

export function stubPhoneViewport() {
  vi.stubGlobal(
    'matchMedia',
    vi.fn((query: string) => ({ matches: NARROW_QUERY.test(query), media: query, onchange: null, addEventListener: () => undefined, removeEventListener: () => undefined, addListener: () => undefined, removeListener: () => undefined, dispatchEvent: () => false })),
  );
}
