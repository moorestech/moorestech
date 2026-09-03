import { useCallback, useEffect, useLayoutEffect, useMemo, useRef, useState } from "react";
import type { ReactNode } from "react";
import { computeTreeCanvasBounds, lineBetween, toTreeCanvasPoint } from "./treeGeometry";
import type { TreePoint } from "./treeGeometry";
import { NODE_ID_ATTRIBUTE, useTreePanGesture } from "./useTreePanGesture";
import { centerViewportOn, loadStoredViewport, saveStoredViewport, toContentBox, toCssScale, usePanInertia, zoomViewportAt } from "./viewport";
import styles from "./TreeView.module.css";

type Props<T> = {
  nodes: T[];
  getId: (node: T) => string;
  getPosition: (node: T) => TreePoint;
  getPrevIds: (node: T) => string[];
  renderNode: (node: T, point: TreePoint) => ReactNode;
  // タップ判定時に通知(省略で選択なし)
  // Reports a tap-classified press (omit for trees with no selection)
  // ビューポートが押下時にポインタを捕捉するため、ノード内のnative clickは届かない。押下由来の操作はここへ配線する
  // The viewport captures the pointer at press time, so a native click inside a node never fires; wire press-driven actions here
  onNodeTap?: (node: T) => void;
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

export default function TreeView<T>(props: Props<T>) {
  const { nodes, getId, getPosition, getPrevIds, renderNode, onNodeTap, testIdPrefix, viewportKey, initialFocus } = props;
  const [storedAtMount] = useState(() => (viewportKey ? loadStoredViewport(viewportKey) : null));
  const [viewport, setViewport] = useState(storedAtMount ?? { x: 0, y: 0, scale: 1 });
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
  // 掴み操作はフックへ委譲、描画に徹する
  // The hook owns the grab gesture; this component sticks to rendering
  const gesture = useTreePanGesture({ panBy, inertia, byId, onNodeTap });
  // 接続線は描画関数に依らない。選択のたび作り直さないようノードと別のキャッシュにする
  // Lines do not depend on the node renderer, so they cache apart from nodes and survive every selection toggle
  const renderedLines = useMemo(() => (
    nodes.flatMap((node) => getPrevIds(node).map((prevId) => {
      const prev = byId.get(prevId);
      if (!prev) return null;
      const line = lineBetween(toTreeCanvasPoint(getPosition(node), bounds), toTreeCanvasPoint(getPosition(prev), bounds));
      return <div key={`${getId(node)}-${prevId}`} className={styles.line}
        style={{ left: line.x, top: line.y, width: line.length, transform: `rotate(${line.angleDeg}deg)` }} />;
    }))
  ), [bounds, byId, getId, getPosition, getPrevIds, nodes]);
  // ノードは意味のある入力が変わるまで同じReact要素を再利用する
  // Reuse node React elements until a semantic input changes
  const renderedNodes = useMemo(() => (
    nodes.map((node) => <div key={getId(node)} {...{ [NODE_ID_ATTRIBUTE]: getId(node) }}>
      {renderNode(node, toTreeCanvasPoint(getPosition(node), bounds))}
    </div>)
  ), [bounds, getId, getPosition, nodes, renderNode]);

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
    const contentBox = toContentBox(element);
    // データ到着前にズーム済みの場合もあるため現在のscaleを保つ
    // Keep the current scale in case the user zoomed before data arrived
    setViewport((current) => centerViewportOn(toTreeCanvasPoint(initialFocus, bounds),
      { width: contentBox.width, height: contentBox.height }, current.scale));
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
      const contentBox = toContentBox(element);
      setViewport((current) => zoomViewportAt(current, {
        x: (event.clientX - rect.left) * scale - contentBox.left, y: (event.clientY - rect.top) * scale - contentBox.top,
      }, event.deltaY));
    };
    element.addEventListener("wheel", handleWheel, { passive: false });
    return () => element.removeEventListener("wheel", handleWheel);
  }, [inertia]);

  return (
    <div ref={viewportElement} className={`${styles.viewport} ${gesture.isPanning ? styles.viewportPanning : ""}`}
      data-testid={`${testIdPrefix}-viewport`} {...gesture.viewportHandlers}>
      <div className={styles.canvas} data-testid={`${testIdPrefix}-canvas`}
        style={{ width: bounds.width, height: bounds.height, transform: `translate(${viewport.x}px, ${viewport.y}px) scale(${viewport.scale})` }}>
        {renderedLines}
        {renderedNodes}
      </div>
    </div>
  );
}
