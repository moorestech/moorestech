import { ModeSwitch } from "@/shared/ui";
import { buildMenuCategoryNameKey, useI18n } from "@/shared/i18n";

type CategorySidebarItem = {
  categoryGuid: string;
  // 検索でヒットが無いカテゴリは押せない
  // Categories with no search hit cannot be pressed
  disabled: boolean;
};

type Props = {
  categories: CategorySidebarItem[];
  // 現在地(ジャンプ中は目標)
  // Current category (target while jumping)
  selected: string;
  onSelect: (categoryGuid: string) => void;
};

// 見出しジャンプサイドバーへ転用(ADR0045)
// Repurposed as the jump-to-heading sidebar (ADR 0045)
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
