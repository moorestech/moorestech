import { useMemo, useState } from 'react';
import { useStore } from '../lib/store';
import { asmColor, asmLayer } from '../lib/asm';
import { statusColor } from '../lib/status';
import { members } from '../lib/members';
import type { FileRec } from '../lib/types';
import { Chevron, ArrowDown, ArrowUp } from '../components/icons';

const NODE_W = 344;       // カード幅は一定。深さはleftインデントのみ(名前を折り返さず収める)
const INDENT = 15;
const COL_W = NODE_W + 2 * INDENT + 22;
const HEAD_H = 33;        // 0.75x(旧44)
const GAP = 7;
const DIR_H = 22;
const SUBGAP = 6;
const PAD_TOP = 40;
const BODY_MAX = 384;
const PAD_BOTTOM = BODY_MAX + 24;  // 最大展開分を常時確保→縦スクロールバーが変動しない
const PAD_RIGHT = 48;              // 横にも余白を確保

const fileDir = (p: string, strip: string) => (strip ? p.replace(strip, '') : p).replace(/\/[^/]+$/, '');

interface DirNode { name: string; dirs: Map<string, DirNode>; files: FileRec[]; }
const newDir = (name: string): DirNode => ({ name, dirs: new Map(), files: [] });

type Item =
  | { type: 'dir'; name: string; depth: number; x: number; y: number; colX: number }
  | { type: 'file'; file: FileRec; depth: number; x: number; y: number; w: number; colX: number };

// 列(asmdef)内のファイルを共通プレフィックスを除いたディレクトリ木に積む。
function buildColTree(files: FileRec[], strip: string): DirNode {
  const dirsSeg = files.map((f) => fileDir(f.path, strip).split('/'));
  let common = dirsSeg[0] ? [...dirsSeg[0]] : [];
  for (const segs of dirsSeg) {
    let i = 0;
    while (i < common.length && i < segs.length && common[i] === segs[i]) i++;
    common = common.slice(0, i);
  }
  const root = newDir('');
  files.forEach((f, idx) => {
    const rest = dirsSeg[idx].slice(common.length).filter(Boolean);
    let cur = root;
    for (const seg of rest) {
      if (!cur.dirs.has(seg)) cur.dirs.set(seg, newDir(seg));
      cur = cur.dirs.get(seg)!;
    }
    cur.files.push(f);
  });
  return root;
}

