import { beforeEach, describe, expect, it, vi } from "vitest";

// 各テストでstoredを初期化
// Reset stored per test via resetModules
beforeEach(() => {
  vi.resetModules();
});

describe("buildMenuSessionState", () => {
  it("初期状態は空検索・先頭スクロール・ホバー無し", async () => {
    const { loadBuildMenuSessionState } = await import("./buildMenuSessionState");
    expect(loadBuildMenuSessionState()).toEqual({ query: "", scrollTop: 0, hoveredEntryId: null });
  });

  it("部分更新が累積し、他フィールドは保たれる", async () => {
    const { loadBuildMenuSessionState, updateBuildMenuSessionState } = await import("./buildMenuSessionState");
    updateBuildMenuSessionState({ query: "鉄" });
    updateBuildMenuSessionState({ scrollTop: 120 });
    expect(loadBuildMenuSessionState()).toEqual({ query: "鉄", scrollTop: 120, hoveredEntryId: null });
  });

  // 前テストの更新が残らないことを確認
  // Confirms updates don't carry across tests
  it("モジュール再読込で前テストの更新が持ち越されない", async () => {
    const { loadBuildMenuSessionState } = await import("./buildMenuSessionState");
    expect(loadBuildMenuSessionState().query).toBe("");
  });

  it("ホバー中エントリを保持し、null で解除できる", async () => {
    const { loadBuildMenuSessionState, updateBuildMenuSessionState } = await import("./buildMenuSessionState");
    updateBuildMenuSessionState({ hoveredEntryId: "entry-1" });
    expect(loadBuildMenuSessionState().hoveredEntryId).toBe("entry-1");
    updateBuildMenuSessionState({ hoveredEntryId: null });
    expect(loadBuildMenuSessionState().hoveredEntryId).toBeNull();
  });
});
