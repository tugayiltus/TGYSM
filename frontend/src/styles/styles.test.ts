import { describe, expect, it } from 'vitest';

import indexHtml from '../../index.html?raw';
import packageJson from '../../package.json';
import { COLOR_MODE_STORAGE_KEY } from '../shared/ui/colorMode';

const allDependencies: Record<string, string> = { ...packageJson.dependencies, ...packageJson.devDependencies };

describe('styling stack', () => {
  it('uses CoreUI and no longer ships Tailwind or Radix', () => {
    for (const removed of ['tailwindcss', '@tailwindcss/vite', 'radix-ui']) {
      expect(allDependencies).not.toHaveProperty(removed);
    }
    expect(allDependencies['@coreui/react']).toMatch(/^[~^]?5\.13(\.|$)/);
    expect(allDependencies['@coreui/coreui']).toMatch(/^[~^]?5\.9(\.|$)/);
    expect(allDependencies['@coreui/react-chartjs']).toMatch(/^[~^]?3\.0(\.|$)/);
    expect(allDependencies['chart.js']).toMatch(/^[~^]?4\.5(\.|$)/);
  });

  it('ships the CoreUI icon packages', () => {
    expect(allDependencies['@coreui/icons']).toMatch(/^[~^]?3\.0(\.|$)/);
    expect(allDependencies['@coreui/icons-react']).toMatch(/^[~^]?2\.3(\.|$)/);
  });

  it('applies the stored color mode before the first paint with the shared storage key', () => {
    expect(indexHtml).toContain(`localStorage.getItem('${COLOR_MODE_STORAGE_KEY}')`);
    expect(indexHtml).toContain('data-coreui-theme');
  });
});
