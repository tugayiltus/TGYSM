import { InternationalizationProvider } from '@astryxdesign/core/i18n';
import { LinkProvider } from '@astryxdesign/core/Link';
import trTR from '@astryxdesign/core/locales/tr-TR.generated.js';
import { Theme } from '@astryxdesign/core/theme';
import { neutralTheme } from '@astryxdesign/theme-neutral/built';
import type { ReactNode } from 'react';
import { Link } from 'react-router';

import { useColorMode } from './colorMode';

/** The UI is Turkish only (ADR-0006); Astryx's own labels use its shipped Turkish catalog. */
const ASTRYX_LOCALE = 'tr-TR';
const ASTRYX_MESSAGES = { [ASTRYX_LOCALE]: trTR };

/**
 * Astryx theme and router link wiring (ADR-0027). The theme follows the app's
 * light/dark choice, so the header menu drives Astryx and CoreUI screens alike
 * while both libraries live side by side during the migration.
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
