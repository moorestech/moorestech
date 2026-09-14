import type { Env } from "../env";
import { fail, json } from "../http";
import { bundlePrefix, isKind, isSafeSegment, joinSafePath, pendingIndexKey, type PlaytestKind } from "../keys";
import { verifyToken } from "../token";

export const MAX_FILE_BYTES = 100 * 1024 * 1024;
export const READY_MARKER = "READY";
export const ACKED_MARKER = "ACKED";

// アップロード経路の一致判定とディスパッチをここへ寄せる。一致しなければnullでindex.tsの次の経路へ委ねる
// Path matching and dispatch live here; returns null on a non-match so index.ts can try the next route
export async function routeUploads(request: Request, env: Env, segments: string[]): Promise<Response | null> {
  if (!(segments[0] === "v1" && segments[1] === "uploads")) return null;

  // "/uploads/kind/../a.txt" は正規化でkind自体が畳まれ3セグメントになりうる。既知prefix配下として400で扱う
  // "/uploads/kind/../a.txt" normalizes away the kind segment itself, landing at 3 segments; still answer 400 under this known prefix, not a generic 404
  if (segments.length < 4) {
    console.warn(`[router] rejected a malformed upload path: /${segments.join("/")}`);
    return fail("bad-path", 400);
  }
  const kind = segments[2] as string;
  const id = segments[3] as string;
  if (!isKind(kind)) {
    console.warn(`[router] rejected an unknown upload kind: ${kind}`);
    return fail("bad-kind", 400);
  }
  // idもキーの一部なので同じ検査を通す。ここを抜かすと ".." のidでprefixを抜けられる
  // The id is part of the key too, so it takes the same check; skipping it would let ".." escape the prefix
  if (!isSafeSegment(id)) {
    console.warn(`[router] rejected an unsafe upload id: ${id}`);
    return fail("bad-path", 400);
  }

  const rest = segments.slice(4);
  if (rest.length === 1 && rest[0] === "complete") {
    if (request.method !== "POST") {
      console.warn(`[router] rejected method ${request.method} for upload complete`);
      return fail("method-not-allowed", 405);
    }
    return completeUpload(request, env, kind, id);
  }
  if (request.method !== "PUT") {
    console.warn(`[router] rejected method ${request.method} for upload put`);
    return fail("method-not-allowed", 405);
  }
  return putUpload(request, env, kind, id, rest);
}

// Bearerトークンだけが置き場を決める。URLにsteamIdは出さないので他人の領域へは書けない
// Only the bearer token decides the destination; the URL carries no steamId, so nobody can write into another's area
async function authorize(request: Request, env: Env): Promise<string | null> {
  const header = request.headers.get("authorization") ?? "";
  if (!header.startsWith("Bearer ")) {
    console.warn("[upload] rejected: missing or malformed Authorization header");
    return null;
  }
  const steamId = await verifyToken(env.SESSION_HMAC_SECRET, header.slice("Bearer ".length), Math.floor(Date.now() / 1000));
  if (steamId === null) return null; // verifyToken自身が拒否理由をwarn済み / verifyToken already warned its own reason
  if (!isSafeSegment(steamId)) {
    // steamIdはR2キーの構成要素になる。token偽造は出来ないが、キー安全性はここでも保証する
    // steamId becomes part of the R2 key; the token can't be forged, but key safety is still enforced here too
    console.warn(`[upload] rejected a token whose steamId is not a safe key segment: ${steamId}`);
    return null;
  }
  return steamId;
}

export async function putUpload(
  request: Request,
  env: Env,
  kind: PlaytestKind,
  id: string,
  pathSegments: string[],
): Promise<Response> {
  const steamId = await authorize(request, env);
  if (steamId === null) return fail("unauthorized", 401);

  const relativePath = joinSafePath(pathSegments);
  if (relativePath === null) {
    console.warn(`[upload] rejected an unsafe path: ${pathSegments.join("/")}`);
    return fail("bad-path", 400);
  }

  // 解釈できないContent-Lengthは拒否側へ倒す: 欠落は411、非数値・非有限は400、上限超過は413
  // An unreadable Content-Length is rejected outright: missing is 411, non-numeric/non-finite is 400, over the limit is 413
  const rawLength = request.headers.get("content-length");
  if (rawLength === null) {
    console.warn(`[upload] ${steamId}/${id}/${relativePath} is missing a Content-Length header`);
    return fail("length-required", 411);
  }
  const declared = Number(rawLength);
  if (!Number.isFinite(declared)) {
    console.warn(`[upload] ${steamId}/${id}/${relativePath} declares a non-numeric Content-Length: "${rawLength}"`);
    return fail("bad-request", 400);
  }
  if (declared > MAX_FILE_BYTES) {
    console.warn(`[upload] ${steamId}/${id}/${relativePath} declares ${declared} bytes, over the limit`);
    return fail("too-large", 413);
  }

  await env.BUCKET.put(`${bundlePrefix(kind, steamId, id)}/${relativePath}`, request.body);
  return json({ stored: relativePath });
}

export async function completeUpload(request: Request, env: Env, kind: PlaytestKind, id: string): Promise<Response> {
  const steamId = await authorize(request, env);
  if (steamId === null) return fail("unauthorized", 401);

  const summary = await request.text();
  await env.BUCKET.put(`${bundlePrefix(kind, steamId, id)}/${READY_MARKER}`, summary, {
    httpMetadata: { contentType: "application/json" },
  });

  // 未ACKの列挙をR2の全走査にしないため、READYと対の索引オブジェクトを置く。ackで消す
  // A paired index object keeps "pending" enumerable without scanning all of R2; ack deletes it
  await env.BUCKET.put(pendingIndexKey(kind, steamId, id), "");
  return json({ ready: true });
}
