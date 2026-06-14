import { useEffect, useState } from 'react';
import type { DataSet } from './lib/types';
import { StoreProvider, useStore, type Mode } from './lib/store';
import { Cockpit } from './modes/Cockpit';
import { DepMap } from './modes/DepMap';

const MODES: { id: Mode; label: string; hint: string }[] = [
  { id: 'cockpit', label: 'Cockpit', hint: 'tree │ folded code │ deps' },
  { id: 'depmap', label: 'Dep-Map', hint: 'dependency graph + members' },
];

function TopBar() {
  const { mode, setMode, data, reviewed } = useStore();
  const total = data.files.length;
  const rev = data.files.filter((f) => reviewed.has(f.path)).length;
  const add = data.files.reduce((s, f) => s + f.add, 0);
  const del = data.files.reduce((s, f) => s + f.del, 0);
  return (
    <header className="topbar">
      <div className="brand">
        <span className="brand-main">CleanRoom Review Cockpit</span>
        <span className="brand-sub">{data.base} ← {data.branch}</span>
      </div>
      <div className="modesw">
        {MODES.map((m) => (
          <button key={m.id} className={`modebtn${mode === m.id ? ' on' : ''}`} onClick={() => setMode(m.id)} title={m.hint}>
            {m.label}
          </button>
        ))}
      </div>
      <div className="topstat">
        <span className="ts-rev">reviewed {rev}/{total}</span>
        <span className="ts-num"><span className="add">+{add}</span> <span className="del">−{del}</span></span>
      </div>
    </header>
  );
}

function Body() {
  const { mode } = useStore();
  return mode === 'cockpit' ? <Cockpit /> : <DepMap />;
}

export function App() {
  const [data, setData] = useState<DataSet | null>(null);
  const [err, setErr] = useState<string>('');
  useEffect(() => {
    fetch('data.json').then((r) => r.json()).then(setData).catch((e) => setErr(String(e)));
  }, []);

  if (err) return <div className="boot err">data.json 読み込み失敗: {err}</div>;
  if (!data) return <div className="boot">loading…</div>;

  return (
    <StoreProvider data={data}>
      <div className="app">
        <TopBar />
        <Body />
      </div>
    </StoreProvider>
  );
}
