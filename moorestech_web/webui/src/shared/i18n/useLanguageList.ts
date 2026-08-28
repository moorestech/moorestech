import { useCallback, useEffect, useState } from "react";
import { localizationLanguagesUrl } from "@/bridge";

export type LanguageEntry = {
  code: string;
  displayName: string;
};

// loadingとreadyかつ0件を区別し、表示不能をerrorへ一本化する
// Distinguish loading from ready-with-zero-entries; fold the unrenderable case into error
export type LanguageListState =
  | { status: "loading" }
  | { status: "error" }
  | { status: "ready"; entries: [LanguageEntry, ...LanguageEntry[]] };

// 言語一覧の取得先を1本化する。ゲート・設定画面の両方がこのフックを介して同じ出所を見る
// Single source for fetching the language list; both the gate and settings screen go through this hook
export function useLanguageList(): { languages: LanguageListState; reload: () => void } {
  const [languages, setLanguages] = useState<LanguageListState>({ status: "loading" });
  const [reloadCount, setReloadCount] = useState(0);

  useEffect(() => {
    // HTTP境界の失敗はerrorとして持ち、unmount時は遅延応答を破棄する
    // Keep HTTP boundary failures as an error state and discard late responses after unmount
    const abort = new AbortController();
    setLanguages({ status: "loading" });
    void fetch(localizationLanguagesUrl, { signal: abort.signal })
      .then((response) => response.ok
        ? response.json() as Promise<unknown>
        : Promise.reject(new Error(`Failed to load languages: HTTP ${response.status}`)))
      .then((data) => {
        if (abort.signal.aborted) return;
        const entries = toLanguageEntries(data);
        // 選択肢ゼロは表示不能なのでHTTP失敗と同じ扱いへ倒す
        // Zero entries cannot be rendered, so fold it into the same case as an HTTP failure
        setLanguages(isNonEmpty(entries) ? { status: "ready", entries } : { status: "error" });
      })
      .catch(() => {
        if (!abort.signal.aborted) setLanguages({ status: "error" });
      });
    return () => abort.abort();
  }, [reloadCount]);

  const reload = useCallback(() => setReloadCount((count) => count + 1), []);

  return { languages, reload };
}

function isNonEmpty(entries: LanguageEntry[]): entries is [LanguageEntry, ...LanguageEntry[]] {
  return entries.length > 0;
}

function toLanguageEntries(data: unknown): LanguageEntry[] {
  // 外部JSONは完全なcode/displayName組だけを表示候補として受理する
  // Accept only complete code/displayName pairs from external JSON as display candidates
  if (!Array.isArray(data)) return [];
  return data.filter((entry): entry is LanguageEntry =>
    typeof entry === "object"
    && entry !== null
    && "code" in entry
    && typeof entry.code === "string"
    && "displayName" in entry
    && typeof entry.displayName === "string");
}
