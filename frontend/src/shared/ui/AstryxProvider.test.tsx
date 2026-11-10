import { Pagination } from '@astryxdesign/core/Pagination';
import { useTheme } from '@astryxdesign/core/theme';
import { act, render, screen } from '@testing-library/react';
import { beforeEach, describe, expect, it } from 'vitest';

import { AstryxProvider } from './AstryxProvider';
import { resetColorModeForTests, setColorMode } from './colorMode';
import { Icons } from './icons';

const FIRST_PAGE = 1;
const PAGE_COUNT = 3;

function ModeProbe() {
  const { mode } = useTheme();
  return <output aria-label="mode">{mode}</output>;
}

describe('AstryxProvider', () => {
  beforeEach(() => {
    window.localStorage.clear();
    resetColorModeForTests();
  });

  it('renders Astryx strings in Turkish', () => {
    render(
      <AstryxProvider>
        <Pagination page={FIRST_PAGE} totalPages={PAGE_COUNT} onChange={() => undefined} />
      </AstryxProvider>,
    );
    expect(screen.getByRole('button', { name: 'Sonraki sayfaya git' })).toBeInTheDocument();
  });

  it('follows the dark color mode', () => {
    render(
      <AstryxProvider>
        <ModeProbe />
      </AstryxProvider>,
    );
    expect(screen.getByLabelText('mode')).toHaveTextContent('light');
    act(() => setColorMode('dark'));
    expect(screen.getByLabelText('mode')).toHaveTextContent('dark');
  });

  it('offers every app icon as an Astryx icon', () => {
    for (const icon of Object.values(Icons)) {
      expect(['string', 'function', 'object']).toContain(typeof icon);
    }
    expect(Object.keys(Icons).length).toBeGreaterThan(0);
  });
});
