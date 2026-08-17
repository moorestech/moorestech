import type { GearNetworkStopReason, MachineProcessState } from "@/bridge";
import { clamp01 } from "@/shared/clamp01";
import { L, type TranslationKey } from "@/shared/i18n";

// uGUI CommonMachineBlockStateDetail.PowerRate と同式（ワイヤ非送信のためWeb側算出）
// Same formula as uGUI CommonMachineBlockStateDetail.PowerRate (not on the wire; computed web-side)
export function computePowerRate(currentPower: number, requestPower: number): number {
  return requestPower === 0 ? 1 : currentPower / requestPower;
}

export function itemsPerMinute(outputCount: number, recipeTimeSeconds: number): number | null {
  return recipeTimeSeconds <= 0 ? null : outputCount * 60 / recipeTimeSeconds;
}

// itemSlots の統合indexを 入力→出力→モジュール に分割（uGUIのスロット構成順）
// Split combined itemSlots indices into input→output→module (uGUI slot ordering)
export function splitSlotIndices(
  layout: { input: number; output: number; module: number },
  total: number,
): { input: number[]; output: number[]; module: number[] } {
  const all = Array.from({ length: total }, (_, i) => i);
  const input = all.slice(0, layout.input);
  const output = all.slice(layout.input, layout.input + layout.output);
  const module = all.slice(layout.input + layout.output, layout.input + layout.output + layout.module);
  return { input, output, module };
}

// 残燃料/満燃料の比を 0..1 にクランプ（分母0は0扱い）。uGUI Generatorの燃料バー相当
// Clamp remaining/full fuel ratio to 0..1 (zero denominator → 0); mirrors the uGUI generator fuel bar
export function fuelRatio(remainingFuelTime: number, currentFuelTime: number): number {
  if (currentFuelTime <= 0) return 0;
  return clamp01(remainingFuelTime / currentFuelTime);
}

// uGUI GearEnergyTransformerUIView.GetStopReasonText と同文言
// Same wording as uGUI GearEnergyTransformerUIView.GetStopReasonText
export function stopReasonTranslationKey(reason: GearNetworkStopReason): TranslationKey | null {
  return GearStopReasonKeys[reason];
}

const GearStopReasonKeys: Record<GearNetworkStopReason, TranslationKey | null> = {
  none: null,
  rocked: L.ui.blockInventory.stopReasonLocked,
  overRequirePower: L.ui.blockInventory.stopReasonInsufficientPower,
};

// 機械の稼働状態→表示ラベル。%が待機を意味しなくなった分、状態はラベルで示す（ADR 0010）
// Machine state → label key; the rate no longer encodes standby, so the state gets its own label (ADR 0010)
export function machineStateTranslationKey(currentState: MachineProcessState): TranslationKey {
  return MachineStateKeys[currentState];
}

const MachineStateKeys: Record<MachineProcessState, TranslationKey> = {
  idle: L.ui.blockInventory.machineStateIdle,
  processing: L.ui.blockInventory.machineStateProcessing,
  halted: L.ui.blockInventory.machineStateHalted,
};

// haltedのみ不足トーン。語彙は上表と同じ集合を参照
// Only halted gets the insufficient tone (--text-insufficient); shares the same state vocabulary as the table above
export function isMachineStateInsufficient(currentState: MachineProcessState): boolean {
  return MachineStateInsufficientTone[currentState];
}

const MachineStateInsufficientTone: Record<MachineProcessState, boolean> = {
  idle: false,
  processing: false,
  halted: true,
};

// 要求電力0（停止中）は充足率が意味を持たないため、表示自体を出さない判断をここで確定する
// A request power of 0 (halted) makes the satisfaction rate meaningless, so the decision to hide it is settled here
export function isPowerRateMeaningful(requestPower: number): boolean {
  return requestPower !== 0;
}
