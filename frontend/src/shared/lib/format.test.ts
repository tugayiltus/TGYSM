import { describe, expect, it } from 'vitest';

import { formatDateTime } from './format';

describe('formatDateTime', () => {
  it('formats UTC timestamps in Istanbul time with Turkish month names', () => {
    const text = formatDateTime('2026-09-26T09:05:00Z');

    expect(text).toContain('Eyl');
    expect(text).toContain('12:05');
  });
});
