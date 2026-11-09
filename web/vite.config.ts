import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// The React app talks to the API through /api. In development Vite proxies it to Kestrel;
// in production the built files are served by the API itself (same origin, no CORS).
export default defineConfig({
  plugins: [react()],
  server: {
    port: 5173,
    proxy: {
      '/api': { target: 'http://localhost:5088', changeOrigin: true },
    },
  },
  build: {
    outDir: '../src/Lims.Api/wwwroot',
    emptyOutDir: true,
    chunkSizeWarningLimit: 900,
  },
})
