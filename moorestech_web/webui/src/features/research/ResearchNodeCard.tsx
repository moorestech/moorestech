import type { ResearchNodeData } from "@/bridge";
import { ItemSlot } from "@/shared/ui";
import { deriveNodeCardState } from "./researchLogic";
import styles from "./style.module.css";
import { tutorialAnchor, researchNodeAnchorId } from "@/shared/tutorialAnchor";
import { researchNameKey, useI18n } from "@/shared/i18n";

type Props = {
  node: ResearchNodeData;
  left: number;
  top: number;
  selected: boolean;
  onSelect: (guid: string) => void;
};

// モック準拠の「研究名+アイコン」ノードカード。詳細は選択時の詳細ペインが担う
// Mock-compliant "name + icon" node card; details live in the selection detail pane
export default function ResearchNodeCard({ node, left, top, selected, onSelect }: Props) {
  const cardState = deriveNodeCardState(node);
  const { t } = useI18n();
  return (
    <div
      className={styles.node}
      style={{ left, top }}
      data-research-node
      data-selected={selected || undefined}
      data-completed={cardState.completed || undefined}
      data-ready={cardState.ready || undefined}
      data-locked={cardState.locked || undefined}
      data-testid={`research-node-${node.guid}`}
      onClick={() => onSelect(node.guid)}
      {...tutorialAnchor(researchNodeAnchorId(node.guid))}
    >
      <span className={styles.nodeName}>{t(researchNameKey(node.guid))}</span>
      <ItemSlot itemId={node.iconItemId} />
    </div>
  );
}
