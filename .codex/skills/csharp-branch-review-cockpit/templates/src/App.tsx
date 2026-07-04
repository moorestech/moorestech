import { useEffect, useState } from 'react';
import type { DataSet } from './lib/types';
import { StoreProvider, useStore } from './lib/store';
import { Cockpit } from './modes/Cockpit';
import { DepMap } from './modes/DepMap';

function Shell() {
  const { mode, setMode, data } = useStore();
  return (
    <div className="app">
      <header className="topbar">
        <span className="brand">review-cockpit</span>
        <span className="branch">{data.branch} <span className="muted">vs</span> {data.base}</span>
        <div className="tabs">
          <button className={`tab${mode === 'cockpit' ? ' active' : ''}`} onClick={() => setMode('cockpit')}>Cockpit</button>
          <button className={`tab${mode === 'depmap' ? ' active' : ''}`} onClick={() => setMode('depmap')}>Dep-Map</button>
        </div>
        <span className="filecount">{data.files.length} files</span>
      </header>
      <div className="body">
        {mode === 'cockpit' ? <Cockpit /> : <DepMap />}
      </div>
    </div>
  );
}

export function App() {
  const [data, setData] = useState<DataSet | null>(null);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    fetch('/data.json')
      .then((res) => {
        if (!res.ok) throw new Error(`data.json の取得に失敗しました (${res.status})`);
        return res.json();
      })
      .then((json: DataSet) => setData(json))
      .catch((e: Error) => setError(e.message));
  }, []);

  if (error) return <div className="app-error">{error}</div>;
  if (!data) return <div className="app-loading">読み込み中…</div>;
  return (
    <StoreProvider data={data}>
      <Shell />
    </StoreProvider>
  );
}
