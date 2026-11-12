/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

const API_TARGET = process.env.TGYSM_API_URL ?? 'http://localhost:5311';
const DEV_PORT = 5310;
const PREVIEW_PORT = 5312;
const CHUNK_WARNING_LIMIT_KB = 500;
const COVERAGE_LINE_THRESHOLD = 80;
// The API runs on plain HTTP locally; this header lets it treat proxied requests as HTTPS
// (Secure session and antiforgery cookies). Honoured only in the API's Development environment.
const FORWARDED_PROTO_HEADER = 'X-Forwarded-Proto';
// react-markdown and its unified tree (remark/micromark/mdast/unist/hast and their small helpers).
const MARKDOWN_PACKAGES =
  /node_modules[\\/](react-markdown|remark-[^\\/]+|micromark[^\\/]*|mdast-[^\\/]+|unist-[^\\/]+|hast-[^\\/]+|unified|vfile[^\\/]*|bail|trough|devlop|ccount|character-[^\\/]+|comma-separated-tokens|space-separated-tokens|decode-named-character-reference|estree-util-[^\\/]+|html-url-attributes|is-(alphabetical|alphanumerical|decimal|hexadecimal|plain-obj)|longest-streak|parse-entities|property-information|stringify-entities|trim-lines|zwitch)[\\/]/;
// Chart.js with its React wrapper and colour helper (ADR-0022).
const CHART_PACKAGES =
  /node_modules[\\/](chart\.js|@kurkle[\\/]color|react-chartjs-2)[\\/]/;
/** Astryx `dist` sub-folders per chunk; a folder name is a component or a shared runtime module. */
const astryxFolders = (folders: string) => new RegExp(`node_modules[\\\\/]@astryxdesign[\\\\/]core[\\\\/]dist[\\\\/](${folders})[\\\\/]`);
const ASTRYX_THEME = astryxFolders('theme|i18n|Icon|Link|Text|Heading');
const ASTRYX_RUNTIME = astryxFolders('utils|hooks|motion|gestures|Layer|keyboard|internal|focus');
const ASTRYX_FORMS = astryxFolders('Selector|MultiSelector|TextInput|TextArea|NumberInput|DateInput|DateTimeInput|DateRangeInput|TimeInput|Calendar|CheckboxInput|Switch|FileInput|Typeahead|BaseTypeahead|Field|InputGroup|ToggleButton|SegmentedControl');
const ASTRYX_DATA = astryxFolders('Table|List|Item|MetadataList|Pagination|Token|StatusDot|EmptyState|Skeleton|Spinner|Markdown|Code');
const ASTRYX_OVERLAYS = astryxFolders('Dialog|AlertDialog|Drawer|DropdownMenu|BottomSheet|Popover|Toast|Tooltip|HoverCard|MoreMenu');
const apiProxy = { '/api': { target: API_TARGET, changeOrigin: false, headers: { [FORWARDED_PROTO_HEADER]: 'https' } } };

export default defineConfig({
  plugins: [react()],
  resolve: { dedupe: ['react', 'react-dom'] },
  server: { port: DEV_PORT, strictPort: true, proxy: apiProxy },
  preview: { port: PREVIEW_PORT, strictPort: true, proxy: apiProxy },
  build: {
    chunkSizeWarningLimit: CHUNK_WARNING_LIMIT_KB,
    rolldownOptions: {
      output: {
        codeSplitting: {
          groups: [
            // React runtime (plus scheduler) in one group; keeps `dedupe` single-instance obvious.
            { name: 'vendor-react', test: /node_modules[\\/](react|react-dom|scheduler)[\\/]/, priority: 100 },
            { name: 'vendor-router', test: /node_modules[\\/]react-router/, priority: 90 },
            { name: 'vendor-tanstack', test: /node_modules[\\/]@tanstack[\\/]/, priority: 90 },
            // Astryx design system with its StyleX runtime and theme (ADR-0027). One group would pass the chunk budget, so its
            // dist folders split by role: theme and text, shared runtime, form controls, data display and overlays; the rest stays in vendor-astryx.
            { name: 'vendor-astryx-theme', test: ASTRYX_THEME, priority: 96 },
            { name: 'vendor-astryx-runtime', test: ASTRYX_RUNTIME, priority: 96 },
            { name: 'vendor-astryx-forms', test: ASTRYX_FORMS, priority: 95 },
            { name: 'vendor-astryx-data', test: ASTRYX_DATA, priority: 95 },
            { name: 'vendor-astryx-overlays', test: ASTRYX_OVERLAYS, priority: 95 },
            { name: 'vendor-astryx', test: /node_modules[\\/](@astryxdesign|@stylexjs)[\\/]/, priority: 90 },
            { name: 'vendor-icons', test: /node_modules[\\/]lucide-react[\\/]/, priority: 90 },
            { name: 'vendor-forms', test: /node_modules[\\/](react-hook-form|@hookform|zod)[\\/]/, priority: 90 },
            { name: 'vendor-i18n', test: /node_modules[\\/](i18next|react-i18next)[\\/]/, priority: 90 },
            { name: 'vendor-markdown', test: MARKDOWN_PACKAGES, priority: 90 },
            { name: 'vendor-charts', test: CHART_PACKAGES, priority: 90 },
            // Groups run from the highest priority down, and each one also takes the not-yet-grouped modules its members import.
            // vendor-other and app-shared therefore run before the feature groups: otherwise the first feature group would
            // swallow shared code and leftover packages, and the entry chunk would import (and preload) that feature chunk.
            { name: 'vendor-other', test: /node_modules/, priority: 60 },
            { name: 'app-shared', test: /[\\/]src[\\/]shared[\\/]/, priority: 50 },
            // Feature modules other features import (article picker, asset queries, admin reference options): kept apart so a ticket page
            // does not load the knowledge base's Markdown renderer or the admin screens.
            { name: 'feature-common', test: /[\\/]src[\\/]features[\\/](kb[\\/]api\.ts|assets[\\/]api\.ts|admin[\\/]shared[\\/])/, priority: 45 },
            // App code; everything else falls to the entry chunk.
            { name: 'feature-admin', test: /[\\/]src[\\/]features[\\/]admin[\\/]/, priority: 40 },
            { name: 'feature-assets', test: /[\\/]src[\\/]features[\\/]assets[\\/]/, priority: 40 },
            { name: 'feature-kb', test: /[\\/]src[\\/]features[\\/]kb[\\/]/, priority: 40 },
            // DistributionChart stays out so the chart code (and vendor-charts) loads only with the first distribution card.
            { name: 'feature-reports', test: /[\\/]src[\\/]features[\\/]reports[\\/](?!DistributionChart)/, priority: 40 },
            { name: 'feature-tickets', test: /[\\/]src[\\/]features[\\/](tickets|workspace|approvals)[\\/]/, priority: 40 },
            { name: 'feature-tasks', test: /[\\/]src[\\/]features[\\/]tasks[\\/]/, priority: 40 },
          ],
        },
      },
    },
  },
  test: {
    environment: 'jsdom',
    setupFiles: ['./src/test/setup.ts'],
    include: ['src/**/*.test.{ts,tsx}'],
    coverage: {
      provider: 'v8',
      include: ['src/**/*.{ts,tsx}'],
      exclude: [
        'src/**/*.test.{ts,tsx}',
        'src/test/**',
        'src/main.tsx',
        'src/vite-env.d.ts',
        'src/shared/api/schema.d.ts',
        'src/shared/i18n/i18next.d.ts',
      ],
      thresholds: { lines: COVERAGE_LINE_THRESHOLD },
    },
  },
});
