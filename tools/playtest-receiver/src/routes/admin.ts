import { readAllowlist, writeAllowlist } from "../allowlist";
import type { Env } from "../env";
import { fail, json, requireAdmin } from "../http";
import { bundlePrefix, isKind, isSafeSegment, joinSafePath, parsePendingIndexKey, pendingIndexKey, type PlaytestKind } from "../keys";
import { ACKED_MARKER } from "./uploads";

const INBOX_PAGE_SIZE = 100;
const PENDING_PREFIX = "index/pending/";

// 管理API経路の一致判定とディスパッチをここへ寄せる。一致しなければnullでindex.tsの次の経路へ委ねる
// Path matching and dispatch for the admin API live here; returns null on a non-match so index.ts can try the next route
export async function routeAdmin(request: Request, env: Env, segments: string[]): Promise<Response | null> {
  if (segments[0] !== "v1") return null;
  if (segments[1] !== "allowlist" && segments[1] !== "inbox") return null;

  // 管理APIの認証はここ1箇所だけで行う。経路の形やメソッドの正誤に関わらず、鍵なし・不一致は必ず401
  // The admin API is authenticated in exactly one place; a missing or mismatched key is always 401, regardless of path shape or method
  const denied = requireAdmin(request, env);
  if (denied !== null) return denied;

  if (segments[1] === "allowlist" && segments.length === 2) {
    if (request.method === "GET") return getAllowlist(env);
    if (request.method === "PUT") return putAllowlist(request, env);
    console.warn(`[router] rejected method ${request.method} for /v1/allowlist`);
    return fail("method-not-allowed", 405);
  }

  if (segments[1] === "inbox") return routeInbox(request, env, segments);

  // ここに来るのは /v1/allowlist/余分なセグメント のような未知の形だけ。認証を経路の存在確認より前に
  // 置いた結果、鍵なしなら401・鍵ありならnullでindex.tsのR1逐語404に落ちる（意図的な非対称。存在を明かす前に認証する側へ倒す）
  // Only unknown shapes like /v1/allowlist/extra-segment reach here. Since auth runs before existence checks,
  // an unauthenticated caller gets 401 while an authenticated one falls through to index.ts's verbatim R1 404 (deliberate asymmetry: authenticate before revealing existence)
  return null;
}

async function routeInbox(request: Request, env: Env, segments: string[]): Promise<Response> {
  if (segments.length === 2) {
    if (request.method !== "GET") {
      console.warn(`[router] rejected method ${request.method} for /v1/inbox`);
      return fail("method-not-allowed", 405);
    }
    return getInbox(request, env);
  }
  // {kind}/{steamId}/{id} の3セグメントに加え、ackか個別パス(1つ以上)が要る
  // Needs the {kind}/{steamId}/{id} trio plus either "ack" or at least one more path segment
  if (segments.length < 6) {
    console.warn(`[router] rejected a malformed inbox path: /${segments.join("/")}`);
    return fail("not-found", 404);
  }

  const kind = segments[2] as string;
  const steamId = segments[3] as string;
  const id = segments[4] as string;
  if (!isKind(kind)) {
    console.warn(`[router] rejected an unknown inbox kind: ${kind}`);
    return fail("bad-kind", 400);
  }
  if (!isSafeSegment(steamId) || !isSafeSegment(id)) {
    console.warn(`[router] rejected an unsafe inbox path segment: ${steamId}/${id}`);
    return fail("bad-path", 400);
  }

  const rest = segments.slice(5);
  if (rest.length === 1 && rest[0] === "ack") {
    if (request.method !== "POST") {
      console.warn(`[router] rejected method ${request.method} for inbox ack`);
      return fail("method-not-allowed", 405);
    }
    return postAck(env, kind, steamId, id);
  }
  if (request.method !== "GET") {
    console.warn(`[router] rejected method ${request.method} for inbox object`);
    return fail("method-not-allowed", 405);
  }
  return getInboxObject(env, kind, steamId, id, rest);
}

