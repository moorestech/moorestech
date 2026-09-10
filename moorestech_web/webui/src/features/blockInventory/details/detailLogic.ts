import type { GearNetworkStopReason, GearRole, MachineProcessState } from "@/bridge";
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

// 機械の稼働状態→表示（ラベル・不足トーン・充足率の表示可否）を1枚のテーブルで確定する
// One table settles every state-driven display decision: label, insufficient tone, and whether the rate is shown
export type MachineStateDisplay = {
  labelKey: TranslationKey;
  insufficient: boolean;
  showPowerRate: boolean;
};

export function machineStateDisplay(currentState: MachineProcessState): MachineStateDisplay {
  return MachineStateDisplayTable[currentState];
}

// haltedは要求電力を出さないため充足率が意味を持たず、不足トーンのラベルだけを見せる
// Halted requests no power, so the rate is meaningless there and only the insufficient-toned label remains
const MachineStateDisplayTable: Record<MachineProcessState, MachineStateDisplay> = {
  idle: { labelKey: L.ui.blockInventory.machineStateIdle, insufficient: false, showPowerRate: true },
  processing: { labelKey: L.ui.blockInventory.machineStateProcessing, insufficient: false, showPowerRate: true },
  halted: { labelKey: L.ui.blockInventory.machineStateHalted, insufficient: true, showPowerRate: false },
};

// 歯車行の文言は役割で決まる。消費側はトルク現在値＋RPM現在/基準、発生側はトルク・RPMとも現在値のみ（ADR 0056）
// Gear row wording follows the role: consumers show current torque plus RPM current/base, generators show current values only (ADR 0056)
export function gearTorqueTranslationKey(role: GearRole): TranslationKey {
  return GearTorqueKeys[role];
}

export function gearRpmTranslationKey(role: GearRole): TranslationKey {
  return GearRpmKeys[role];
}

const GearTorqueKeys: Record<GearRole, TranslationKey> = {
  consumer: L.ui.blockInventory.gearConsumedTorque,
  generator: L.ui.blockInventory.gearGeneratedTorque,
};

const GearRpmKeys: Record<GearRole, TranslationKey> = {
  consumer: L.ui.blockInventory.gearRpmWithBase,
  generator: L.ui.blockInventory.gearRpmCurrent,
};
