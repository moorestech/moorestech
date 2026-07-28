import { describe, expect, it } from "vitest";
import type { BuildMenuCategory, BuildMenuEntryData } from "../../bridge/contract/payloadTypes";
import { resolveSelectedCategory, searchSections, sectionsForCategory, visibleCategories } from "./buildMenuGrouping";

// idはGuid形状という契約不変条件を保つため、labelではなく連番Guidを使う
// id keeps the GUID-shaped contract invariant, so use a sequential GUID instead of the label
let nextIdSuffix = 1;
const entry = (label: string, category: string, subCategory: string): BuildMenuEntryData => ({
  id: `aaaaaaaa-0000-4000-8000-${String(nextIdSuffix++).padStart(12, "0")}`, kind: "block", label, category, subCategory, requiredItems: [],
});

const categories: BuildMenuCategory[] = [
  { name: "採掘", subCategories: ["採掘機", "液体採取"] },
  { name: "物流", subCategories: ["チェスト", "電気コンベア"] },
  { name: "建材", subCategories: ["土台"] },
];

const entries = [
  entry("木のチェスト", "物流", "チェスト"),
  entry("鉄の採掘機", "採掘", "採掘機"),
  entry("ベルトコンベア", "物流", "電気コンベア"),
];

describe("visibleCategories", () => {
  it("エントリの無いカテゴリを除外し定義順を維持する", () => {
    expect(visibleCategories(categories, entries).map((c) => c.name)).toEqual(["採掘", "物流"]);
  });
});

describe("resolveSelectedCategory", () => {
  it("nullなら先頭カテゴリへフォールバックする", () => {
    expect(resolveSelectedCategory(null, visibleCategories(categories, entries))).toBe("採掘");
  });
  it("表示対象外のカテゴリ名なら先頭へフォールバックする", () => {
    expect(resolveSelectedCategory("建材", visibleCategories(categories, entries))).toBe("採掘");
  });
  it("表示中のカテゴリ名は維持する", () => {
    expect(resolveSelectedCategory("物流", visibleCategories(categories, entries))).toBe("物流");
  });
  it("表示カテゴリが無ければnull", () => {
    expect(resolveSelectedCategory("物流", [])).toBeNull();
  });
});

describe("sectionsForCategory", () => {
  it("サブカテゴリ定義順で空サブカテゴリを除外する", () => {
    const sections = sectionsForCategory("物流", categories, entries);
    expect(sections.map((s) => s.subCategory)).toEqual(["チェスト", "電気コンベア"]);
    expect(sections[0].entries.map((e) => e.label)).toEqual(["木のチェスト"]);
  });
});

describe("searchSections", () => {
  it("横断部分一致でカテゴリ定義順にグループ化する", () => {
    const sections = searchSections("鉄", categories, entries);
    expect(sections.map((s) => `${s.category}/${s.subCategory}`)).toEqual(["採掘/採掘機"]);
  });
  it("大文字小文字を無視する", () => {
    const en = [entry("Iron Chest", "物流", "チェスト")];
    expect(searchSections("iron", categories, en)).toHaveLength(1);
  });
  it("0件なら空配列", () => {
    expect(searchSections("存在しない", categories, entries)).toEqual([]);
  });
});
