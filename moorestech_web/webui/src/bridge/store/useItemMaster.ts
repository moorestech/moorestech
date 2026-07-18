import { useEffect } from "react";
import type { ItemMasterEntry } from "../contract/payloadTypes";
import { ensureItemMasterLoaded, useItemMasterStore } from "./itemMasterStore";

// アイテムマスタを購読する React フック。未ロード中は null（ロード完了時に自動再レンダー）
// React hook subscribing to the item master; null while unloaded, re-renders automatically on load
export function useItemMaster(): Map<number, ItemMasterEntry> | null {
  useEffect(() => {
    ensureItemMasterLoaded();
  }, []);
  return useItemMasterStore((s) => s.master);
}

// イベントハンドラから購読せず最新マスタを読む
// Read the latest master from event handlers without subscribing
export function readItemMaster(): Map<number, ItemMasterEntry> | null {
  return useItemMasterStore.getState().master;
}
