import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { stubSystemColorScheme } from '../../test/colorMode';

import { applyColorMode, COLOR_MODE_STORAGE_KEY, readStoredMode, resetColorModeForTests, resolveMode, setColorMode, useColorMode } from './colorMode';

describe('color mode', () => {
  beforeEach(() => {
    window.localStorage.clear();
    document.documentElement.removeAttribute('data-coreui-theme');
    resetColorModeForTests();
  });
  afterEach(() => vi.restoreAllMocks());

  it('defaults to auto and follows the system', () => {
    stubSystemColorScheme(true);
    expect(readStoredMode()).toBe('auto');
    expect(resolveMode('auto')).toBe('dark');
  });

  it('writes the choice and the resolved attribute', () => {
    stubSystemColorScheme(false);
    setColorMode('dark');
    expect(window.localStorage.getItem(COLOR_MODE_STORAGE_KEY)).toBe('dark');
    expect(document.documentElement.getAttribute('data-coreui-theme')).toBe('dark');
  });

  it('ignores an unknown stored value', () => {
    stubSystemColorScheme(false);
    window.localStorage.setItem(COLOR_MODE_STORAGE_KEY, 'purple');
    expect(readStoredMode()).toBe('auto');
  });

  it('survives blocked storage', () => {
    stubSystemColorScheme(false);
    vi.spyOn(Storage.prototype, 'getItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    vi.spyOn(Storage.prototype, 'setItem').mockImplementation(() => {
      throw new Error('blocked');
    });
    expect(readStoredMode()).toBe('auto');
    expect(() => setColorMode('dark')).not.toThrow();
    expect(document.documentElement.getAttribute('data-coreui-theme')).toBe('dark');
  });

  it('re-resolves auto when the system switches', () => {
    const system = stubSystemColorScheme(false);
    const { result } = renderHook(() => useColorMode());
    act(() => result.current.setMode('auto'));
    expect(result.current.resolved).toBe('light');
    act(() => system.flip(true));
    expect(result.current.resolved).toBe('dark');
    expect(document.documentElement.getAttribute('data-coreui-theme')).toBe('dark');
  });

  it('keeps a fixed mode when the system switches', () => {
    const system = stubSystemColorScheme(false);
    const { result } = renderHook(() => useColorMode());
    act(() => result.current.setMode('light'));
    act(() => system.flip(true));
    expect(result.current.resolved).toBe('light');
  });

  it('applies a fixed mode without consulting the system', () => {
    stubSystemColorScheme(true);
    applyColorMode('light');
    expect(document.documentElement.getAttribute('data-coreui-theme')).toBe('light');
  });
});
