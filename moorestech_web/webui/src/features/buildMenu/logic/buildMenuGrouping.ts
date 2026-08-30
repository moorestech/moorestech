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
  // 全カテゴリ常時マウントでは総当たり走査が操作1回ごとに全スロット分効くため、1パスのバケツ分けで配る
  // With every category mounted, a nested rescan runs across all slots on each interaction, so bucket the entries in one pass
  const bucketsByCategory = new Map<string, Map<string, BuildMenuDisplayEntry[]>>();
  for (const entry of entries) {
    const buckets = bucketsByCategory.get(entry.categoryGuid) ?? new Map<string, BuildMenuDisplayEntry[]>();
    bucketsByCategory.set(entry.categoryGuid, buckets);
    const bucket = buckets.get(entry.subCategoryGuid) ?? [];
    buckets.set(entry.subCategoryGuid, bucket);
    bucket.push(entry);
  }

  const groups: BuildMenuCategoryGroup[] = [];
  for (const category of categories) {
    const buckets = bucketsByCategory.get(category.categoryGuid);
    if (buckets === undefined) continue;
    const sections: BuildMenuSection[] = [];
    for (const subCategoryGuid of category.subCategoryGuids) {
      const sectionEntries = buckets.get(subCategoryGuid);
      if (sectionEntries === undefined) continue;
      sections.push({ categoryGuid: category.categoryGuid, subCategoryGuid, entries: sectionEntries });
    }
    if (sections.length === 0) continue;
    groups.push({ categoryGuid: category.categoryGuid, sections });
  }
  return groups;
}

// 表示名の部分一致検索（大文字小文字無視）。空文字は全件
// Case-insensitive substring search on display labels; empty query returns everything
export function searchBuildMenuEntries(query: string, entries: BuildMenuDisplayEntry[]): BuildMenuDisplayEntry[] {
  if (query === "") return entries;
  const lowered = query.toLowerCase();
  return entries.filter((entry) => entry.displayLabel.toLowerCase().includes(lowered));
}
