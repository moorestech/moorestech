import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import type { PointerEvent, ReactNode } from "react";
import { computeTreeCanvasBounds, lineBetween, toTreeCanvasPoint } from "./treeGeometry";
import type { TreePoint } from "./treeGeometry";
import { centerViewportOn, loadStoredViewport, saveStoredViewport, usePanInertia, zoomViewportAt } from "./viewport";
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
  // ビューポート保持キー(省略で無効)
  // マウント中は不変が契約。切り替える場合は key={viewportKey} で再マウントする
  // In-session viewport persistence key (omit to disable)
  // Contract: immutable while mounted; remount via key={viewportKey} to switch
  viewportKey?: string;
  // 初回の中央寄せ座標(nullで無効)
  // First-open centering point (null = no centering)
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
  // パン移動は両経路ともこの1本を通る
  // Drag and inertia both pan through this
  const panBy = useCallback((dx: number, dy: number) => {
    setViewport((current) => ({ ...current, x: current.x + dx, y: current.y + dy }));
  }, []);
  const inertia = usePanInertia(panBy);
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

  // 保存が無い初回のみ、注目点が定まった最初の機会に中央へ据える（以後の状態変化で視界を飛ばさない）
  // Center once when a focus point first exists without a stored viewport (later state changes never jump the view)
  // サーバー状態未着のtopic初回配信は注目点を持たないため、届くまで一度きりの権利を消費しない
  // The first topic push lacks server states and thus a focus point, so don't burn the one-shot before it arrives
  const hasCentered = useRef(false);
  useLayoutEffect(() => {
    if (hasCentered.current || storedAtMount || nodes.length === 0 || !initialFocus) return;
    const element = viewportElement.current;
    if (!element || element.offsetWidth === 0) return;
    hasCentered.current = true;
    // データ到着前にズーム済みの場合もあるため現在のscaleを保つ
    // Keep the current scale in case the user zoomed before data arrived
    setViewport((current) => centerViewportOn(toTreeCanvasPoint(initialFocus, bounds),
      { width: element.offsetWidth, height: element.offsetHeight }, current.scale));
  }, [storedAtMount, initialFocus, bounds, nodes]);

  // 初期値以外(センタリング・操作)を保存
  // Save all but the untouched mount value
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
    // ノード押下でも滑走停止(パン可否と独立)
    // A node press also stops the glide
    inertia.cancel();
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
    const dx = (event.clientX - pan.clientX) * scale;
    const dy = (event.clientY - pan.clientY) * scale;
    inertia.trackMove(dx, dy);
    panBy(dx, dy);
    panPointer.current = { pointerId: event.pointerId, clientX: event.clientX, clientY: event.clientY };
  };
  const endPan = (event: PointerEvent<HTMLDivElement>) => {
    if (panPointer.current?.pointerId !== event.pointerId) return false;
    panPointer.current = null;
    setIsPanning(false);
    return true;
  };
  // pointerupのみ滑走、他は中断
  // Only pointerup flings; the rest abort
  const handlePointerUp = (event: PointerEvent<HTMLDivElement>) => {
    if (endPan(event)) inertia.release();
  };
  const handlePointerAbort = (event: PointerEvent<HTMLDivElement>) => {
    if (endPan(event)) inertia.cancel();
  };

  return (
    <div ref={viewportElement} className={`${styles.viewport} ${isPanning ? styles.viewportPanning : ""}`}
      data-testid={`${testIdPrefix}-viewport`} onPointerDown={handlePointerDown} onPointerMove={handlePointerMove}
      onPointerUp={handlePointerUp} onPointerCancel={handlePointerAbort} onLostPointerCapture={handlePointerAbort}>
      <div className={styles.canvas} data-testid={`${testIdPrefix}-canvas`}
        style={{ width: bounds.width, height: bounds.height, transform: `translate(${viewport.x}px, ${viewport.y}px) scale(${viewport.scale})` }}>
        {renderedScene}
      </div>
    </div>
  );
}
