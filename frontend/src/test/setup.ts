import '@testing-library/jest-dom/vitest';
import { cleanup } from '@testing-library/react';
import { afterEach, vi } from 'vitest';

import '../shared/i18n';

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});
