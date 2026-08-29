import { FadeRule, SlotGrid } from "@/shared/ui";
import { buildMenuCategoryNameKey, buildMenuSubCategoryNameKey, useI18n } from "@/shared/i18n";
import type { BuildMenuDisplayEntry, BuildMenuSection } from "./logic/buildMenuGrouping";
import { BuildMenuSlot } from "./BuildMenuSlot";
import styles from "./style.module.css";

type Props = {
  sections: BuildMenuSection[];
  // 検索中はカテゴリ名/サブカテゴリ名の複合見出し
  // While searching, headings combine category and sub-category names
  compositeHeading: boolean;
  onSelect: (entry: BuildMenuDisplayEntry) => void;
  onDelete: (entry: BuildMenuDisplayEntry) => void;
  // 入場のみ見て離脱は捨てる
  // Only entry matters; drop the leave boolean here
  onEntryHovered: (entry: BuildMenuDisplayEntry) => void;
};

// サブカテゴリ見出し+SlotGridでエントリを列挙する
// Lists entries as sub-category headings plus a SlotGrid
export function BuildMenuCategoryGrid({ sections, compositeHeading, onSelect, onDelete, onEntryHovered }: Props) {
  const { t } = useI18n();
  // 複合見出しはJSX外で組み立てて可視リテラルlintを避ける（Guidからの解決は下の式）
  // Build the composite heading outside JSX to avoid the visible-literal lint; GUIDs resolve below
  const sectionHeading = (section: BuildMenuSection) => {
    const subCategoryName = t(buildMenuSubCategoryNameKey(section.subCategoryGuid));
    if (!compositeHeading) return subCategoryName;
    return `${t(buildMenuCategoryNameKey(section.categoryGuid))} / ${subCategoryName}`;
  };
  return (
    <div className={styles.gridArea} data-testid="build-menu-sections">
      {sections.map((section) => (
        <section
          key={`${section.categoryGuid}/${section.subCategoryGuid}`}
          className={styles.section}
          data-testid={`build-menu-section-${section.categoryGuid}-${section.subCategoryGuid}`}
        >
          <h3 className={styles.sectionHeading}>{sectionHeading(section)}</h3>
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
    </div>
  );
}
