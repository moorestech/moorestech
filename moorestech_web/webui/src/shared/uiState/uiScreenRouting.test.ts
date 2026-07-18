import { describe, it, expect } from "vitest";

import { screenForUiState } from "./uiScreenRouting";

describe("screenForUiState", () => {
  it("PlayerInventory はインベントリ画面", () => {
    expect(screenForUiState("PlayerInventory")).toBe("playerInventory");
  });
  it("SubInventory はブロック画面", () => {
    expect(screenForUiState("SubInventory")).toBe("subInventory");
  });
  it("BuildMenu は buildMenu 画面に解決される", () => {
    expect(screenForUiState("BuildMenu")).toBe("buildMenu");
  });
  it("ChallengeList は challengeList 画面に解決される", () => {
    expect(screenForUiState("ChallengeList")).toBe("challengeList");
  });
  it("PauseMenu は pauseMenu 画面に解決される", () => {
    expect(screenForUiState("PauseMenu")).toBe("pauseMenu");
  });
  it("PlaceBlock は画面を占有しないHUD stateとして扱う", () => {
    expect(screenForUiState("PlaceBlock")).toBe("none");
  });
  it("GameScreen・未受信・未知state はパネル無し", () => {
    expect(screenForUiState("GameScreen")).toBe("none");
    expect(screenForUiState(null)).toBe("none");
    expect(screenForUiState("UnknownState")).toBe("none");
  });
});
