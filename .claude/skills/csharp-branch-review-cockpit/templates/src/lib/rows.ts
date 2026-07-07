import type { FileRec, FoldRange, RegionRange } from './types';

export type Row =
  | { kind: 'code'; n: number; added: boolean }
  | { kind: 'sig' | 'region'; n: number; added: boolean; foldKey: string; collapsed: boolean; badge?: number; strip: boolean };

export const methodKey = (file: FileRec, fold: FoldRange): string => `${file.path}#m${fold.start}`;
export const regionKey = (file: FileRec, region: RegionRange): string => `${file.path}#r${region.start}`;

const isBraceOnlyLine = (line: string | undefined): boolean => (line ?? '').trim() === '{';

interface Marker {
  hideFrom: number;
  hideTo: number;
  key: string;
  kind: 'sig' | 'region';
  strip: boolean; // K&R: 同じ行の末尾 '{' を折りたたみ時に隠す
  count: number;  // 折りたたみ時のバッジ行数
}

// 折りたたみ対象行(collapsed 時にバッジを出す行) → Marker のマップを構築する。
// Build a line-number -> Marker map for fold/region badge lines (shown when collapsed).
function buildMarkers(file: FileRec): Map<number, Marker> {
  const lines = file.text.split('\n');
  const markers = new Map<number, Marker>();

  for (const fold of file.parsed.folds) {
    const allman = isBraceOnlyLine(lines[fold.start - 1]);
    const badgeLine = allman ? fold.start - 1 : fold.start;
    markers.set(badgeLine, {
      hideFrom: allman ? fold.start : fold.start + 1,
      hideTo: fold.end,
      key: methodKey(file, fold),
      kind: 'sig',
      strip: !allman,
      count: fold.end - fold.start,
    });
  }
  for (const region of file.parsed.regions) {
    markers.set(region.start, {
      hideFrom: region.start + 1,
      hideTo: region.end,
      key: regionKey(file, region),
      kind: 'region',
      strip: false,
      count: region.end - region.start,
    });
  }
  return markers;
}

// 折りたたみ状態(expanded)に応じて表示すべき行だけを組み立てる。折りたたみ中の本体行はそもそも出力しない。
// Build only the visible rows for the current fold state; collapsed body lines are omitted entirely.
export function buildRows(file: FileRec, expanded: Set<string>): Row[] {
  const markers = buildMarkers(file);
  const addedSet = new Set(file.addedLines);
  const rows: Row[] = [];

  let i = 1;
  const total = file.parsed.lineCount;
  while (i <= total) {
    const marker = markers.get(i);
    if (marker) {
      const isExpanded = expanded.has(marker.key);
      rows.push({
        kind: marker.kind,
        n: i,
        added: addedSet.has(i),
        foldKey: marker.key,
        collapsed: !isExpanded,
        badge: isExpanded ? undefined : marker.count,
        strip: marker.strip && !isExpanded,
      });
      i = isExpanded ? i + 1 : marker.hideTo + 1;
      continue;
    }
    rows.push({ kind: 'code', n: i, added: addedSet.has(i) });
    i++;
  }
  return rows;
}
