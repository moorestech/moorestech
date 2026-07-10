import { describe, it, expect, vi } from "vitest";

// uiScreenRouting は bridge barrel から UiStateNames を読む。node 環境では webSocketClient の location.host を stub する
// uiScreenRouting reads UiStateNames from the bridge barrel; stub webSocketClient's location.host access in node
vi.mock("@/bridge/transport/webSocketClient", () => ({ sendAction: vi.fn() }));

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
  it("GameScreen・未受信・未知state はパネル無し", () => {
    expect(screenForUiState("GameScreen")).toBe("none");
    expect(screenForUiState(null)).toBe("none");
    expect(screenForUiState("PauseMenu")).toBe("none");
  });
});
