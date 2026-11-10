/**
 * @input children: the whole app tree.
 * @output Astryx locale, theme and router-link context around the children.
 * @position Top of the app, inside the query client (`src/app/App.tsx`).
 */
import { InternationalizationProvider } from '@astryxdesign/core/i18n';
import { LinkProvider } from '@astryxdesign/core/Link';
import trTR from '@astryxdesign/core/locales/tr-TR.generated.js';
import { Theme } from '@astryxdesign/core/theme';
import { neutralTheme } from '@astryxdesign/theme-neutral/built';
import type { ReactNode } from 'react';
import { Link } from 'react-router';

import { useColorMode } from './colorMode';

/** The UI is Turkish only (ADR-0006). Astryx's own labels use its Turkish catalog. */
const ASTRYX_LOCALE = 'tr-TR';
const ASTRYX_MESSAGES = { [ASTRYX_LOCALE]: trTR };

/**
 * Astryx context for the app (ADR-0027). The theme mode follows the app color mode, so one menu controls Astryx and
 * CoreUI screens.
 */
export function AstryxProvider({ children }: { children: ReactNode }) {
  const { resolved } = useColorMode();
  return (
    <InternationalizationProvider locale={ASTRYX_LOCALE} messages={ASTRYX_MESSAGES}>
      <Theme theme={neutralTheme} mode={resolved}>
        <LinkProvider component={Link}>{children}</LinkProvider>
      </Theme>
    </InternationalizationProvider>
  );
}
