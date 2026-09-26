/// <reference types="vitest/config" />
import tailwindcss from '@tailwindcss/vite';
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
const apiProxy = { '/api': { target: API_TARGET, changeOrigin: false, headers: { [FORWARDED_PROTO_HEADER]: 'https' } } };

export default defineConfig({
  plugins: [react(), tailwindcss()],
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
            { name: 'vendor-radix', test: /node_modules[\\/](radix-ui|@radix-ui)[\\/]/, priority: 90 },
            { name: 'vendor-forms', test: /node_modules[\\/](react-hook-form|@hookform|zod)[\\/]/, priority: 90 },
            { name: 'vendor-i18n', test: /node_modules[\\/](i18next|react-i18next)[\\/]/, priority: 90 },
            { name: 'vendor-other', test: /node_modules/, priority: 10 },
            // App code; everything else falls to the entry chunk.
            { name: 'feature-admin', test: /[\\/]src[\\/]features[\\/]admin[\\/]/, priority: 40 },
            { name: 'app-shared', test: /[\\/]src[\\/]shared[\\/]/, priority: 30 },
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
