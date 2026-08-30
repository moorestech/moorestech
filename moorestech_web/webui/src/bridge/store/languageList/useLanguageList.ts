import { useEffect } from "react";
import { ensureLanguageListLoaded, useLanguageListStore, type LanguageEntries } from "./languageListStore";

// 取得失敗はローダーが3秒間隔で自動再試行するため状態に持たない。到着するまではloading
// Load failures are retried by the loader every 3s, so they are not a state; it stays loading until entries arrive
export type LanguageListState =
  | { status: "loading" }
  | { status: "ready"; entries: LanguageEntries };

const LoadingState: LanguageListState = { status: "loading" };

// 言語一覧を購読する React フック。ゲート・設定画面の両方が同じ出所を見る
// React hook subscribing to the language list; both the gate and the settings screen see the same source
export function useLanguageList(): LanguageListState {
  useEffect(() => {
    ensureLanguageListLoaded();
  }, []);

  const entries = useLanguageListStore((state) => state.entries);
  return entries === null ? LoadingState : { status: "ready", entries };
}
