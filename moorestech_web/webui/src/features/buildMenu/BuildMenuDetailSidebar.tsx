import { FadeRule, ItemSlot, SlotGrid } from "@/shared/ui";
import { L, useI18n } from "@/shared/i18n";
import { useMaterialTooltipText } from "@/shared/materialTooltipText";
import type { BuildMenuDisplayEntry } from "./logic/buildMenuGrouping";
import styles from "./style.module.css";

type Props = { entry: BuildMenuDisplayEntry | null };

// §8.11のsticky詳細サイドバー。対象無し時は案内
// §8.11 sticky detail sidebar; shows a hint when nothing is selected
export function BuildMenuDetailSidebar({ entry }: Props) {
  const { t } = useI18n();
  const materialTooltipText = useMaterialTooltipText();

  // 複数設置はホストが財布判定済みの setPlacement で届く。有無だけで分岐する
  // Multi-placement arrives as the host's already-decided setPlacement; branch on presence alone
  const setPlacement = entry !== null && entry.kind === "block" ? entry.setPlacement ?? null : null;

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
                {setPlacement !== null
                  ? t(L.ui.buildMenu.requiredItemsPerSet, { count: setPlacement.perCost })
                  : t(L.ui.buildMenu.requiredItems)}
              </span>
              <SlotGrid cols={3}>
                {entry.requiredItems.map((item) => (
                  <div key={item.itemId} className={styles.materialSlot}>
                    {/* 不足判定はホストのlackingが唯一の正。所持と必要の比較をここでやり直さない */}
                    {/* The host's lacking flag is the sole authority; no owned-vs-required comparison happens here */}
                    <ItemSlot
                      itemId={item.itemId}
                      insufficient={item.lacking}
                      tooltip={<span style={{ whiteSpace: "pre-line" }}>
                        {materialTooltipText(L.ui.buildMenu.materialTooltip, item.itemId, item.count, new Map([[item.itemId, item.held]]))}
                      </span>}
                    />
                    <span className={`iconTextOutlineLight ${styles.materialCount}`} data-lack={item.lacking || undefined}>
                      {t(L.ui.recipe.itemCountSummary, { ownedCount: item.held, requiredCount: item.count })}
                    </span>
                  </div>
                ))}
              </SlotGrid>
            </>
          )}
          {setPlacement !== null && (
            <span className={styles.detailCostLabel} data-testid="build-menu-remaining-placements">
              {t(L.ui.buildMenu.remainingPlacementCount, { count: setPlacement.remaining })}
            </span>
          )}
        </>
      )}
    </div>
  );
}
