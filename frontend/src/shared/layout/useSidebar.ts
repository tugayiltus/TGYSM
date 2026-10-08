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

/** Desktop narrow mode (remembered per browser) and the phone panel state. */
export function useSidebar() {
  const [narrow, setNarrow] = useState(readNarrow);
  const [mobileOpen, setMobileOpen] = useState(false);
  const toggleNarrow = () =>
    setNarrow((current) => {
      const next = !current;
      try {
        window.localStorage.setItem(SIDEBAR_STORAGE_KEY, String(next));
      } catch {
        // Not remembered; the toggle still works for this visit.
      }
      return next;
    });
  return { narrow, toggleNarrow, mobileOpen, setMobileOpen };
}
