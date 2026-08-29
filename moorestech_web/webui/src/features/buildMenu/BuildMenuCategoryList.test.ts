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

// host要素のtestidだけを持つモックノードを返す。attachLastGroup/attachHeadingが
// どの要素に付いたかをtestid経由で実際に検証できるようにする
// Mocks host nodes down to their testid so attachLastGroup/attachHeading assertions
// can verify which element the ref actually landed on
const createNodeMock = (element: { props: Record<string, unknown> }) => ({
  testid: element.props["data-testid"],
});

describe("BuildMenuCategoryList", () => {
  it("カテゴリ群ごとに大見出しを置き、末尾群のsectionと各見出しのh2をrefへ登録し、末尾スペーサ高を反映する", () => {
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
    }), { createNodeMock });
    const headings = renderer.root.findAllByType("h2");
    expect(headings.map((h) => h.props["data-testid"])).toEqual([
      "build-menu-category-heading-cat-a",
      "build-menu-category-heading-cat-b",
    ]);
    const spacer = renderer.root.findByProps({ "data-testid": "build-menu-trailing-spacer" });
    expect(spacer.props.style).toEqual({ minHeight: 123 });
    // attachHeadingは各<h2>実体に、attachLastGroupは末尾groupの<section>実体にのみ付く
    // attachHeading lands on each <h2> node; attachLastGroup lands only on the trailing group's <section>
    expect(attachHeading).toHaveBeenCalledWith("cat-a", { testid: "build-menu-category-heading-cat-a" });
    expect(attachHeading).toHaveBeenCalledWith("cat-b", { testid: "build-menu-category-heading-cat-b" });
    expect(attachLastGroup).toHaveBeenCalledTimes(1);
    expect(attachLastGroup).toHaveBeenCalledWith({ testid: "build-menu-category-cat-b-group" });
  });

  it("groupsが空でもクラッシュせずスペーサのみ出す", () => {
    const attachHeading = vi.fn();
    const attachLastGroup = vi.fn();
    const renderer = create(createElement(BuildMenuCategoryList, {
      groups: [],
      spacerHeight: 42,
      attachHeading,
      attachLastGroup,
      onSelect: () => undefined,
      onDelete: () => undefined,
      onEntryHovered: () => undefined,
    }), { createNodeMock });
    expect(renderer.root.findAllByType("h2")).toEqual([]);
    const spacer = renderer.root.findByProps({ "data-testid": "build-menu-trailing-spacer" });
    expect(spacer.props.style).toEqual({ minHeight: 42 });
    expect(attachHeading).not.toHaveBeenCalled();
    expect(attachLastGroup).not.toHaveBeenCalled();
  });
});
