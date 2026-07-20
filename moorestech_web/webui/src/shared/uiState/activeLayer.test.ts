import { describe, it, expect } from "vitest";

import { deriveActiveLayer, isPointerOverWebUi, isTextInputElement, reduceWebInputState } from "./activeLayer";

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

describe("web input exclusivity", () => {
  it("transparent surface passes through while a child panel captures the pointer", () => {
    const surface = { hasAttribute: (name: string) => name === "data-web-ui-transparent" } as unknown as EventTarget;
    const panel = { hasAttribute: () => false } as unknown as EventTarget;

    expect(isPointerOverWebUi(surface)).toBe(false);
    expect(isPointerOverWebUi(panel)).toBe(true);
  });

  it("recognizes editable text controls but not ordinary buttons", () => {
    const editable = { matches: () => true } as unknown as EventTarget;
    const button = { matches: () => false } as unknown as EventTarget;
    expect(isTextInputElement(editable)).toBe(true);
    expect(isTextInputElement(button)).toBe(false);
  });

  it("updates pointer and text focus as independent axes", () => {
    const focused = reduceWebInputState({ pointerOverUi: false, textInputFocused: false }, { textInputFocused: true });
    expect(reduceWebInputState(focused, { pointerOverUi: true })).toEqual({ pointerOverUi: true, textInputFocused: true });
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
