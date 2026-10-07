// @vitest-environment jsdom

import { describe, it, expect } from "vitest";

import { browserDefaultSuppressionFor, deriveActiveLayer, isPointerOverWebUi, isTextInputElement, isWheelPassthrough, reduceWebInputState } from "./activeLayer";

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

  it("印の付いたHUDの子孫だけがホイール素通しになる", () => {
    const marked = { closest: (selector: string) => (selector === "[data-wheel-passthrough]" ? {} : null) } as unknown as EventTarget;
    const plain = { closest: () => null } as unknown as EventTarget;

    expect(isWheelPassthrough(marked)).toBe(true);
    expect(isWheelPassthrough(plain)).toBe(false);
    expect(isWheelPassthrough(null)).toBe(false);
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

describe("browserDefaultSuppressionFor", () => {
  const button = { matches: () => false } as unknown as EventTarget;
  const textInput = { matches: () => true } as unknown as EventTarget;

  it("Tabは文字入力中でも封じるが、伝播は止めない", () => {
    expect(browserDefaultSuppressionFor("Tab", button)).toBe("preventDefault");
    expect(browserDefaultSuppressionFor("Tab", textInput)).toBe("preventDefault");
  });
  it("Spaceは文字入力欄以外で封じ、Reactハンドラへも渡さない", () => {
    expect(browserDefaultSuppressionFor(" ", button)).toBe("preventDefaultAndStopPropagation");
    expect(browserDefaultSuppressionFor(" ", null)).toBe("preventDefaultAndStopPropagation");
  });
  it("文字入力欄のSpaceは空白入力として通す", () => {
    expect(browserDefaultSuppressionFor(" ", textInput)).toBe("allow");
  });
  it("Enterなど他のキーは封じない", () => {
    expect(browserDefaultSuppressionFor("Enter", button)).toBe("allow");
  });
});

// セレクタ文字列を実DOMで評価し、許可リストの網羅と未知typeのfail-closedを押さえる
// Evaluate the selector string against a real DOM to pin the allowlist and the fail-closed unknown type
describe("isTextInputElement against real DOM elements", () => {
  const inputOfType = (type: string | null) => {
    const element = document.createElement("input");
    if (type != null) element.setAttribute("type", type);
    return element;
  };

  it("文字を打てるinput typeは文字入力欄として数える", () => {
    for (const type of ["text", "search", "url", "tel", "email", "password", "number", "date", "datetime-local", "month", "week", "time"]) {
      expect(isTextInputElement(inputOfType(type))).toBe(true);
    }
    expect(isTextInputElement(inputOfType(null))).toBe(true);
    expect(isTextInputElement(document.createElement("textarea"))).toBe(true);
  });

  it("押すだけのinput typeと未知のtypeは文字入力欄にしない", () => {
    for (const type of ["button", "submit", "reset", "checkbox", "radio", "range", "color", "file", "image", "hidden", "not-a-real-type"]) {
      expect(isTextInputElement(inputOfType(type))).toBe(false);
    }
    expect(isTextInputElement(document.createElement("button"))).toBe(false);
    expect(isTextInputElement(document.createElement("div"))).toBe(false);
  });

  it("contenteditableは有効な値のときだけ文字入力欄になる", () => {
    const editable = (value: string) => {
      const element = document.createElement("div");
      element.setAttribute("contenteditable", value);
      return element;
    };
    expect(isTextInputElement(editable(""))).toBe(true);
    expect(isTextInputElement(editable("true"))).toBe(true);
    expect(isTextInputElement(editable("plaintext-only"))).toBe(true);
    expect(isTextInputElement(editable("false"))).toBe(false);
  });
});
