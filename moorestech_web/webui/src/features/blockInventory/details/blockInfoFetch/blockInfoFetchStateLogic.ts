import type { BlockInfoUnavailableData } from "@/bridge";
import { L, type TranslationKey } from "@/shared/i18n";

// 取得できていない状態ごとの文言キー。欄の名前を {target} へ差し込む
// The message key for each unavailable state; the section name goes into {target}
const UNAVAILABLE_KEYS: Record<BlockInfoUnavailableData["fetchState"], TranslationKey> = {
  notFetched: L.ui.blockInventory.blockInfoNotFetched,
  transportFailed: L.ui.blockInventory.blockInfoTransportFailed,
  unreadable: L.ui.blockInventory.blockInfoUnreadable,
  unsupportedTarget: L.ui.blockInventory.blockInfoUnsupportedTarget,
  vanished: L.ui.blockInventory.blockInfoVanished,
};
export function blockInfoUnavailableKey(unavailable: BlockInfoUnavailableData): TranslationKey {
  return UNAVAILABLE_KEYS[unavailable.fetchState];
}
