import { describe, expect, it } from 'vitest';

import packageJson from '../../package.json';
import { applyTheme, THEME } from '../shared/ui/theme';

const allDependencies: Record<string, string> = { ...packageJson.dependencies, ...packageJson.devDependencies };

describe('styling stack', () => {
  it('uses CoreUI and no longer ships Tailwind or Radix', () => {
    for (const removed of ['tailwindcss', '@tailwindcss/vite', 'radix-ui']) {
      expect(allDependencies).not.toHaveProperty(removed);
    }
    expect(allDependencies['@coreui/react']).toMatch(/^[~^]?5\.13(\.|$)/);
    expect(allDependencies['@coreui/coreui']).toMatch(/^[~^]?5\.9(\.|$)/);
    expect(allDependencies['@coreui/icons-react']).toMatch(/^[~^]?2\.3(\.|$)/);
    expect(allDependencies['@coreui/react-chartjs']).toMatch(/^[~^]?3\.0(\.|$)/);
    expect(allDependencies['chart.js']).toMatch(/^[~^]?4\.5(\.|$)/);
  });

  it('marks the document with the light CoreUI theme', () => {
    document.documentElement.removeAttribute('data-coreui-theme');
    applyTheme();
    expect(THEME).toBe('light');
    expect(document.documentElement.getAttribute('data-coreui-theme')).toBe('light');
  });
});
