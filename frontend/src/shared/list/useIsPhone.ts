/**
 * @input Nothing.
 * @output True below the tablet width.
 * @position Lets list parts swap a table for cards, and inline filters for a drawer.
 */
import { useMediaQuery } from '@astryxdesign/core/hooks';

/** The query matches phones, so a missing `matchMedia` (tests, old browsers) gives the wide layout. */
export const PHONE_QUERY = '(max-width: 767.98px)';

export function useIsPhone(): boolean {
  return useMediaQuery(PHONE_QUERY);
}
