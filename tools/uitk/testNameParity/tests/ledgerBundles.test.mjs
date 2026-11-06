// ledgerBundles.mjs の自己テスト: 台帳の行の読み取りとパス→束
// Self-test for ledgerBundles.mjs: reading ledger rows and mapping paths to bundles
import { test } from "node:test";
import assert from "node:assert/strict";
import { dueBundle, parseLedgerRows } from "../ledgerBundles.mjs";

const ledger = [
  "## 束",
  "",
  "## 行",
  "",
  "| 行 | 種別 | 束 | 所有者 | Web パス | agent 既定 | 実測の結論 | 参照キャプチャ |",
  "|---|---|---|---|---|---|---|---|",
  "| toast | 機能 | 1 | Web | `src/features/toast/` `e2e/tests/regression/actionErrors.spec.ts` | — | — | 1-toast |",
  "| shared/ui | 共通部品 | 1 2 3 4 5 | Web | `src/shared/ui/` | — | — | — |",
  "| 検証基盤 | 横断 | 5 | Web | `e2e/` | — | — | — |",
  "",
  "## 追従記録",
  "",
].join("\n");

test("最長一致の行の束を返し、複数の束を持つ行は最大の束を返す", () => {
  const { rows, errors } = parseLedgerRows(ledger);
  assert.deepEqual(errors, []);
  assert.equal(dueBundle("src/features/toast/toastStore.test.ts", rows), 1);
  assert.equal(dueBundle("e2e/tests/regression/actionErrors.spec.ts", rows), 1);
  assert.equal(dueBundle("e2e/tests/system/other.spec.ts", rows), 5);
  assert.equal(dueBundle("src/shared/ui/Button.test.tsx", rows), 5);
  assert.equal(dueBundle("src/features/unknown/x.test.ts", rows), null);
});

test("束の列が 1〜5 の空白区切りでない行は誤りとして返す", () => {
  const broken = ledger.replace("| toast | 機能 | 1 |", "| toast | 機能 | 一 |");
  const { rows, errors } = parseLedgerRows(broken);
  assert.equal(rows.length, 2);
  assert.equal(errors.length, 1);
});

function withRow(row) {
  return ledger.replace("| 検証基盤 | 横断 | 5 | Web | `e2e/` | — | — | — |", row);
}

test("G1 の _row が拒否する行を同じ条件で誤りにする", () => {
  // 束の指数表記・全角数字、引用外の Web パス、絶対/空/..・バックスラッシュのパス、パス無しは G1 でも誤り
  // Exponent or full-width bundles, unquoted Web paths, absolute/empty/.. or backslash paths and no path are G1 errors too
  const invalidRows = [
    "| x | 横断 | 1e0 | Web | `e2e/` | — | — | — |",
    "| x | 横断 | ５ | Web | `e2e/` | — | — | — |",
    "| x | 横断 | 5 | Web | e2e/ | — | — | — |",
    "| x | 横断 | 5 | Web | `e2e/` と補足 | — | — | — |",
    "| x | 横断 | 5 | Web | `/e2e/` | — | — | — |",
    "| x | 横断 | 5 | Web | `e2e//a` | — | — | — |",
    "| x | 横断 | 5 | Web | `../e2e/` | — | — | — |",
    "| x | 横断 | 5 | Web | `e2e\\a` | — | — | — |",
    "| x | 横断 | 5 | Web | — | — | — | — |",
  ];
  for (const row of invalidRows) {
    const { rows, errors } = parseLedgerRows(withRow(row));
    assert.equal(errors.length, 1, row);
    assert.equal(rows.length, 2, row);
  }
});

test("同じ長さの最長一致が別の行にまたがるときは持ち主の束の最大を返す（G1 の ownership.py と同じ）", () => {
  const tied = withRow("| 同点 | 横断 | 3 | Web | `src/features/toast/` | — | — | — |");
  const { rows, errors } = parseLedgerRows(tied);
  assert.deepEqual(errors, []);
  assert.equal(dueBundle("src/features/toast/a.test.ts", rows), 3);
});
