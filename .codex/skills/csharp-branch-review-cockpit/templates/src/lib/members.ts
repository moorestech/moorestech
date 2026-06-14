import type { FileRec } from './types';
import { methodKey } from './rows';

export type Member = { line: number; kind: 'method' | 'region' | 'type'; label: string; foldKey?: string };

export function tidySig(sig: string) {
  return sig.replace(/\s+/g, ' ').replace(/\s*\{\s*$/, '').trim();
}

// ファイルの宣言型・メソッドシグネチャ・#region を行順に並べたメンバ一覧。
export function members(file: FileRec): Member[] {
  const out: Member[] = [];
  for (const t of file.parsed.declTypes) out.push({ line: t.line, kind: 'type', label: `${t.kind} ${t.name}` });
  for (const f of file.parsed.folds) out.push({ line: f.sigStart, kind: 'method', label: tidySig(f.name), foldKey: methodKey(file.path, f.sigStart) });
  for (const r of file.parsed.regions) out.push({ line: r.start, kind: 'region', label: `#region ${r.name}` });
  return out.sort((a, b) => a.line - b.line);
}
