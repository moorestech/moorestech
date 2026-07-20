import { describe, expect, it } from "vitest";
import { tutorialAnchor } from "./tutorialAnchor";

describe("tutorialAnchor", () => {
  it("creates only the tutorial contract attribute", () => {
    expect(tutorialAnchor("inventory.close-button")).toEqual({
      "data-tutorial-anchor": "inventory.close-button",
    });
  });
});
