// difit と同一の C# ハイライト構成を再現する。
// prism-react-renderer の Prism にグローバル登録 → prismjs の csharp 文法を動的ロード → vsDark(背景除去)。
import { Prism, themes, type PrismTheme } from 'prism-react-renderer';
import { useEffect, useState } from 'react';

// prism-csharp を prism-react-renderer の Prism インスタンスへ登録させるため、先にグローバル化する(difit utils/prism.ts と同じ)。
(globalThis as unknown as { Prism: typeof Prism }).Prism = Prism;
if (typeof window !== 'undefined') (window as unknown as { Prism: typeof Prism }).Prism = Prism;

let csharpPromise: Promise<void> | null = null;
function loadCsharp(): Promise<void> {
  if (!csharpPromise) {
    // difit languageLoader と同じ import(deps不要)。
    csharpPromise = import('prismjs/components/prism-csharp.js').then(() => void 0);
  }
  return csharpPromise;
}

// csharp 文法ロード完了で true。未ロード中は 'text' でフォールバック描画(difit useHighlightedCode と同挙動)。
export function useCsharpReady(): boolean {
  const [ready, setReady] = useState(false);
  useEffect(() => { let m = true; loadCsharp().then(() => m && setReady(true)); return () => { m = false; }; }, []);
  return ready;
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
