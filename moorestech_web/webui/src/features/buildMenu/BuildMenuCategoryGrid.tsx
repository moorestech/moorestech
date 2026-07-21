import { SlotGrid } from "@/shared/ui";
import type { BuildMenuEntryData } from "@/bridge";
import type { BuildMenuSection } from "./buildMenuGrouping";
import { BuildMenuSlot } from "./BuildMenuSlot";
import styles from "./style.module.css";

type Props = {
  sections: BuildMenuSection[];
  // 検索中はカテゴリ名/サブカテゴリ名の複合見出し
  // While searching, headings combine category and sub-category names
  compositeHeading: boolean;
  onSelect: (entry: BuildMenuEntryData) => void;
  onDelete: (entry: BuildMenuEntryData) => void;
  onHoverChange: (entry: BuildMenuEntryData | null) => void;
};

// サブカテゴリ見出し+SlotGridでエントリを列挙する
// Lists entries as sub-category headings plus a SlotGrid
export function BuildMenuCategoryGrid({ sections, compositeHeading, onSelect, onDelete, onHoverChange }: Props) {
  // マスタ由来文字列のためt()不要。複合見出しはJSX外で組み立てて可視リテラルlintを避ける
  // Master-derived strings need no t(); build the composite heading outside JSX to dodge the visible-literal lint
  const sectionHeading = (section: BuildMenuSection) => (compositeHeading ? `${section.category} / ${section.subCategory}` : section.subCategory);
  return (
    <div className={styles.gridArea}>
      {sections.map((section) => (
        <section key={`${section.category}/${section.subCategory}`} data-testid={`build-menu-section-${section.category}-${section.subCategory}`}>
          <h3 className={styles.sectionHeading}>{sectionHeading(section)}</h3>
          <SlotGrid cols={8}>
            {section.entries.map((entry) => (
              <BuildMenuSlot
                key={`${entry.entryType}-${entry.entryKey}`}
                entry={entry}
                onLeftClick={() => onSelect(entry)}
                onRightClick={entry.entryType === "blueprint" ? () => onDelete(entry) : undefined}
                onHoverChange={(hovering) => onHoverChange(hovering ? entry : null)}
              />
            ))}
          </SlotGrid>
        </section>
      ))}
    </div>
  );
}
