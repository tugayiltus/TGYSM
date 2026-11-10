/**
 * @input Nothing.
 * @output The collapsed state of the side navigation (kept per browser) and the phone drawer state.
 * @position Used by `ShellFrame`.
 */
import { useState } from 'react';

export const SIDEBAR_STORAGE_KEY = 'tgysm.sidebar.narrow';

function readNarrow(): boolean {
  try {
    return window.localStorage.getItem(SIDEBAR_STORAGE_KEY) === 'true';
  } catch {
    // Private mode or blocked storage: start wide.
    return false;
  }
}

export function useSidebar() {
  const [narrow, setNarrowState] = useState(readNarrow);
  const [mobileOpen, setMobileOpen] = useState(false);
  const setNarrow = (next: boolean) => {
    setNarrowState(next);
    try {
      window.localStorage.setItem(SIDEBAR_STORAGE_KEY, String(next));
    } catch {
      // Not kept; the toggle still works for this visit.
    }
  };
  return { narrow, setNarrow, mobileOpen, setMobileOpen };
}