export function DepMap() {
  const { data, selected, selectFile, jumpToFile, expanded, toggleFold } = useStore();
  const [hover, setHover] = useState<string | null>(null);

  const layout = useMemo(() => {
    const byAsm = new Map<string, FileRec[]>();
    for (const f of data.files) {
      if (!byAsm.has(f.asmdef)) byAsm.set(f.asmdef, []);
      byAsm.get(f.asmdef)!.push(f);
    }
    const cols = [...byAsm.entries()].sort((a, b) => asmLayer(a[0]) - asmLayer(b[0]));
    const items: Item[] = [];
    const pos = new Map<string, { x: number; y: number; w: number; colX: number }>();
    const colHeads: { asm: string; x: number }[] = [];
    let maxH = 0;

    cols.forEach(([asm, files], ci) => {
      const colX = ci * COL_W + 20;
      colHeads.push({ asm, x: colX });
      let y = PAD_TOP;
      const tree = buildColTree(files, data.sourceRoot);
      // ファイル先 → サブディレクトリの順で DFS。
      const walk = (node: DirNode, depth: number) => {
        for (const f of [...node.files].sort((a, b) => a.name.localeCompare(b.name))) {
          const x = colX + depth * INDENT;
          const w = NODE_W;
          items.push({ type: 'file', file: f, depth, x, y, w, colX });
          pos.set(f.path, { x, y, w, colX });
          y += HEAD_H + GAP;
        }
        for (const [name, child] of [...node.dirs.entries()].sort((a, b) => a[0].localeCompare(b[0]))) {
          items.push({ type: 'dir', name, depth, x: colX + depth * INDENT, y, colX });
          y += DIR_H;
          walk(child, depth + 1);
          y += SUBGAP;
        }
      };
      walk(tree, 0);
      maxH = Math.max(maxH, y);
    });
    return { items, pos, colHeads, width: cols.length * COL_W + 20, height: maxH + 24 };
  }, [data]);

  const edges = useMemo(() => {
    const e: { from: string; to: string }[] = [];
    for (const f of data.files) for (const d of f.depsOut) e.push({ from: f.path, to: d });
    return e;
  }, [data]);

  const active = hover;
  const activeFile = active ? data.files.find((x) => x.path === active) : null;
  const litOut = new Set(activeFile?.depsOut ?? []);
  const litIn = new Set(activeFile?.depsIn ?? []);

  // 展開で増える高さ。下のカードと SVG はこの分だけずらす。
  const activeExtra = useMemo(() => {
    if (!activeFile) return 0;
    const n = members(activeFile).length;
    return Math.min(BODY_MAX, 32 + n * 18 + 14);
  }, [activeFile]);
  const activePos = active ? layout.pos.get(active) : undefined;

  // 同じ列でホバーカードより下にあるものを activeExtra 分だけ押し下げる。
  function shift(colX: number, y: number): number {
    if (!activePos) return 0;
    return colX === activePos.colX && y > activePos.y ? activeExtra : 0;
  }
  const effY = (p: string) => {
    const q = layout.pos.get(p);
    if (!q) return 0;
    return q.y + shift(q.colX, q.y) + HEAD_H / 2;
  };

  const hotEdges = active ? edges.filter((e) => e.from === active || e.to === active) : [];

  function edgeGeo(from: string, to: string) {
    const pa = layout.pos.get(from), pb = layout.pos.get(to);
    if (!pa || !pb) return null;
    const x1 = pa.x + pa.w, y1 = effY(from);
    const x2 = pb.x, y2 = effY(to);
    const mx = (x1 + x2) / 2, my = (y1 + y2) / 2;
    const ang = Math.atan2(1.5 * (y2 - y1), 0.75 * (x2 - x1)) * 180 / Math.PI;
    return { d: `M${x1},${y1} C${mx},${y1} ${mx},${y2} ${x2},${y2}`, mx, my, ang };
  }

  function open(p: string, key?: string) {
    if (key && !expanded.has(key)) toggleFold(key);
    jumpToFile(p);
  }

  return (
    <div className="depmap">
      <div className="depmap-legend">
        <span>列 = asmdef（左ほど基盤）／列内 = ディレクトリ階層</span>
        <span className="lg"><i className="sw" style={{ background: statusColor('A') }} />A 追加</span>
        <span className="lg"><i className="sw" style={{ background: statusColor('M') }} />M 変更</span>
        <span className="ehint out">▸ 依存先</span>
        <span className="ehint in">▸ 依存元</span>
        <span className="muted">ホバー → 矢印付き1ホップ＋メソッド一覧が展開（下のカードは押し下げ）</span>
      </div>
      <div className="depmap-scroll">
        {/* 展開で伸びてもスクロールバーが変動しないよう、最大展開分(縦)＋右に常時パディングを確保 */}
        <div className="depmap-canvas" style={{ width: layout.width + PAD_RIGHT, height: layout.height + PAD_BOTTOM }}>
          <svg className="edges" width={layout.width + PAD_RIGHT} height={layout.height + PAD_BOTTOM}>
            {hotEdges.map((e, i) => {
              const g = edgeGeo(e.from, e.to);
              if (!g) return null;
              const cls = e.from === active ? 'out' : 'in';
              return (
                <g key={i}>
                  <path className={`e-${cls}`} d={g.d} />
                  <path className={`arrow-${cls}`} d="M-5,-4 L6,0 L-5,4 Z" transform={`translate(${g.mx},${g.my}) rotate(${g.ang})`} />
                </g>
              );
            })}
          </svg>

          {layout.colHeads.map((c) => (
            <div key={c.asm} className="col-head" style={{ left: c.x, top: 12, width: NODE_W }}>
              <span className="asmdot" style={{ background: asmColor(c.asm) }} />{c.asm}
            </div>
          ))}

          {layout.items.map((it, idx) => {
            if (it.type === 'dir') {
              return (
                <div key={'d' + idx} className="dirhead" style={{ left: it.x, top: it.y + shift(it.colX, it.y), width: NODE_W }}>
                  <span className="dirtick">▸</span>{it.name}/
                </div>
              );
            }
            const f = it.file;
            const isActive = f.path === active;
            const litO = litOut.has(f.path);  // active が import する先 → 青エッジ → 青ハイライト
            const litI = litIn.has(f.path);   // active を使う側 → 橙エッジ → 橙ハイライト
            const dim = !!active && !isActive && !litO && !litI;
            const mem = members(f);
            return (
              <div
                key={f.path}
                className={`node${isActive ? ' active' : ''}${litO ? ' lit-out' : litI ? ' lit-in' : ''}${dim ? ' dim' : ''}${f.path === selected ? ' selected' : ''}`}
                style={{ left: it.x, top: it.y + shift(it.colX, it.y), width: it.w, borderLeftColor: statusColor(f.status) }}
                onMouseEnter={() => setHover(f.path)}
                onMouseLeave={() => setHover((h) => (h === f.path ? null : h))}
                onClick={() => selectFile(f.path)}
                onDoubleClick={() => jumpToFile(f.path)}
              >
                <div className="node-head" style={{ minHeight: HEAD_H, height: HEAD_H }}>
                  <span className="node-name">{f.name}</span>
                  <span className="node-num"><span className="add">+{f.add}</span>{f.del > 0 && <span className="del">−{f.del}</span>}</span>
                </div>
                <div className={`node-body${isActive ? ' open' : ''}`} style={isActive ? { maxHeight: activeExtra } : undefined}>
                  <div className="node-deps">
                    <span><ArrowDown />{f.depsOut.length}</span><span><ArrowUp />{f.depsIn.length}</span>
                    <button className="node-open" onClick={(e) => { e.stopPropagation(); jumpToFile(f.path); }}>open ↗</button>
                  </div>
                  <div className="node-members">
                    {mem.map((m, i) => (
                      <div key={i} className={`nm ${m.kind}`} onClick={(e) => { e.stopPropagation(); open(f.path, m.foldKey); }} title={m.label}>
                        {m.kind === 'method' && <span className="nmchev"><Chevron /></span>}
                        <span className="nmlabel">{m.label}</span>
                      </div>
                    ))}
                    {mem.length === 0 && <div className="nm muted"><span className="nmlabel">（メンバーなし）</span></div>}
                  </div>
                </div>
              </div>
            );
          })}
        </div>
      </div>
    </div>
  );
}
