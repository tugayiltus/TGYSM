import { describe, expect, it } from 'vitest';

import { FIRST_PAGE, readPageParam } from './pageParam';

describe('readPageParam', () => {
  it('reads a whole positive page number', () => {
    expect(readPageParam('3')).toBe(3);
  });

  it.each([null, '', '0', '-2', '1.5', 'Infinity', '1e21', '2abc', ' 2', '9999999999'])('falls back to the first page for %s', (value) => {
    expect(readPageParam(value)).toBe(FIRST_PAGE);
  });
});
