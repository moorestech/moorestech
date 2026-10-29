import { createRequire } from 'node:module';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const evidence = path.dirname(fileURLToPath(import.meta.url));
const qa = '/Users/sakastudio/hermes-agent/data/worktrees/tier9-integrated-qa/moorestech/moorestech_web/webui';
const require = createRequire(path.join(qa, 'package.json'));
const { WebSocketServer } = require('ws');
const { createMockHttpServer } = await import(`${qa}/e2e/mock-host/httpHandler.ts`);
const { attachWsHandlers } = await import(`${qa}/e2e/mock-host/wsHandler.ts`);
const server = createMockHttpServer();
const wss = new WebSocketServer({ server, path: '/ws' });
attachWsHandlers(wss);
const meta = { pid: process.pid, ppid: process.ppid, port: Number(process.env.MOCK_PORT), start: new Date().toISOString(), demo: process.env.MOCK_DEMO };
const filename = path.join(evidence, `server-${meta.port}.json`);
fs.writeFileSync(filename, JSON.stringify(meta, null, 2));
// 所有サーバーの停止を証拠へ残す。
// Record shutdown of this owned server as evidence.
process.on('SIGTERM', () => {
  fs.writeFileSync(filename, JSON.stringify({ ...meta, end: new Date().toISOString(), signal: 'SIGTERM' }, null, 2));
  process.exit(0);
});
server.listen(meta.port, '127.0.0.1', () => console.log(`QA mock on ${meta.port}, PID ${process.pid}`));
