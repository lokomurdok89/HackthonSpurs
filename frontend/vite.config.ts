import { defineConfig, loadEnv } from 'vite'
import react from '@vitejs/plugin-react'

export default defineConfig(({ mode }) => {
  // Match Compose's host port; do not expose the root .env to browser code.
  const port = loadEnv(mode, '..', 'API_PORT').API_PORT || '5080'
  if (!/^\d+$/.test(port) || +port < 1 || +port > 65535) throw new Error('Invalid API_PORT')
  return {
    plugins: [react()],
    server: { host: '127.0.0.1', proxy: { '/api/': 'http://127.0.0.1:' + port } },
  }
})
