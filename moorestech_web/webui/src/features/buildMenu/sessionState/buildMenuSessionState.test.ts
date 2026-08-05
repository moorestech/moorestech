import { beforeEach, describe, expect, it } from "vitest";
import {
  loadBuildMenuSessionState,
  resetBuildMenuSessionState,
  updateBuildMenuSessionState,
} from "./buildMenuSessionState";

describe("buildMenuSessionState", () => {
  beforeEach(() => resetBuildMenuSessionState());

  it("初期状態は未選択・空検索・先頭スクロール・ホバー無し", () => {
    expect(loadBuildMenuSessionState()).toEqual({
      categoryGuid: null,
      query: "",
      scrollTop: 0,
      hoveredEntryId: null,
    });
  });

  it("部分更新が累積し、他フィールドは保たれる", () => {
    updateBuildMenuSessionState({ categoryGuid: "cat-1" });
    updateBuildMenuSessionState({ query: "鉄", scrollTop: 120 });
    expect(loadBuildMenuSessionState()).toEqual({
      categoryGuid: "cat-1",
      query: "鉄",
      scrollTop: 120,
      hoveredEntryId: null,
    });
  });

  it("resetで初期状態へ戻る", () => {
    updateBuildMenuSessionState({ hoveredEntryId: "entry-1" });
    resetBuildMenuSessionState();
    expect(loadBuildMenuSessionState().hoveredEntryId).toBeNull();
  });
});
