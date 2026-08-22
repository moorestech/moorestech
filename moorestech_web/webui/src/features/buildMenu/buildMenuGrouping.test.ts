import { describe, expect, it } from "vitest";
import type { BuildMenuCategory, BuildMenuEntryData } from "../../bridge/contract/payloadTypes";
import { connectToolNameKey, trainCarNameKey } from "@/shared/i18n";
import {
  localizeBuildMenuEntries,
  resolveSelectedCategory,
  searchSections,
  sectionsForCategory,
  visibleCategories,
} from "./buildMenuGrouping";

const miningCategoryGuid = "10000000-0000-4000-8000-000000000001";
const logisticsCategoryGuid = "10000000-0000-4000-8000-000000000002";
const buildingCategoryGuid = "10000000-0000-4000-8000-000000000003";
const minerSubCategoryGuid = "20000000-0000-4000-8000-000000000001";
const liquidSubCategoryGuid = "20000000-0000-4000-8000-000000000002";
const chestSubCategoryGuid = "20000000-0000-4000-8000-000000000003";
const conveyorSubCategoryGuid = "20000000-0000-4000-8000-000000000004";
const foundationSubCategoryGuid = "20000000-0000-4000-8000-000000000005";
const connectToolGuid = "40000000-0000-4000-8000-000000000001";
const trainCarGuid = "8f9c2a51-0000-4000-8000-000000000001";

const blockEntry = (id: string, categoryGuid: string, subCategoryGuid: string): BuildMenuEntryData => ({
  kind: "block", id, categoryGuid, subCategoryGuid, requiredItems: [], placementsPerCost: 1, remainingPlacementCount: 0,
});

const categories: BuildMenuCategory[] = [
  { categoryGuid: miningCategoryGuid, subCategoryGuids: [minerSubCategoryGuid, liquidSubCategoryGuid] },
  { categoryGuid: logisticsCategoryGuid, subCategoryGuids: [chestSubCategoryGuid, conveyorSubCategoryGuid] },
  { categoryGuid: buildingCategoryGuid, subCategoryGuids: [foundationSubCategoryGuid] },
];

const rawEntries = [
  blockEntry("30000000-0000-4000-8000-000000000001", logisticsCategoryGuid, chestSubCategoryGuid),
  blockEntry("30000000-0000-4000-8000-000000000002", miningCategoryGuid, minerSubCategoryGuid),
  blockEntry("30000000-0000-4000-8000-000000000003", logisticsCategoryGuid, conveyorSubCategoryGuid),
] satisfies BuildMenuEntryData[];
const translations: Record<string, string> = {
  "block.30000000-0000-4000-8000-000000000001.name": "木のチェスト",
  "block.30000000-0000-4000-8000-000000000002.name": "鉄の採掘機",
  "block.30000000-0000-4000-8000-000000000003.name": "ベルトコンベア",
};
const entries = localizeBuildMenuEntries(rawEntries, (key) => translations[key]);

describe("visibleCategories", () => {
  it("エントリの無いカテゴリを除外し定義順を維持する", () => {
    expect(visibleCategories(categories, entries).map((c) => c.categoryGuid)).toEqual([
      miningCategoryGuid,
      logisticsCategoryGuid,
    ]);
  });
});

describe("resolveSelectedCategory", () => {
  it("nullなら先頭カテゴリへフォールバックする", () => {
    expect(resolveSelectedCategory(null, visibleCategories(categories, entries))).toBe(miningCategoryGuid);
  });
  it("表示対象外のカテゴリ名なら先頭へフォールバックする", () => {
    expect(resolveSelectedCategory(buildingCategoryGuid, visibleCategories(categories, entries))).toBe(miningCategoryGuid);
  });
  it("表示中のカテゴリ名は維持する", () => {
    expect(resolveSelectedCategory(logisticsCategoryGuid, visibleCategories(categories, entries))).toBe(logisticsCategoryGuid);
  });
  it("表示カテゴリが無ければnull", () => {
    expect(resolveSelectedCategory(logisticsCategoryGuid, [])).toBeNull();
  });
});

