import { useViewportSize } from "@mantine/hooks";
import { Topics, useTopic, type WorldPinPresentationData } from "@/bridge";
import styles from "./worldPin.module.css";

type WorldPin = WorldPinPresentationData["pins"][number];

// Unityが射影済みの正規化座標を描くだけで、3D射影の知識は持たない
// Renders Unity-projected normalized coordinates only; no 3D projection knowledge here
export function WorldPinOverlay() {
  const data = useTopic(Topics.worldPins);
  const { width, height } = useViewportSize();
  if (!data || data.pins.length === 0) return null;
  return (
    <div className={styles.overlay} data-testid="world-pin-overlay">
      {data.pins.map((pin) => pin.onScreen
        ? <OnScreenPin key={pin.pinId} pin={pin} />
        : <EdgeArrow key={pin.pinId} pin={pin} width={width} height={height} />)}
    </div>
  );
}

function OnScreenPin({ pin }: { pin: WorldPin }) {
  return (
    <div className={styles.pin} data-testid={`world-pin-${pin.pinId}`}
      style={{ left: `${pin.screenX * 100}%`, top: `${pin.screenY * 100}%` }}>
      <div className={styles.label}>{pin.text}</div>
      <svg className={styles.marker} viewBox="0 0 24 24" aria-hidden="true">
        <path d="M12 22 L5 10 A8 8 0 1 1 19 10 Z" />
      </svg>
    </div>
  );
}

// 方向ベクトルを固定マージンの画面端矩形へクランプし、方向へ回転したシェブロンを置く
// Clamp the direction vector onto the fixed-margin screen rectangle and rotate a chevron toward it
function EdgeArrow({ pin, width, height }: { pin: WorldPin; width: number; height: number }) {
  if (width === 0 || height === 0) return null;
  const margin = readEdgeMargin();
  const maxX = width / 2 - margin;
  const maxY = height / 2 - margin;
  const scaleX = Math.abs(pin.directionX) > 0.001 ? maxX / Math.abs(pin.directionX) : Number.MAX_VALUE;
  const scaleY = Math.abs(pin.directionY) > 0.001 ? maxY / Math.abs(pin.directionY) : Number.MAX_VALUE;
  const scale = Math.min(scaleX, scaleY);
  const left = width / 2 + pin.directionX * scale;
  const top = height / 2 + pin.directionY * scale;
  const angle = (Math.atan2(pin.directionY, pin.directionX) * 180) / Math.PI;
  return (
    <div className={styles.arrow} data-testid={`world-pin-arrow-${pin.pinId}`}
      style={{ left, top, transform: `translate(-50%, -50%) rotate(${angle}deg)` }}>
      <svg viewBox="0 0 24 24" aria-hidden="true">
        <path d="M8 4 L18 12 L8 20" />
      </svg>
    </div>
  );
}

const FALLBACK_EDGE_MARGIN_PX = 28;

// クランプ計算はJSで行うため、固定長トークンをCSS変数から読み取り単一の値源を保つ
// The clamp math runs in JS, so read the fixed-length token from the CSS variable to keep one source
function readEdgeMargin(): number {
  const raw = getComputedStyle(document.documentElement).getPropertyValue("--world-pin-edge-margin");
  const value = Number.parseFloat(raw);
  return Number.isNaN(value) ? FALLBACK_EDGE_MARGIN_PX : value;
}
