import tailwindcss from '@tailwindcss/vite'
import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

export default defineConfig({
  plugins: [react(), tailwindcss()],
  server: {
    host: '127.0.0.1',
    port: 5173,
    strictPort: true,
    proxy: {
      '/api': {
        target: 'http://127.0.0.1:5232',
        changeOrigin: true,
      },
      '/health': {
        target: 'http://127.0.0.1:5232',
        changeOrigin: true,
      },
      '/cognito': {
        target: 'http://127.0.0.1:9229',
        changeOrigin: true,
        rewrite: (path) => path.replace(/^\/cognito/, ''),
      },
    },
  },
})
