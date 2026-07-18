import { clamp01 } from "@/shared/clamp01";

// 長押しクラフトの経過時間モデル。uGUI CraftButton.Update の累積ロジックを純関数へ移植
// Pure time-accumulation model for hold-to-craft; ported from uGUI CraftButton.Update
type HoldCraftStep = { elapsed: number; didCraft: boolean };

// バックグラウンド復帰時の巨大 delta で連続発火が暴走しないよう1フレーム進行量を上限で丸める
// Cap per-frame progression so a huge delta after a background tab resume cannot burst-fire crafts
export const MAX_FRAME_DELTA_SECONDS = 0.25;

// craftTime 到達で1回発火し経過を0へ戻す（=連続クラフト）
// On reaching craftTime, fire one craft and reset elapsed to 0 (continuous craft)
export function advanceHoldCraft(elapsed: number, deltaSeconds: number, craftTime: number): HoldCraftStep {
  const next = elapsed + deltaSeconds;

  // craftTime<=0 は毎tick即発火。1tick最大1クラフトなので rAF 頻度に律速される
  // craftTime<=0 fires every tick; capped to one craft per tick so it is rate-limited by rAF
  if (next >= craftTime) return { elapsed: 0, didCraft: true };
  return { elapsed: next, didCraft: false };
}

// 進捗率 0..1。craftTime<=0 は常に満杯扱い（uGUI SetProgressAllow 相当）
// Progress ratio 0..1; craftTime<=0 is treated as full (mirrors uGUI SetProgressAllow)
export function holdCraftProgress(elapsed: number, craftTime: number): number {
  if (craftTime <= 0) return 1;
  return clamp01(elapsed / craftTime);
}
