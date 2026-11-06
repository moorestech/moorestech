// 台帳 docs/uitk/TODO.md の「## 行」を読み、Web のパスがどの束で消えるかを決める（依存なし）
// Reads "## 行" of docs/uitk/TODO.md and decides in which bundle a Web path goes away (no dependencies)
import { readSectionTable } from "./markdownTable.mjs";

export const LEDGER_ROW_HEADER = ["行", "種別", "束", "所有者", "Web パス", "agent 既定", "実測の結論", "参照キャプチャ"];

// 束の集合と判定はここ 1 か所に置く（disposition・compareBundles・CLI が使う）
// The bundle set and its check live only here (used by disposition, compareBundles and the CLI)
export const BUNDLE_IDS = [1, 2, 3, 4, 5];

export function isBundleId(value) {
  return BUNDLE_IDS.includes(value);
}

export function parseLedgerRows(text) {
  const { rows, errors } = readSectionTable(text, "## 行", LEDGER_ROW_HEADER);
  const ledgerRows = [];
  for (const { cells, lineNumber } of rows) {
    const bundles = cells[2].split(/\s+/).filter((value) => value !== "").map(Number);
    if (bundles.length === 0 || !bundles.every(isBundleId)) {
      errors.push(`台帳 ${lineNumber} 行目: 束「${cells[2]}」が 1〜5 の空白区切りではない`);
      continue;
    }
    const webPaths = [...cells[4].matchAll(/`([^`]+)`/g)].map((match) => match[1]);
    ledgerRows.push({ name: cells[0], bundles, webPaths });
  }
  return { rows: ledgerRows, errors };
}

export function dueBundle(webPath, ledgerRows) {
  // G1 と同じく最長一致の Web パスを書いた行を持ち主にする（末尾 / はディレクトリの前方一致）
  // Like G1, rows listing the longest matching Web path own the file (a trailing slash is a directory prefix)
  let best = 0;
  let owners = [];
  for (const row of ledgerRows) {
    for (const pattern of row.webPaths) {
      const matches = pattern.endsWith("/") ? webPath.startsWith(pattern) : webPath === pattern;
      if (!matches) continue;
      if (pattern.length > best) {
        best = pattern.length;
        owners = [row];
      } else if (pattern.length === best && !owners.includes(row)) {
        owners.push(row);
      }
    }
  }

  // 持ち主の束の最大で消える（全部の束が切り替わるまで Web に残るため）
  // It goes away with the largest owning bundle (it stays on Web until every owning bundle switches)
  if (owners.length === 0) return null;
  return Math.max(...owners.flatMap((row) => row.bundles));
}
