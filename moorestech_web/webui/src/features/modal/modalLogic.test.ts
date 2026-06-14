import { describe, it, expect } from "vitest";
import { respondPayload, buttonClass } from "./modalLogic";

describe("respondPayload", () => {
  it("id と confirm 結果をそのまま返す", () => {
    expect(respondPayload("m1", "confirm")).toEqual({ id: "m1", result: "confirm" });
  });
  it("cancel 結果も同様に返す", () => {
    expect(respondPayload("x", "cancel")).toEqual({ id: "x", result: "cancel" });
  });
});

describe("buttonClass", () => {
  it("confirm は青系を含む", () => {
    expect(buttonClass("confirm")).toContain("bg-blue-700");
  });
  it("error は赤系を含む", () => {
    expect(buttonClass("error")).toContain("bg-red-700");
  });
});
