import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { createElement } from 'react';
import { afterEach, vi } from 'vitest';

import '../shared/i18n';

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

// jsdom has no canvas: Chart.js cannot draw or measure and throws while resizing a detached canvas. Tests check the
// chart's container and its text alternative, so the CoreUI chart is a plain canvas here.
vi.mock('@coreui/react-chartjs', () => ({
  CChartBar: () => createElement('canvas', { 'data-testid': 'chart-canvas' }),
}));