// 認証はrouteAdminが済ませている。ここでは呼び出さない（重複させない）
// Authentication is already done by routeAdmin; not repeated here
export async function getInbox(request: Request, env: Env): Promise<Response> {
  const cursor = new URL(request.url).searchParams.get("cursor") ?? undefined;
  const listed = await env.BUCKET.list({ prefix: PENDING_PREFIX, limit: INBOX_PAGE_SIZE, cursor });

  const items = [];
  for (const object of listed.objects) {
    const parsed = parsePendingIndexKey(object.key);
    if (parsed === null) {
      console.warn(`[inbox] skipped an index key with an unexpected shape: ${object.key}`);
      continue;
    }
    items.push({ kind: parsed.kind, steamId: parsed.steamId, id: parsed.id, readyAt: object.uploaded.toISOString() });
  }

  return json({ items, cursor: listed.truncated ? listed.cursor : null });
}

export async function getInboxObject(
  env: Env,
  kind: PlaytestKind,
  steamId: string,
  id: string,
  pathSegments: string[],
): Promise<Response> {
  const relativePath = joinSafePath(pathSegments);
  if (relativePath === null) {
    console.warn(`[inbox] rejected an unsafe object path: ${pathSegments.join("/")}`);
    return fail("bad-path", 400);
  }

  const key = `${bundlePrefix(kind, steamId, id)}/${relativePath}`;
  const object = await env.BUCKET.get(key);
  if (object === null) {
    console.warn(`[inbox] object not found: ${key}`);
    return fail("not-found", 404);
  }
  return new Response(object.body, { headers: { "content-type": "application/octet-stream" } });
}

// ackは「pendingの実在確認→ACKEDを書く→索引を消す」の3手。途中で落ちても項目は見えたままになる
// Ack is three steps: confirm the pending entry exists, write ACKED, then drop the index, so a crash in between leaves the item still visible
export async function postAck(env: Env, kind: PlaytestKind, steamId: string, id: string): Promise<Response> {
  const indexKey = pendingIndexKey(kind, steamId, id);
  const ackedKey = `${bundlePrefix(kind, steamId, id)}/${ACKED_MARKER}`;
  const pending = await env.BUCKET.head(indexKey);
  if (pending === null) {
    // 索引が無くてもACKED済みならこの呼び出しは再送（at-least-onceの取り込みがackの応答だけ取りこぼした形）。
    // 冪等にするため200を返す。ACKEDも無ければ本物のID取り違えなので404
    // A missing index doesn't necessarily mean unknown; if ACKED already exists this is a retry (the ingest side
    // lost only the ack response under at-least-once retries), so answer 200 to stay idempotent. If ACKED is
    // also absent, the id is a genuine mismatch, so 404
    const alreadyAcked = await env.BUCKET.head(ackedKey);
    if (alreadyAcked !== null) return json({ acked: true });
    console.warn(`[inbox] rejected ack for an item that is not pending: ${indexKey}`);
    return fail("not-found", 404);
  }

  await env.BUCKET.put(ackedKey, new Date().toISOString());
  await env.BUCKET.delete(indexKey);
  return json({ acked: true });
}

export async function getAllowlist(env: Env): Promise<Response> {
  return json({ steamIds: await readAllowlist(env.BUCKET) });
}

export async function putAllowlist(request: Request, env: Env): Promise<Response> {
  // 管理者が手で送るJSON本文のパースは外部入力境界。全置換なので壊れた本文で上書きせず現状を残して400を返す
  // Parsing an admin-supplied JSON body is an external-input boundary; this replaces the whole list, so a parse/shape failure keeps the current list and answers 400
  let steamIds: string[];
  try {
    const body = (await request.json()) as { steamIds?: unknown };
    if (!Array.isArray(body.steamIds) || body.steamIds.some((value) => typeof value !== "string")) {
      console.warn("[allowlist] rejected a PUT whose steamIds is missing or not a string array");
      return fail("bad-request", 400);
    }
    steamIds = body.steamIds as string[];
  } catch (error) {
    console.warn(`[allowlist] rejected a PUT with a body that is not JSON: ${error instanceof Error ? error.message : String(error)}`);
    return fail("bad-request", 400);
  }

  await writeAllowlist(env.BUCKET, steamIds);
  // 成功時の監査ログ。拒否理由ではないのでwarnではなくlogを使う
  // Success-path audit log; not a rejection reason, so this uses log rather than warn
  console.log(`[allowlist] replaced with ${new Set(steamIds).size} steamIds`);
  return json({ steamIds: [...new Set(steamIds)] });
}
