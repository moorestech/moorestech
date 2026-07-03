import { useMemo } from 'react';
import type { FileRec } from '../lib/types';
import { useStore } from '../lib/store';
import { asmColor } from '../lib/asm';
import { Check } from './icons';

interface DirNode { name: string; dirs: Map<string, DirNode>; files: FileRec[]; }
const newDir = (name: string): DirNode => ({ name, dirs: new Map(), files: [] });

function buildTree(files: FileRec[], strip: string): DirNode {
  const root = newDir('');
  for (const f of files) {
    const rel = strip ? f.path.replace(strip, '') : f.path;
    const segs = rel.split('/');
    const dirs = segs.slice(0, -1);
    let cur = root;
    for (const seg of dirs) {
      if (!cur.dirs.has(seg)) cur.dirs.set(seg, newDir(seg));
      cur = cur.dirs.get(seg)!;
    }
    cur.files.push(f);
  }
  return root;
}

function DirView({ node, depth }: { node: DirNode; depth: number }) {
  const { selected, selectFile, reviewed } = useStore();
  return (
    <>
      {[...node.dirs.entries()].sort((a, b) => a[0].localeCompare(b[0])).map(([name, child]) => (
        <div key={name}>
          <div className="tree-dir" style={{ paddingLeft: 8 + depth * 12 }}>{name}/</div>
          <DirView node={child} depth={depth + 1} />
        </div>
      ))}
      {[...node.files].sort((a, b) => a.name.localeCompare(b.name)).map((f) => (
        <div
          key={f.path}
          className={`tree-file${f.path === selected ? ' selected' : ''}`}
          style={{ paddingLeft: 8 + depth * 12 }}
          onClick={() => selectFile(f.path)}
          title={f.path}
        >
          <span className="tf-status" data-s={f.status}>{f.status}</span>
          <span className="tf-dot" style={{ background: asmColor(f.asmdef) }} />
          <span className="tf-name">{f.name}</span>
          <span className="tf-num"><span className="add">+{f.add}</span>{f.del > 0 && <span className="del">−{f.del}</span>}</span>
          {reviewed.has(f.path) && <span className="tf-reviewed"><Check /></span>}
        </div>
      ))}
    </>
  );
}

// 変更ファイルの階層ツリー(req1)。検索、reviewed 状況を含む。
export function FileTree() {
  const { data, search, setSearch } = useStore();

  const filtered = useMemo(() => {
    const q = search.trim().toLowerCase();
    if (!q) return data.files;
    return data.files.filter((f) => f.path.toLowerCase().includes(q));
  }, [data, search]);

  const tree = useMemo(() => buildTree(filtered, data.sourceRoot), [filtered, data.sourceRoot]);

  return (
    <div className="filetree">
      <div className="filetree-head">
        <input
          className="tree-search"
          placeholder="ファイルを検索…"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
        />
        <span className="tree-count">{filtered.length}/{data.files.length}</span>
      </div>
      <div className="filetree-body">
        {filtered.length === 0 ? (
          <div className="tree-empty">一致するファイルがありません</div>
        ) : (
          <DirView node={tree} depth={0} />
        )}
      </div>
    </div>
  );
}
