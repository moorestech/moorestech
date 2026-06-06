import { defineConfig } from 'vite';
import react from '@vitejs/plugin-react';

export default defineConfig({
  plugins: [react()],
  // host: true で全インターフェース。スマホ実機確認のため必須
  // host: true binds all interfaces so the page is reachable from a phone on the same LAN
  server: { port: 5181, host: true },
});
