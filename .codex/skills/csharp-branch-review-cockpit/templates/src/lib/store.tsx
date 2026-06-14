import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import type { DataSet, FileRec } from './types';
import { methodKey, regionKey } from './rows';

export type Mode = 'cockpit' | 'depmap';

interface Store {
  data: DataSet;
  byPath: Map<string, FileRec>;
  mode: Mode;
  setMode: (m: Mode) => void;
  selected: string;
  selectFile: (p: string) => void;
  jumpToFile: (p: string) => void; // 選択 + cockpit へ
  expanded: Set<string>;
  toggleFold: (key: string) => void;
  setFileFolds: (path: string, expand: boolean) => void;
  allFoldKeys: (path: string) => string[];
  reviewed: Set<string>;
  toggleReviewed: (p: string) => void;
  search: string;
  setSearch: (s: string) => void;
}

const Ctx = createContext<Store | null>(null);
export const useStore = () => {
  const s = useContext(Ctx);
  if (!s) throw new Error('no store');
  return s;
};

const REVIEW_KEY = 'cleanroom-cockpit-reviewed';

export function StoreProvider({ data, children }: { data: DataSet; children: ReactNode }) {
  const byPath = useMemo(() => new Map(data.files.map((f) => [f.path, f])), [data]);
  const [mode, setMode] = useState<Mode>('cockpit');
  const [selected, setSelected] = useState<string>(data.files[0]?.path ?? '');
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [search, setSearch] = useState('');
  const [reviewed, setReviewed] = useState<Set<string>>(() => {
    try {
      const raw = localStorage.getItem(REVIEW_KEY);
      return raw ? new Set(JSON.parse(raw)) : new Set();
    } catch {
      return new Set();
    }
  });

  useEffect(() => {
    try { localStorage.setItem(REVIEW_KEY, JSON.stringify([...reviewed])); } catch { /* ignore */ }
  }, [reviewed]);

  function allFoldKeys(path: string): string[] {
    const f = byPath.get(path);
    if (!f) return [];
    const keys: string[] = [];
    for (const m of f.parsed.folds) keys.push(methodKey(path, m.sigStart));
    for (const r of f.parsed.regions) keys.push(regionKey(path, r.start));
    return keys;
  }

  const store: Store = {
    data, byPath, mode, setMode,
    selected,
    selectFile: setSelected,
    jumpToFile: (p) => { setSelected(p); setMode('cockpit'); },
    expanded,
    toggleFold: (key) => setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key); else next.add(key);
      return next;
    }),
    setFileFolds: (path, expand) => setExpanded((prev) => {
      const next = new Set(prev);
      const keys = allFoldKeys(path);
      if (expand) keys.forEach((k) => next.add(k));
      else keys.forEach((k) => next.delete(k));
      return next;
    }),
    allFoldKeys,
    reviewed,
    toggleReviewed: (p) => setReviewed((prev) => {
      const next = new Set(prev);
      if (next.has(p)) next.delete(p); else next.add(p);
      return next;
    }),
    search, setSearch,
  };

  return <Ctx.Provider value={store}>{children}</Ctx.Provider>;
}
