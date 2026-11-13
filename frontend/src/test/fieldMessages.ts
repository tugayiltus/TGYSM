/**
 * @input The field status text to find.
 * @output The field status messages that show the text.
 * @position Test-only. Astryx also writes each field status into its live regions on `document.body`.
 */
import { screen } from '@testing-library/react';

/**
 * The Astryx live regions, plus the elements Testing Library ignores by default.
 * A region gets the joined status text one animation frame after the fields show it and keeps it for 2 s.
 * A slow run can query in that window, so a text query must leave the regions out.
 */
const IGNORED = '[data-astryx-live-region], script, style';

/** Field status messages with exactly `text`. The live region copy is left out. */
export function findFieldMessages(text: string): Promise<HTMLElement[]> {
  return screen.findAllByText(text, { ignore: IGNORED });
}
