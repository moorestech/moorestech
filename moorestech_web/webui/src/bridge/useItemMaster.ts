import { useEffect, useState } from "react";
import type { ItemMasterData, ItemMasterEntry } from "../types/itemMaster";

let cached: Map<number, ItemMasterEntry> | null = null;
let inflight: Promise<void> | null = null;

// アイテムマスタを一度だけ fetch して itemId → entry の Map を返す
// Fetch the item master once and return an itemId → entry map
export function useItemMaster(): Map<number, ItemMasterEntry> | null {
  const [master, setMaster] = useState(cached);

  useEffect(() => {
    if (cached) {
      setMaster(cached);
      return;
    }
    let cancelled = false;
    // 進行中の fetch をモジュールで共有し、同時マウントの重複リクエストを防ぐ
    // Share the in-flight fetch at module level to avoid duplicate concurrent requests
    inflight ??= fetch("/api/master/items")
      .then((r) => {
        // ゲーム起動前の 503 等は次のマウントで再試行する
        // Non-OK responses (e.g. 503 before game start) retry on the next mount
        if (!r.ok) return null;
        return r.json();
      })
      .then((data: ItemMasterData | null) => {
        if (!data) return;
        cached = new Map(data.items.map((i) => [i.itemId, i]));
      })
      .catch(() => {
        // ネットワーク失敗は次マウントの再試行に委ねる
        // Network failures are retried on the next mount
      })
      .finally(() => {
        inflight = null;
      });
    inflight.then(() => {
      if (!cancelled && cached) setMaster(cached);
    });
    return () => {
      cancelled = true;
    };
  }, []);

  return master;
}
