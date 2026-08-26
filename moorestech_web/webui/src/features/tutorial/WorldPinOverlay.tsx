import { useMemo } from "react";
import { useViewportSize } from "@mantine/hooks";
import { Topics, useTopic, type WorldPinPresentationData } from "@/bridge";
import { challengeTutorialTextKey, useI18n } from "@/shared/i18n";
import { useBlockingSkitActive } from "@/shared/uiState";
import styles from "./worldPin.module.css";

type WorldPin = WorldPinPresentationData["pins"][number];

// Unityが射影済みの正規化座標を描くだけで、3D射影の知識は持たない
// Renders Unity-projected normalized coordinates only; no 3D projection knowledge here
export function WorldPinOverlay() {
  const data = useTopic(Topics.worldPins);
  // ピンはPortal層で会話窓より上に来るため、blockingスキット中は演出を専有させて引っ込む
  // Pins paint above the dialogue window from the portal layer, so they withdraw and let a blocking skit own the screen
  const blockingSkitActive = useBlockingSkitActive();
  const { width, height } = useViewportSize();
  // 矢印は--ui-scaleで拡大するため端クランプの余白も同率で広げないと画面外へ食い込む
  // The arrow grows with --ui-scale, so the edge-clamp margin must widen at the same rate or the arrow overruns the screen
  const edgeMargin = useMemo(() => readEdgeMargin() * readUiScale(), [width, height]);
  if (blockingSkitActive) return null;
  if (!data || data.pins.length === 0) return null;
  return (
    <div className={styles.overlay} data-testid="world-pin-overlay">
      {data.pins.map((pin) => pin.onScreen
        ? <OnScreenPin key={pin.pinId} pin={pin} />
        : <EdgeArrow key={pin.pinId} pin={pin} width={width} height={height} edgeMargin={edgeMargin} />)}
    </div>
  );
}

function OnScreenPin({ pin }: { pin: WorldPin }) {
  const { t } = useI18n();
  return (
    <div className={styles.pin} data-testid={`world-pin-${pin.pinId}`}
      style={{ left: `${pin.screenX * 100}%`, top: `${pin.screenY * 100}%` }}>
      <div className={styles.label}>{t(challengeTutorialTextKey(pin.tutorialGuid))}</div>
      <svg className={styles.marker} viewBox="0 0 24 24" aria-hidden="true">
        <path d="M12 22 L5 10 A8 8 0 1 1 19 10 Z" />
      </svg>
    </div>
  );
}

// 方向を画面端へクランプ配置
// Clamp toward the screen edge
function EdgeArrow({ pin, width, height, edgeMargin }: { pin: WorldPin; width: number; height: number; edgeMargin: number }) {
  if (width === 0 || height === 0) return null;
  const maxX = width / 2 - edgeMargin;
  const maxY = height / 2 - edgeMargin;
  const scaleX = Math.abs(pin.directionX) > 0.001 ? maxX / Math.abs(pin.directionX) : Number.MAX_VALUE;
  const scaleY = Math.abs(pin.directionY) > 0.001 ? maxY / Math.abs(pin.directionY) : Number.MAX_VALUE;
  const scale = Math.min(scaleX, scaleY);
  const left = width / 2 + pin.directionX * scale;
  const top = height / 2 + pin.directionY * scale;
  const angle = (Math.atan2(pin.directionY, pin.directionX) * 180) / Math.PI;
  return (
    <div className={styles.arrow} data-testid={`world-pin-arrow-${pin.pinId}`}
      style={{ left, top, transform: `translate(-50%, -50%) rotate(${angle}deg) scale(var(--ui-scale, 1))` }}>
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M2 8 H13 V3 L22 12 L13 21 V16 H2 Z" />
      </svg>
    </div>
  );
}

let cachedEdgeMargin: number | null = null;

// クランプ計算はJSで行うため、固定長トークンをCSS変数から読み取り単一の値源を保つ
// The clamp math runs in JS, so read the fixed-length token from the CSS variable to keep one source
// トークンは実行中に変わらないため初回読み取りをキャッシュし、毎レンダーのgetComputedStyleを避ける
// The token never changes at runtime, so cache the first read and avoid per-render getComputedStyle
function readEdgeMargin(): number {
  if (cachedEdgeMargin !== null) return cachedEdgeMargin;
  const raw = getComputedStyle(document.documentElement).getPropertyValue("--world-pin-edge-margin");
  const parsedMargin = Number.parseFloat(raw);
  if (!Number.isFinite(parsedMargin) || parsedMargin <= 0) {
    throw new Error("--world-pin-edge-margin must be a positive CSS length");
  }
  cachedEdgeMargin = parsedMargin;
  return cachedEdgeMargin;
}

// 余白と違い--ui-scaleはリサイズで変わるためキャッシュせず、viewport変化時の再計算だけで読み直す
// Unlike the margin, --ui-scale changes on resize, so it is read fresh on each viewport change instead of cached
function readUiScale(): number {
  const parsedScale = Number.parseFloat(getComputedStyle(document.documentElement).getPropertyValue("--ui-scale"));
  return Number.isFinite(parsedScale) && parsedScale > 0 ? parsedScale : 1;
}
