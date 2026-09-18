import { parseDeclaration, readDeclaration, writeDeclaration } from "../bundleDeclaration";
import { isAcked, READY_MARKER } from "../bundleMarkers";
import { UPLOAD_URL_TTL_SECONDS } from "../contract";
import type { Env } from "../env";
import { fail, json, requireMethod } from "../http";
import { bundlePrefix, isKind, isSafeSegment, pendingIndexKey, type PlaytestKind } from "../keys";
import { presignPut } from "../presign";
import { verifyToken } from "../token";
import { verifyDeclaredObjects } from "./uploadsVerify";

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
  if (rest.length === 1 && rest[0] === "prepare") {
    const denied = requireMethod(request, ["POST"], "upload prepare");
    if (denied !== null) return denied;
    return prepareUpload(request, env, kind, id);
  }
  if (rest.length === 1 && rest[0] === "complete") {
    const denied = requireMethod(request, ["POST"], "upload complete");
    if (denied !== null) return denied;
    return completeUpload(request, env, kind, id);
  }
  const denied = requireMethod(request, ["PUT"], "upload put");
  if (denied !== null) return denied;
  // 旧クライアントのPUT中継。バイト列はもう受けない（ADR 0064）。理由を返して再ビルドを促す
  // The old client's relayed PUT; bytes are no longer accepted (ADR 0064), so answer with the reason
  console.warn(`[upload] rejected a relayed PUT (direct upload required): /${segments.join("/")}`);
  return fail("direct-upload-required", 410);
}

// Bearerトークンだけが置き場を決める。戻り値はsteamIdか、そのまま返す拒否応答
// Only the bearer token decides the destination; returns the steamId or a rejection response to return as-is
async function authorize(request: Request, env: Env): Promise<string | Response> {
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

async function prepareUpload(request: Request, env: Env, kind: PlaytestKind, id: string): Promise<Response> {
  const steamId = await authorize(request, env);
  if (typeof steamId !== "string") return steamId;

  // 取り込み済みの箱へは宣言もURLも出さない。ACKED後の上書きで取り込み内容とR2がずれるのを防ぐ
  // An already acked box gets neither a declaration nor URLs, so R2 never drifts from what was ingested
  if (await isAcked(env.BUCKET, kind, steamId, id)) {
    console.warn(`[upload] ignored prepare for an already acked bundle: ${steamId}/${id}`);
    return json({ outcome: "acked" });
  }

  // 本文は外部入力のJSON（パース例外の境界）。壊れていれば400で理由をwarnする
  // The body is external JSON (a parse-exception boundary); a broken one is a 400 with the reason warned
  let body: unknown;
  try {
    body = await request.json();
  } catch {
    console.warn(`[upload] prepare body of ${steamId}/${id} is not JSON`);
    return fail("bad-request", 400);
  }
  const declaration = parseDeclaration(body);
  if (!declaration.ok) {
    console.warn(`[upload] rejected the declaration of ${steamId}/${id}: ${declaration.error} (${declaration.detail})`);
    return fail(declaration.error, declaration.status);
  }

  // 宣言を先に保存し、completeの照合元にする。URLは同じ時刻で揃えて発行する
  // The declaration is stored first as complete's reference; every URL is signed at the same instant
  await writeDeclaration(env.BUCKET, kind, steamId, id, declaration.files);
  const now = new Date();
  const uploads: { path: string; bytes: number; url: string }[] = [];
  for (const file of declaration.files) {
    const key = `${bundlePrefix(kind, steamId, id)}/${file.path}`;
    uploads.push({ path: file.path, bytes: file.bytes, url: await presignPut(env, key, file.bytes, now) });
  }
  return json({ outcome: "prepared", uploads, expiresInSeconds: UPLOAD_URL_TTL_SECONDS });
}

async function completeUpload(request: Request, env: Env, kind: PlaytestKind, id: string): Promise<Response> {
  const steamId = await authorize(request, env);
  if (typeof steamId !== "string") return steamId;

  // ACKED後のcompleteで索引を作り直すと取り込み済みが未ACKへ戻る。書かずに冪等の成功を返す
  // Re-creating the index after ACKED would resurrect an ingested bundle as pending, so answer an idempotent success without writing
  if (await isAcked(env.BUCKET, kind, steamId, id)) {
    console.warn(`[upload] ignored complete for an already acked bundle: ${steamId}/${id}`);
    return json({ ready: true });
  }
  const declared = await readDeclaration(env.BUCKET, kind, steamId, id);
  if (declared === null) {
    console.warn(`[upload] complete without a declaration: ${steamId}/${id}`);
    return fail("not-prepared", 409);
  }
  const verified = await verifyDeclaredObjects(env.BUCKET, kind, steamId, id, declared);
  if (verified.missing.length > 0) {
    console.warn(`[upload] ${steamId}/${id} is incomplete: ${verified.missing.map((m) => `${m.path}(${m.actualBytes ?? "absent"}/${m.expectedBytes})`).join(", ")}`);
    return json({ reason: "incomplete", missing: verified.missing }, 409);
  }

  // ファイル一覧は照合済みの側を使い、クライアントの申告は使わない。本文はmanifest原文とskippedの補足だけ
  // The file list comes from the verified side, never the client's claim; the body only supplements the raw manifest and skipped
  const supplement = await readCompleteSupplement(request, `${steamId}/${id}`);
  const summary = JSON.stringify({ kind, id, fileCount: verified.present.length, files: verified.present, ...supplement });
  await env.BUCKET.put(`${bundlePrefix(kind, steamId, id)}/${READY_MARKER}`, summary, { httpMetadata: { contentType: "application/json" } });

  // 未ACKの列挙をR2の全走査にしないため、READYと対の索引オブジェクトを置く。ackで消す
  // A paired index object keeps "pending" enumerable without scanning all of R2; ack deletes it
  await env.BUCKET.put(pendingIndexKey(kind, steamId, id), "");
  return json({ ready: true, fileCount: verified.present.length });
}

// 補足は取り込みの診断用で、欠けても箱は成立する。読めない部分は空に倒してwarnする（READYを止めない）
// The supplement only aids ingest diagnostics and the box stands without it; unreadable parts fall back to empty with a warning, never blocking READY
async function readCompleteSupplement(request: Request, label: string): Promise<{ skipped: unknown[]; manifest: string | null }> {
  // 本文は外部入力のJSON（パース例外の境界）
  // The body is external JSON (a parse-exception boundary)
  let body: unknown = null;
  try {
    body = await request.json();
  } catch {
    console.warn(`[upload] complete body of ${label} is not JSON; storing READY without manifest/skipped`);
    return { skipped: [], manifest: null };
  }
  // manifestのnullは正規（進行記録等）。形が契約と違うときだけwarnする
  // A null manifest is legitimate (e.g. progress records); only a shape off the contract is warned
  const fields = typeof body === "object" && body !== null ? (body as { manifest?: unknown; skipped?: unknown }) : {};
  const skipped = Array.isArray(fields.skipped) ? fields.skipped : [];
  const manifest = typeof fields.manifest === "string" ? fields.manifest : null;
  if (!Array.isArray(fields.skipped) || (fields.manifest !== null && typeof fields.manifest !== "string")) {
    console.warn(`[upload] complete body of ${label} does not match {manifest: string|null, skipped: []}; storing READY with those parts empty`);
  }
  return { skipped, manifest };
}
