import { spawn } from 'node:child_process';
import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';
const evidence = path.dirname(fileURLToPath(import.meta.url));
const cwd = '/Users/sakastudio/hermes-agent/data/worktrees/tier9-integrated-qa/moorestech/moorestech_web/webui';
const [name, command, ...args] = process.argv.slice(2);
const meta = { name, command, args, cwd, sha: '7fd8f95723407b5d9418744078644ff3c6a498a5', start: new Date().toISOString(), ownerPid: process.pid };
const output = fs.openSync(path.join(evidence, `${name}.log`), 'w');
const env = { ...process.env, MOORESTECH_E2E: 'true' };
// アイコン404の単体契約を守る。実画像モードはブラウザ配信時だけ使う。
// Keep the unit-test 404 contract; real-image mode is only for browser serving.
delete env.MOCK_DEMO;
const child = spawn(command, args, { cwd, stdio: ['ignore', output, output], env });
meta.pid = child.pid;
fs.writeFileSync(path.join(evidence, `${name}.json`), JSON.stringify(meta, null, 2));
const timer = setTimeout(() => { meta.timedOut = true; child.kill('SIGTERM'); }, 570000);
child.on('error', error => { meta.error = error.message; });
child.on('exit', (code, signal) => {
  clearTimeout(timer);
  Object.assign(meta, { code, signal, end: new Date().toISOString() });
  fs.writeFileSync(path.join(evidence, `${name}.json`), JSON.stringify(meta, null, 2));
  fs.closeSync(output);
  console.log(JSON.stringify(meta));
  console.log(fs.readFileSync(path.join(evidence, `${name}.log`), 'utf8').slice(-3500));
  process.exitCode = code ?? 1;
});
