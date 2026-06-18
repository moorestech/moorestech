// difit と同一のハイライト構成を再現しつつ、C#(.cs) と TS/TSX(.ts/.tsx) を扱う。
// prism-react-renderer の Prism にグローバル登録 → 各文法を動的ロード → vsDark(背景除去)。
import { Prism, themes, type PrismTheme } from 'prism-react-renderer';
import { useEffect, useState } from 'react';

// prismjs の文法を prism-react-renderer の Prism インスタンスへ登録させるため、先にグローバル化する(difit utils/prism.ts と同じ)。
(globalThis as unknown as { Prism: typeof Prism }).Prism = Prism;
if (typeof window !== 'undefined') (window as unknown as { Prism: typeof Prism }).Prism = Prism;

let grammarsPromise: Promise<void> | null = null;
function loadGrammars(): Promise<void> {
  if (!grammarsPromise) {
    // 依存順にロード(typescript→jsx→tsx)。csharp は独立。difit languageLoader と同じ import 方式。
    grammarsPromise = (async () => {
      await import('prismjs/components/prism-typescript.js');
      await import('prismjs/components/prism-jsx.js');
      await import('prismjs/components/prism-tsx.js');
      await import('prismjs/components/prism-csharp.js');
    })();
  }
  return grammarsPromise;
}

// 文法ロード完了で true。未ロード中は 'text' でフォールバック描画(difit useHighlightedCode と同挙動)。
export function useGrammarsReady(): boolean {
  const [ready, setReady] = useState(false);
  useEffect(() => { let m = true; loadGrammars().then(() => m && setReady(true)); return () => { m = false; }; }, []);
  return ready;
}

// 拡張子からハイライト言語を決める。未ロード中・未知拡張子は 'text'。
export function langForPath(path: string, ready: boolean): string {
  if (!ready) return 'text';
  if (path.endsWith('.cs')) return 'csharp';
  if (path.endsWith('.tsx') || path.endsWith('.ts')) return 'tsx';
  return 'text';
}

// difit syntaxThemes.removeBackgrounds: テーマの背景色を消す(コード面の背景は別管理)。
function removeBackgrounds(theme: PrismTheme): PrismTheme {
  return {
    ...theme,
    styles: theme.styles.map((s) => ({ ...s, style: { ...s.style, background: undefined, backgroundColor: undefined } })),
  };
}

// difit のデフォルト syntaxTheme = 'vsDark'。
export const VS_DARK = removeBackgrounds(themes.vsDark);
export { Prism };
