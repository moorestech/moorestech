import { FadeRule } from "@/shared/ui";
import { buildMenuCategoryNameKey, useI18n } from "@/shared/i18n";
import type { BuildMenuCategoryGroup, BuildMenuDisplayEntry } from "./logic/buildMenuGrouping";
import { BuildMenuCategoryGrid } from "./BuildMenuCategoryGrid";
import styles from "./style.module.css";

type Props = {
  groups: BuildMenuCategoryGroup[];
  spacerHeight: number;
  attachHeading: (categoryGuid: string, element: HTMLElement | null) => void;
  attachLastGroup: (element: HTMLElement | null) => void;
  onSelect: (entry: BuildMenuDisplayEntry) => void;
  onDelete: (entry: BuildMenuDisplayEntry) => void;
  onEntryHovered: (entry: BuildMenuDisplayEntry) => void;
};

// 全カテゴリを「大見出し → サブカテゴリ群」で1本に並べる（ADR 0045）
// Lays every category out as "heading → sub-category sections" in one scroll (ADR 0045)
export function BuildMenuCategoryList({ groups, spacerHeight, attachHeading, attachLastGroup, onSelect, onDelete, onEntryHovered }: Props) {
  const { t } = useI18n();
  const lastIndex = groups.length - 1;
  return (
    <div className={styles.gridArea} data-testid="build-menu-sections">
      {groups.map((group, index) => (
        <section
          key={group.categoryGuid}
          className={styles.categoryGroup}
          data-testid={`build-menu-category-${group.categoryGuid}-group`}
          ref={index === lastIndex ? attachLastGroup : undefined}
        >
          <h2
            className={styles.categoryHeading}
            data-testid={`build-menu-category-heading-${group.categoryGuid}`}
            ref={(element) => attachHeading(group.categoryGuid, element)}
          >
            {t(buildMenuCategoryNameKey(group.categoryGuid))}
          </h2>
          <FadeRule />
          <BuildMenuCategoryGrid
            sections={group.sections}
            onSelect={onSelect}
            onDelete={onDelete}
            onEntryHovered={onEntryHovered}
          />
        </section>
      ))}
      {/* 末尾カテゴリの見出しを視口上端へ持ち上げるための余白 */}
      {/* Trailing room so the last category heading can reach the viewport top */}
      <div data-testid="build-menu-trailing-spacer" style={{ height: spacerHeight }} />
    </div>
  );
}
