import { FadeRule, ItemSlot, SlotGrid } from "@/shared/ui";
import { L, useI18n } from "@/shared/i18n";
import type { BuildMenuDisplayEntry } from "../logic/buildMenuGrouping";
import { isItemInsufficient } from "../logic/buildMenuShortage";
import styles from "../style.module.css";

type Props = { entry: BuildMenuDisplayEntry | null };

// §8.11のsticky詳細サイドバー。対象無し時は案内
// §8.11 sticky detail sidebar; shows a hint when nothing is selected
export function BuildMenuDetailSidebar({ entry }: Props) {
  const { t } = useI18n();

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
                {entry.requiredItems.map((item, index) => (
                  // 不足の表示はホストのlackingと支払い免除の合成。所持と必要の比較をここでやり直さない
                  // The display shortage composes the host's lacking with the payment waiver; no owned-vs-required comparison happens here
                  <ItemSlot
                    key={`${item.itemId}-${index}`}
                    itemId={item.itemId}
                    insufficient={isItemInsufficient(entry, item)}
                    shortage={{ ownedCount: item.held, requiredCount: item.count, tooltipKey: L.ui.buildMenu.materialTooltip }}
                  />
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
