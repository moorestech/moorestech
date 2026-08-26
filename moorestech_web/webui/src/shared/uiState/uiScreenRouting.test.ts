import { describe, it, expect } from "vitest";

import { screenAllowsGrab, screenForUiState, screenShowsAlwaysOnHud, screenShowsBackdrop, screenShowsPauseMenu, type UiScreen } from "./uiScreenRouting";

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
  it("TrainHUDScreen は入れ子stateでHUDとPauseを分ける", () => {
    expect(screenForUiState("TrainHUDScreen", "GameScreen")).toBe("trainHud");
    expect(screenForUiState("TrainHUDScreen", "PauseMenuScreen")).toBe("trainPause");
  });
  it("Story は入れ子のPauseMenuだけをskitPause画面にする", () => {
    expect(screenForUiState("Story", "Playing")).toBe("none");
    expect(screenForUiState("Story", "PauseMenu")).toBe("skitPause");
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

describe("screenAllowsGrab", () => {
  // Record<UiScreen, boolean> なので画面種別が増えたらこの表が型エラーになり、更新漏れが防がれる
  // Typing it as Record<UiScreen, boolean> makes a new screen a compile error here, so the table cannot go stale
  const expectations: Record<UiScreen, boolean> = {
    none: false,
    playerInventory: true,
    subInventory: true,
    researchTree: true,
    buildMenu: false,
    challengeList: false,
    pauseMenu: false,
    trainHud: false,
    trainPause: false,
    skitPause: false,
  };

  it.each(Object.entries(expectations))("%s の grab 成立可否は %s", (screen, allowed) => {
    expect(screenAllowsGrab(screen as UiScreen)).toBe(allowed);
  });
});

describe("screenShowsAlwaysOnHud", () => {
  // 常時表示族を引っ込めるのは研究画面のみ
  // Only the research screen withdraws the always-on family
  const expectations: Record<UiScreen, boolean> = {
    none: true,
    playerInventory: true,
    subInventory: true,
    researchTree: false,
    buildMenu: true,
    challengeList: true,
    pauseMenu: true,
    trainHud: true,
    trainPause: true,
    skitPause: true,
  };

  it.each(Object.entries(expectations))("%s の常時表示HUD可否は %s", (screen, shown) => {
    expect(screenShowsAlwaysOnHud(screen as UiScreen)).toBe(shown);
  });
});

describe("screenShowsPauseMenu", () => {
  // Record<UiScreen, boolean> なので画面種別が増えたらこの表が型エラーになり、更新漏れが防がれる
  // Typing it as Record<UiScreen, boolean> makes a new screen a compile error here, so the table cannot go stale
  const expectations: Record<UiScreen, boolean> = {
    none: false,
    playerInventory: false,
    subInventory: false,
    researchTree: false,
    buildMenu: false,
    challengeList: false,
    pauseMenu: true,
    trainHud: false,
    trainPause: true,
    skitPause: true,
  };

  it.each(Object.entries(expectations))("%s のポーズメニュー表示可否は %s", (screen, shown) => {
    expect(screenShowsPauseMenu(screen as UiScreen)).toBe(shown);
  });
});

describe("screenShowsBackdrop", () => {
  // Record<UiScreen, boolean> なので画面種別が増えたらこの表が型エラーになり、更新漏れが防がれる
  // Typing it as Record<UiScreen, boolean> makes a new screen a compile error here, so the table cannot go stale
  const expectations: Record<UiScreen, boolean> = {
    none: false,
    playerInventory: true,
    subInventory: true,
    researchTree: true,
    buildMenu: true,
    challengeList: true,
    pauseMenu: true,
    trainHud: false,
    trainPause: true,
    skitPause: true,
  };

  it.each(Object.entries(expectations))("%s の背景ディム表示可否は %s", (screen, shown) => {
    expect(screenShowsBackdrop(screen as UiScreen)).toBe(shown);
  });
});
