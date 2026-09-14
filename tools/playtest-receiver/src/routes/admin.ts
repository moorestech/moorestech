import { readAllowlist, writeAllowlist } from "../allowlist";
import type { Env } from "../env";
import { fail, json, requireAdmin, requireMethod } from "../http";
import {
  bundlePrefix,
  isKind,
  isSafeSegment,
  joinSafePath,
  parsePendingIndexKey,
  PENDING_LIST_PREFIX,
  pendingIndexKey,
  type PlaytestKind,
} from "../keys";
import { ACKED_MARKER } from "./uploads";

const INBOX_PAGE_SIZE = 100;

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

  // 未知の形はここに来る。認証を存在確認より前に置くため、鍵なしは401・鍵ありはnullで404に落ちる（意図的な非対称）
  // Unknown shapes land here; auth runs before existence checks, so unauthed=401, authed falls through to 404 (deliberate asymmetry)
  return null;
}

async function routeInbox(request: Request, env: Env, segments: string[]): Promise<Response> {
  if (segments.length === 2) {
    const denied = requireMethod(request, "GET", "/v1/inbox");
    if (denied !== null) return denied;
    return getInbox(request, env);
  }
  // 3セグメント後にackか個別パス要る
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
    const denied = requireMethod(request, "POST", "inbox ack");
    if (denied !== null) return denied;
    return postAck(env, kind, steamId, id);
  }
  const denied = requireMethod(request, "GET", "inbox object");
  if (denied !== null) return denied;
  return getInboxObject(env, kind, steamId, id, rest);
}

// 認証はrouteAdminが済ませている。ここでは呼び出さない（重複させない）
// Authentication is already done by routeAdmin; not repeated here
async function getInbox(request: Request, env: Env): Promise<Response> {
  const cursor = new URL(request.url).searchParams.get("cursor") ?? undefined;
  const listed = await env.BUCKET.list({ prefix: PENDING_LIST_PREFIX, limit: INBOX_PAGE_SIZE, cursor });

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

async function getInboxObject(
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
async function postAck(env: Env, kind: PlaytestKind, steamId: string, id: string): Promise<Response> {
  const indexKey = pendingIndexKey(kind, steamId, id);
  const ackedKey = `${bundlePrefix(kind, steamId, id)}/${ACKED_MARKER}`;
  const pending = await env.BUCKET.head(indexKey);
  if (pending === null) {
    // 索引が無くてもACKED済みなら再送とみなし冪等に200。ACKEDも無ければ本物のID取り違えで404
    // No index but ACKED present means a retry, so answer 200 idempotently; if ACKED is absent too, it's a genuine mismatch, so 404
    const alreadyAcked = await env.BUCKET.head(ackedKey);
    if (alreadyAcked !== null) return json({ acked: true });
    console.warn(`[inbox] rejected ack for an item that is not pending: ${indexKey}`);
    return fail("not-found", 404);
  }

  await env.BUCKET.put(ackedKey, new Date().toISOString());
  await env.BUCKET.delete(indexKey);
  return json({ acked: true });
}

async function getAllowlist(env: Env): Promise<Response> {
  return json({ steamIds: await readAllowlist(env.BUCKET) });
}

async function putAllowlist(request: Request, env: Env): Promise<Response> {
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
