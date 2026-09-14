import type { Env } from "./env";
import { fail, json } from "./http";
import { bundlePrefix, joinSafePath, pendingIndexKey, type PlaytestKind } from "./keys";
import { verifyToken } from "./token";

export const MAX_FILE_BYTES = 100 * 1024 * 1024;
export const READY_MARKER = "READY";
export const ACKED_MARKER = "ACKED";

// Bearerトークンだけが置き場を決める。URLにsteamIdは出さないので他人の領域へは書けない
// Only the bearer token decides the destination; the URL carries no steamId, so nobody can write into another's area
async function authorize(request: Request, env: Env): Promise<string | null> {
  const header = request.headers.get("authorization") ?? "";
  if (!header.startsWith("Bearer ")) {
    console.warn("[upload] rejected: missing or malformed Authorization header");
    return null;
  }
  const steamId = await verifyToken(env.SESSION_HMAC_SECRET, header.slice("Bearer ".length), Math.floor(Date.now() / 1000));
  // verifyToken自身が拒否理由をwarn済みなのでここでは重複させない
  // verifyToken already warns its own rejection reason, so this does not duplicate it
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

  const declared = Number(request.headers.get("content-length") ?? "0");
  if (Number.isFinite(declared) && declared > MAX_FILE_BYTES) {
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
