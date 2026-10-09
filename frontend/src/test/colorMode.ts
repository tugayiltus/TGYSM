import { vi } from 'vitest';

const DARK_QUERY = '(prefers-color-scheme: dark)';

/** Replaces `matchMedia` with a controllable system color scheme; `flip` fires the change event like the OS does. */
export function stubSystemColorScheme(dark: boolean) {
  const listeners = new Set<(event: MediaQueryListEvent) => void>();
  const mql = {
    matches: dark,
    media: DARK_QUERY,
    addEventListener: (_: string, listener: (event: MediaQueryListEvent) => void) => listeners.add(listener),
    removeEventListener: (_: string, listener: (event: MediaQueryListEvent) => void) => listeners.delete(listener),
  };
  vi.stubGlobal('matchMedia', vi.fn(() => mql));
  return {
    flip(next: boolean) {
      mql.matches = next;
      listeners.forEach((listener) => listener({ matches: next } as MediaQueryListEvent));
    },
  };
}
