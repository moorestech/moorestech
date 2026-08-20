import { describe, expect, it } from "vitest";
import { tutorialAnchor, tutorialAnchorSelector } from "./tutorialAnchor";

describe("tutorialAnchor", () => {
  it("creates only the tutorial contract attribute", () => {
    expect(tutorialAnchor("inventory.close-button")).toEqual({
      "data-tutorial-anchor": "inventory.close-button",
    });
  });

  // 複数のアンカー名は空白1つで結合する
  // Several anchor names join with a single space
  it("joins multiple anchor ids with a single space", () => {
    expect(tutorialAnchor("equipment.slot-0", "equipment.selected-slot")).toEqual({
      "data-tutorial-anchor": "equipment.slot-0 equipment.selected-slot",
    });
  });

  it("builds a whitespace token match selector", () => {
    expect(tutorialAnchorSelector("equipment.selected-slot")).toBe('[data-tutorial-anchor~="equipment.selected-slot"]');
  });
});
