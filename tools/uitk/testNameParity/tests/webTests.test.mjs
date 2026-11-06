// webTests.mjs の自己テスト: vitest・Playwright の名前の抽出とファイルの判定
// Self-test for webTests.mjs: name extraction for vitest / Playwright and test-file detection
import { test } from "node:test";
import assert from "node:assert/strict";
import { extractWebTestTitles, isWebTestFile, normalizeTitle } from "../webTests.mjs";

test("vitest と Playwright の it/test の名前を行番号付きで拾う", () => {
  const text = [
    'describe("束", () => {',
    '  it("emitToast で追加され 3秒後に消える", () => {});',
    "  test('単引用符の\\'名前\\'', async () => {});",
    "  test(`block_inventory.splitの${error}はtoastを出さない`, async ({ page }) => {});",
    "});",
  ].join("\n");
  assert.deepEqual(extractWebTestTitles(text), [
    { title: "emitToast で追加され 3秒後に消える", line: 2 },
    { title: "単引用符の'名前'", line: 3 },
    { title: "block_inventory.splitの${error}はtoastを出さない", line: 4 },
  ]);
});

test("only・skip・each 付きも拾い、describe・step・beforeEach と識別子の途中は拾わない", () => {
  const text = [
    'test.describe("画面", () => {',
    "  test.beforeEach(async () => {});",
    '  it.skip("飛ばしたテスト", () => {});',
    '  it.each([[1, 2], [3, (4)]])("表 %s", (a, b) => {});',
    '  test.only("だけ", async () => { await test.step("段", async () => {}); });',
    '  submit("it(ではない", () => {});',
    "});",
  ].join("\n");
  assert.deepEqual(extractWebTestTitles(text).map((found) => found.title), ["飛ばしたテスト", "表 %s", "だけ"]);
});

test("型引数付きの it.each<…>(…) も拾う", () => {
  const text = 'it.each<[A["b"], C, string]>([[1, 2, "x"]])("名前%s", () => {});';
  assert.deepEqual(extractWebTestTitles(text).map((found) => found.title), ["名前%s"]);
});

test("複数行のテンプレート文字列の名前は空白を 1 つに畳む", () => {
  const text = "test(`長い\n    名前`, async () => {});";
  assert.deepEqual(extractWebTestTitles(text), [{ title: "長い 名前", line: 1 }]);
});

test("src と e2e の test/spec ファイルだけを Web テストとみなす", () => {
  assert.equal(isWebTestFile("src/features/toast/toastStore.test.ts"), true);
  assert.equal(isWebTestFile("src/shared/ui/GaugeBar/index.test.tsx"), true);
  assert.equal(isWebTestFile("e2e/tests/system/pauseMenu.spec.ts"), true);
  assert.equal(isWebTestFile("e2e/mock-host/detailActions.test.ts"), true);
  assert.equal(isWebTestFile("src/features/toast/toastStore.ts"), false);
  assert.equal(isWebTestFile("scripts/check.test.mjs"), false);
});

test("名前の空白は 1 つに畳み前後を落とす", () => {
  assert.equal(normalizeTitle("  a \n  b  "), "a b");
});
