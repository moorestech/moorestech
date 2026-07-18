import { useEffect, useMemo, useRef, useState } from "react";
import type { PointerEvent, ReactNode } from "react";
import { computeTreeCanvasBounds, lineBetween, toTreeCanvasPoint } from "./treeGeometry";
import type { TreePoint } from "./treeGeometry";
import { zoomViewportAt } from "./viewport";
import styles from "./TreeView.module.css";

type PanPointer = { pointerId: number; clientX: number; clientY: number };
type Props<T> = {
  nodes: T[];
  getId: (node: T) => string;
  getPosition: (node: T) => TreePoint;
  getPrevIds: (node: T) => string[];
  renderNode: (node: T, point: TreePoint) => ReactNode;
  nodeTargetSelector: string;
  testIdPrefix: string;
};

const toCssScale = (element: HTMLDivElement) => element.offsetWidth / element.getBoundingClientRect().width;

export default function TreeView<T>(props: Props<T>) {
  const { nodes, getId, getPosition, getPrevIds, renderNode, nodeTargetSelector, testIdPrefix } = props;
  const [viewport, setViewport] = useState({ x: 0, y: 0, scale: 1 });
  const [isPanning, setIsPanning] = useState(false);
  const panPointer = useRef<PanPointer | null>(null);
  const viewportElement = useRef<HTMLDivElement | null>(null);
  const bounds = useMemo(
    () => computeTreeCanvasBounds(nodes.map((node) => ({ id: getId(node), ...getPosition(node) })), 200),
    [nodes, getId, getPosition],
  );
  const byId = useMemo(() => new Map(nodes.map((node) => [getId(node), node])), [nodes, getId]);

  useEffect(() => {
    const element = viewportElement.current;
    if (!element) return;
    const handleWheel = (event: WheelEvent) => {
      event.preventDefault();
      const rect = element.getBoundingClientRect();
      const scale = toCssScale(element);
      setViewport((current) => zoomViewportAt(current, {
        x: (event.clientX - rect.left) * scale, y: (event.clientY - rect.top) * scale,
      }, event.deltaY));
    };
    element.addEventListener("wheel", handleWheel, { passive: false });
    return () => element.removeEventListener("wheel", handleWheel);
  }, []);

  const handlePointerDown = (event: PointerEvent<HTMLDivElement>) => {
    const target = event.target;
    if (!event.isPrimary || event.button !== 0 || (target instanceof Element && target.closest(nodeTargetSelector))) return;
    event.currentTarget.setPointerCapture(event.pointerId);
    panPointer.current = { pointerId: event.pointerId, clientX: event.clientX, clientY: event.clientY };
    setIsPanning(true);
  };
  const handlePointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const pan = panPointer.current;
    if (!pan || pan.pointerId !== event.pointerId) return;
    const scale = toCssScale(event.currentTarget);
    setViewport((current) => ({
      ...current, x: current.x + (event.clientX - pan.clientX) * scale, y: current.y + (event.clientY - pan.clientY) * scale,
    }));
    panPointer.current = { pointerId: event.pointerId, clientX: event.clientX, clientY: event.clientY };
  };
  const handlePointerEnd = (event: PointerEvent<HTMLDivElement>) => {
    if (panPointer.current?.pointerId !== event.pointerId) return;
    panPointer.current = null;
    setIsPanning(false);
  };

  return (
    <div ref={viewportElement} className={`${styles.viewport} ${isPanning ? styles.viewportPanning : ""}`}
      data-testid={`${testIdPrefix}-viewport`} onPointerDown={handlePointerDown} onPointerMove={handlePointerMove}
      onPointerUp={handlePointerEnd} onPointerCancel={handlePointerEnd} onLostPointerCapture={handlePointerEnd}>
      <div className={styles.canvas} data-testid={`${testIdPrefix}-canvas`}
        style={{ width: bounds.width, height: bounds.height, transform: `translate(${viewport.x}px, ${viewport.y}px) scale(${viewport.scale})` }}>
        {nodes.flatMap((node) => getPrevIds(node).map((prevId) => {
          const prev = byId.get(prevId);
          if (!prev) return null;
          const line = lineBetween(toTreeCanvasPoint(getPosition(node), bounds), toTreeCanvasPoint(getPosition(prev), bounds));
          return <div key={`${getId(node)}-${prevId}`} className={styles.line}
            style={{ left: line.x, top: line.y, width: line.length, transform: `rotate(${line.angleDeg}deg)` }} />;
        }))}
        {nodes.map((node) => <div key={getId(node)}>{renderNode(node, toTreeCanvasPoint(getPosition(node), bounds))}</div>)}
      </div>
    </div>
  );
}
