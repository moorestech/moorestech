import { FadeRule } from "@/shared/ui";
import { buildMenuCategoryNameKey, useI18n } from "@/shared/i18n";
import type { BuildMenuCategoryGroup, BuildMenuDisplayEntry } from "../logic/buildMenuGrouping";
import { BuildMenuCategoryGrid } from "./BuildMenuCategoryGrid";
import styles from "../style.module.css";

type Props = {
  groups: BuildMenuCategoryGroup[];
  spacerHeight: number;
  headingRef: (categoryGuid: string) => (element: HTMLElement | null) => void;
  attachGroup: (categoryGuid: string) => (element: HTMLElement | null) => void;
  onSelect: (entry: BuildMenuDisplayEntry) => void;
  onDelete: (entry: BuildMenuDisplayEntry) => void;
  onEntryHovered: (entry: BuildMenuDisplayEntry) => void;
};

// 大見出し→サブカテゴリで1本化(ADR0045)
// Heading → sub-categories in one scroll (ADR 0045)
export function BuildMenuCategoryList({ groups, spacerHeight, headingRef, attachGroup, onSelect, onDelete, onEntryHovered }: Props) {
  const { t } = useI18n();

  return (
    <div className={styles.gridArea} data-testid="build-menu-sections">
      {groups.map((group) => (
        <section
          key={group.categoryGuid}
          className={styles.categoryGroup}
          data-testid={`build-menu-category-${group.categoryGuid}-group`}
          ref={attachGroup(group.categoryGuid)}
        >
          <h2
            className={styles.categoryHeading}
            data-testid={`build-menu-category-heading-${group.categoryGuid}`}
            ref={headingRef(group.categoryGuid)}
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
      <div className={styles.trailingSpacer} data-testid="build-menu-trailing-spacer" style={{ minHeight: spacerHeight }} />
    </div>
  );
}
