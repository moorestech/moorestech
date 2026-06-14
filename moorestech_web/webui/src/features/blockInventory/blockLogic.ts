import type { ComponentType } from "react";
import type { ActionPayloads } from "@/bridge";
import type { BlockInventoryData } from "@/bridge/payloadTypes";
import ChestInventory from "./ChestInventory";
import GenericBlockInventory from "./GenericBlockInventory";

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

// blockType → React コンポーネントの静的レジストリ。後続 feature が再代入なしで拡張できるよう可変オブジェクト
// Static blockType → React component registry; a mutable object so later features extend it without rewrites
export type BlockInventoryComponent = ComponentType<{ data: BlockInventoryData }>;
export const blockComponents: Record<string, BlockInventoryComponent> = {
  chest: ChestInventory,
};

// 未登録 blockType はフォールバックで汎用描画（tank 等が実装前でもクラッシュしない）
// Unknown blockType falls back to a generic view (tank etc. won't crash before its feature lands)
export function resolveBlockComponent(blockType: string): BlockInventoryComponent {
  return blockComponents[blockType] ?? GenericBlockInventory;
}
