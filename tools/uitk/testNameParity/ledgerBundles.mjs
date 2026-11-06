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

const EMPTY_CELLS = ["", "—"];
const CODE_SPAN = /`([^`]+)`/g;

function parseBundleCell(cell) {
  // G1（ledger.py の _row）と同じく ASCII の数字だけを束番号にする（Number("1e0") のような読み替えをしない）
  // Like G1 (_row in ledger.py), only ASCII digits count as a bundle id (no Number("1e0") coercion)
  const tokens = cell.split(/\s+/).filter((value) => value !== "");
  if (tokens.length === 0 || !tokens.every((value) => /^[0-9]+$/.test(value) && isBundleId(Number(value)))) return null;
  return tokens.map(Number);
}

function parseWebPathCell(cell) {
  // G1 と同じ規則: 引用外の文字・絶対パス・バックスラッシュ・空/./.. の部品・パス無しは誤り。誤りの理由を返す
  // Same rules as G1: text outside quotes, absolute paths, backslashes, empty/./.. parts and no path are errors; returns the reason
  if (!EMPTY_CELLS.includes(cell) && cell.replace(CODE_SPAN, "").trim() !== "") return { reason: "Web パスに引用されていない文字列がある" };
  const paths = [...cell.matchAll(CODE_SPAN)].map((match) => match[1]);
  const isBadPath = (path) => path.startsWith("/") || path.includes("\\") || path.replace(/\/+$/, "").split("/").some((part) => ["", ".", ".."].includes(part));
  if (paths.some(isBadPath)) return { reason: "Web パスが正規の相対パスではない" };
  if (paths.length === 0) return { reason: "Web パスが無い" };
  return { paths };
}

export function parseLedgerRows(text) {
  const { rows, errors } = readSectionTable(text, "## 行", LEDGER_ROW_HEADER);
  const ledgerRows = [];
  for (const { cells, lineNumber } of rows) {
    const bundles = parseBundleCell(cells[2]);
    if (bundles === null) {
      errors.push(`台帳 ${lineNumber} 行目: 束「${cells[2]}」が 1〜5 の空白区切りではない`);
      continue;
    }
    const web = parseWebPathCell(cells[4]);
    if (web.reason !== undefined) {
      errors.push(`台帳 ${lineNumber} 行目: 行「${cells[0]}」の${web.reason}: ${cells[4]}`);
      continue;
    }
    ledgerRows.push({ name: cells[0], bundles, webPaths: web.paths });
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
