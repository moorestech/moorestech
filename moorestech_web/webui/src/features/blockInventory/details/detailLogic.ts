import type { GearNetworkStopReason } from "@/bridge";
import { clamp01 } from "@/shared/clamp01";

// uGUI CommonMachineBlockStateDetail.PowerRate と同式（ワイヤ非送信のためWeb側算出）
// Same formula as uGUI CommonMachineBlockStateDetail.PowerRate (not on the wire; computed web-side)
export function computePowerRate(currentPower: number, requestPower: number): number {
  return requestPower === 0 ? 1 : currentPower / requestPower;
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
export function stopReasonText(reason: GearNetworkStopReason): string {
  if (reason === "rocked") return "ロック";
  if (reason === "overRequirePower") return "パワー不足";
  return "";
}
