/**
 * @input Text an alert must contain, or nothing.
 * @output Alert elements that carry a message.
 * @position Test-only. Astryx keeps its own empty `role="alert"` live region, so a bare role query is ambiguous.
 */
import { screen, waitFor } from '@testing-library/react';

/** Alerts with a message; the empty Astryx live region is left out. */
export function messageAlerts(): HTMLElement[] {
  return screen.queryAllByRole('alert').filter((alert) => (alert.textContent ?? '').trim() !== '');
}

/** The first alert whose text contains `text`, or the first alert with any message. */
export function findAlert(text?: string): Promise<HTMLElement> {
  return waitFor(() => {
    const match = messageAlerts().find((alert) => text === undefined || alert.textContent?.includes(text) === true);
    if (match === undefined) {
      throw new Error(text === undefined ? 'No alert has a message.' : `No alert contains "${text}".`);
    }
    return match;
  });
}
