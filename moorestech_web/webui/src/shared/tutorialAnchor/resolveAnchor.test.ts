import { beforeEach, describe, expect, it, vi } from "vitest";
import { resolveTutorialAnchor } from "./resolveAnchor";

type FakeElement = {
  hidden: boolean;
  closest: () => null;
  getBoundingClientRect: () => DOMRectReadOnly;
};

const visible = (rect: DOMRectReadOnly): FakeElement => ({
  hidden: false, closest: () => null, getBoundingClientRect: () => rect,
});

describe("resolveTutorialAnchor", () => {
  let matches: FakeElement[];

  beforeEach(() => {
    matches = [];
    vi.stubGlobal("innerWidth", 1280);
    vi.stubGlobal("innerHeight", 720);
    vi.stubGlobal("document", { querySelectorAll: () => matches });
    vi.stubGlobal("getComputedStyle", () => ({ display: "block", visibility: "visible" }));
  });

  it("reports a unique visible anchor as ready", () => {
    matches = [visible({ left: 10, top: 10, right: 50, bottom: 30, width: 40, height: 20 } as DOMRectReadOnly)];
    expect(resolveTutorialAnchor("inventory.craft-button")).toMatchObject({ status: "ready", reason: "mounted" });
  });

  it("rejects duplicate anchors instead of choosing one", () => {
    const rect = { left: 0, top: 0, right: 10, bottom: 10, width: 10, height: 10 } as DOMRectReadOnly;
    matches = [visible(rect), visible(rect)];
    expect(resolveTutorialAnchor("research.node")).toEqual({ status: "not-found", reason: "duplicate-anchor" });
  });

  it("distinguishes missing and zero-area anchors", () => {
    expect(resolveTutorialAnchor("missing.anchor")).toEqual({ status: "not-found", reason: "missing" });
    matches = [visible({ left: 0, top: 0, right: 0, bottom: 0, width: 0, height: 0 } as DOMRectReadOnly)];
    expect(resolveTutorialAnchor("hidden.anchor")).toEqual({ status: "hidden", reason: "zero-area" });
  });
});
