import { ModeSwitch } from "@/shared/ui";
import { buildMenuCategoryNameKey, useI18n } from "@/shared/i18n";

export type CategorySidebarItem = {
  categoryGuid: string;
  // 検索でヒットが無いカテゴリは押せない
  // Categories with no search hit cannot be pressed
  disabled: boolean;
};

type Props = {
  categories: CategorySidebarItem[];
  // scroll-spyの現在地（ジャンプ中は目標）
  // Scroll-spy current category (the target while jumping)
  selected: string;
  onSelect: (categoryGuid: string) => void;
};

// §8.6の縦ModeSwitchをカテゴリ見出しへのジャンプサイドバーとして転用する（ADR 0045）
// Reuses the §8.6 vertical ModeSwitch as the jump-to-category-heading sidebar (ADR 0045)
export function CategorySidebar({ categories, selected, onSelect }: Props) {
  const { t } = useI18n();
  return (
    <ModeSwitch
      value={selected}
      options={categories.map((category) => ({
        value: category.categoryGuid,
        label: t(buildMenuCategoryNameKey(category.categoryGuid)),
        testId: `build-menu-category-${category.categoryGuid}`,
        disabled: category.disabled,
      }))}
      onChange={onSelect}
      orientation="vertical"
      testId="build-menu-sidebar"
    />
  );
}
