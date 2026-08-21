import { FadeRule, ItemSlot, SlotGrid } from "@/shared/ui";
import { L, useI18n } from "@/shared/i18n";
import type { BuildMenuDisplayEntry } from "./buildMenuGrouping";
import styles from "./style.module.css";

type Props = { entry: BuildMenuDisplayEntry | null };

// §8.11のsticky詳細サイドバー。対象無し時は案内
// §8.11 sticky detail sidebar; shows a hint when nothing is selected
export function BuildMenuDetailSidebar({ entry }: Props) {
  const { t } = useI18n();
  return (
    <div className={styles.detail} data-testid="build-menu-detail">
      {entry === null ? (
        <span className={styles.detailHint}>{t(L.ui.buildMenu.detailHint)}</span>
      ) : (
        <>
          {entry.iconUrl && (
            <img className={styles.detailIcon} src={entry.iconUrl} alt={entry.displayLabel} draggable={false} />
          )}
          <span className={styles.detailName}>{entry.displayLabel}</span>
          <FadeRule />
          {entry.requiredItems.length > 0 && (
            <>
              <span className={styles.detailCostLabel}>
                {entry.placementsPerCost > 1
                  ? t(L.ui.buildMenu.requiredItemsPerSet, { count: entry.placementsPerCost })
                  : t(L.ui.buildMenu.requiredItems)}
              </span>
              <SlotGrid cols={3}>
                {entry.requiredItems.map((item) => (
                  <ItemSlot key={item.itemId} itemId={item.itemId} count={item.count} />
                ))}
              </SlotGrid>
              {entry.placementsPerCost > 1 && (
                <span className={styles.detailCostLabel} data-testid="build-menu-remaining-placements">
                  {t(L.ui.buildMenu.remainingPlacementCount, { count: entry.remainingPlacementCount })}
                </span>
              )}
            </>
          )}
        </>
      )}
    </div>
  );
}
