import { describe, expect, it } from 'vitest';

import { periodText } from './period';

const TODAY = '2026-11-11';

describe('periodText', () => {
  it('names a matching preset', () => {
    expect(periodText({ from: '2026-10-13', to: TODAY }, TODAY)).toBe('Son 30 gün');
  });

  it('writes other ranges as two days', () => {
    expect(periodText({ from: '2026-09-01', to: '2026-09-30' }, TODAY)).toBe('1 Eyl 2026 – 30 Eyl 2026');
  });

  it('has no text without both days', () => {
    expect(periodText({ from: '2026-09-01' }, TODAY)).toBeNull();
    expect(periodText({}, TODAY)).toBeNull();
  });
});
