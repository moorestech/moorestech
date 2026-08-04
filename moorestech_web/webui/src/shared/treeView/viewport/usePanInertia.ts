import { useEffect, useMemo, useRef } from "react";
import {
  PAN_MAX_FRAME_DELTA_MS,
  PAN_MIN_FLING_SPEED,
  PAN_RELEASE_STALL_MS,
  PAN_STOP_SPEED,
  PAN_VELOCITY_SAMPLE_MAX_GAP_MS,
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

// ドラッグ速度追跡、離した後rAFで慣性減衰パン継続
// Tracks drag velocity, keeps inertial panning via rAF after release
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
      // 間隔が異常な時は速度を作らない
      // Skip velocity on abnormal sample gaps
      if (dt === null || dt <= 0 || dt > PAN_VELOCITY_SAMPLE_MAX_GAP_MS) {
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
        const dt = Math.min(Math.max(frameAt - lastFrameAt, 0), PAN_MAX_FRAME_DELTA_MS);
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
