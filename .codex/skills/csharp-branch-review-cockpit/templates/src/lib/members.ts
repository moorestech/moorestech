import type { FileRec } from './types';
import { methodKey, regionKey } from './rows';

export interface Member {
  kind: 'type' | 'method' | 'region';
  label: string;
  line: number;
  foldKey?: string;
}

// ファイルの宣言型・メソッド・#region を行順に並べる(Dep-Map のホバー展開で使用)。
export function members(file: FileRec): Member[] {
  const list: Member[] = [];
  for (const t of file.parsed.declTypes) {
    list.push({ kind: 'type', label: `${t.kind} ${t.name}`, line: t.line });
  }
  for (const fold of file.parsed.folds) {
    list.push({ kind: 'method', label: fold.name, line: fold.sigStart, foldKey: methodKey(file, fold) });
  }
  for (const region of file.parsed.regions) {
    list.push({ kind: 'region', label: region.name, line: region.start, foldKey: regionKey(file, region) });
  }
  return list.sort((a, b) => a.line - b.line);
}
