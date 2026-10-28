import { type ComponentType } from "react";
import type { BlockInventoryOpen } from "@/bridge";
import FilterSplitterInventory from "../views/FilterSplitterInventory";
import ElectricToGearInventory from "../views/ElectricToGearInventory";
import SectionStackView from "../views/SectionStackView";
import ElectricPoleInventory from "../views/ElectricPoleInventory";
import SpaceElevatorInventory from "../views/SpaceElevatorInventory";
import RocketLaunchPadInventory from "../views/RocketLaunchPadInventory";
import RocketLaunchPadCommandAction from "../details/rocket/RocketLaunchPadCommandAction";
import TrainPlatformInventory from "../views/TrainPlatformInventory";

// blockType別の静的レジストリ
// Static blockType → React component registry; a mutable object so later features extend it without rewrites
// マスタのblockTypeと一致させる
// Keys must exactly match C# BlockMasterElement.BlockType (the real master uses PascalCase like "Chest")
// 大型パネル（高さ確定面）かどうかはパネルが決め、器の伸長が要るビューだけが受け取る
// The panel decides whether this is the height-determining large panel; only views needing to stretch read it
export type BlockInventoryComponent = ComponentType<{ data: BlockInventoryOpen; fillsPanelHeight: boolean }>;
type BlockInventoryComponents = {
  body: BlockInventoryComponent;
  titleAction?: ComponentType<{ data: BlockInventoryOpen }>;
};
export const blockComponents: Record<string, BlockInventoryComponents> = {
  FilterSplitter: { body: FilterSplitterInventory },
  Shaft: { body: SectionStackView },
  Gear: { body: SectionStackView },
  GearBeltConveyor: { body: SectionStackView },
  ElectricToGearGenerator: { body: ElectricToGearInventory },
  TrainStation: { body: TrainPlatformInventory },
  TrainItemPlatform: { body: TrainPlatformInventory },
  TrainFluidPlatform: { body: TrainPlatformInventory },
  ElectricPole: { body: ElectricPoleInventory },
  SpaceElevatorBase: { body: SpaceElevatorInventory },
  RocketLaunchPad: { body: RocketLaunchPadInventory, titleAction: RocketLaunchPadCommandAction },
};

// 未登録種別は共通ビューへ戻す
// Unknown blockType falls back to a generic view (fluid blocks etc. won't crash before a dedicated UI lands)
const defaultComponents: BlockInventoryComponents = { body: SectionStackView };

export function resolveBlockComponent(blockType: string): BlockInventoryComponents {
  return blockComponents[blockType] ?? defaultComponents;
}
