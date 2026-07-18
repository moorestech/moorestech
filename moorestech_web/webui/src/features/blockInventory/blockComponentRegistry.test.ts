import { describe, it, expect } from "vitest";

import { resolveBlockComponent } from "./blockComponentRegistry";
import SectionStackView from "./views/SectionStackView";

describe("resolveBlockComponent", () => {
  it("標準ビューのblockTypeは単一のフォールバックへ統合する", () => {
    const fallback = resolveBlockComponent("unknown");
    expect(resolveBlockComponent("Chest")).toBe(fallback);
    expect(resolveBlockComponent("ElectricMachine")).toBe(fallback);
    expect(resolveBlockComponent("GearMiner")).toBe(fallback);
  });
  it("小文字 chest は実マスタ値でないため fallback になる", () => {
    expect(resolveBlockComponent("chest")).toBe(SectionStackView);
  });
  it("tank は登録キー削除済みのためフォールバックを返す", () => {
    expect(resolveBlockComponent("tank")).toBe(SectionStackView);
  });
  it("未登録 blockType はフォールバックを返す", () => {
    expect(resolveBlockComponent("unknown")).toBe(SectionStackView);
  });
});
