import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'

// Aspire injects the API URL via services__api__http__0; fall back to launchSettings port
const apiTarget = process.env['services__api__http__0'] ?? 'http://localhost:5210'

export default defineConfig({
  plugins: [react()],
  server: {
    port: 3000,
    proxy: {
      '/api': {
        target: apiTarget,
        changeOrigin: true,
      },
      '/health': {
        target: apiTarget,
        changeOrigin: true,
      },
      '/openapi': {
        target: apiTarget,
        changeOrigin: true,
      },
    },
  },
})
