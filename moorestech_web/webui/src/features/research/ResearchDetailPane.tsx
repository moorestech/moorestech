import type { ResearchNodeData } from "@/bridge";
import { dispatchAction } from "@/bridge";
import { GamePanel, ItemSlot } from "@/shared/ui";
import { deriveResearchButton, isItemSufficient } from "./researchLogic";
import {
  L,
  researchNodeDescriptionKey,
  researchNodeNameKey,
  useI18n,
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
  const button = deriveResearchButton(node, owned);
  return (
    <div className={styles.detailPane} data-testid="research-detail-pane">
      <GamePanel variant="craft">
        <div className={styles.detailBody}>
          <div className={styles.detailHeader}>
            <span className={styles.detailName}>{t(researchNodeNameKey(node.guid))}</span>
            <button type="button" className={styles.detailClose} data-testid="research-detail-close" onClick={onClose}>
              {t(L.ui.research.closeSymbol)}
            </button>
          </div>
          <p className={styles.detailDescription}>{t(researchNodeDescriptionKey(node.guid))}</p>
          {node.consumeItems.length > 0 && (
            <div className={styles.detailSlots}>
              {node.consumeItems.map((c, i) => (
                <ItemSlot key={`consume-${c.itemId}-${i}`} itemId={c.itemId} count={c.count}
                  insufficient={!isItemSufficient(node, c.itemId, c.count, owned) && node.state !== "completed"} />
              ))}
            </div>
          )}
          {node.rewardItems.length + node.unlockItemIds.length > 0 && (
            <div className={styles.detailSlots}>
              {node.rewardItems.map((reward, i) => (
                <ItemSlot key={`reward-${reward.itemId}-${i}`} itemId={reward.itemId} count={reward.count} />
              ))}
              {node.unlockItemIds.map((id, i) => (
                <ItemSlot key={`unlock-${id}-${i}`} itemId={id} />
              ))}
            </div>
          )}
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
