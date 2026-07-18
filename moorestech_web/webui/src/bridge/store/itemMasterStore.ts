import { create } from "zustand";
import type { ItemMasterData, ItemMasterEntry } from "../contract/payloadTypes";

type ItemMasterState = {
  master: Map<number, ItemMasterEntry> | null;
  setMaster: (master: Map<number, ItemMasterEntry>) => void;
};

// アイテムマスタの zustand ストア。常時マウントのコンポーネントにも遅延ロードがリアクティブに届く
// Zustand store for the item master; late loads reach always-mounted components reactively
export const useItemMasterStore = create<ItemMasterState>((set) => ({
  master: null,
  setMaster: (master) => set({ master }),
}));

// ゲーム起動前の 503 やネットワーク断は、マウントに依存せず一定間隔で自動再試行する
// Retry on a fixed interval independent of mounts (e.g. 503 before game start, network drop)
const RETRY_INTERVAL_MS = 3000;
let started = false;

// HTTP 由来の各アイテムに必須フィールド型が揃うことを検証する
// Validate required field types for each item received over HTTP
function isItemMasterEntry(item: unknown): item is ItemMasterEntry {
  return (
    typeof item === "object" &&
    item !== null &&
    "itemId" in item &&
    typeof item.itemId === "number" &&
    "name" in item &&
    typeof item.name === "string" &&
    "maxStack" in item &&
    typeof item.maxStack === "number"
  );
}

// コンテナ形状と全要素を検証して不正データの流入を防ぐ
// Validate the container and every entry to keep malformed data out of the store
function isItemMasterData(data: unknown): data is ItemMasterData {
  return (
    typeof data === "object" &&
    data !== null &&
    "items" in data &&
    Array.isArray(data.items) &&
    data.items.every(isItemMasterEntry)
  );
}

export function ensureItemMasterLoaded(): void {
  if (started) return;
  started = true;
  void loadWithRetry();
}

async function loadWithRetry(): Promise<void> {
  for (;;) {
    const res = await fetch("/api/master/items").catch(() => null);
    if (res?.ok) {
      const data: unknown = await res.json().catch(() => null);
      if (isItemMasterData(data)) {
        useItemMasterStore.getState().setMaster(new Map(data.items.map((i) => [i.itemId, i])));
        return;
      }
    }
    await new Promise((resolve) => setTimeout(resolve, RETRY_INTERVAL_MS));
  }
}
