import type { ResearchNodeData } from "@/bridge";
import { ItemSlot } from "@/shared/ui";
import { deriveNodeCardState, deriveNodeStateLabelKey } from "./researchLogic";
import styles from "./style.module.css";
import { tutorialAnchor, researchNodeAnchorId } from "@/shared/tutorialAnchor";
import { researchNameKey, useI18n } from "@/shared/i18n";

type Props = {
  node: ResearchNodeData;
  left: number;
  top: number;
  selected: boolean;
};

// モック準拠カード。詳細は選択時の詳細ペインへ
// Mock-compliant card; details live in the detail pane on selection
// 選択の入口はTreeViewのタップ判定へ一本化しているのでカード自身は押下を受けない(ADR 0033)
// TreeView's tap detection is the single entry for selection, so the card itself takes no press (ADR 0033)
export default function ResearchNodeCard({ node, left, top, selected }: Props) {
  const cardState = deriveNodeCardState(node);
  const { t } = useI18n();
  return (
    <div
      className={styles.node}
      style={{ left, top }}
      data-selected={selected || undefined}
      data-completed={cardState.completed || undefined}
      data-ready={cardState.ready || undefined}
      data-locked={cardState.locked || undefined}
      data-testid={`research-node-${node.guid}`}
      {...tutorialAnchor(researchNodeAnchorId(node.guid))}
    >
      <span className={styles.nodeName}>{t(researchNameKey(node.guid))}</span>
      <ItemSlot itemId={node.iconItemId} />
      {/* 状態ラベル。枠色4状態を補助する3語表示（ADR 0044） */}
      {/* State label; 3-word text that supplements the 4-state border color (ADR 0044) */}
      <span className={styles.nodeState} data-testid={`research-node-state-${node.guid}`}>
        {t(deriveNodeStateLabelKey(node))}
      </span>
    </div>
  );
}
