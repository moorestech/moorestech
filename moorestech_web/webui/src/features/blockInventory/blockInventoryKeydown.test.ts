import { describe, expect, it, vi } from "vitest";
import { handleBlockInventoryKeydown } from "./blockInventoryKeydown";

describe("handleBlockInventoryKeydown", () => {
  it("blockInventoryレイヤーのEscapeでGameScreen遷移を要求する", () => {
    const requestGameScreen = vi.fn();

    handleBlockInventoryKeydown("Escape", "blockInventory", requestGameScreen);

    expect(requestGameScreen).toHaveBeenCalledOnce();
  });

  it("modalレイヤーではEscapeでも遷移を要求しない", () => {
    const requestGameScreen = vi.fn();

    handleBlockInventoryKeydown("Escape", "modal", requestGameScreen);

    expect(requestGameScreen).not.toHaveBeenCalled();
  });

  it("gameレイヤーではEscapeでも遷移を要求しない", () => {
    const requestGameScreen = vi.fn();

    handleBlockInventoryKeydown("Escape", "game", requestGameScreen);

    expect(requestGameScreen).not.toHaveBeenCalled();
  });

  it("blockInventoryレイヤーでもEscape以外では遷移を要求しない", () => {
    const requestGameScreen = vi.fn();

    handleBlockInventoryKeydown("Enter", "blockInventory", requestGameScreen);

    expect(requestGameScreen).not.toHaveBeenCalled();
  });
});
