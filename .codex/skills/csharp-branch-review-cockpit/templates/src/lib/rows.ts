import type { FileRec } from './types';

export type Row =
  | { kind: 'code'; n: number; text: string; added: boolean; foldKey?: string; badge?: number; strip?: boolean }
  | { kind: 'sig'; n: number; text: string; added: boolean; foldKey: string; collapsed: boolean; badge?: number; strip?: boolean }
  | { kind: 'region'; n: number; text: string; added: boolean; foldKey: string; collapsed: boolean; badge?: number };

export function methodKey(path: string, sigStart: number) { return `${path}#m${sigStart}`; }
export function regionKey(path: string, start: number) { return `${path}#r${start}`; }

// ファイルを折りたたみ状態に応じた表示行へ変換する。
// 折りたたみ時は signature 行に { Nline } バッジをインライン付与し、{ 本体・} は隠す。
export function buildRows(file: FileRec, expanded: Set<string>): Row[] {
  const lines = file.text.split('\n');
  const added = new Set(file.addedLines);
  const methodByStart = new Map<number, (typeof file.parsed.folds)[number]>();
  for (const f of file.parsed.folds) methodByStart.set(f.sigStart, f);
  const regionByStart = new Map<number, (typeof file.parsed.regions)[number]>();
  for (const r of file.parsed.regions) regionByStart.set(r.start, r);

  const rows: Row[] = [];
  let i = 1;
  const N = lines.length;
  while (i <= N) {
    const region = regionByStart.get(i);
    const method = methodByStart.get(i);

    if (region) {
      const key = regionKey(file.path, region.start);
      const collapsed = !expanded.has(key);
      rows.push({ kind: 'region', n: i, text: lines[i - 1], added: added.has(i), foldKey: key, collapsed, badge: collapsed ? region.end - region.start : undefined });
      i = collapsed ? region.end + 1 : region.start + 1;
      continue;
    }

    if (method) {
      const key = methodKey(file.path, method.sigStart);
      const collapsed = !expanded.has(key);
      if (!collapsed) {
        rows.push({ kind: 'sig', n: i, text: lines[i - 1], added: added.has(i), foldKey: key, collapsed: false });
        for (let k = method.sigStart + 1; k <= method.start; k++) rows.push({ kind: 'code', n: k, text: lines[k - 1], added: added.has(k) });
        i = method.start + 1;
      } else {
        // 開き { が独立行(Allman)なら最後の signature 行はその手前。K&R なら { 行から末尾の { を除去。
        const braceOwn = lines[method.start - 1].trim() === '{';
        const lastSig = braceOwn ? method.start - 1 : method.start;
        const count = Math.max(0, method.end - method.start - 1);
        for (let k = method.sigStart; k <= lastSig; k++) {
          const isLast = k === lastSig;
          const badge = isLast ? count : undefined;
          const strip = isLast && !braceOwn;
          if (k === method.sigStart) rows.push({ kind: 'sig', n: k, text: lines[k - 1], added: added.has(k), foldKey: key, collapsed: true, badge, strip });
          else rows.push({ kind: 'code', n: k, text: lines[k - 1], added: added.has(k), foldKey: key, badge, strip });
        }
        i = method.end + 1;
      }
      continue;
    }

    rows.push({ kind: 'code', n: i, text: lines[i - 1], added: added.has(i) });
    i++;
  }
  return rows;
}
