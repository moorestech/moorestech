const qa = '/Users/sakastudio/hermes-agent/data/worktrees/tier9-integrated-qa/moorestech/moorestech_web/webui';
const { default: original } = await import(`${qa}/eslint.config.mjs`);
// 製品設定は変えず、既存ルールの対象srcだけをe2eへ写して明示検査する。
// Keep product config intact and explicitly apply its existing rules with src selectors mapped to e2e.
export default original.map(config => {
  const mapped = { ...config, basePath: qa };
  if (config.files) mapped.files = config.files.map(pattern => pattern.replace(/^src\//, 'e2e/'));
  if (config.ignores) mapped.ignores = config.ignores.map(pattern => pattern.replace(/^src\//, 'e2e/'));
  return mapped;
});
