import type { ResearchNodeData } from "@/bridge";
import { dispatchAction } from "@/bridge";
import { GamePanel, ItemSlot } from "@/shared/ui";
import { deriveResearchButton, isItemSufficient } from "./researchLogic";
import UnlockSections from "./UnlockSections";
import {
  L,
  researchDescriptionKey,
  researchNameKey,
  useI18n,
  useItemNameResolver,
} from "@/shared/i18n";
import styles from "./style.module.css";

type Props = {
  node: ResearchNodeData;
  owned: Map<number, number>;
  onClose: () => void;
};

// 選択ノードの詳細と研究実行を担うフロートペイン（パン・ズーム非追従）
// Floating pane for selected-node details and research execution (not affected by pan/zoom)
export default function ResearchDetailPane({ node, owned, onClose }: Props) {
  const { t } = useI18n();
  const resolveItemName = useItemNameResolver();
  const button = deriveResearchButton(node, owned);
  return (
    <div className={styles.detailPane} data-testid="research-detail-pane">
      <GamePanel variant="craft">
        <div className={styles.detailBody}>
          <div className={styles.detailHeader}>
            <span className={styles.detailName}>{t(researchNameKey(node.guid))}</span>
            <button type="button" className={styles.detailClose} data-testid="research-detail-close" onClick={onClose}>
              {t(L.ui.research.closeSymbol)}
            </button>
          </div>
          <p className={styles.detailDescription}>{t(researchDescriptionKey(node.guid))}</p>
          {node.consumeItems.length > 0 && (
            <div data-testid="research-consume-items">
              <span className={styles.sectionLabel}>{t(L.ui.research.consumeItemsLabel)}</span>
              <div className={styles.detailSlots}>
                {node.consumeItems.map((c, i) => (
                  <ItemSlot key={`consume-${c.itemId}-${i}`} itemId={c.itemId} count={c.count}
                    insufficient={!isItemSufficient(node, c.itemId, c.count, owned) && node.state !== "completed"}
                    tooltip={<span style={{ whiteSpace: "pre-line" }}>{t(L.ui.recipe.materialTooltip, {
                      itemName: resolveItemName(c.itemId) ?? t(L.ui.common.itemFallback, { itemId: c.itemId }),
                      ownedCount: owned.get(c.itemId) ?? 0,
                      requiredCount: c.count,
                    })}</span>}
                  />
                ))}
              </div>
            </div>
          )}
          <UnlockSections node={node} />
          <button
            type="button"
            className={styles.researchButton}
            disabled={!button.interactable}
            data-testid={`research-button-${node.guid}`}
            onClick={() => void dispatchAction("research.complete", { researchGuid: node.guid })}
          >
            {button.completed ? t(L.ui.research.completed) : t(L.ui.research.action)}
          </button>
          {!button.completed && !button.interactable && (
            <p className={styles.detailReason} data-testid="research-detail-reason">
              {t(button.tooltipKey)}
            </p>
          )}
        </div>
      </GamePanel>
    </div>
  );
}
