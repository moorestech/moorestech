import type { ActionPayloads } from "../../src/bridge/transport/protocol";
import type {
  PlayerInventoryData,
  SlotData,
  SlotRef,
  BlockInventoryData,
  BlockSlotRef,
} from "../../src/bridge/contract/payloadTypes";

export function slotOf(inv: PlayerInventoryData, ref: SlotRef): SlotData {
  if (ref.area === "grab") return inv.grab;
  const list = ref.area === "main" ? inv.mainSlots : inv.hotbarSlots;
  return list[ref.slot];
}

// block 領域はテスト用 currentBlock を、それ以外は接続ごとの inv を参照する
// The block area refers to the test-only currentBlock; other areas refer to the per-connection inv
export function blockSlotOf(inv: PlayerInventoryData, currentBlock: BlockInventoryData, ref: BlockSlotRef): SlotData {
  if (ref.area !== "block") return slotOf(inv, ref as SlotRef);
  // block 操作は開状態でのみ発生する。閉なら空スロット扱いで安全に倒す
  // Block ops only happen while open; treat a closed block as an empty slot to stay safe
  if (!currentBlock.open) return { itemId: 0, count: 0 };
  return currentBlock.itemSlots[ref.slot];
}

// ブロック⇔プレイヤー間の移動。block 領域を跨ぐ点以外は applyMove と同型
// Block⇔player move; same shape as applyMove except it can span the block area
export function applyBlockMove(
  inv: PlayerInventoryData,
  currentBlock: BlockInventoryData,
  p: ActionPayloads["block_inventory.move_item"],
): string | null {
  const from = blockSlotOf(inv, currentBlock, p.from);
  const to = blockSlotOf(inv, currentBlock, p.to);
  if (from.count === 0) return "empty_slot";
  if (from.count < p.count) return "insufficient_count";
  if (to.count === 0) to.itemId = from.itemId;
  to.count += p.count;
  from.count -= p.count;
  if (from.count <= 0) {
    from.count = 0;
    from.itemId = 0;
  }
  return null;
}

// from の count 個を to へ移す最小モデル。block を跨がない移動は「閉ブロック」扱いの applyBlockMove と完全同型
// Minimal move model; a non-block move is exactly applyBlockMove against a closed block
export function applyMove(inv: PlayerInventoryData, p: ActionPayloads["inventory.move_item"]): string | null {
  return applyBlockMove(inv, { open: false }, p);
}

// host と同じく mock 自身の現在 grab 状態で集積先を決め、同種スタックを集約する
// Like the host, decide the target from the mock's own current grab and consolidate same-type stacks
export function applyCollect(inv: PlayerInventoryData, p: ActionPayloads["inventory.collect"]) {
  const grabHeld = inv.grab.count > 0;
  const target = grabHeld ? inv.grab : slotOf(inv, p.slot);
  if (target.count === 0) return;
  for (const s of [...inv.mainSlots, ...inv.hotbarSlots]) {
    if (s === target || s.itemId !== target.itemId || s.count === 0) continue;
    target.count += s.count;
    s.count = 0;
    s.itemId = 0;
  }
}

// host の CollectItems と同様に grab 状態で集積先を決め、main/hotbar/block を跨いで同種を集約する
// Like the host's CollectItems, pick the target from grab state and consolidate across main/hotbar/block
export function applyBlockCollect(
  inv: PlayerInventoryData,
  currentBlock: BlockInventoryData,
  p: ActionPayloads["block_inventory.collect"],
) {
  const grabHeld = inv.grab.count > 0;
  const target = grabHeld ? inv.grab : blockSlotOf(inv, currentBlock, p.slot);
  if (target.count === 0) return;
  const blockSlots = currentBlock.open ? currentBlock.itemSlots : [];
  for (const s of [...inv.mainSlots, ...inv.hotbarSlots, ...blockSlots]) {
    if (s === target || s.itemId !== target.itemId || s.count === 0) continue;
    target.count += s.count;
    s.count = 0;
    s.itemId = 0;
  }
}
