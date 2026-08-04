import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import type { PointerEvent, ReactNode } from "react";
import { computeTreeCanvasBounds, lineBetween, toTreeCanvasPoint } from "./treeGeometry";
import type { TreePoint } from "./treeGeometry";
import { centerViewportOn, zoomViewportAt } from "./viewport";
import { loadStoredViewport, saveStoredViewport } from "./viewportStore";
import { usePanInertia } from "./usePanInertia";
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
  // セッション内でパン・ズームを保持するキー（省略時は保持しない）
  // Key for in-session viewport persistence (omit to disable)
  viewportKey?: string;
  // 保存が無い初回に中央へ据えるツリー座標（nullで中央寄せなし）
  // Tree-space point centered on first open without a stored viewport (null = no centering)
  initialFocus?: TreePoint | null;
};

const toCssScale = (element: HTMLDivElement) => element.offsetWidth / element.getBoundingClientRect().width;

export default function TreeView<T>(props: Props<T>) {
  const { nodes, getId, getPosition, getPrevIds, renderNode, nodeTargetSelector, testIdPrefix, viewportKey, initialFocus } = props;
  const [storedAtMount] = useState(() => (viewportKey ? loadStoredViewport(viewportKey) : null));
  const [viewport, setViewport] = useState(storedAtMount ?? { x: 0, y: 0, scale: 1 });
  const [isPanning, setIsPanning] = useState(false);
  const panPointer = useRef<PanPointer | null>(null);
  const viewportElement = useRef<HTMLDivElement | null>(null);
  const applyInertiaPan = useCallback((dx: number, dy: number) => {
    setViewport((current) => ({ ...current, x: current.x + dx, y: current.y + dy }));
  }, []);
  const inertia = usePanInertia(applyInertiaPan);
  const bounds = useMemo(
    () => computeTreeCanvasBounds(nodes.map((node) => ({ id: getId(node), ...getPosition(node) })), 200),
    [nodes, getId, getPosition],
  );
  const byId = useMemo(() => new Map(nodes.map((node) => [getId(node), node])), [nodes, getId]);
  // ノードと接続線は意味のある入力が変わるまで同じReact要素を再利用する
  // Reuse node and connection React elements until a semantic input changes
  const renderedScene = useMemo(() => (
    <>
      {nodes.flatMap((node) => getPrevIds(node).map((prevId) => {
        const prev = byId.get(prevId);
        if (!prev) return null;
        const line = lineBetween(toTreeCanvasPoint(getPosition(node), bounds), toTreeCanvasPoint(getPosition(prev), bounds));
        return <div key={`${getId(node)}-${prevId}`} className={styles.line}
          style={{ left: line.x, top: line.y, width: line.length, transform: `rotate(${line.angleDeg}deg)` }} />;
      }))}
      {nodes.map((node) => <div key={getId(node)}>{renderNode(node, toTreeCanvasPoint(getPosition(node), bounds))}</div>)}
    </>
  ), [bounds, byId, getId, getPosition, getPrevIds, nodes, renderNode]);

  // 保存が無い初回のみ、最初のデータ到着時に注目点を中央へ据える（以後の状態変化で視界を飛ばさない）
  // Center once when data first arrives without a stored viewport (later state changes never jump the view)
  const hasCentered = useRef(false);
  useLayoutEffect(() => {
    if (hasCentered.current || storedAtMount || nodes.length === 0) return;
    const element = viewportElement.current;
    if (!element || element.offsetWidth === 0) return;
    hasCentered.current = true;
    if (!initialFocus) return;
    setViewport(centerViewportOn(toTreeCanvasPoint(initialFocus, bounds),
      { width: element.offsetWidth, height: element.offsetHeight }, 1));
  }, [storedAtMount, initialFocus, bounds, nodes]);

  // 操作で動いた時だけ保存する（マウント時の初期値は保存しない）
  // Save only when the viewport actually moved (skip the untouched mount value)
  const lastSaved = useRef(storedAtMount ?? viewport);
  useEffect(() => {
    if (!viewportKey || viewport === lastSaved.current) return;
    lastSaved.current = viewport;
    saveStoredViewport(viewportKey, viewport);
  }, [viewportKey, viewport]);

  useEffect(() => {
    const element = viewportElement.current;
    if (!element) return;
    const handleWheel = (event: WheelEvent) => {
      event.preventDefault();
      inertia.cancel();
      const rect = element.getBoundingClientRect();
      const scale = toCssScale(element);
      setViewport((current) => zoomViewportAt(current, {
        x: (event.clientX - rect.left) * scale, y: (event.clientY - rect.top) * scale,
      }, event.deltaY));
    };
    element.addEventListener("wheel", handleWheel, { passive: false });
    return () => element.removeEventListener("wheel", handleWheel);
  }, [inertia]);

  const handlePointerDown = (event: PointerEvent<HTMLDivElement>) => {
    const target = event.target;
    if (!event.isPrimary || event.button !== 0 || (target instanceof Element && target.closest(nodeTargetSelector))) return;
    inertia.cancel();
    event.currentTarget.setPointerCapture(event.pointerId);
    panPointer.current = { pointerId: event.pointerId, clientX: event.clientX, clientY: event.clientY };
    setIsPanning(true);
  };
  const handlePointerMove = (event: PointerEvent<HTMLDivElement>) => {
    const pan = panPointer.current;
    if (!pan || pan.pointerId !== event.pointerId) return;
    const scale = toCssScale(event.currentTarget);
    const dx = (event.clientX - pan.clientX) * scale;
    const dy = (event.clientY - pan.clientY) * scale;
    inertia.trackMove(dx, dy);
    setViewport((current) => ({ ...current, x: current.x + dx, y: current.y + dy }));
    panPointer.current = { pointerId: event.pointerId, clientX: event.clientX, clientY: event.clientY };
  };
  const handlePointerEnd = (event: PointerEvent<HTMLDivElement>) => {
    if (panPointer.current?.pointerId !== event.pointerId) return;
    panPointer.current = null;
    setIsPanning(false);
    inertia.release();
  };

  return (
    <div ref={viewportElement} className={`${styles.viewport} ${isPanning ? styles.viewportPanning : ""}`}
      data-testid={`${testIdPrefix}-viewport`} onPointerDown={handlePointerDown} onPointerMove={handlePointerMove}
      onPointerUp={handlePointerEnd} onPointerCancel={handlePointerEnd} onLostPointerCapture={handlePointerEnd}>
      <div className={styles.canvas} data-testid={`${testIdPrefix}-canvas`}
        style={{ width: bounds.width, height: bounds.height, transform: `translate(${viewport.x}px, ${viewport.y}px) scale(${viewport.scale})` }}>
        {renderedScene}
      </div>
    </div>
  );
}
