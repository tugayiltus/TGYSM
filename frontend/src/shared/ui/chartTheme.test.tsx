import { useTheme } from '@astryxdesign/core/theme';
import { act, renderHook } from '@testing-library/react';
import type { ReactNode } from 'react';
import { beforeEach, describe, expect, it } from 'vitest';

import { stubSystemColorScheme } from '../../test/colorMode';

import { AstryxProvider } from './AstryxProvider';
import { CHART_TOKENS, useChartColors } from './chartTheme';
import { resetColorModeForTests, setColorMode } from './colorMode';

const wrapper = ({ children }: { children: ReactNode }) => <AstryxProvider>{children}</AstryxProvider>;

describe('chart theme', () => {
  beforeEach(() => {
    window.localStorage.clear();
    resetColorModeForTests();
    stubSystemColorScheme(false);
  });

  it('takes the chart colors from the Astryx data and border tokens', () => {
    const { result } = renderHook(() => ({ chart: useChartColors(), theme: useTheme() }), { wrapper });
    expect(result.current.chart.colors.bar).toBe(result.current.theme.token(CHART_TOKENS.bar));
    expect(result.current.chart.colors.text).toBe(result.current.theme.token(CHART_TOKENS.text));
    expect(result.current.chart.colors.grid).toBe(result.current.theme.token(CHART_TOKENS.grid));
    expect(result.current.chart.colors.bar).not.toBe('');
  });

  it('changes its key with the color mode so the chart redraws', () => {
    const { result } = renderHook(() => useChartColors(), { wrapper });
    act(() => setColorMode('light'));
    const lightKey = result.current.key;
    act(() => setColorMode('dark'));
    expect(result.current.key).not.toBe(lightKey);
  });
});
