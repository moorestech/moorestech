import { create } from "zustand";
import { localizationLanguagesUrl } from "../../transport/httpEndpoints";
import { createMasterLoader } from "../itemMasterStore";

export type LanguageEntry = {
  code: string;
  displayName: string;
};

// 表示できる一覧は必ず1件以上。空配列を型で締め出す
// A renderable list always holds at least one entry; the type keeps an empty array out
export type LanguageEntries = [LanguageEntry, ...LanguageEntry[]];

type LanguageListStoreState = {
  entries: LanguageEntries | null;
  setEntries: (entries: LanguageEntries) => void;
};

// 言語一覧の zustand ストア（itemMasterStore.ts を踏襲）。遅延ロードが常時マウント側へも届く
// Zustand store for the language list (mirrors itemMasterStore.ts); late loads reach always-mounted components
export const useLanguageListStore = create<LanguageListStoreState>((set) => ({
  entries: null,
  setEntries: (entries) => set({ entries }),
}));

// 外部JSONは完全なcode/displayName組だけを受理し、選択肢ゼロは未取得と同じくローダーの再試行へ載せる
// Accept only complete code/displayName pairs; zero entries stay unloaded and fall back to the loader's retry
function parseLanguageEntries(data: unknown): LanguageEntries | null {
  if (!Array.isArray(data)) return null;
  const entries = data.filter((entry): entry is LanguageEntry =>
    typeof entry === "object"
    && entry !== null
    && "code" in entry
    && typeof entry.code === "string"
    && "displayName" in entry
    && typeof entry.displayName === "string");
  return 0 < entries.length ? (entries as LanguageEntries) : null;
}

export const ensureLanguageListLoaded = createMasterLoader<LanguageEntries>({
  url: localizationLanguagesUrl,
  parse: parseLanguageEntries,
  apply: (entries) => useLanguageListStore.getState().setEntries(entries),
});
