import { useMemo } from "react";
import { Box, Title } from "@mantine/core";
import { useTopic, Topics, useItemMaster } from "@/bridge";
import type { ResearchNodeData } from "@/bridge";
import { buildOwnedCounts } from "@/shared/ownedCounts";
import { TreeView } from "@/shared/treeView";
import ResearchNodeCard from "./ResearchNodeCard";
import styles from "./style.module.css";

// topic未受信時の空配列を固定参照にしてuseMemoの空振りを防ぐ
// Stable empty-array reference so useMemo doesn't recompute every render before the topic arrives
const EMPTY_NODES: ResearchNodeData[] = [];

// 研究ツリー全画面表示
// Full-screen research tree panel
export default function ResearchTreePanel() {
  const tree = useTopic(Topics.researchTree);
  const inventory = useTopic(Topics.inventory);
  const itemMaster = useItemMaster();
  const nodes = tree?.nodes ?? EMPTY_NODES;
  const owned = useMemo(
    () => buildOwnedCounts([...(inventory?.mainSlots ?? []), ...(inventory?.hotbarSlots ?? [])]),
    [inventory],
  );
  const resolveName = (itemId: number) => itemMaster?.get(itemId)?.name;

  return (
    <Box className={styles.panel} data-testid="research-tree">
      <Title order={2} size="h4" p="sm">研究ツリー</Title>
      <TreeView nodes={nodes} getId={(node) => node.guid} getPosition={(node) => node.position}
        getPrevIds={(node) => node.prevGuids} nodeTargetSelector="[data-research-node]" testIdPrefix="research"
        renderNode={(node, point) => (
            <ResearchNodeCard
              node={node}
              left={point.x}
              top={point.y}
              owned={owned}
              resolveName={resolveName}
            />
        )} />
    </Box>
  );
}
