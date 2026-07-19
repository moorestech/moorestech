import { useCallback, useMemo } from "react";
import { Box, Title } from "@mantine/core";
import { useTopic, Topics, useItemMaster } from "@/bridge";
import type { ResearchNodeData } from "@/bridge";
import { buildOwnedCounts } from "@/shared/ownedCounts";
import { TreeView } from "@/shared/treeView";
import type { TreePoint } from "@/shared/treeView";
import ResearchNodeCard from "./ResearchNodeCard";
import { useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

// topic未受信時の空配列を固定参照にしてuseMemoの空振りを防ぐ
// Stable empty-array reference so useMemo doesn't recompute every render before the topic arrives
const EMPTY_NODES: ResearchNodeData[] = [];
const getResearchNodeId = (node: ResearchNodeData) => node.guid;
const getResearchNodePosition = (node: ResearchNodeData) => node.position;
const getPreviousResearchNodeIds = (node: ResearchNodeData) => node.prevGuids;

// 研究ツリー全画面表示
// Full-screen research tree panel
export default function ResearchTreePanel() {
  const { t } = useI18n();
  const tree = useTopic(Topics.researchTree);
  const inventory = useTopic(Topics.inventory);
  const itemMaster = useItemMaster();
  const nodes = tree?.nodes ?? EMPTY_NODES;
  const owned = useMemo(
    () => buildOwnedCounts([...(inventory?.mainSlots ?? []), ...(inventory?.hotbarSlots ?? [])]),
    [inventory],
  );
  const resolveName = useCallback((itemId: number) => itemMaster?.get(itemId)?.name, [itemMaster]);
  // 所持数かマスタ名が変わった場合だけ研究カード場面を再構築する
  // Rebuild the research-card scene only when owned counts or master names change
  const renderResearchNode = useCallback((node: ResearchNodeData, point: TreePoint) => (
    <ResearchNodeCard
      node={node}
      left={point.x}
      top={point.y}
      owned={owned}
      resolveName={resolveName}
    />
  ), [owned, resolveName]);

  return (
    <Box className={styles.panel} data-testid="research-tree">
      <Title order={2} size="h4" p="sm">{t("研究ツリー")}</Title>
      <TreeView nodes={nodes} getId={getResearchNodeId} getPosition={getResearchNodePosition}
        getPrevIds={getPreviousResearchNodeIds} nodeTargetSelector="[data-research-node]" testIdPrefix="research"
        renderNode={renderResearchNode} />
    </Box>
  );
}
