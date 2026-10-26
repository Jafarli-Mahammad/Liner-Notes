import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  build: { assetsDir: 'home-assets' },
  server: {
    proxy: {
      '/api': { target: 'http://localhost:5080' },
      '/health': { target: 'http://localhost:5080' },
    },
  },
});
