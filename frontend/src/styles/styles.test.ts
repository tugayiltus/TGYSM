import { describe, expect, it } from 'vitest';

import indexHtml from '../../index.html?raw';
import packageJson from '../../package.json';
import viteConfig from '../../vite.config.ts?raw';
import { COLOR_MODE_STORAGE_KEY } from '../shared/ui/colorMode';

const allDependencies: Record<string, string> = { ...packageJson.dependencies, ...packageJson.devDependencies };

/** Every source file as raw text, keyed by path; the glob runs at build time, so the test sees the real tree. */
const sources = import.meta.glob(['../**/*.{ts,tsx}', '!../**/*.test.{ts,tsx}', '!../styles/styles.test.ts'], { query: '?raw', import: 'default', eager: true });
const styleSheets = import.meta.glob('../**/*.{scss,sass}', { eager: true });

describe('styling stack', () => {
  it('uses Astryx and ships no CoreUI, Sass, Tailwind or Radix', () => {
    for (const removed of ['@coreui/coreui', '@coreui/react', '@coreui/icons', '@coreui/icons-react', '@coreui/react-chartjs', 'sass', 'tailwindcss', '@tailwindcss/vite', 'radix-ui']) {
      expect(allDependencies).not.toHaveProperty(removed);
    }
    expect(allDependencies).toHaveProperty('@astryxdesign/core');
    expect(allDependencies['chart.js']).toMatch(/^[~^]?4\.5(\.|$)/);
  });

  it('has no source file that imports CoreUI', () => {
    const offenders = Object.entries(sources)
      .filter(([, text]) => String(text).includes('@coreui/'))
      .map(([path]) => path);
    expect(offenders).toEqual([]);
  });

  it('has no Sass file and no CoreUI chunk group', () => {
    expect(Object.keys(styleSheets)).toEqual([]);
    expect(viteConfig).not.toContain('coreui');
  });

  it('applies the stored color mode before the first paint with the shared storage key', () => {
    expect(indexHtml).toContain(`localStorage.getItem('${COLOR_MODE_STORAGE_KEY}')`);
    expect(indexHtml).toContain('colorScheme');
    expect(indexHtml).not.toContain('data-coreui-theme');
  });
});
