import type { ComponentType } from "react";
import type { ActionPayloads } from "@/bridge";
import type { BlockInventoryOpen, SlotData } from "@/bridge/contract/payloadTypes";
import { planDirectMoves } from "@/features/inventory/inventoryLogic";
import ChestInventory from "./views/ChestInventory";
import FilterSplitterInventory from "./views/FilterSplitterInventory";
import GearMachineInventory from "./views/GearMachineInventory";
import GearMinerInventory from "./views/GearMinerInventory";
import GeneratorInventory from "./views/GeneratorInventory";
import GenericBlockInventory from "./views/GenericBlockInventory";
import MachineInventory from "./views/MachineInventory";
import MinerInventory from "./views/MinerInventory";

type MoveItemPayload = ActionPayloads["block_inventory.move_item"];

// grab が空のとき: block スロットの中身を丸ごと grab へ拾い上げる
// When grab is empty: pick the whole block slot stack up into grab
export function pickUpPayload(blockSlotIndex: number, count: number): MoveItemPayload {
  return { from: { area: "block", slot: blockSlotIndex }, to: { area: "grab", slot: 0 }, count };
}

// grab を保持しているとき: grab の中身を丸ごと block スロットへ置く
// When grab is held: place the whole grab stack into the block slot
export function placePayload(blockSlotIndex: number, grabCount: number): MoveItemPayload {
  return { from: { area: "grab", slot: 0 }, to: { area: "block", slot: blockSlotIndex }, count: grabCount };
}

// スロットクリックの共通分岐: grab 保持なら置く / 中身ありなら丸ごと拾う / それ以外は無操作
// Shared slot-click branch: place while holding grab / pick the whole stack when filled / otherwise no-op
export function blockSlotClickPayload(
  slotIndex: number,
  slotItemId: number,
  slotCount: number,
  grabCount: number,
): MoveItemPayload | null {
  if (grabCount > 0) return placePayload(slotIndex, grabCount);
  if (slotItemId > 0) return pickUpPayload(slotIndex, slotCount);
  return null;
}

// 右クリック: grab保持なら1個置き / 空手で2個以上なら半分(切り捨て)を grab へ / それ以外は無操作
// Right-click: place one while holding grab / grab half (floor) of 2+ items empty-handed / otherwise no-op
export function blockSlotRightClickPayload(
  slotIndex: number,
  slotItemId: number,
  slotCount: number,
  grabCount: number,
): MoveItemPayload | null {
  if (grabCount > 0) return { from: { area: "grab", slot: 0 }, to: { area: "block", slot: slotIndex }, count: 1 };
  // uGUI 準拠の切り捨て半分。1個は half=0 になるため無操作
  // uGUI-style floored half; a single item halves to 0, hence a no-op
  const half = Math.floor(slotCount / 2);
  if (slotItemId > 0 && half > 0) return pickUpPayload(slotIndex, half);
  return null;
}

// Shift+クリック: block スロットからプレイヤー main へ配分（uGUI はサブ→メインのみでホットバー除外）
// Shift-click: allocate a block slot into the player's main area (uGUI moves sub→main only, hotbar excluded)
export function blockShiftMovePayloads(
  blockSlotIndex: number,
  slotItemId: number,
  slotCount: number,
  mainSlots: SlotData[],
  maxStack: number | undefined,
): MoveItemPayload[] {
  return planDirectMoves(slotCount, slotItemId, maxStack, mainSlots).map((move) => ({
    from: { area: "block", slot: blockSlotIndex },
    to: { area: "main", slot: move.slot },
    count: move.count,
  }));
}

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
