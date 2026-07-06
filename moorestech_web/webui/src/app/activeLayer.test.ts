import { describe, it, expect, vi } from "vitest";

// activeLayer は barrel 経由で webSocketClient を読み込む。import 時に location.host を触るため node で stub
// activeLayer loads webSocketClient via the barrel, which touches location.host at import; stub it for node
vi.mock("@/bridge/transport/webSocketClient", () => ({ sendAction: vi.fn() }));

import { deriveActiveLayer } from "./activeLayer";

describe("deriveActiveLayer", () => {
  it("modal があれば block が開いていても modal", () => {
    expect(deriveActiveLayer({ modalOpen: true, blockInventoryOpen: true, researchOpen: false })).toBe("modal");
  });
  it("modal が無く block が開いていれば blockInventory", () => {
    expect(deriveActiveLayer({ modalOpen: false, blockInventoryOpen: true, researchOpen: false })).toBe("blockInventory");
  });
  it("どちらも無ければ game", () => {
    expect(deriveActiveLayer({ modalOpen: false, blockInventoryOpen: false, researchOpen: false })).toBe("game");
  });
  it("research layer sits between blockInventory and game", () => {
    expect(deriveActiveLayer({ modalOpen: false, blockInventoryOpen: false, researchOpen: true })).toBe("research");
    expect(deriveActiveLayer({ modalOpen: true, blockInventoryOpen: false, researchOpen: true })).toBe("modal");
  });
});
