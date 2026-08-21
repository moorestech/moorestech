import { useCallback, useMemo, useState } from "react";
import { useTopic, Topics } from "@/bridge";
import type { ResearchNodeData } from "@/bridge";
import { buildOwnedCounts } from "@/shared/ownedCounts";
import { GamePanel } from "@/shared/ui";
import { TreeView } from "@/shared/treeView";
import type { TreePoint } from "@/shared/treeView";
import ResearchNodeCard from "./ResearchNodeCard";
import ResearchDetailPane from "./ResearchDetailPane";
import { findInitialFocusNode } from "./researchLogic";
import { L, useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

// topic未受信時の空配列を固定参照にしてuseMemoの空振りを防ぐ
// Stable empty-array reference so useMemo doesn't recompute every render before the topic arrives
const EMPTY_NODES: ResearchNodeData[] = [];
const getResearchNodeId = (node: ResearchNodeData) => node.guid;
const getResearchNodePosition = (node: ResearchNodeData) => node.position;
const getPreviousResearchNodeIds = (node: ResearchNodeData) => node.prevGuids;

// GamePanel上の研究グラフ + 選択式詳細ペイン
// Research graph on a GamePanel plus a selection-driven detail pane
export default function ResearchTreePanel() {
  const { t } = useI18n();
  const tree = useTopic(Topics.researchTree);
  const inventory = useTopic(Topics.inventory);
  const nodes = tree?.nodes ?? EMPTY_NODES;
  const [selectedGuid, setSelectedGuid] = useState<string | null>(null);
  // topic未受信はnullのまま渡し、読む側が「所持0」と取り違えられない形にする(D4)
  // Pass null through while the topic has not arrived, so no reader can mistake it for "owns zero" (D4)
  const owned = useMemo(() => (inventory ? buildOwnedCounts(inventory.mainSlots) : null), [inventory]);
  // 同ノード再クリックで閉じるトグル選択
  // Toggle selection: clicking the same node again closes the pane
  const toggleSelect = useCallback((guid: string) => {
    setSelectedGuid((current) => (current === guid ? null : guid));
  }, []);
  const renderResearchNode = useCallback((node: ResearchNodeData, point: TreePoint) => (
    <ResearchNodeCard node={node} left={point.x} top={point.y}
      selected={node.guid === selectedGuid} onSelect={toggleSelect} />
  ), [selectedGuid, toggleSelect]);
  const selectedNode = nodes.find((node) => node.guid === selectedGuid);
  // 中央寄せはresearchLogic準拠
  // Centering target follows researchLogic
  const initialFocus = useMemo(() => {
    const focusNode = findInitialFocusNode(nodes);
    return focusNode ? getResearchNodePosition(focusNode) : null;
  }, [nodes]);

  return (
    <div className={styles.researchArea} data-testid="research-tree">
      <GamePanel title={t(L.ui.research.title)} style={{ height: "100%", boxSizing: "border-box" }}>
        <div className={styles.treeContainer}>
          <TreeView nodes={nodes} getId={getResearchNodeId} getPosition={getResearchNodePosition}
            getPrevIds={getPreviousResearchNodeIds} nodeTargetSelector="[data-research-node]" testIdPrefix="research"
            renderNode={renderResearchNode} viewportKey="research" initialFocus={initialFocus} />
        </div>
      </GamePanel>
      {selectedNode && (
        <ResearchDetailPane node={selectedNode} owned={owned}
          onClose={() => setSelectedGuid(null)} />
      )}
    </div>
  );
}
