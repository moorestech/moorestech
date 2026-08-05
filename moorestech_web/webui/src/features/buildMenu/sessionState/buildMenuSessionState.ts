// ビルドメニューのセッション内状態
// In-session build menu state
type BuildMenuSessionState = {
  categoryGuid: string | null;
  query: string;
  scrollTop: number;
  hoveredEntryId: string | null;
};

const initialState: BuildMenuSessionState = {
  categoryGuid: null,
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
