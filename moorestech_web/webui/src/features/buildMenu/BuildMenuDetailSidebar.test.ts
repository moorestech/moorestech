import { createElement } from "react";
import { create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BuildMenuDisplayEntry } from "./buildMenuGrouping";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
  useItemNameResolver: () => (itemId: number) => `item-${itemId}`,
}));
// MantineProvider依存（Tooltip等）を避けるため共有UIはスタブにする
// Stub the shared UI to avoid MantineProvider dependencies (Tooltip, etc.)
// tooltipはReact要素なのでpropsへ残すと循環参照になる。子として描画し本文だけを検証可能にする
// The tooltip is a React element, so keeping it in props would be circular; render it as a child and assert its text
vi.mock("@/shared/ui", () => ({
  FadeRule: () => createElement("mock-fade-rule"),
  ItemSlot: ({ itemId, count, insufficient, tooltip }: { itemId: number; count?: number; insufficient?: boolean; tooltip?: unknown }) =>
    createElement("mock-item-slot", { itemId, count, insufficient }, tooltip as never),
  SlotGrid: ({ children }: { children: unknown }) => createElement("mock-slot-grid", null, children as never),
}));

import { BuildMenuDetailSidebar } from "./BuildMenuDetailSidebar";

const entry = (lacking: boolean, held: number): BuildMenuDisplayEntry => ({
  id: "30000000-0000-4000-8000-000000000001",
  kind: "block",
  categoryGuid: "10000000-0000-4000-8000-000000000001",
  subCategoryGuid: "20000000-0000-4000-8000-000000000001",
  requiredItems: [{ itemId: 3, count: 5, held, lacking }],
  displayLabel: "belt",
}) as BuildMenuDisplayEntry;

describe("BuildMenuDetailSidebar", () => {
  it("不足素材は赤枠と赤字の所持/必要を出す", () => {
    const tree = create(createElement(BuildMenuDetailSidebar, { entry: entry(true, 2) })).toJSON();
    const json = JSON.stringify(tree);
    expect(json).toContain('"insufficient":true');
    expect(json).toContain('"data-lack":true');
    expect(json).toContain("ui.buildMenu.materialTooltip");
    // 必要数バッジ(count)は廃止し、所持/必要のテキストへ置き換わっている
    // The required-count badge is gone, replaced by the owned/required text
    expect(json).not.toContain('"count":5');
  });

  it("残りが賄う素材は赤くしない", () => {
    const tree = create(createElement(BuildMenuDetailSidebar, { entry: entry(false, 0) })).toJSON();
    const json = JSON.stringify(tree);
    expect(json).toContain('"insufficient":false');
    expect(json).not.toContain('"data-lack":true');
  });
});
