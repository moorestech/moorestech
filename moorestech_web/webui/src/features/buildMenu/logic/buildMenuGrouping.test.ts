import { describe, expect, it } from "vitest";
import type { BuildMenuCategory, BuildMenuEntryData } from "../../../bridge/contract/payloadTypes";
import { connectToolNameKey, trainCarNameKey } from "@/shared/i18n";
import {
  groupBuildMenuCategories,
  localizeBuildMenuEntries,
  searchBuildMenuEntries,
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
  kind: "block", id, categoryGuid, subCategoryGuid, requiredItems: [], paymentWaived: false,
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

describe("groupBuildMenuCategories", () => {
  it("エントリの無いカテゴリを除外し定義順のカテゴリ群を返す", () => {
    const groups = groupBuildMenuCategories(categories, entries);
    expect(groups.map((g) => g.categoryGuid)).toEqual([miningCategoryGuid, logisticsCategoryGuid]);
  });
  it("各カテゴリ内はサブカテゴリ定義順で空サブカテゴリを除外する", () => {
    const logistics = groupBuildMenuCategories(categories, entries)[1];
    expect(logistics.sections.map((s) => s.subCategoryGuid)).toEqual([chestSubCategoryGuid, conveyorSubCategoryGuid]);
    expect(logistics.sections[0].entries.map((e) => e.displayLabel)).toEqual(["木のチェスト"]);
    expect(logistics.sections[0].categoryGuid).toBe(logisticsCategoryGuid);
  });
  it("エントリが空なら空配列", () => {
    expect(groupBuildMenuCategories(categories, [])).toEqual([]);
  });
});

describe("searchBuildMenuEntries", () => {
  it("表示名の部分一致で大文字小文字を無視して絞り込む", () => {
    expect(searchBuildMenuEntries("鉄", entries).map((e) => e.displayLabel)).toEqual(["鉄の採掘機"]);
    expect(searchBuildMenuEntries("ベルト", entries).map((e) => e.displayLabel)).toEqual(["ベルトコンベア"]);
  });
  it("空文字は全件を返す", () => {
    expect(searchBuildMenuEntries("", entries)).toHaveLength(3);
  });
  it("不一致は空配列", () => {
    expect(searchBuildMenuEntries("存在しない", entries)).toEqual([]);
  });
  it("絞り込み結果をグルーピングするとヒットの無いカテゴリが消える", () => {
    const groups = groupBuildMenuCategories(categories, searchBuildMenuEntries("鉄", entries));
    expect(groups.map((g) => g.categoryGuid)).toEqual([miningCategoryGuid]);
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
      paymentWaived: false,
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
      paymentWaived: false,
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
      paymentWaived: false,
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
      paymentWaived: false,
    };

    const displayLabel = localizeBuildMenuEntries(
      [trainCar],
      (key) => (key === trainCarNameKey(trainCarGuid) ? "蒸気機関車" : "unused"),
    )[0].displayLabel;
    expect(displayLabel).toBe("蒸気機関車");
  });
});
