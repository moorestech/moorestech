// ビルドメニューのセッション内状態(リロードで消える)。前例: shared/treeView/viewport/viewportStore.ts
// In-session build menu state (cleared on reload); precedent: shared/treeView/viewport/viewportStore.ts
export type BuildMenuSessionState = {
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

export function resetBuildMenuSessionState(): void {
  stored = { ...initialState };
}
