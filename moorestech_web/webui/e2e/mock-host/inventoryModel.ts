import type { ActionPayloads } from "../../src/bridge/transport/protocol";
import type {
  PlayerInventoryData,
  SlotData,
  SlotRef,
  BlockInventoryData,
  BlockSlotRef,
  CraftRecipe,
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

// host の BlockSplitGrabActionHandler と同型: 空手前提で from の半分(床)を grab へ取る。1個は成功 no-op
// Mirrors the host's BlockSplitGrabActionHandler: empty-handed only, grab floor(count/2) from the slot; 1 item is a success no-op
export function applyBlockSplit(
  inv: PlayerInventoryData,
  currentBlock: BlockInventoryData,
  p: ActionPayloads["block_inventory.split"],
): string | null {
  if (inv.grab.count > 0) return "grab_not_empty";
  const from = blockSlotOf(inv, currentBlock, p.from);
  if (from.count === 0) return "empty_slot";
  const half = Math.floor(from.count / 2);
  if (half === 0) return null;
  inv.grab.itemId = from.itemId;
  inv.grab.count = half;
  from.count -= half;
  return null;
}

// from の count 個を to へ移す最小モデル。block を跨がない移動は「閉ブロック」扱いの applyBlockMove と完全同型
// Minimal move model; a non-block move is exactly applyBlockMove against a closed block
export function applyMove(inv: PlayerInventoryData, p: ActionPayloads["inventory.move_item"]): string | null {
  return applyBlockMove(inv, { open: false }, p);
}

export function applySplitDrag(inv: PlayerInventoryData, p: ActionPayloads["inventory.split_drag"]): string | null {
  if (inv.grab.count === 0 || p.slots.length === 0) return "grab_empty";
  const uniqueSlots = p.slots.filter((slot, index) =>
    p.slots.findIndex((candidate) => candidate.area === slot.area && candidate.slot === slot.slot) === index);
  const targets = uniqueSlots.map((slot) => slotOf(inv, slot)).filter((slot) => slot.count === 0 || slot.itemId === inv.grab.itemId);
  if (targets.length === 0) return "no_valid_slots";
  const count = Math.floor(inv.grab.count / targets.length);
  if (count === 0) return null;
  for (const target of targets) {
    target.itemId = inv.grab.itemId;
    target.count += count;
    inv.grab.count -= count;
  }
  return null;
}

// クラフト1回分: main+hotbar から必要素材を消費し結果を追加する。素材不足なら false で no-op
// One craft: consume required materials from main+hotbar and add the result; returns false (no-op) if short
// 実 host の OneClickCraft は main+hotbar のみ参照するため grab は対象外
// The real host's OneClickCraft only consults main+hotbar, so grab is excluded
export function applyCraft(inv: PlayerInventoryData, recipe: CraftRecipe): boolean {
  const pool = [...inv.mainSlots, ...inv.hotbarSlots];

  const owned = new Map<number, number>();
  for (const s of pool) if (s.count > 0) owned.set(s.itemId, (owned.get(s.itemId) ?? 0) + s.count);
  for (const req of recipe.requiredItems) if ((owned.get(req.itemId) ?? 0) < req.count) return false;

  // 必要素材を前方スロットから順に差し引く
  // Deduct each required material from the front-most matching slots
  for (const req of recipe.requiredItems) {
    let remaining = req.count;
    for (const s of pool) {
      if (remaining <= 0) break;
      if (s.itemId !== req.itemId || s.count === 0) continue;
      const take = Math.min(s.count, remaining);
      s.count -= take;
      remaining -= take;
      if (s.count <= 0) { s.count = 0; s.itemId = 0; }
    }
  }

  addItem(inv, recipe.resultItemId, recipe.resultCount);
  return true;
}

// 結果アイテムを同種スタックへ、無ければ最初の空 main スロットへ積む
// Stack the result onto a same-type slot, else the first empty main slot
function addItem(inv: PlayerInventoryData, itemId: number, count: number) {
  const same = [...inv.mainSlots, ...inv.hotbarSlots].find((s) => s.itemId === itemId && s.count > 0);
  if (same) { same.count += count; return; }
  const empty = inv.mainSlots.find((s) => s.count === 0);
  if (empty) { empty.itemId = itemId; empty.count = count; }
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
