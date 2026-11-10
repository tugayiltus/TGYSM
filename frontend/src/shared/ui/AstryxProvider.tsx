import { LinkProvider } from '@astryxdesign/core/Link';
import { Theme } from '@astryxdesign/core/theme';
import { neutralTheme } from '@astryxdesign/theme-neutral/built';
import type { ReactNode } from 'react';
import { Link } from 'react-router';

import { useColorMode } from './colorMode';

/**
 * Astryx theme and router link wiring (ADR-0027). The theme follows the app's
 * light/dark choice, so the header menu drives Astryx and CoreUI screens alike
 * while both libraries live side by side during the migration.
 */
export function AstryxProvider({ children }: { children: ReactNode }) {
  const { resolved } = useColorMode();
  return (
    <Theme theme={neutralTheme} mode={resolved}>
      <LinkProvider component={Link}>{children}</LinkProvider>
    </Theme>
  );
}