describe("sectionsForCategory", () => {
  it("サブカテゴリ定義順で空サブカテゴリを除外する", () => {
    const sections = sectionsForCategory(logisticsCategoryGuid, categories, entries);
    expect(sections.map((s) => s.subCategoryGuid)).toEqual([chestSubCategoryGuid, conveyorSubCategoryGuid]);
    expect(sections[0].entries.map((e) => e.displayLabel)).toEqual(["木のチェスト"]);
  });
});

describe("searchSections", () => {
  it("横断部分一致でカテゴリ定義順にグループ化する", () => {
    const sections = searchSections("鉄", categories, entries);
    expect(sections.map((s) => `${s.categoryGuid}/${s.subCategoryGuid}`)).toEqual([
      `${miningCategoryGuid}/${minerSubCategoryGuid}`,
    ]);
  });
  it("大文字小文字を無視する", () => {
    const en = localizeBuildMenuEntries(
      [blockEntry("30000000-0000-4000-8000-000000000004", logisticsCategoryGuid, chestSubCategoryGuid)],
      () => "Iron Chest",
    );
    expect(searchSections("iron", categories, en)).toHaveLength(1);
  });
  it("0件なら空配列", () => {
    expect(searchSections("存在しない", categories, entries)).toEqual([]);
  });
});

describe("localizeBuildMenuEntries", () => {
  it("blockはGuid導出キーで表示名を解決しraw labelを要求しない", () => {
    expect(entries[0].displayLabel).toBe("木のチェスト");
  });

  it.each([
    ["english", "Blueprint Copy"],
    ["japanese", "ブループリントコピー"],
  ])("blueprintCopyはraw labelなしで%s辞書から表示名を解決する", (_languageCode, expected) => {
    const blueprintCopy: BuildMenuEntryData = {
      kind: "blueprintCopy",
      id: "50000000-0000-4000-8000-000000000001",
      categoryGuid: logisticsCategoryGuid,
      subCategoryGuid: chestSubCategoryGuid,
      requiredItems: [],
    };

    expect(localizeBuildMenuEntries([blueprintCopy], () => expected)[0].displayLabel).toBe(expected);
  });

  it("ユーザー命名blueprintはlabelをそのまま維持する", () => {
    const blueprint: BuildMenuEntryData = {
      kind: "blueprint",
      id: "60000000-0000-4000-8000-000000000001",
      label: "starter-base",
      categoryGuid: logisticsCategoryGuid,
      subCategoryGuid: chestSubCategoryGuid,
      requiredItems: [],
    };
    expect(localizeBuildMenuEntries([blueprint], () => "unused")[0].displayLabel).toBe("starter-base");
  });

  it("connectToolはraw labelなしでGuid導出キーから表示名を解決する", () => {
    const connectTool: BuildMenuEntryData = {
      kind: "connectTool",
      id: connectToolGuid,
      categoryGuid: logisticsCategoryGuid,
      subCategoryGuid: chestSubCategoryGuid,
      requiredItems: [],
    };

    const displayLabel = localizeBuildMenuEntries(
      [connectTool],
      (key) => (key === connectToolNameKey(connectToolGuid) ? "電線ツール" : "unused"),
    )[0].displayLabel;
    expect(displayLabel).toBe("電線ツール");
  });

  it("trainCarはraw labelなしでGuid導出キーから表示名を解決する", () => {
    const trainCar: BuildMenuEntryData = {
      kind: "trainCar",
      id: trainCarGuid,
      categoryGuid: logisticsCategoryGuid,
      subCategoryGuid: chestSubCategoryGuid,
      requiredItems: [],
    };

    const displayLabel = localizeBuildMenuEntries(
      [trainCar],
      (key) => (key === trainCarNameKey(trainCarGuid) ? "蒸気機関車" : "unused"),
    )[0].displayLabel;
    expect(displayLabel).toBe("蒸気機関車");
  });
});
