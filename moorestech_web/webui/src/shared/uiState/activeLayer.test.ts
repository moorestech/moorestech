import { describe, it, expect } from "vitest";

import { deriveActiveLayer } from "./activeLayer";

describe("deriveActiveLayer", () => {
  it("modal があれば block が開いていても modal", () => {
    expect(deriveActiveLayer({ modalOpen: true, blockInventoryOpen: true, researchOpen: false, buildMenuOpen: false })).toBe("modal");
  });
  it("modal が無く block が開いていれば blockInventory", () => {
    expect(deriveActiveLayer({ modalOpen: false, blockInventoryOpen: true, researchOpen: false, buildMenuOpen: false })).toBe("blockInventory");
  });
  it("どちらも無ければ game", () => {
    expect(deriveActiveLayer({ modalOpen: false, blockInventoryOpen: false, researchOpen: false, buildMenuOpen: false })).toBe("game");
  });
  it("research layer sits between blockInventory and game", () => {
    expect(deriveActiveLayer({ modalOpen: false, blockInventoryOpen: false, researchOpen: true, buildMenuOpen: false })).toBe("research");
    expect(deriveActiveLayer({ modalOpen: true, blockInventoryOpen: false, researchOpen: true, buildMenuOpen: false })).toBe("modal");
  });
});

describe("deriveActiveLayer buildMenu", () => {
  it("buildMenu 中は game レイヤーにならない", () => {
    expect(deriveActiveLayer({ modalOpen: false, blockInventoryOpen: false, researchOpen: false, buildMenuOpen: true })).toBe("buildMenu");
  });
  it("modal は buildMenu より優先される", () => {
    expect(deriveActiveLayer({ modalOpen: true, blockInventoryOpen: false, researchOpen: false, buildMenuOpen: true })).toBe("modal");
  });
});
