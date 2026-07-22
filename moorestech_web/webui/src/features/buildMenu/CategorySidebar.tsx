import { ModeSwitch } from "@/shared/ui";
import type { BuildMenuCategory } from "@/bridge";

type Props = {
  categories: BuildMenuCategory[];
  selected: string;
  // 検索中はサイドバー無効
  // Disabled while searching
  disabled: boolean;
  onSelect: (name: string) => void;
};

// §8.6の縦ModeSwitchをカテゴリ切替サイドバーへ転用する
// Reuses the §8.6 vertical ModeSwitch as the category-switch sidebar
export function CategorySidebar({ categories, selected, disabled, onSelect }: Props) {
  return (
    <ModeSwitch
      value={selected}
      options={categories.map((c) => ({ value: c.name, label: c.name, testId: `build-menu-category-${c.name}` }))}
      onChange={onSelect}
      orientation="vertical"
      disabled={disabled}
      testId="build-menu-sidebar"
    />
  );
}
