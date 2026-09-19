import type { Env } from "../env";
import { fail } from "../http";
import { isSafeSegment } from "../keys";
import { verifyToken } from "../token";

// Bearerトークンだけが置き場を決める。戻り値はsteamIdか、そのまま返す拒否応答
// Only the bearer token decides the destination; returns the steamId or a rejection response to return as-is
export async function authorizeUpload(request: Request, env: Env): Promise<string | Response> {
  const header = request.headers.get("authorization") ?? "";
  if (!header.startsWith("Bearer ")) {
    console.warn("[upload] rejected: missing or malformed Authorization header");
    return fail("unauthorized", 401);
  }
  const verification = await verifyToken(env.SESSION_HMAC_SECRET, header.slice("Bearer ".length), Math.floor(Date.now() / 1000));
  if (verification.kind === "misconfigured") {
    console.warn("[upload] cannot verify tokens because SESSION_HMAC_SECRET is missing");
    return fail("server-misconfigured", 500);
  }
  if (verification.kind === "rejected") return fail("unauthorized", 401); // verifyToken自身が理由をwarn済み / verifyToken already warned its reason
  if (!isSafeSegment(verification.steamId)) {
    // steamIdはR2キーの構成要素になる。token偽造は出来ないが、キー安全性はここでも保証する
    // steamId becomes part of the R2 key; the token can't be forged, but key safety is still enforced here too
    console.warn(`[upload] rejected a token whose steamId is not a safe key segment: ${verification.steamId}`);
    return fail("unauthorized", 401);
  }
  return verification.steamId;
}
