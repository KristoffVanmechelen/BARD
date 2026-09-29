import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';
import path from 'path';

export default defineConfig({
  plugins: [react()],
  resolve: {
    alias: {
      '@': path.resolve(__dirname, './src'),
    },
  },
  server: {
    port: 5173,
    host: true, // listen on all interfaces — required for Codespaces port forwarding
    // Codespaces rewrites the Host header of forwarded requests to the
    // *.app.github.dev hostname; Vite's dev server otherwise rejects
    // unrecognised hosts.
    allowedHosts: true,
    proxy: {
      '/api': {
        // Codespaces/local dev API (see appsettings.Development.json /
        // scripts/start-dev.sh) runs HTTP-only on 5080. Production
        // deployments don't use this dev proxy at all (the built
        // frontend is served behind a real reverse proxy/HTTPS).
        target: 'http://localhost:5080',
        changeOrigin: true,
      },
    },
  },
});
