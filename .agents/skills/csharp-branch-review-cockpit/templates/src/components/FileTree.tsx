import { useMemo, useState } from 'react';
import type { FileRec } from '../lib/types';
import { useStore } from '../lib/store';
import { asmColor } from '../lib/asm';
import { Chevron, FileAdd, FilePen, Folder, Check } from './icons';

interface Node {
  name: string;
  path: string; // display path (prefix stripped) for dirs; full for files via file.path
  isDir: boolean;
  children: Node[];
  file?: FileRec;
}

function buildTree(files: FileRec[], prefix: string): Node {
  const root: Node = { name: '', path: '', isDir: true, children: [] };
  for (const f of files) {
    const disp = prefix ? f.path.replace(prefix, '') : f.path;
    const parts = disp.split('/');
    let cur = root;
    let acc = '';
    for (let i = 0; i < parts.length; i++) {
      const part = parts[i];
      const last = i === parts.length - 1;
      acc = acc ? `${acc}/${part}` : part;
      if (last) {
        cur.children.push({ name: part, path: f.path, isDir: false, children: [], file: f });
      } else {
        let child = cur.children.find((c) => c.isDir && c.name === part);
        if (!child) { child = { name: part, path: acc, isDir: true, children: [] }; cur.children.push(child); }
        cur = child;
      }
    }
  }
  // ディレクトリ→ファイルの順、名前順
  const sort = (n: Node) => {
    n.children.sort((a, b) => (a.isDir === b.isDir ? a.name.localeCompare(b.name) : a.isDir ? -1 : 1));
    n.children.forEach(sort);
  };
  sort(root);
  return root;
}

// 単一の子ディレクトリだけを持つ連鎖は1行に畳む(a/b/c)。
function collapseChain(node: Node): Node {
  const kids = node.children.map(collapseChain);
  if (node.isDir && kids.length === 1 && kids[0].isDir) {
    return { ...kids[0], name: `${node.name}/${kids[0].name}`, children: kids[0].children };
  }
  return { ...node, children: kids };
}

function fileStat(node: Node): { add: number; del: number; n: number } {
  if (node.file) return { add: node.file.add, del: node.file.del, n: 1 };
  return node.children.reduce((acc, c) => {
    const s = fileStat(c);
    return { add: acc.add + s.add, del: acc.del + s.del, n: acc.n + s.n };
  }, { add: 0, del: 0, n: 0 });
}

export function FileTree() {
  const { data, selected, selectFile, reviewed, search, setSearch } = useStore();
  const [collapsed, setCollapsed] = useState<Set<string>>(new Set());

  const tree = useMemo(() => collapseChain(buildTree(data.files, data.sourceRoot)), [data]);
  const q = search.trim().toLowerCase();

  const matches = (n: Node): boolean => {
    if (n.file) return !q || n.file.path.toLowerCase().includes(q);
    return n.children.some(matches);
  };

  function renderNode(node: Node, depth: number): React.ReactNode {
    if (node.isDir) {
      if (q && !matches(node)) return null;
      const open = q ? true : !collapsed.has(node.path);
      const st = fileStat(node);
      return (
        <div key={'d:' + node.path}>
          <div className="trow dir" style={{ paddingLeft: depth * 13 + 8 }}
            onClick={() => setCollapsed((p) => { const n = new Set(p); if (n.has(node.path)) n.delete(node.path); else n.add(node.path); return n; })}>
            <span className="chev"><Chevron open={open} /></span>
            <span className="ficon dir"><Folder open={open} /></span>
            <span className="tname">{node.name}</span>
            <span className="tcount">{st.n}</span>
          </div>
          {open && node.children.map((c) => renderNode(c, depth + 1))}
        </div>
      );
    }
    const f = node.file!;
    if (q && !f.path.toLowerCase().includes(q)) return null;
    const isSel = selected === f.path;
    const isRev = reviewed.has(f.path);
    return (
      <div key={'f:' + f.path}
        className={`trow file${isSel ? ' sel' : ''}${isRev ? ' rev' : ''}`}
        style={{ paddingLeft: depth * 13 + 8 }}
        onClick={() => selectFile(f.path)}>
        <span className="ficon" data-s={f.status} style={{ color: f.status === 'A' ? '#16A34A' : '#D97706' }}>
          {f.status === 'A' ? <FileAdd /> : <FilePen />}
        </span>
        <span className="tname" title={f.path}>{f.name}</span>
        {isRev && <span className="revtick"><Check size={11} /></span>}
        <span className="tnum"><span className="add">+{f.add}</span>{f.del > 0 && <span className="del">−{f.del}</span>}</span>
        <span className="asmdot" style={{ background: asmColor(f.asmdef) }} title={f.asmdef} />
      </div>
    );
  }

  const total = data.files.length;
  const revCount = data.files.filter((f) => reviewed.has(f.path)).length;

  return (
    <aside className="tree">
      <div className="tree-top">
        <div className="tree-title">
          Changed files <span className="tt-count">{revCount}/{total}</span>
        </div>
        <input className="search" placeholder="filter…" value={search} onChange={(e) => setSearch(e.target.value)} />
        <div className="tree-actions">
          <button onClick={() => setCollapsed(new Set(allDirs(tree)))}>collapse all</button>
          <button onClick={() => setCollapsed(new Set())}>expand all</button>
        </div>
      </div>
      <div className="tree-body">{tree.children.map((c) => renderNode(c, 0))}</div>
    </aside>
  );
}

function allDirs(n: Node): string[] {
  const out: string[] = [];
  const walk = (x: Node) => { if (x.isDir && x.path) out.push(x.path); x.children.forEach(walk); };
  n.children.forEach(walk);
  return out;
}
