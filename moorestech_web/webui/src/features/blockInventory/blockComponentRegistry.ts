import type { ComponentType } from "react";
import type { BlockInventoryOpen } from "@/bridge/contract/payloadTypes";
import ChestInventory from "./views/ChestInventory";
import FilterSplitterInventory from "./views/FilterSplitterInventory";
import GearMachineInventory from "./views/GearMachineInventory";
import GearMinerInventory from "./views/GearMinerInventory";
import GeneratorInventory from "./views/GeneratorInventory";
import GenericBlockInventory from "./views/GenericBlockInventory";
import MachineInventory from "./views/MachineInventory";
import MinerInventory from "./views/MinerInventory";

// blockType → React コンポーネントの静的レジストリ。後続 feature が再代入なしで拡張できるよう可変オブジェクト
// Static blockType → React component registry; a mutable object so later features extend it without rewrites
// キーは C# BlockMasterElement.BlockType の実値に厳密一致させる(実マスタは "Chest" 等の PascalCase)
// Keys must exactly match C# BlockMasterElement.BlockType (the real master uses PascalCase like "Chest")
export type BlockInventoryComponent = ComponentType<{ data: BlockInventoryOpen }>;
export const blockComponents: Record<string, BlockInventoryComponent> = {
  Chest: ChestInventory,
  FilterSplitter: FilterSplitterInventory,
  ElectricMachine: MachineInventory,
  GearMachine: GearMachineInventory,
  ElectricGenerator: GeneratorInventory,
  FuelGearGenerator: GeneratorInventory,
  SimpleGearGenerator: GeneratorInventory,
  ElectricMiner: MinerInventory,
  GearMiner: GearMinerInventory,
};

// 未登録 blockType はフォールバックで汎用描画（流体ブロック等が専用 UI 未実装でもクラッシュしない）
// Unknown blockType falls back to a generic view (fluid blocks etc. won't crash before a dedicated UI lands)
export function resolveBlockComponent(blockType: string): BlockInventoryComponent {
  return blockComponents[blockType] ?? GenericBlockInventory;
}
