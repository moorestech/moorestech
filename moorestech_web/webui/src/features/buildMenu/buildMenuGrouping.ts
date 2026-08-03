import type { BuildMenuCategory, BuildMenuEntryData } from "../../bridge/contract/payloadTypes";
import { L, blockNameKey, connectToolNameKey, trainCarNameKey, type TranslationKey } from "../../shared/i18n";

export type BuildMenuSection = {
  categoryGuid: string;
  subCategoryGuid: string;
  entries: BuildMenuDisplayEntry[];
};

export type BuildMenuDisplayEntry = BuildMenuEntryData & { displayLabel: string };

// 配置対象の種別。rawはユーザー命名など辞書キーを持たないものだけ
// Selectable target kinds; raw covers only user-authored names without a dictionary key
export type SelectableTarget =
  | { type: "block"; guid: string }
  | { type: "connectTool"; guid: string }
  | { type: "trainCar"; guid: string }
  | { type: "blueprintCopy" }
  | { type: "raw"; label: string };

// 配置対象の表示名解決はこの1本に集約する（BuildMenu/配置HUD共用・分岐の複製禁止）
// All selectable-target display names resolve here, shared by BuildMenu and the placement HUD
export function localizeSelectableTargetName(
  target: SelectableTarget,
  translate: (key: TranslationKey) => string,
): string {
  switch (target.type) {
    case "block": return translate(blockNameKey(target.guid));
    case "connectTool": return translate(connectToolNameKey(target.guid));
    case "trainCar": return translate(trainCarNameKey(target.guid));
    case "blueprintCopy": return translate(L.ui.buildMenu.blueprintCopy);
    case "raw": return target.label;
  }
}

// 各エントリを表示名解決へ回す
// Route every entry through the shared display-name resolution
export function localizeBuildMenuEntries(
  entries: BuildMenuEntryData[],
  translate: (key: TranslationKey) => string,
): BuildMenuDisplayEntry[] {
  return entries.map((entry) => ({
    ...entry,
    displayLabel: localizeSelectableTargetName(entryTarget(entry), translate),
  }));
}

// entryTypeを表示名解決の種別へ写す。保存BPだけが原文labelのまま
// Map entryType onto the resolution kind; only saved blueprints keep their raw label
function entryTarget(entry: BuildMenuEntryData): SelectableTarget {
  switch (entry.entryType) {
    case "block": return { type: "block", guid: entry.entryKey };
    case "connectTool": return { type: "connectTool", guid: entry.entryKey };
    case "trainCar": return { type: "trainCar", guid: entry.entryKey };
    case "blueprintCopy": return { type: "blueprintCopy" };
    default: return { type: "raw", label: entry.label };
  }
}

// エントリが1件以上あるカテゴリのみを定義順で返す（unlock進行で自然に増える）
// Return only categories with entries, preserving definition order
export function visibleCategories(categories: BuildMenuCategory[], entries: BuildMenuDisplayEntry[]): BuildMenuCategory[] {
  return categories.filter((category) => entries.some((entry) => entry.categoryGuid === category.categoryGuid));
}

// 選択Guidを解決し、無効なら表示中の先頭へfallback
// Resolve the selected category GUID and fall back to the first visible category
export function resolveSelectedCategory(selected: string | null, visible: BuildMenuCategory[]): string | null {
  if (visible.length === 0) return null;
  if (selected !== null && visible.some((category) => category.categoryGuid === selected)) return selected;
  return visible[0].categoryGuid;
}

// カテゴリ内をサブカテゴリ定義順でグループ化。エントリ並びは配信配列順（=sortPriority昇順）を維持
// Group entries in sub-category definition order while preserving payload order
export function sectionsForCategory(categoryGuid: string, categories: BuildMenuCategory[], entries: BuildMenuDisplayEntry[]): BuildMenuSection[] {
  const definition = categories.find((category) => category.categoryGuid === categoryGuid);
  if (!definition) return [];
  return definition.subCategoryGuids
    .map((subCategoryGuid) => ({
      categoryGuid,
      subCategoryGuid,
      entries: entries.filter((entry) =>
        entry.categoryGuid === categoryGuid && entry.subCategoryGuid === subCategoryGuid),
    }))
    .filter((section) => section.entries.length > 0);
}

// 全カテゴリ横断の表示名部分一致検索（大文字小文字無視）
// Search localized display labels across all categories, ignoring case
export function searchSections(query: string, categories: BuildMenuCategory[], entries: BuildMenuDisplayEntry[]): BuildMenuSection[] {
  const lowered = query.toLowerCase();
  const hits = entries.filter((entry) => entry.displayLabel.toLowerCase().includes(lowered));
  return categories.flatMap((category) => sectionsForCategory(category.categoryGuid, categories, hits));
}
