import { describe, expect, it } from "vitest";
import { L } from "@/shared/i18n";
import { machineInputStallMessageKey } from "../machineInputStallLogic";

describe("machineInputStallMessageKey", () => {
  it("レベル違いはレベル違いの文言にする", () => {
    expect(machineInputStallMessageKey({ reason: "mixedLevel", itemId: 12 })).toBe(L.ui.blockInventory.machineInputStallMixedLevel);
  });

  it("上限超えは製作上限の文言にする", () => {
    expect(machineInputStallMessageKey({ reason: "aboveCraftCap", itemId: 12 })).toBe(L.ui.blockInventory.machineInputStallAboveCraftCap);
  });
});
