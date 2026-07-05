import { createContext, useContext, useEffect, useMemo, useState, type ReactNode } from 'react';
import type { DataSet, FileRec } from './types';
import { methodKey, regionKey } from './rows';

export type Mode = 'cockpit' | 'depmap';

interface StoreState {
  data: DataSet;
  byPath: Map<string, FileRec>;
  mode: Mode;
  setMode: (m: Mode) => void;
  selected?: string;
  selectFile: (path: string) => void;
  jumpToFile: (path: string) => void;
  expanded: Set<string>;
  toggleFold: (key: string) => void;
  setFileFolds: (path: string, expand: boolean) => void;
  reviewed: Set<string>;
  toggleReviewed: (path: string) => void;
  search: string;
  setSearch: (s: string) => void;
}

const REVIEWED_KEY = 'review-cockpit:reviewed';
const StoreCtx = createContext<StoreState | null>(null);

function loadReviewed(): Set<string> {
  if (typeof window === 'undefined') return new Set();
  const raw = window.localStorage.getItem(REVIEWED_KEY);
  if (!raw) return new Set();
  const parsed: unknown = JSON.parse(raw);
  return Array.isArray(parsed) ? new Set(parsed as string[]) : new Set();
}

export function StoreProvider({ data, children }: { data: DataSet; children: ReactNode }) {
  const byPath = useMemo(() => new Map(data.files.map((f) => [f.path, f] as const)), [data]);
  const [mode, setMode] = useState<Mode>('cockpit');
  const [selected, setSelected] = useState<string | undefined>(data.files[0]?.path);
  const [expanded, setExpanded] = useState<Set<string>>(new Set());
  const [search, setSearch] = useState('');
  const [reviewed, setReviewed] = useState<Set<string>>(loadReviewed);

  useEffect(() => {
    window.localStorage.setItem(REVIEWED_KEY, JSON.stringify([...reviewed]));
  }, [reviewed]);

  function selectFile(path: string) {
    setSelected(path);
  }

  function jumpToFile(path: string) {
    setSelected(path);
    setMode('cockpit');
  }

  function toggleFold(key: string) {
    setExpanded((prev) => {
      const next = new Set(prev);
      if (next.has(key)) next.delete(key); else next.add(key);
      return next;
    });
  }

  function setFileFolds(path: string, expand: boolean) {
    const file = byPath.get(path);
    if (!file) return;
    const keys = [
      ...file.parsed.folds.map((f) => methodKey(file, f)),
      ...file.parsed.regions.map((r) => regionKey(file, r)),
    ];
    setExpanded((prev) => {
      const next = new Set(prev);
      for (const k of keys) {
        if (expand) next.add(k); else next.delete(k);
      }
      return next;
    });
  }

  function toggleReviewed(path: string) {
    setReviewed((prev) => {
      const next = new Set(prev);
      if (next.has(path)) next.delete(path); else next.add(path);
      return next;
    });
  }

  const value: StoreState = {
    data, byPath, mode, setMode, selected, selectFile, jumpToFile,
    expanded, toggleFold, setFileFolds, reviewed, toggleReviewed, search, setSearch,
  };
  return <StoreCtx.Provider value={value}>{children}</StoreCtx.Provider>;
}

export function useStore(): StoreState {
  const ctx = useContext(StoreCtx);
  if (!ctx) throw new Error('useStore must be used within <StoreProvider>');
  return ctx;
}
