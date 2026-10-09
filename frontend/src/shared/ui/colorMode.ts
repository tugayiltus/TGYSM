import { useSyncExternalStore } from 'react';

/** Light, dark or the system's choice (ADR-0026). CoreUI reads the resolved mode from `data-coreui-theme`. */
export const COLOR_MODES = ['light', 'dark', 'auto'] as const;
export type ColorMode = (typeof COLOR_MODES)[number];
export type ResolvedMode = Exclude<ColorMode, 'auto'>;

/** Mirrored in the pre-paint script in `index.html`; a test keeps the two equal. */
export const COLOR_MODE_STORAGE_KEY = 'tgysm-theme';
const THEME_ATTRIBUTE = 'data-coreui-theme';
const DARK_QUERY = '(prefers-color-scheme: dark)';
const DEFAULT_MODE: ColorMode = 'auto';

const isColorMode = (value: unknown): value is ColorMode => typeof value === 'string' && (COLOR_MODES as readonly string[]).includes(value);

export function readStoredMode(): ColorMode {
  try {
    const stored = window.localStorage.getItem(COLOR_MODE_STORAGE_KEY);
    return isColorMode(stored) ? stored : DEFAULT_MODE;
  } catch {
    // Private mode or blocked storage: follow the system.
    return DEFAULT_MODE;
  }
}

const systemPrefersDark = () => window.matchMedia(DARK_QUERY).matches;

export function resolveMode(mode: ColorMode): ResolvedMode {
  if (mode !== 'auto') {
    return mode;
  }
  return systemPrefersDark() ? 'dark' : 'light';
}

export function applyColorMode(mode: ColorMode, root: HTMLElement = document.documentElement) {
  root.setAttribute(THEME_ATTRIBUTE, resolveMode(mode));
}

interface ColorModeState {
  mode: ColorMode;
  resolved: ResolvedMode;
}

// One state for the whole page: the header menu and every chart read the same mode.
let current: ColorModeState | null = null;
const listeners = new Set<() => void>();
let systemSubscribed = false;

function snapshot(): ColorModeState {
  if (current === null) {
    const mode = readStoredMode();
    current = { mode, resolved: resolveMode(mode) };
  }
  return current;
}

function publish(mode: ColorMode) {
  current = { mode, resolved: resolveMode(mode) };
  applyColorMode(mode);
  listeners.forEach((listener) => listener());
}

function subscribeSystem() {
  if (systemSubscribed) {
    return;
  }
  systemSubscribed = true;
  window.matchMedia(DARK_QUERY).addEventListener('change', () => {
    if (snapshot().mode === 'auto') {
      publish('auto');
    }
  });
}

export function setColorMode(mode: ColorMode) {
  try {
    window.localStorage.setItem(COLOR_MODE_STORAGE_KEY, mode);
  } catch {
    // Not remembered; the mode still applies for this visit.
  }
  publish(mode);
}

/** Called once at start-up: the pre-paint script already set the attribute; this keeps it in sync afterwards. */
export function initColorMode() {
  current = null;
  applyColorMode(snapshot().mode);
  subscribeSystem();
}

function subscribe(listener: () => void) {
  subscribeSystem();
  listeners.add(listener);
  return () => {
    listeners.delete(listener);
  };
}

export function useColorMode() {
  const state = useSyncExternalStore(subscribe, snapshot, snapshot);
  return { mode: state.mode, resolved: state.resolved, setMode: setColorMode };
}

/** Tests replace `matchMedia` per test; this forgets the cached state and the old system listener. */
export function resetColorModeForTests() {
  current = null;
  systemSubscribed = false;
  listeners.clear();
}
