// ビルドメニューのセッション内状態。現在地はscrollTopから再現できるためカテゴリは持たない
// In-session build menu state; the current category is derivable from scrollTop, so it is not stored
type BuildMenuSessionState = {
  query: string;
  scrollTop: number;
  hoveredEntryId: string | null;
};

const initialState: BuildMenuSessionState = {
  query: "",
  scrollTop: 0,
  hoveredEntryId: null,
};

let stored: BuildMenuSessionState = { ...initialState };

export function loadBuildMenuSessionState(): BuildMenuSessionState {
  return stored;
}

export function updateBuildMenuSessionState(patch: Partial<BuildMenuSessionState>): void {
  stored = { ...stored, ...patch };
}
