// 装備選択の純粋ロジック。実スロット間の循環移動とホイール量の累積を担う
// Pure equipment-selection logic: circular cycling across real slots, plus wheel accumulation

// 0..slotCount-1 を周期 slotCount で循環させる（負の delta でも同じ環を逆走する）
// Cycle 0..slotCount-1 over a period of slotCount, walking the same ring backwards for negative delta
export function cycleEquipment(current: number, delta: number, slotCount: number): number {
  return (((current + delta) % slotCount) + slotCount) % slotCount;
}

// deltaModeごとのノッチ換算量。OSやデバイスでdelta値の桁が変わるため単位側で正規化する
// Per-deltaMode notch scale; delta magnitude varies by OS and device, so normalize at the unit level
const WHEEL_NOTCH_UNIT: Record<number, number> = {
  0: 100, // pixel
  1: 3, // line
  2: 1, // page
};

// ホイールを累積し、閾値を越えたら段数に関わらず1段だけ進める（スクロール加速で1ノッチが多段化しないように）
// Accumulate the wheel and advance exactly one step past the threshold, so scroll acceleration cannot turn one notch into many
export function accumulateWheelSteps(remainder: number, delta: number, deltaMode: number): { remainder: number; steps: number } {
  const unit = WHEEL_NOTCH_UNIT[deltaMode] ?? WHEEL_NOTCH_UNIT[0];
  const normalized = delta / unit;
  // 逆回転では溜まっていた順方向の端数を捨て、直前の回転の残りで即発火させない
  // On a reversal, discard the opposite-signed leftover so the previous rotation cannot fire immediately
  const base = remainder * normalized < 0 ? 0 : remainder;
  const total = base + normalized;
  if (total >= 1) return { remainder: 0, steps: 1 };
  if (total <= -1) return { remainder: 0, steps: -1 };
  return { remainder: total, steps: 0 };
}
