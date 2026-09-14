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

  if (segments[1] === "allowlist" && segments.length === 2) {
    if (request.method === "GET") return getAllowlist(request, env);
    if (request.method === "PUT") return putAllowlist(request, env);
    console.warn(`[router] rejected method ${request.method} for /v1/allowlist`);
    return fail("method-not-allowed", 405);
  }

  if (segments[1] === "inbox") return routeInbox(request, env, segments);

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
    return postAck(request, env, kind, steamId, id);
  }
  if (request.method !== "GET") {
    console.warn(`[router] rejected method ${request.method} for inbox object`);
    return fail("method-not-allowed", 405);
  }
  return getInboxObject(request, env, kind, steamId, id, rest);
}

export async function getInbox(request: Request, env: Env): Promise<Response> {
  const denied = requireAdmin(request, env);
  if (denied !== null) return denied;

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
  request: Request,
  env: Env,
  kind: PlaytestKind,
  steamId: string,
  id: string,
  pathSegments: string[],
): Promise<Response> {
  const denied = requireAdmin(request, env);
  if (denied !== null) return denied;

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

// ackは「ACKEDを書いてから索引を消す」の2手。途中で落ちても項目は見えたままになる
// Ack is two steps: write ACKED, then drop the index, so a crash in between leaves the item still visible
export async function postAck(request: Request, env: Env, kind: PlaytestKind, steamId: string, id: string): Promise<Response> {
  const denied = requireAdmin(request, env);
  if (denied !== null) return denied;

  await env.BUCKET.put(`${bundlePrefix(kind, steamId, id)}/${ACKED_MARKER}`, new Date().toISOString());
  await env.BUCKET.delete(pendingIndexKey(kind, steamId, id));
  return json({ acked: true });
}

export async function getAllowlist(request: Request, env: Env): Promise<Response> {
  const denied = requireAdmin(request, env);
  if (denied !== null) return denied;
  return json({ steamIds: await readAllowlist(env.BUCKET) });
}

export async function putAllowlist(request: Request, env: Env): Promise<Response> {
  const denied = requireAdmin(request, env);
  if (denied !== null) return denied;

  // 全置換なので壊れた本文で上書きしない。パースまたは形が不正なら現状を残して400を返す
  // This replaces the whole list, so a malformed body must never overwrite it; a parse or shape failure keeps the current list
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
  console.warn(`[allowlist] replaced with ${new Set(steamIds).size} steamIds`);
  return json({ steamIds: [...new Set(steamIds)] });
}
