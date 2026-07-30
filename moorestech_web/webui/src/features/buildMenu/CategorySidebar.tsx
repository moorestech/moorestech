import { ModeSwitch } from "@/shared/ui";
import type { BuildMenuCategory } from "@/bridge";
import { buildMenuCategoryNameKey, useI18n } from "@/shared/i18n";

type Props = {
  categories: BuildMenuCategory[];
  selected: string;
  // 検索中はサイドバー無効
  // Disabled while searching
  disabled: boolean;
  onSelect: (categoryGuid: string) => void;
};

// §8.6の縦ModeSwitchをカテゴリ切替サイドバーへ転用する
// Reuses the §8.6 vertical ModeSwitch as the category-switch sidebar
export function CategorySidebar({ categories, selected, disabled, onSelect }: Props) {
  const { t } = useI18n();
  return (
    <ModeSwitch
      value={selected}
      options={categories.map((category) => ({
        value: category.categoryGuid,
        label: t(buildMenuCategoryNameKey(category.categoryGuid)),
        testId: `build-menu-category-${category.categoryGuid}`,
      }))}
      onChange={onSelect}
      orientation="vertical"
      disabled={disabled}
      testId="build-menu-sidebar"
    />
  );
}
