import { beforeEach, describe, expect, it, vi } from "vitest";

// モジュール変数(stored)を各テストで初期化するため resetModules + 動的 import を使う
// Reset module-level state (stored) per test via resetModules + dynamic import
beforeEach(() => {
  vi.resetModules();
});

describe("buildMenuSessionState", () => {
  it("初期状態は未選択・空検索・先頭スクロール・ホバー無し", async () => {
    const { loadBuildMenuSessionState } = await import("./buildMenuSessionState");
    expect(loadBuildMenuSessionState()).toEqual({
      categoryGuid: null,
      query: "",
      scrollTop: 0,
      hoveredEntryId: null,
    });
  });

  it("部分更新が累積し、他フィールドは保たれる", async () => {
    const { loadBuildMenuSessionState, updateBuildMenuSessionState } = await import("./buildMenuSessionState");
    updateBuildMenuSessionState({ categoryGuid: "cat-1" });
    updateBuildMenuSessionState({ query: "鉄", scrollTop: 120 });
    expect(loadBuildMenuSessionState()).toEqual({
      categoryGuid: "cat-1",
      query: "鉄",
      scrollTop: 120,
      hoveredEntryId: null,
    });
  });

  // 前テストの更新が持ち越されないこと自体を、resetModules後の初期値で確認する
  // Confirms the previous test's updates do not carry over, by checking the post-resetModules initial value
  it("モジュール再読込で前テストの更新が持ち越されない", async () => {
    const { loadBuildMenuSessionState } = await import("./buildMenuSessionState");
    expect(loadBuildMenuSessionState().categoryGuid).toBeNull();
  });

  it("ホバー中エントリを保持し、null で解除できる", async () => {
    const { loadBuildMenuSessionState, updateBuildMenuSessionState } = await import("./buildMenuSessionState");
    updateBuildMenuSessionState({ hoveredEntryId: "entry-1" });
    expect(loadBuildMenuSessionState().hoveredEntryId).toBe("entry-1");
    updateBuildMenuSessionState({ hoveredEntryId: null });
    expect(loadBuildMenuSessionState().hoveredEntryId).toBeNull();
  });
});
