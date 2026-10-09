import { act, renderHook } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';

import { stubSystemColorScheme } from '../../test/colorMode';

import { CHART_FALLBACK, readChartColors, useChartColors } from './chartTheme';
import { resetColorModeForTests, setColorMode } from './colorMode';

describe('chart theme', () => {
  beforeEach(() => {
    window.localStorage.clear();
    resetColorModeForTests();
    stubSystemColorScheme(false);
  });

  it('falls back to fixed colors when CSS variables are missing (jsdom)', () => {
    expect(readChartColors()).toEqual(CHART_FALLBACK);
  });

  it('reads CoreUI variables when they are set', () => {
    document.documentElement.style.setProperty('--cui-primary', '#5856d6');
    expect(readChartColors().bar).toBe('#5856d6');
    document.documentElement.style.removeProperty('--cui-primary');
  });

  it('changes its key with the color mode so the chart redraws', () => {
    const { result } = renderHook(() => useChartColors());
    act(() => setColorMode('light'));
    const lightKey = result.current.key;
    act(() => setColorMode('dark'));
    expect(result.current.key).not.toBe(lightKey);
  });
});
