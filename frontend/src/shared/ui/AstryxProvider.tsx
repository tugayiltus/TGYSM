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
import { useMemo, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';
import { Link } from 'react-router';

import { useColorMode } from './colorMode';

/** The UI is Turkish only (ADR-0006). Astryx's own labels use its Turkish catalog. */
const ASTRYX_LOCALE = 'tr-TR';
const ASTRYX_MESSAGES = { [ASTRYX_LOCALE]: trTR };

/** Astryx labels that the app already names in its own words. E2E tests and the user guide use these names. */
const ASTRYX_TEXT_KEYS = {
  '@astryx.mobileNav.toggle.open': 'actions.openMenu',
  '@astryx.mobileNav.closeNavigation': 'layout.closeMenu',
  '@astryx.mobileNav.navigation': 'layout.menu',
  '@astryx.appShell.mobileNavigation': 'layout.menu',
  '@astryx.appShell.skipToContent': 'layout.skipToContent',
  '@astryx.sideNavCollapseButton.collapseSidebar': 'layout.collapseSidebar',
  '@astryx.sideNavCollapseButton.expandSidebar': 'layout.expandSidebar',
} as const;

/**
 * Astryx context for the app (ADR-0027). The theme mode follows the app color mode, so one menu controls Astryx and
 * CoreUI screens.
 */
export function AstryxProvider({ children }: { children: ReactNode }) {
  const { resolved } = useColorMode();
  const { t } = useTranslation();
  const overrides = useMemo(() => ({ [ASTRYX_LOCALE]: Object.fromEntries(Object.entries(ASTRYX_TEXT_KEYS).map(([key, textKey]) => [key, t(textKey)])) }), [t]);
  return (
    <InternationalizationProvider locale={ASTRYX_LOCALE} messages={ASTRYX_MESSAGES} overrides={overrides}>
      <Theme theme={neutralTheme} mode={resolved}>
        <LinkProvider component={Link}>{children}</LinkProvider>
      </Theme>
    </InternationalizationProvider>
  );
}
