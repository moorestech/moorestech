import type { ServerResponse } from "node:http";
import { BLOCK_ICON_PREFIX, FLUID_ICON_PREFIX, ITEM_ICON_PREFIX } from "../../../src/bridge/transport/httpEndpoints";
import { fluidIconDirectory, fluidIconFiles, fluidIconSeed, iconDirectory, iconFiles, placeholderIcon, realIconFor } from "./demoAssets";

// item/block/fluidの3分岐が共有する「実アセット→placeholder→404」の3段だけを括る(D9)
// Consolidates only the shared real-asset -> placeholder -> 404 fallback chain across the item/block/fluid branches (D9)
function respondGameIcon(res: ServerResponse, real: Buffer | null, placeholderSeed: number, demo: boolean): void {
  if (!demo) {
    res.statusCode = 404;
    res.end();
    return;
  }
  if (real) {
    res.setHeader("content-type", "image/jpeg");
    res.end(real);
    return;
  }
  res.setHeader("content-type", "image/svg+xml");
  res.end(placeholderIcon(placeholderSeed));
}

export function serveGameIcon(url: string, res: ServerResponse, demo: boolean): boolean {
  // アイテムとブロックのアイコンは本番 httpEndpoints と同じ2経路を同形で配信する
  // Item and block icons are served identically on the same two paths as production httpEndpoints
  const isBlockIcon = url.startsWith(BLOCK_ICON_PREFIX);
  if (url.startsWith(ITEM_ICON_PREFIX) || isBlockIcon) {
    // demo時は実ゲームアイコン(無ければプレースホルダ)、通常時は404で#idフォールバック
    // demo serves real game icons (placeholder if absent); otherwise 404 for the #id fallback
    const parsedId = Number(url.split("/").pop()?.replace(".png", ""));
    const id = isBlockIcon ? parsedId || 0 : parsedId;
    // 不正な外部IDで画像選択が例外を投げるのを防ぐ
    // Reject invalid external IDs before image selection can throw
    if (!isBlockIcon && (!Number.isFinite(id) || !Number.isInteger(id) || 0 > id)) {
      console.warn(`[mock-host] Invalid item icon ID: ${url}`);
      res.statusCode = 404;
      res.end();
      return true;
    }
    // ブロック画像は実アセットが無いため常にプレースホルダ（実ホストはBlockImageContainerのPNG）
    // Block images have no real asset so always use the placeholder (the real host renders BlockImageContainer PNGs)
    const real = demo && !isBlockIcon ? realIconFor(id, iconDirectory, iconFiles) : null;
    respondGameIcon(res, real, id, demo);
    return true;
  }
  // 液体アイコンはguidベースで別配信
  // Fluid icons are served separately, keyed by guid
  if (url.startsWith(FLUID_ICON_PREFIX)) {
    // demoは実/プレースホルダ、他は404。guid欠落は即404
    // demo: real/placeholder icon; otherwise 404. A missing guid is an immediate 404
    const guid = url.split("/").pop()?.replace(".png", "") ?? "";
    if (!guid) {
      res.statusCode = 404;
      res.end();
      return true;
    }
    const seed = fluidIconSeed(guid);
    const real = demo ? realIconFor(seed, fluidIconDirectory, fluidIconFiles) : null;
    respondGameIcon(res, real, seed, demo);
    return true;
  }
  return false;
}
