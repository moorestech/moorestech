import { createElement } from "react";
import { create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BuildMenuDisplayEntry } from "./logic/buildMenuGrouping";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  // 本物のtは名前付きパラメータを差し込む。素材名が行へ載ることを見たいので差し込みを再現する
  // The real t interpolates named params; reproduce that so the material name lands in the line
  useI18n: () => ({ t: (key: string, params?: Record<string, unknown>) => (params ? `${key} ${Object.values(params).join(" ")}` : key) }),
  useItemNameResolver: () => (itemId: number) => `item-${itemId}`,
}));
// labelはReact要素なのでpropsへ残すと循環参照になる。子として描画し本文だけを検証可能にする
// The label is a React element, so keeping it in props would be circular; render it as a child and assert its text
vi.mock("@/shared/ui", () => ({
  HoverTooltip: ({ disabled, label, children }: { disabled?: boolean; label?: unknown; children?: unknown }) =>
    createElement("mock-hover-tooltip", { disabled }, label as never, children as never),
  PlacementTargetFace: (props: object) => createElement("mock-placement-target-face", props),
  SlotFrame: ({ children, ...props }: { children?: unknown }) => createElement("mock-slot-frame", props, children as never),
}));
vi.mock("@/features/hotbar", () => ({ useHotbarDragSource: () => ({}) }));

import { BuildMenuSlot } from "./BuildMenuSlot";

const entryWith = (requiredItems: BuildMenuDisplayEntry["requiredItems"]): BuildMenuDisplayEntry => ({
  id: "30000000-0000-4000-8000-000000000001",
  kind: "block",
  categoryGuid: "10000000-0000-4000-8000-000000000001",
  subCategoryGuid: "20000000-0000-4000-8000-000000000001",
  requiredItems,
  displayLabel: "belt",
}) as BuildMenuDisplayEntry;

const render = (entry: BuildMenuDisplayEntry) => JSON.stringify(create(createElement(BuildMenuSlot, {
  entry,
  onLeftClick: () => undefined,
  onHoverChange: () => undefined,
})).toJSON());

describe("BuildMenuSlot", () => {
  it("不足時は見出しと不足行だけをツールチップに出す", () => {
    const json = render(entryWith([
      { itemId: 3, count: 5, held: 2, lacking: true },
      { itemId: 4, count: 1, held: 9, lacking: false },
    ]));
    expect(json).toContain("ui.buildMenu.materialShortageTitle");
    expect(json).toContain("ui.buildMenu.materialShortageLine");
    expect(json).toContain("item-3");
    expect(json).not.toContain("item-4");
    expect(json).toContain('"disabled":false');
  });

  it("充足時はツールチップを無効にする", () => {
    const json = render(entryWith([{ itemId: 3, count: 5, held: 9, lacking: false }]));
    expect(json).toContain('"disabled":true');
  });

  it("不足していてもスロットに赤枠を付けない", () => {
    const json = render(entryWith([{ itemId: 3, count: 5, held: 2, lacking: true }]));
    expect(json).not.toContain('"insufficient":true');
  });
});
