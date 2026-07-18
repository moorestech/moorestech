import { useCallback, useEffect, useRef, useState } from "react";
import { advanceHoldCraft, holdCraftProgress, MAX_FRAME_DELTA_SECONDS } from "./holdCraftLogic";

type HoldCraft = {
  progress: number;
  isHolding: boolean;
  start: () => void;
  stop: () => void;
};

// ボタン長押し中に craftTime ごとにクラフトを発火する rAF 駆動フック（uGUI CraftButton 相当）。
// requestAnimationFrame-driven hook that fires a craft every craftTime while the button is held (mirrors uGUI CraftButton).
export function useHoldCraft(craftTime: number, craftable: boolean, onCraft: () => void): HoldCraft {
  // 毎レンダーで最新値を ref へ反映し、rAF ループを再起動せず最新の craftTime/craftable/onCraft を参照する
  // Mirror the latest values into refs each render so the rAF loop reads current craftTime/craftable/onCraft without restarting
  const craftTimeRef = useRef(craftTime);
  const craftableRef = useRef(craftable);
  const onCraftRef = useRef(onCraft);
  craftTimeRef.current = craftTime;
  craftableRef.current = craftable;
  onCraftRef.current = onCraft;

  const [progress, setProgress] = useState(0);
  const [isHolding, setIsHolding] = useState(false);
  const elapsedRef = useRef(0);
  const rafRef = useRef<number | null>(null);
  const lastTimeRef = useRef(0);

  const stop = useCallback(() => {
    if (rafRef.current !== null) {
      cancelAnimationFrame(rafRef.current);
      rafRef.current = null;
    }
    elapsedRef.current = 0;
    setProgress(0);
    setIsHolding(false);
  }, []);

  const start = useCallback(() => {
    // 二重押下・素材不足での開始を弾く
    // Reject double-press and start-while-uncraftable
    if (rafRef.current !== null || !craftableRef.current) return;

    elapsedRef.current = 0;
    lastTimeRef.current = performance.now();
    setIsHolding(true);
    setProgress(0);

    const loop = (now: number) => {
      // フレーム間隔を秒へ換算し、上限で丸めてから経過に加算する
      // Convert the frame interval to seconds, cap it, then accumulate
      const delta = Math.min((now - lastTimeRef.current) / 1000, MAX_FRAME_DELTA_SECONDS);
      lastTimeRef.current = now;

      // 保持中に素材が尽きたら経過をリセットして待機（補充されれば再開）
      // If materials run out mid-hold, reset elapsed and idle until they are replenished
      if (craftableRef.current) {
        const step = advanceHoldCraft(elapsedRef.current, delta, craftTimeRef.current);
        elapsedRef.current = step.elapsed;
        if (step.didCraft) onCraftRef.current();
      } else {
        elapsedRef.current = 0;
      }

      setProgress(holdCraftProgress(elapsedRef.current, craftTimeRef.current));
      rafRef.current = requestAnimationFrame(loop);
    };
    rafRef.current = requestAnimationFrame(loop);
  }, []);

  // 素材が尽きてボタンが disabled 化すると pointerup を受け取れず stop できないため、craftable が落ちた時点で長押しを打ち切る（uGUI ResetButton 相当）
  // A disabled button can't receive pointerup, so abort the hold the moment craftable drops (mirrors uGUI ResetButton)
  useEffect(() => {
    if (!craftable && rafRef.current !== null) stop();
  }, [craftable, stop]);

  // アンマウント時に rAF を確実に破棄する
  // Ensure the rAF is torn down on unmount
  useEffect(() => stop, [stop]);

  return { progress, isHolding, start, stop };
}
