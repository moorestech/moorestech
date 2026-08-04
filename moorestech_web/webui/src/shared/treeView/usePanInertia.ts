import { useEffect, useMemo, useRef } from "react";
import {
  PAN_MIN_FLING_SPEED,
  PAN_RELEASE_STALL_MS,
  PAN_STOP_SPEED,
  blendPanVelocity,
  clampPanVelocity,
  decayPanVelocity,
} from "./viewport";
import type { PanVelocity } from "./viewport";

type ApplyPan = (dx: number, dy: number) => void;
export type PanInertia = {
  trackMove: (dx: number, dy: number) => void;
  release: () => void;
  cancel: () => void;
};

// ドラッグ速度を追跡し、離した後にrAFで慣性減衰パンを続けるフック
// Hook that tracks drag velocity and keeps panning with inertial decay via rAF after release
export function usePanInertia(applyPan: ApplyPan): PanInertia {
  const applyRef = useRef(applyPan);
  applyRef.current = applyPan;
  const velocity = useRef<PanVelocity>({ x: 0, y: 0 });
  const lastMoveAt = useRef<number | null>(null);
  const frame = useRef<number | null>(null);

  const inertia = useMemo<PanInertia>(() => {
    const cancel = () => {
      if (frame.current !== null) {
        cancelAnimationFrame(frame.current);
        frame.current = null;
      }
      velocity.current = { x: 0, y: 0 };
      lastMoveAt.current = null;
    };
    const trackMove = (dx: number, dy: number) => {
      const now = performance.now();
      const dt = lastMoveAt.current === null ? null : now - lastMoveAt.current;
      lastMoveAt.current = now;
      // サンプル間隔が空きすぎ/詰まりすぎの時は速度を作らない
      // Skip velocity when the sample gap is too large or too small
      if (dt === null || dt <= 0 || dt > PAN_RELEASE_STALL_MS) {
        velocity.current = { x: 0, y: 0 };
        return;
      }
      velocity.current = blendPanVelocity(velocity.current, dx, dy, dt);
    };
    const release = () => {
      const releasedAt = performance.now();
      // 静止してから離した場合は滑走させない
      // No fling when the pointer was held still before release
      if (lastMoveAt.current === null || releasedAt - lastMoveAt.current > PAN_RELEASE_STALL_MS) return;
      let flying = clampPanVelocity(velocity.current);
      if (Math.hypot(flying.x, flying.y) < PAN_MIN_FLING_SPEED) return;
      let lastFrameAt = releasedAt;
      const step = (frameAt: number) => {
        // タブ非アクティブ等の巨大フレーム間隔で吹き飛ばないよう上限を設ける
        // Cap the frame delta so a background-tab gap doesn't teleport the view
        const dt = Math.min(Math.max(frameAt - lastFrameAt, 0), 64);
        lastFrameAt = frameAt;
        applyRef.current(flying.x * dt, flying.y * dt);
        flying = decayPanVelocity(flying, dt);
        if (Math.hypot(flying.x, flying.y) < PAN_STOP_SPEED) {
          frame.current = null;
          return;
        }
        frame.current = requestAnimationFrame(step);
      };
      frame.current = requestAnimationFrame(step);
    };
    return { trackMove, release, cancel };
  }, []);

  useEffect(() => inertia.cancel, [inertia]);
  return inertia;
}
