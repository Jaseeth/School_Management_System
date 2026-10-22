import { defineConfig } from 'vite'
import react from '@vitejs/plugin-react'
import tailwindcss from '@tailwindcss/vite'
import fs from 'node:fs'
import { fileURLToPath } from 'node:url'

const certPath = process.env.SCHOOL_HTTPS_CERT || fileURLToPath(new URL('../.local-tools/https/localhost.pem', import.meta.url))
const keyPath = process.env.SCHOOL_HTTPS_KEY || fileURLToPath(new URL('../.local-tools/https/localhost.key', import.meta.url))
const https = fs.existsSync(certPath) && fs.existsSync(keyPath)
  ? { cert: fs.readFileSync(certPath), key: fs.readFileSync(keyPath) } : undefined

export default defineConfig({
  server: {
    https,
    proxy: {
      '/api': {
        target: process.env.SCHOOL_API_PROXY_TARGET || 'https://localhost:7063',
        changeOrigin: true,
        // Local development certificate; browsers access Vite on the same origin.
        secure: false,
      },
    },
  },
  plugins: [
    react(),
    tailwindcss(),
  ],
})
