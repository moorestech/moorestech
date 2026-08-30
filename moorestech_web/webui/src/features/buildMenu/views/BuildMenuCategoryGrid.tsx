import { FadeRule, SlotGrid } from "@/shared/ui";
import { buildMenuSubCategoryNameKey, useI18n } from "@/shared/i18n";
import type { BuildMenuDisplayEntry, BuildMenuSection } from "../logic/buildMenuGrouping";
import { BuildMenuSlot } from "./BuildMenuSlot";
import styles from "../style.module.css";

type Props = {
  sections: BuildMenuSection[];
  onSelect: (entry: BuildMenuDisplayEntry) => void;
  onDelete: (entry: BuildMenuDisplayEntry) => void;
  // 入場のみ見て離脱は捨てる
  // Only entry matters; drop the leave boolean here
  onEntryHovered: (entry: BuildMenuDisplayEntry) => void;
};

// サブカテゴリ小見出し+SlotGridでエントリを列挙する
// Lists entries as sub-category headings plus a SlotGrid
export function BuildMenuCategoryGrid({ sections, onSelect, onDelete, onEntryHovered }: Props) {
  const { t } = useI18n();
  return (
    <>
      {sections.map((section) => (
        <section
          key={`${section.categoryGuid}/${section.subCategoryGuid}`}
          className={styles.section}
          data-testid={`build-menu-section-${section.categoryGuid}-${section.subCategoryGuid}`}
        >
          <h3 className={styles.sectionHeading}>{t(buildMenuSubCategoryNameKey(section.subCategoryGuid))}</h3>
          <FadeRule />
          <SlotGrid cols={8} testId={`build-menu-grid-${section.categoryGuid}-${section.subCategoryGuid}`}>
            {section.entries.map((entry) => (
              <BuildMenuSlot
                key={entry.id}
                entry={entry}
                onLeftClick={() => onSelect(entry)}
                onRightClick={entry.kind === "blueprint" ? () => onDelete(entry) : undefined}
                onHoverChange={(hovering) => { if (hovering) onEntryHovered(entry); }}
              />
            ))}
          </SlotGrid>
        </section>
      ))}
    </>
  );
}
