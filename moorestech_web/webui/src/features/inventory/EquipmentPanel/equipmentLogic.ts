// 装備選択の純粋ロジック。素手(-1)を含む循環移動とホイール量の累積を担う
// Pure equipment-selection logic: circular cycling that includes bare hands (-1), plus wheel accumulation

// C#の素手定数と同値
// Matches the C# bare-hands constant
export const BARE_HANDS_INDEX = -1;

// -1..slotCount-1 を周期 slotCount+1 で循環させる（負の delta でも同じ環を逆走する）
// Cycle -1..slotCount-1 over a period of slotCount+1, walking the same ring backwards for negative delta
export function cycleEquipment(current: number, delta: number, slotCount: number): number {
  const period = slotCount + 1;
  // 素手を先頭に寄せた 0..slotCount の序数へ移してから剰余を取る
  // Shift to a 0..slotCount ordinal with bare hands first, then take the remainder there
  const ordinal = current - BARE_HANDS_INDEX;
  return ((((ordinal + delta) % period) + period) % period) + BARE_HANDS_INDEX;
}

// wheel/100を累積し整数分だけを消費する（旧uGUIホットバーの刻みをそのまま装備側へ引き継ぐ）
// Accumulate wheel/100 and consume only crossed integer steps, carrying over the old uGUI hotbar's granularity
export function accumulateWheelSteps(remainder: number, delta: number): { remainder: number; steps: number } {
  const total = remainder + delta / 100;
  if (total >= 1) {
    const steps = Math.floor(total);
    return { remainder: total - steps, steps };
  }
  if (total <= -1) {
    const steps = Math.ceil(total);
    return { remainder: total - steps, steps };
  }
  return { remainder: total, steps: 0 };
}
