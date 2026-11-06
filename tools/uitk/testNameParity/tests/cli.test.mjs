// test-name-parity.mjs の自己テスト: 一時 git リポジトリで CLI の終了コードと出力を確かめる
// Self-test for test-name-parity.mjs: checks CLI exit codes and output in a temporary git repository
import { test } from "node:test";
import assert from "node:assert/strict";
import { execFileSync, spawnSync } from "node:child_process";
import { mkdirSync, mkdtempSync, rmSync, unlinkSync, writeFileSync } from "node:fs";
import { tmpdir } from "node:os";
import { dirname, join } from "node:path";
import { fileURLToPath } from "node:url";

const cliPath = join(dirname(fileURLToPath(import.meta.url)), "..", "..", "test-name-parity.mjs");
const ledgerText = [
  "## 束", "", "## 行", "",
  "| 行 | 種別 | 束 | 所有者 | Web パス | agent 既定 | 実測の結論 | 参照キャプチャ |",
  "|---|---|---|---|---|---|---|---|",
  "| toast | 機能 | 1 | Web | `src/features/toast/` | — | — | — |",
  "| 検証基盤 | 横断 | 5 | Web | `e2e/` | — | — | — |",
  "", "## 追従記録", "",
].join("\n");
const dispositionText = [
  "# 処遇", "", "## 処遇", "",
  "| 束 | Web のファイル | Web のテスト名 | 処遇 | 証跡のパスか理由 | PR |",
  "|---|---|---|---|---|---|",
  "| 1 | `src/features/toast/toastWire.test.ts` | * | 捨てる | 通信の契約は UITK に無い | — |",
  "",
].join("\n");
const tagLine = (title) => `    /// <web-test file="src/features/toast/toastStore.test.ts">${title}</web-test>\n    [Test]\n    public void T() {}\n`;
const csPath = "moorestech_client/Assets/Scripts/Client.Tests/ToastTest.cs";

function createRepository() {
  const root = mkdtempSync(join(tmpdir(), "test-name-parity-"));
  const files = {
    "docs/uitk/TODO.md": ledgerText,
    "docs/uitk/disposition.md": dispositionText,
    "moorestech_web/webui/src/features/toast/toastStore.test.ts": 'it("追加され消える", () => {});\n',
    "moorestech_web/webui/src/features/toast/toastWire.test.ts": 'it("WS へ送る", () => {});\n',
    [csPath]: tagLine("追加され消える"),
  };
  for (const [path, text] of Object.entries(files)) {
    mkdirSync(dirname(join(root, path)), { recursive: true });
    writeFileSync(join(root, path), text);
  }
  execFileSync("git", ["init", "-q"], { cwd: root });
  execFileSync("git", ["add", "-A"], { cwd: root });
  execFileSync("git", ["-c", "user.name=t", "-c", "user.email=t", "commit", "-q", "-m", "fixture"], { cwd: root });
  return root;
}

function runCli(root, args) {
  const result = spawnSync(process.execPath, [cliPath, "--root", root, ...args], { encoding: "utf8" });
  return { status: result.status, output: result.stdout + result.stderr };
}

test("全部対応していれば exit 0 で件数を出す", () => {
  const root = createRepository();
  const result = runCli(root, ["--bundle", "1"]);
  rmSync(root, { recursive: true, force: true });
  assert.equal(result.status, 0, result.output);
  assert.match(result.output, /束 1: Web 2 件 \/ 写した 1 \/ 処遇 1 \/ 写す予定 0 \/ 未対応 0/);
});

test("タグが無ければ exit 1、--emit-missing は処遇の表の行の形で出す", () => {
  const root = createRepository();
  writeFileSync(join(root, csPath), "public class ToastTest {}\n");
  const result = runCli(root, ["--bundle", "1", "--emit-missing"]);
  rmSync(root, { recursive: true, force: true });
  assert.equal(result.status, 1, result.output);
  assert.match(result.output, /未対応 1/);
  assert.match(result.output, /\| 1 \| `src\/features\/toast\/toastStore\.test\.ts` \| 追加され消える \| 要判断 \| 要判断 \| — \|/);
});

test("宛先の無いタグは exit 2", () => {
  const root = createRepository();
  writeFileSync(join(root, csPath), tagLine("存在しない名前"));
  const result = runCli(root, ["--bundle", "1"]);
  rmSync(root, { recursive: true, force: true });
  assert.equal(result.status, 2, result.output);
  assert.match(result.output, /構造の誤り/);
});

test("Web を消した後は --web-rev で消す前の名前と突き合わせる", () => {
  const root = createRepository();
  unlinkSync(join(root, "moorestech_web/webui/src/features/toast/toastStore.test.ts"));
  const withoutRevision = runCli(root, ["--bundle", "1"]);
  const withRevision = runCli(root, ["--bundle", "1", "--web-rev", "HEAD"]);
  rmSync(root, { recursive: true, force: true });
  assert.equal(withoutRevision.status, 2, withoutRevision.output);
  assert.equal(withRevision.status, 0, withRevision.output);
});

test("束が 1〜5・all 以外なら exit 2", () => {
  const root = createRepository();
  const result = runCli(root, ["--bundle", "9"]);
  rmSync(root, { recursive: true, force: true });
  assert.equal(result.status, 2, result.output);
});

test("処遇が写す予定の行が残っていれば exit 1 で件数を出す", () => {
  const root = createRepository();
  const pendingText = dispositionText.replace("| 捨てる | 通信の契約は UITK に無い | — |", "| 写す予定 | moorestech-abcd | — |");
  writeFileSync(join(root, "docs/uitk/disposition.md"), pendingText);
  const result = runCli(root, ["--bundle", "1"]);
  rmSync(root, { recursive: true, force: true });
  assert.equal(result.status, 1, result.output);
  assert.match(result.output, /写す予定 1/);
});
