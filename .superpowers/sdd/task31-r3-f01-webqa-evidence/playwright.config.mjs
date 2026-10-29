import path from 'node:path';
import { fileURLToPath } from 'node:url';
const evidence = path.dirname(fileURLToPath(import.meta.url));
const qa = '/Users/sakastudio/hermes-agent/data/worktrees/tier9-integrated-qa/moorestech/moorestech_web/webui';
const matrix = process.env.QA_MATRIX === '1';
const port = matrix ? 58392 : 58391;
export default {
  testDir: matrix ? evidence : path.join(qa, 'e2e/tests'),
  testMatch: matrix ? 'matrix.spec.mjs' : '**/*.spec.ts',
  timeout: 15000, fullyParallel: false, workers: 1,
  outputDir: path.join(evidence, 'results', matrix ? 'matrix-traces' : 'existing-traces'),
  reporter: [['list'], ['json', { outputFile: path.join(evidence, 'results', matrix ? 'matrix.json' : 'playwright.json') }]],
  use: { baseURL: `http://127.0.0.1:${port}`, trace: 'retain-on-failure' },
  webServer: {
    command: `node --import ${qa}/node_modules/tsx/dist/loader.mjs ${evidence}/mock-server.mjs`,
    cwd: qa, port, reuseExistingServer: false, timeout: 30000,
    env: { MOCK_PORT: String(port), MOORESTECH_E2E: 'true', MOCK_DEMO: matrix ? '1' : '0', QA_MATRIX: matrix ? '1' : '0' },
  },
};
