/// <reference types="vitest/config" />
import react from '@vitejs/plugin-react';
import { defineConfig } from 'vite';

const API_TARGET = process.env.TGYSM_API_URL ?? 'http://localhost:5080';
const DEV_PORT = 5173;
const PREVIEW_PORT = 4173;
const CHUNK_WARNING_LIMIT_KB = 500;
const COVERAGE_LINE_THRESHOLD = 80;
// The API runs on plain HTTP locally; this header lets it treat proxied requests as HTTPS
// (Secure session and antiforgery cookies). Honoured only in the API's Development environment.
const FORWARDED_PROTO_HEADER = 'X-Forwarded-Proto';
// react-markdown and its unified tree (remark/micromark/mdast/unist/hast and their small helpers).
const MARKDOWN_PACKAGES =
  /node_modules[\\/](react-markdown|remark-[^\\/]+|micromark[^\\/]*|mdast-[^\\/]+|unist-[^\\/]+|hast-[^\\/]+|unified|vfile[^\\/]*|bail|trough|devlop|ccount|character-[^\\/]+|comma-separated-tokens|space-separated-tokens|decode-named-character-reference|estree-util-[^\\/]+|html-url-attributes|is-(alphabetical|alphanumerical|decimal|hexadecimal|plain-obj)|longest-streak|parse-entities|property-information|stringify-entities|trim-lines|zwitch)[\\/]/;
// Chart.js with its CoreUI wrapper and colour helper (ADR-0022).
const CHART_PACKAGES =
  /node_modules[\\/](chart\.js|@kurkle[\\/]color|@coreui[\\/]react-chartjs)[\\/]/;
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
            { name: 'vendor-coreui-icons', test: /node_modules[\\/]@coreui[\\/](icons|icons-react)[\\/]/, priority: 95 },
            { name: 'vendor-coreui', test: /node_modules[\\/](@coreui[\\/](react|coreui|utils)|@popperjs)[\\/]/, priority: 90 },
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
