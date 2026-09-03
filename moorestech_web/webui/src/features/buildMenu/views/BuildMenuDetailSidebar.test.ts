import { createElement } from "react";
import { create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BuildMenuDisplayEntry } from "../logic/buildMenuGrouping";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
  useItemNameResolver: () => (itemId: number) => `item-${itemId}`,
  useItemDisplayName: () => (itemId: number) => `item-${itemId}`,
}));
// MantineProvider依存（Tooltip等）を避けるため共有UIはスタブにする
// Stub the shared UI to avoid MantineProvider dependencies (Tooltip, etc.)
// tooltipはReact要素なのでpropsへ残すと循環参照になる。子として描画し本文だけを検証可能にする
// The tooltip is a React element, so keeping it in props would be circular; render it as a child and assert its text
vi.mock("@/shared/ui", () => ({
  FadeRule: () => createElement("mock-fade-rule"),
  ItemSlot: ({ itemId, count, insufficient, shortage }: { itemId: number; count?: number; insufficient?: boolean; shortage?: unknown }) =>
    createElement("mock-item-slot", { itemId, count, insufficient, shortage }),
  SlotGrid: ({ children }: { children: unknown }) => createElement("mock-slot-grid", null, children as never),
}));

import { BuildMenuDetailSidebar } from "./BuildMenuDetailSidebar";

const entry = (lacking: boolean, held: number, paymentWaived = false): BuildMenuDisplayEntry => ({
  id: "30000000-0000-4000-8000-000000000001",
  kind: "block" as const,
  categoryGuid: "10000000-0000-4000-8000-000000000001",
  subCategoryGuid: "20000000-0000-4000-8000-000000000001",
  requiredItems: [{ itemId: 3, count: 5, held, lacking }],
  paymentWaived,
  displayLabel: "belt",
});

describe("BuildMenuDetailSidebar", () => {
  // 所持と必要をスロットへ渡す対応は完全一致で固定する（入れ替わっても描画自体は成立するため）
  // The owned/required handoff is pinned by exact match, since a swap would still render fine
  it("不足素材は赤枠と所持/必要の数値をスロットへ渡す", () => {
    const renderer = create(createElement(BuildMenuDetailSidebar, { entry: entry(true, 2) }));
    const slot = renderer.root.findByType("mock-item-slot" as never);

    expect(slot.props.insufficient).toBe(true);
    expect(slot.props.shortage).toEqual({ ownedCount: 2, requiredCount: 5, tooltipKey: "ui.buildMenu.materialTooltip" });
    // countバッジ廃止・所持/必要へ置換
    // The count badge is gone, replaced by owned/required text
    expect(slot.props.count).toBeUndefined();
  });

  it("素材が足りていれば赤くしない", () => {
    const renderer = create(createElement(BuildMenuDetailSidebar, { entry: entry(false, 5) }));

    expect(renderer.root.findByType("mock-item-slot" as never).props.insufficient).toBe(false);
  });

  it("支払い免除中は素材が足りなくても赤くしない", () => {
    const renderer = create(createElement(BuildMenuDetailSidebar, { entry: entry(true, 0, true) }));

    expect(renderer.root.findByType("mock-item-slot" as never).props.insufficient).toBe(false);
  });
});
