import { defineConfig } from 'vitest/config';
import react from '@vitejs/plugin-react';
import { resolve } from 'node:path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': resolve(__dirname, '.'),
      '@app': resolve(__dirname, 'app'),
      '@widgets': resolve(__dirname, 'widgets'),
      '@features': resolve(__dirname, 'features'),
      '@entities': resolve(__dirname, 'entities'),
      '@shared': resolve(__dirname, 'shared'),
    },
  },
  css: { postcss: { plugins: [] } },
  test: {
    environment: 'jsdom',
    globals: true,
    setupFiles: ['./vitest.setup.ts'],
    include: ['**/*.{test,spec}.{ts,tsx}'],
    exclude: ['node_modules', '.next', 'playwright/**'],
  },
});
