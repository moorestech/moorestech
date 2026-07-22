import type { BuildMenuCategory, BuildMenuEntryData } from "../../bridge/contract/payloadTypes";

export type BuildMenuSection = {
  category: string;
  subCategory: string;
  entries: BuildMenuEntryData[];
};

// エントリが1件以上あるカテゴリのみを定義順で返す（unlock進行で自然に増える）
export function visibleCategories(categories: BuildMenuCategory[], entries: BuildMenuEntryData[]): BuildMenuCategory[] {
  return categories.filter((category) => entries.some((e) => e.category === category.name));
}

// 選択カテゴリ名の解決。null・表示対象外なら表示中の先頭へフォールバック
export function resolveSelectedCategory(selected: string | null, visible: BuildMenuCategory[]): string | null {
  if (visible.length === 0) return null;
  if (selected !== null && visible.some((c) => c.name === selected)) return selected;
  return visible[0].name;
}

// カテゴリ内をサブカテゴリ定義順でグループ化。エントリ並びは配信配列順（=sortPriority昇順）を維持
export function sectionsForCategory(categoryName: string, categories: BuildMenuCategory[], entries: BuildMenuEntryData[]): BuildMenuSection[] {
  const definition = categories.find((c) => c.name === categoryName);
  if (!definition) return [];
  return definition.subCategories
    .map((subCategory) => ({
      category: categoryName,
      subCategory,
      entries: entries.filter((e) => e.category === categoryName && e.subCategory === subCategory),
    }))
    .filter((section) => section.entries.length > 0);
}

// 全カテゴリ横断のlabel部分一致検索（大文字小文字無視）。カテゴリ定義順→サブカテゴリ定義順
export function searchSections(query: string, categories: BuildMenuCategory[], entries: BuildMenuEntryData[]): BuildMenuSection[] {
  const lowered = query.toLowerCase();
  const hits = entries.filter((e) => e.label.toLowerCase().includes(lowered));
  return categories.flatMap((c) => sectionsForCategory(c.name, categories, hits));
}
