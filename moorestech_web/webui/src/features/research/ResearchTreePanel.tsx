import { useMemo, useRef, useState } from "react";
import type { PointerEvent, WheelEvent } from "react";
import { Box, Title } from "@mantine/core";
import { useTopic, Topics, useItemMaster } from "@/bridge";
import type { ResearchNodeData } from "@/bridge/contract/payloadTypes";
import { buildOwnedCounts } from "@/shared/ownedCounts";
import { computeCanvasBounds, lineBetween, zoomViewportAt } from "./researchLogic";
import ResearchNodeCard from "./ResearchNodeCard";
import styles from "./style.module.css";

// topic未受信時の空配列を固定参照にしてuseMemoの空振りを防ぐ
// Stable empty-array reference so useMemo doesn't recompute every render before the topic arrives
const EMPTY_NODES: ResearchNodeData[] = [];

type PanPointer = { pointerId: number; clientX: number; clientY: number };

// 研究ツリー全画面表示
// Full-screen research tree panel
export default function ResearchTreePanel() {
  const tree = useTopic(Topics.researchTree);
  const inventory = useTopic(Topics.inventory);
  const itemMaster = useItemMaster();
  const nodes = tree?.nodes ?? EMPTY_NODES;
  const [viewport, setViewport] = useState({ x: 0, y: 0, scale: 1 });
  const [isPanning, setIsPanning] = useState(false);
  const panPointer = useRef<PanPointer | null>(null);

  const bounds = useMemo(() => computeCanvasBounds(nodes), [nodes]);
  const byGuid = useMemo(() => new Map(nodes.map((n) => [n.guid, n])), [nodes]);
  const owned = useMemo(
    () => buildOwnedCounts([...(inventory?.mainSlots ?? []), ...(inventory?.hotbarSlots ?? [])]),
    [inventory],
  );
  const resolveName = (itemId: number) => itemMaster?.get(itemId)?.name;

  // stage縮小をCSS座標へ補正
  // Convert stage-scaled browser coordinates to logical CSS coordinates
  const toCssScale = (element: HTMLDivElement) => element.offsetWidth / element.getBoundingClientRect().width;

  // カーソル位置を基準にズーム
  // Zoom the canvas around the wheel position
  const handleWheel = (event: WheelEvent<HTMLDivElement>) => {
    event.preventDefault();
    const rect = event.currentTarget.getBoundingClientRect();
    const scale = toCssScale(event.currentTarget);
    setViewport((current) => zoomViewportAt(current, {
      x: (event.clientX - rect.left) * scale,
      y: (event.clientY - rect.top) * scale,
    }, event.deltaY));
  };

  // 空背景の左入力でパン開始
  // Start panning with a primary pointer on the empty background
  const handlePointerDown = (event: PointerEvent<HTMLDivElement>) => {
    const target = event.target;
    if (!event.isPrimary || event.button !== 0 || (target instanceof Element && target.closest("[data-research-node]"))) return;
    event.currentTarget.setPointerCapture(event.pointerId);
    panPointer.current = { pointerId: event.pointerId, clientX: event.clientX, clientY: event.clientY };
    setIsPanning(true);
  };

  // ポインター差分でパン移動
  // Pan using pointer deltas converted to logical CSS coordinates
  const handlePointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const pan = panPointer.current;
    if (!pan || pan.pointerId !== event.pointerId) return;
    const scale = toCssScale(event.currentTarget);
    setViewport((current) => ({
      ...current,
      x: current.x + (event.clientX - pan.clientX) * scale,
      y: current.y + (event.clientY - pan.clientY) * scale,
    }));
    panPointer.current = { pointerId: event.pointerId, clientX: event.clientX, clientY: event.clientY };
  };

  // 入力終了時にパン解除
  // Clear panning when the pointer ends, cancels, or loses capture
  const handlePointerEnd = (event: PointerEvent<HTMLDivElement>) => {
    if (panPointer.current?.pointerId !== event.pointerId) return;
    panPointer.current = null;
    setIsPanning(false);
  };

  return (
    <Box className={styles.panel} data-testid="research-tree">
      <Title order={2} size="h4" p="sm">研究ツリー</Title>
      <div
        className={`${styles.viewport} ${isPanning ? styles.viewportPanning : ""}`}
        data-testid="research-viewport"
        onWheel={handleWheel}
        onPointerDown={handlePointerDown}
        onPointerMove={handlePointerMove}
        onPointerUp={handlePointerEnd}
        onPointerCancel={handlePointerEnd}
        onLostPointerCapture={handlePointerEnd}
      >
        <div
          className={styles.canvas}
          data-testid="research-canvas"
          style={{
            width: bounds.width,
            height: bounds.height,
            transform: `translate(${viewport.x}px, ${viewport.y}px) scale(${viewport.scale})`,
          }}
        >
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
      </div>
    </Box>
  );
}
