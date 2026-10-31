import { readPurged } from "../bundleMarkers";
import { parseByteRange } from "../byteRange";
import type { Env } from "../env";
import { fail, json } from "../http";
import { bundlePrefix, joinSafePath, type PlaytestKind } from "../keys";

const OCTET_STREAM = "application/octet-stream";

// 個別ファイル取得。ダッシュボードが動画をシークしながら中継するため単一範囲のRangeに応じる
// Fetches one file; honours a single Range so the dashboard can relay a video while seeking
export async function getInboxObject(request: Request, env: Env, kind: PlaytestKind, steamId: string, id: string, pathSegments: string[]): Promise<Response> {
  const relativePath = joinSafePath(pathSegments);
  if (relativePath === null) {
    console.warn(`[inbox] rejected an unsafe object path: ${pathSegments.join("/")}`);
    return fail("bad-path", 400);
  }

  // 範囲を全長に照らすため先にheadする。無ければ片付け済みかを見て410と404を分ける
  // Head first so the range can be checked against the size; when absent, a purged box answers 410 instead of 404
  const key = `${bundlePrefix(kind, steamId, id)}/${relativePath}`;
  const head = await env.BUCKET.head(key);
  if (head === null) return absentAnswer(env, kind, steamId, id, key);

  const range = parseByteRange(request.headers.get("range"), head.size);
  if (range.kind === "unsatisfiable") {
    console.warn(`[inbox] rejected an unsatisfiable range for ${key}: ${range.reason}`);
    return new Response(null, { status: 416, headers: { "content-range": `bytes */${head.size}` } });
  }
  if (range.kind === "whole") return bodyAnswer(env, key, 200, head.size, {}, null);

  const last = range.offset + range.length - 1;
  return bodyAnswer(env, key, 206, range.length, { "content-range": `bytes ${range.offset}-${last}/${head.size}` }, { offset: range.offset, length: range.length });
}

// headとgetの間に消された場合も、同じ不在の判定へ合流させる
// An object deleted between head and get joins the same absence handling
async function bodyAnswer(env: Env, key: string, status: 200 | 206, length: number, extraHeaders: Record<string, string>, range: R2Range | null): Promise<Response> {
  const object = await env.BUCKET.get(key, range === null ? {} : { range });
  if (object === null) {
    console.warn(`[inbox] object vanished between head and get: ${key}`);
    return fail("not-found", 404);
  }
  const headers = { "content-type": OCTET_STREAM, "content-length": String(length), "accept-ranges": "bytes", ...extraHeaders };
  return new Response(object.body, { status, headers });
}

async function absentAnswer(env: Env, kind: PlaytestKind, steamId: string, id: string, key: string): Promise<Response> {
  const purged = await readPurged(env.BUCKET, kind, steamId, id);
  if (purged.purged) {
    console.warn(`[inbox] object requested from a purged bundle: ${key}`);
    return json({ reason: "purged", purgedAt: purged.purgedAt }, 410);
  }
  console.warn(`[inbox] object not found: ${key}`);
  return fail("not-found", 404);
}
