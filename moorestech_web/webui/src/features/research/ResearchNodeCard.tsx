import type { ResearchNodeData } from "@/bridge";
import { ItemSlot } from "@/shared/ui";
import { deriveNodeCardState } from "./researchLogic";
import styles from "./style.module.css";
import { tutorialAnchor, type AnchorId } from "@/shared/tutorialAnchor";

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
  const cardState = deriveNodeCardState(node.state);
  return (
    <div
      className={styles.node}
      style={{ left, top }}
      data-research-node
      data-selected={selected || undefined}
      data-completed={cardState.completed || undefined}
      data-researchable={cardState.researchable || undefined}
      data-locked={cardState.locked || undefined}
      data-testid={`research-node-${node.guid}`}
      onClick={() => onSelect(node.guid)}
      {...tutorialAnchor(`research.node-${node.guid}`.toLowerCase() as AnchorId)}
    >
      <span className={styles.nodeName}>{node.name}</span>
      <ItemSlot itemId={node.iconItemId} />
    </div>
  );
}
