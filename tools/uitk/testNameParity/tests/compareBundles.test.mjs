// compareBundles.mjs の自己テスト: 束ごとの分類と矛盾の検出
// Self-test for compareBundles.mjs: per-bundle classification and contradiction detection
import { test } from "node:test";
import assert from "node:assert/strict";
import { compareTests } from "../compareBundles.mjs";

const rows = [
  { name: "toast", bundles: [1], webPaths: ["src/features/toast/"] },
  { name: "検証基盤", bundles: [5], webPaths: ["e2e/"] },
];
const webTests = [
  { file: "src/features/toast/toastStore.test.ts", title: "追加され消える", line: 10 },
  { file: "src/features/toast/toastStore.test.ts", title: "variant を保持する", line: 17 },
  { file: "src/features/toast/toastWire.test.ts", title: "WS へ送る", line: 3 },
  { file: "src/features/toast/toastWire.test.ts", title: "WS から受ける", line: 9 },
  { file: "src/features/toast/toastLayout.test.ts", title: "右下に寄る", line: 4 },
  { file: "e2e/tests/x.spec.ts", title: "束5", line: 1 },
];
const tags = [{ file: "src/features/toast/toastStore.test.ts", title: "追加され消える", line: 20, csFile: "A.cs" }];
const dispositions = [
  { bundle: 1, file: "src/features/toast/toastWire.test.ts", title: "*", verdict: "捨てる", evidence: "通信", lineNumber: 7 },
  { bundle: 1, file: "src/features/toast/toastLayout.test.ts", title: "右下に寄る", verdict: "写す予定", evidence: "moorestech-abcd", lineNumber: 8 },
];

test("束ごとに 写した・処遇・写す予定・未対応 を数える", () => {
  const { bundles, errors } = compareTests(webTests, tags, dispositions, rows);
  assert.deepEqual(errors, []);
  const first = bundles.get(1);
  assert.equal(first.total, 5);
  assert.equal(first.copied, 1);
  assert.equal(first.disposed, 2);
  assert.deepEqual(first.pending.map((pending) => pending.test.title), ["右下に寄る"]);
  assert.deepEqual(first.missing.map((missing) => missing.title), ["variant を保持する"]);
  assert.equal(bundles.get(5).missing.length, 1);
  assert.equal(bundles.get(2).total, 0);
});

test("宛先の無いタグ・束の違う処遇・写したのに処遇もある行は誤り", () => {
  const { errors } = compareTests(
    webTests,
    [...tags, { file: "src/features/toast/toastStore.test.ts", title: "存在しない", line: 3, csFile: "B.cs" }],
    [
      { bundle: 5, file: "src/features/toast/toastStore.test.ts", title: "variant を保持する", verdict: "捨てる", evidence: "x", lineNumber: 9 },
      { bundle: 1, file: "src/features/toast/toastStore.test.ts", title: "追加され消える", verdict: "捨てる", evidence: "x", lineNumber: 10 },
    ],
    rows,
  );
  assert.equal(errors.length, 3);
});

test("同じファイルの同じ名前は 1 件に数え、台帳に無いファイルは 1 回だけ誤りにする", () => {
  const duplicated = [...webTests, { file: "src/features/toast/toastStore.test.ts", title: "追加され消える", line: 30 }];
  assert.equal(compareTests(duplicated, tags, dispositions, rows).bundles.get(1).total, 5);
  const unowned = [{ file: "src/features/other/a.test.ts", title: "a", line: 1 }, { file: "src/features/other/a.test.ts", title: "b", line: 2 }];
  assert.equal(compareTests(unowned, [], [], rows).errors.length, 1);
});
