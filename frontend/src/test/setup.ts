import '@testing-library/jest-dom/vitest';
import { cleanup, configure } from '@testing-library/react';
import { createElement } from 'react';
import { afterEach, vi } from 'vitest';

import '../shared/i18n';
import { resetColorModeForTests } from '../shared/ui/colorMode';

import { installDialogPolyfill } from './dialogPolyfill';

installDialogPolyfill();

/** Pages render inside the Astryx shell and its providers; under a full parallel run the first paint can pass 1 s. */
const ASYNC_UTIL_TIMEOUT_MS = 3000;
configure({ asyncUtilTimeout: ASYNC_UTIL_TIMEOUT_MS });

// jsdom has no matchMedia: the color mode reads the system scheme through it. Default: a light system that never changes;
// tests that need a dark or switching system replace it with `stubSystemColorScheme` (src/test/colorMode.ts).
if (typeof window.matchMedia !== 'function') {
  Object.defineProperty(window, 'matchMedia', {
    configurable: true,
    writable: true,
    value: (query: string) => ({ matches: false, media: query, onchange: null, addEventListener: () => undefined, removeEventListener: () => undefined, addListener: () => undefined, removeListener: () => undefined, dispatchEvent: () => false }),
  });
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  resetColorModeForTests();
});

// jsdom has no canvas: Chart.js cannot draw or measure and throws while resizing a detached canvas. Tests check the
// chart's container and its text alternative, so the CoreUI chart is a plain canvas here.
vi.mock('@coreui/react-chartjs', () => ({
  CChartBar: () => createElement('canvas', { 'data-testid': 'chart-canvas' }),
}));
