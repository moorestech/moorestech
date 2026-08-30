import type { BuildMenuCategory, BuildMenuEntryData } from "../../../bridge/contract/payloadTypes";
import type { TranslationKey } from "../../../shared/i18n";
import { localizeSelectableTargetName, placementTargetOf } from "../../../shared/placementTarget";

export type BuildMenuSection = {
  categoryGuid: string;
  subCategoryGuid: string;
  entries: BuildMenuDisplayEntry[];
};

export type BuildMenuDisplayEntry = BuildMenuEntryData & { displayLabel: string };

// 1本スクロールの群単位
// A unit of the single scroll
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

// エントリを1件以上持つカテゴリのguidを定義順で返す（サイドバー表示専用、sectionsは組まない）
// Returns guids, in definition order, of categories that have at least one entry (sidebar-only; does not build sections)
export function categoriesWithEntries(categories: BuildMenuCategory[], entries: BuildMenuDisplayEntry[]): string[] {
  const guidsWithEntries = new Set(entries.map((entry) => entry.categoryGuid));
  return categories
    .filter((category) => guidsWithEntries.has(category.categoryGuid))
    .map((category) => category.categoryGuid);
}

// 表示名の部分一致検索（大文字小文字無視）。空文字は全件
// Case-insensitive substring search on display labels; empty query returns everything
export function searchBuildMenuEntries(query: string, entries: BuildMenuDisplayEntry[]): BuildMenuDisplayEntry[] {
  if (query === "") return entries;
  const lowered = query.toLowerCase();
  return entries.filter((entry) => entry.displayLabel.toLowerCase().includes(lowered));
}
