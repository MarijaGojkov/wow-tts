import react from '@vitejs/plugin-react'
import { defineConfig } from 'vite'

// Requests to /api go to the ASP.NET Core API, so the browser sees one site and no CORS setup is needed.
const api = {
  '/api': 'http://localhost:5219',
}

// https://vite.dev/config/
export default defineConfig({
  plugins: [react()],
  server: { port: 5173, proxy: api },
  preview: { port: 4173, proxy: api },
})
