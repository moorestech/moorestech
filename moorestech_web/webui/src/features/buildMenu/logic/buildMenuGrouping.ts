import type { BuildMenuCategory, BuildMenuEntryData } from "../../../bridge/contract/payloadTypes";
import type { TranslationKey } from "../../../shared/i18n";
import { localizeSelectableTargetName, placementTargetOf } from "../../../shared/placementTarget";

export type BuildMenuSection = {
  categoryGuid: string;
  subCategoryGuid: string;
  entries: BuildMenuDisplayEntry[];
};

export type BuildMenuDisplayEntry = BuildMenuEntryData & { displayLabel: string };

// 1本スクロールの単位。カテゴリ大見出し1つとその下のサブカテゴリ群
// Unit of the single scroll: one category heading and its sub-category sections
export type BuildMenuCategoryGroup = {
  categoryGuid: string;
  sections: BuildMenuSection[];
};

// 各エントリを共有の表示名解決へ回す
// Route every entry through the shared display-name resolution
export function localizeBuildMenuEntries(
  entries: BuildMenuEntryData[],
  translate: (key: TranslationKey) => string,
): BuildMenuDisplayEntry[] {
  return entries.map((entry) => ({
    ...entry,
    displayLabel: localizeSelectableTargetName(placementTargetOf(entry), translate),
  }));
}

// カテゴリ定義順→サブカテゴリ定義順で群化し、空の群は落とす。エントリ並びは配信順（sortPriority昇順）を維持
// Group by category then sub-category definition order, dropping empty groups; entry order stays as delivered
export function groupBuildMenuCategories(
  categories: BuildMenuCategory[],
  entries: BuildMenuDisplayEntry[],
): BuildMenuCategoryGroup[] {
  return categories
    .map((category) => ({
      categoryGuid: category.categoryGuid,
      sections: category.subCategoryGuids
        .map((subCategoryGuid) => ({
          categoryGuid: category.categoryGuid,
          subCategoryGuid,
          entries: entries.filter((entry) =>
            entry.categoryGuid === category.categoryGuid && entry.subCategoryGuid === subCategoryGuid),
        }))
        .filter((section) => section.entries.length > 0),
    }))
    .filter((group) => group.sections.length > 0);
}

// 表示名の部分一致検索（大文字小文字無視）。空文字は全件
// Case-insensitive substring search on display labels; empty query returns everything
export function searchBuildMenuEntries(query: string, entries: BuildMenuDisplayEntry[]): BuildMenuDisplayEntry[] {
  if (query === "") return entries;
  const lowered = query.toLowerCase();
  return entries.filter((entry) => entry.displayLabel.toLowerCase().includes(lowered));
}
