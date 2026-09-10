import type { ComponentType } from "react";
import type { BlockInventoryOpen } from "@/bridge";
import FilterSplitterInventory from "../views/FilterSplitterInventory";
import ElectricToGearInventory from "../views/ElectricToGearInventory";
import SectionStackView from "../views/SectionStackView";
import ElectricPoleInventory from "../views/ElectricPoleInventory";
import TrainPlatformInventory from "../views/TrainPlatformInventory";

// blockType別の静的レジストリ
// Static blockType → React component registry; a mutable object so later features extend it without rewrites
// マスタのblockTypeと一致させる
// Keys must exactly match C# BlockMasterElement.BlockType (the real master uses PascalCase like "Chest")
// 大型パネル（高さ確定面）かどうかはパネルが決め、器の伸長が要るビューだけが受け取る
// The panel decides whether this is the height-determining large panel; only views needing to stretch read it
export type BlockInventoryComponent = ComponentType<{ data: BlockInventoryOpen; fillsPanelHeight: boolean }>;
export const blockComponents: Record<string, BlockInventoryComponent> = {
  FilterSplitter: FilterSplitterInventory,
  Shaft: SectionStackView,
  Gear: SectionStackView,
  GearBeltConveyor: SectionStackView,
  ElectricToGearGenerator: ElectricToGearInventory,
  TrainStation: TrainPlatformInventory,
  TrainItemPlatform: TrainPlatformInventory,
  TrainFluidPlatform: TrainPlatformInventory,
  ElectricPole: ElectricPoleInventory,
};

// 未登録種別は共通ビューへ戻す
// Unknown blockType falls back to a generic view (fluid blocks etc. won't crash before a dedicated UI lands)
export function resolveBlockComponent(blockType: string): BlockInventoryComponent {
  return blockComponents[blockType] ?? SectionStackView;
}
