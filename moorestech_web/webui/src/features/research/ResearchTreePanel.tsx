import { useMemo } from "react";
import { Box, ScrollArea, Title } from "@mantine/core";
import { useTopic, Topics, useItemMaster } from "@/bridge";
import type { ResearchNodeData } from "@/bridge/contract/payloadTypes";
import { computeCanvasBounds, lineBetween, buildOwnedCounts } from "./researchLogic";
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

  const bounds = useMemo(() => computeCanvasBounds(nodes), [nodes]);
  const byGuid = useMemo(() => new Map(nodes.map((n) => [n.guid, n])), [nodes]);
  const owned = useMemo(
    () => buildOwnedCounts([...(inventory?.mainSlots ?? []), ...(inventory?.hotbarSlots ?? [])]),
    [inventory],
  );
  const resolveName = (itemId: number) => itemMaster?.get(itemId)?.name;

  return (
    <Box className={styles.panel} data-testid="research-tree">
      <Title order={2} size="h4" p="sm">研究ツリー</Title>
      <ScrollArea className={styles.scroll} type="auto">
        <div className={styles.canvas} style={{ width: bounds.width, height: bounds.height }}>
          {/* 接続線: 子ノード → 前提ノードへ距離+角度の棒を引く（最背面） */}
          {/* Connection lines: length+angle bars from child to prerequisite (behind nodes) */}
          {nodes.flatMap((node) =>
            node.prevGuids.map((prevGuid) => {
              const prev = byGuid.get(prevGuid);
              if (!prev) return null;
              const line = lineBetween(
                { x: node.position.x + bounds.offsetX, y: bounds.offsetY - node.position.y },
                { x: prev.position.x + bounds.offsetX, y: bounds.offsetY - prev.position.y },
              );
              return (
                <div
                  key={`${node.guid}-${prevGuid}`}
                  className={styles.line}
                  style={{ left: line.x, top: line.y, width: line.length, transform: `rotate(${line.angleDeg}deg)` }}
                />
              );
            }),
          )}
          {nodes.map((node) => (
            <ResearchNodeCard
              key={node.guid}
              node={node}
              left={node.position.x + bounds.offsetX}
              top={bounds.offsetY - node.position.y}
              owned={owned}
              resolveName={resolveName}
            />
          ))}
        </div>
      </ScrollArea>
    </Box>
  );
}
