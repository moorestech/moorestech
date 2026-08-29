import { createElement } from "react";
import { create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BuildMenuCategoryGroup } from "./logic/buildMenuGrouping";

vi.mock("@/shared/i18n", async (importOriginal) => ({
  ...(await importOriginal<typeof import("@/shared/i18n")>()),
  useI18n: () => ({ t: (key: string) => key }),
}));
vi.mock("@/shared/ui", () => ({
  FadeRule: () => createElement("mock-fade-rule"),
  SlotGrid: ({ children, ...props }: { children?: unknown }) => createElement("mock-slot-grid", props, children as never),
}));
vi.mock("./BuildMenuSlot", () => ({
  BuildMenuSlot: (props: { entry: { id: string } }) => createElement("mock-slot", { "data-id": props.entry.id }),
}));

import { BuildMenuCategoryList } from "./BuildMenuCategoryList";

const entry = (id: string, categoryGuid: string, subCategoryGuid: string) => ({
  kind: "block" as const, id, categoryGuid, subCategoryGuid, requiredItems: [], paymentWaived: false, displayLabel: id,
});
const groups: BuildMenuCategoryGroup[] = [
  { categoryGuid: "cat-a", sections: [{ categoryGuid: "cat-a", subCategoryGuid: "sub-1", entries: [entry("e1", "cat-a", "sub-1")] }] },
  { categoryGuid: "cat-b", sections: [{ categoryGuid: "cat-b", subCategoryGuid: "sub-2", entries: [entry("e2", "cat-b", "sub-2")] }] },
];

describe("BuildMenuCategoryList", () => {
  it("カテゴリ群ごとに大見出しを置き、末尾群と各見出しをrefへ登録し、末尾スペーサ高を反映する", () => {
    const attachHeading = vi.fn();
    const attachLastGroup = vi.fn();
    const renderer = create(createElement(BuildMenuCategoryList, {
      groups,
      spacerHeight: 123,
      attachHeading,
      attachLastGroup,
      onSelect: () => undefined,
      onDelete: () => undefined,
      onEntryHovered: () => undefined,
    }));
    const headings = renderer.root.findAllByType("h2");
    expect(headings.map((h) => h.props["data-testid"])).toEqual([
      "build-menu-category-heading-cat-a",
      "build-menu-category-heading-cat-b",
    ]);
    const spacer = renderer.root.findByProps({ "data-testid": "build-menu-trailing-spacer" });
    expect(spacer.props.style).toEqual({ height: 123 });
    // ref callback はマウント時にelement付きで呼ばれる（react-test-renderer では null）
    // Ref callbacks fire on mount (react-test-renderer passes null)
    expect(attachHeading).toHaveBeenCalledWith("cat-a", null);
    expect(attachHeading).toHaveBeenCalledWith("cat-b", null);
    expect(attachLastGroup).toHaveBeenCalledTimes(1);
  });
});
