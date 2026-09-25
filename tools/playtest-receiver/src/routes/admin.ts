import { ackedMarkerKey, isAcked } from "../bundleMarkers";
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

const INBOX_PAGE_SIZE = 100;

// 管理API経路の一致判定とディスパッチをここへ寄せる。一致しなければnullでindex.tsの次の経路へ委ねる
// Path matching and dispatch for the admin API live here; returns null on a non-match so index.ts can try the next route
export async function routeAdmin(request: Request, env: Env, segments: string[]): Promise<Response | null> {
  if (segments[0] !== "v1") return null;
  if (segments[1] !== "inbox") return null;

  // 管理APIの認証はここ1箇所だけで行う。経路の形やメソッドの正誤に関わらず、鍵なし・不一致は必ず401
  // The admin API is authenticated in exactly one place; a missing or mismatched key is always 401, regardless of path shape or method
  const denied = requireAdmin(request, env);
  if (denied !== null) return denied;

  return routeInbox(request, env, segments);
}

async function routeInbox(request: Request, env: Env, segments: string[]): Promise<Response> {
  if (segments.length === 2) {
    const denied = requireMethod(request, ["GET"], "/v1/inbox");
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
    const denied = requireMethod(request, ["POST"], "inbox ack");
    if (denied !== null) return denied;
    return postAck(env, kind, steamId, id);
  }
  const denied = requireMethod(request, ["GET"], "inbox object");
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
    // ACKEDが正本。ack途中の失敗等で索引だけ残った項目は一覧から外し、古い索引も片付ける
    // ACKED is the source of truth; an index entry left behind (e.g. a crash mid-ack) is excluded and its stale key deleted
    if (await isAcked(env.BUCKET, parsed.kind, parsed.steamId, parsed.id)) {
      console.warn(`[inbox] dropped a stale pending index entry for an acked bundle: ${object.key}`);
      await env.BUCKET.delete(object.key);
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

// ackは「ACKED済みなら冪等200→pendingの実在確認→ACKEDを書く→索引を消す」。ACKEDを先に書くので途中で落ちても取り込み済みは確定する
// Ack: idempotent 200 if already ACKED, else confirm pending, write ACKED, drop the index; ACKED is written first so a crash still settles "ingested"
async function postAck(env: Env, kind: PlaytestKind, steamId: string, id: string): Promise<Response> {
  const indexKey = pendingIndexKey(kind, steamId, id);
  if (await isAcked(env.BUCKET, kind, steamId, id)) {
    // 再送（at-least-once）。残っていれば索引も消して200
    // A retry (at-least-once); also drop any leftover index and answer 200
    await env.BUCKET.delete(indexKey);
    return json({ acked: true });
  }
  if ((await env.BUCKET.head(indexKey)) === null) {
    console.warn(`[inbox] rejected ack for an item that is not pending: ${indexKey}`);
    return fail("not-found", 404);
  }

  await env.BUCKET.put(ackedMarkerKey(kind, steamId, id), new Date().toISOString());
  await env.BUCKET.delete(indexKey);
  return json({ acked: true });
}
