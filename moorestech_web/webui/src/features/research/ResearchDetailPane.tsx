import type { ResearchNodeData } from "@/bridge";
import { dispatchAction } from "@/bridge";
import { GamePanel, ItemSlot } from "@/shared/ui";
import { ownedCountOf } from "@/shared/ownedCounts";
import { deriveResearchButton, isConsumeItemLacking } from "./researchLogic";
import UnlockSections from "./unlock/UnlockSections";
import {
  L,
  researchDescriptionKey,
  researchNameKey,
  useI18n,
} from "@/shared/i18n";
import { useMaterialTooltipText } from "@/shared/materialTooltipText";
import styles from "./style.module.css";

type Props = {
  node: ResearchNodeData;
  owned: Map<number, number> | null;
  onClose: () => void;
};

// 選択ノードの詳細と研究実行を担うフロートペイン（パン・ズーム非追従）
// Floating pane for selected-node details and research execution (not affected by pan/zoom)
export default function ResearchDetailPane({ node, owned, onClose }: Props) {
  const { t } = useI18n();
  const materialTooltipText = useMaterialTooltipText();
  const button = deriveResearchButton(node);
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
                {node.consumeItems.map((c, i) => {
                  const lacking = isConsumeItemLacking(node, c.itemId, c.count, owned);
                  return (
                    <div key={`consume-${c.itemId}-${i}`} className={styles.consumeSlot}>
                      <ItemSlot itemId={c.itemId}
                        insufficient={lacking}
                        tooltip={owned
                          ? <span style={{ whiteSpace: "pre-line" }}>
                              {materialTooltipText(L.ui.research.consumeItemTooltip, c.itemId, c.count, owned)}
                            </span>
                          : undefined}
                      />
                      {/* 不足時は数値も赤で示す(ADR 0014決定4・CraftRecipeView同型)。所持数未受信中は数値自体を出さない */}
                      {/* Shortages also color the count red (ADR 0014 decision 4; mirrors CraftRecipeView); the number is hidden while owned counts are unknown */}
                      {owned && (
                        <span className={`iconTextOutlineLight ${styles.consumeCount}`} data-lack={lacking || undefined}>
                          {t(L.ui.recipe.itemCountSummary, { ownedCount: ownedCountOf(owned, c.itemId), requiredCount: c.count })}
                        </span>
                      )}
                    </div>
                  );
                })}
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
