import { createRequire } from 'node:module';
const qa = '/Users/sakastudio/hermes-agent/data/worktrees/tier9-integrated-qa/moorestech/moorestech_web/webui';
const require = createRequire(`${qa}/package.json`);
const tseslint = require('typescript-eslint');
// 既存設定にはe2e対象が無い。scratchでTS構文検査だけを明示する。
// Existing config has no e2e selector; explicitly run TS syntax lint from scratch.
export default [{
  basePath: qa,
  files: ['e2e/**/*.{ts,tsx}'],
  languageOptions: { parser: tseslint.parser, parserOptions: { ecmaFeatures: { jsx: true }, sourceType: 'module' } },
}];
