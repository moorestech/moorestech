import { ACKED_MARKER, isAcked, PURGED_MARKER, purgedMarkerKey } from "../bundleMarkers";
import type { Env } from "../env";
import { fail, json } from "../http";
import { bundlePrefix, type PlaytestKind } from "../keys";

const MAX_PURGE_REASON_LENGTH = 200;
// R2の一括deleteが1回に受けるキーの上限
// The most keys R2's bulk delete accepts per call
const R2_BULK_DELETE_LIMIT = 1000;

// 片付いた箱の生データを消す。取り込み済み（ACKED）だけを対象にし、PURGEDを先に書いてから消すので途中で落ちても削除済みと読める
// Purges a settled box's raw data; only ACKED boxes qualify, and PURGED is written first so a crash midway still reads as purged
export async function deleteBundle(request: Request, env: Env, kind: PlaytestKind, steamId: string, id: string): Promise<Response> {
  const label = `${kind}/${steamId}/${id}`;
  const parsed = await readPurgeReason(request, label);
  if (!parsed.ok) return fail("bad-request", 400);

  // 未取り込みの報告を消すと復旧できない。ACKEDの無い箱は拒否する
  // Purging an un-ingested report is unrecoverable, so a box without ACKED is refused
  if (!(await isAcked(env.BUCKET, kind, steamId, id))) {
    console.warn(`[purge] refused to purge a bundle that is not acked: ${label}`);
    return json({ reason: "not-acked" }, 409);
  }

  // 再実行でも最初の削除日時を保つため、印は無いときだけ書く
  // The marker is written only when absent so a re-run keeps the first purge time
  const marker = JSON.stringify({ purgedAt: new Date().toISOString(), reason: parsed.reason });
  // silent-ok: 既にあれば条件付きputが書かずにnullを返すのが冪等の意図 / 削除日時は410とGETの応答で読める
  await env.BUCKET.put(purgedMarkerKey(kind, steamId, id), marker, { onlyIf: { etagDoesNotMatch: "*" }, httpMetadata: { contentType: "application/json" } });

  const { keys, bytes } = await listPurgeTargets(env.BUCKET, `${bundlePrefix(kind, steamId, id)}/`);
  for (let start = 0; start < keys.length; start += R2_BULK_DELETE_LIMIT) {
    await env.BUCKET.delete(keys.slice(start, start + R2_BULK_DELETE_LIMIT));
  }
  console.log(`[purge] purged ${keys.length} objects (${bytes} bytes) from ${label}`);
  return json({ purged: true, deletedObjects: keys.length, deletedBytes: bytes });
}

type PurgeReason = { ok: true; reason: string | null } | { ok: false };

// 本文は任意。空なら理由なし(null)、壊れたJSON・文字列でない理由・長すぎる理由はok:false（呼び出し側が400にする）
// The body is optional; empty means no reason (null), while broken JSON or a non-string or overlong reason is ok:false (the caller answers 400)
async function readPurgeReason(request: Request, label: string): Promise<PurgeReason> {
  const text = await request.text();
  if (text.trim() === "") return { ok: true, reason: null };
  // 本文は外部入力のJSON（パース例外の境界）。壊れていればwarnして400へ
  // The body is external JSON (a parse-exception boundary); a broken one is warned and becomes a 400
  let body: unknown;
  try {
    body = JSON.parse(text);
  } catch {
    console.warn(`[purge] purge body of ${label} is not JSON`);
    return { ok: false };
  }
  const reason = (body as { reason?: unknown })?.reason;
  if (reason === undefined || reason === null) return { ok: true, reason: null };
  if (typeof reason !== "string" || reason.length > MAX_PURGE_REASON_LENGTH) {
    console.warn(`[purge] purge reason of ${label} is not a string of at most ${MAX_PURGE_REASON_LENGTH} chars`);
    return { ok: false };
  }
  return { ok: true, reason };
}

// 箱のprefix配下でACKEDとPURGED以外の全キー（__attachments/配下の子転送も含む）。消す前に全ページを読み切ってからまとめて消す
// Every key under the box prefix except ACKED and PURGED (including __attachments/ child transfers); all pages are read before anything is deleted
async function listPurgeTargets(bucket: R2Bucket, prefix: string): Promise<{ keys: string[]; bytes: number }> {
  const kept = new Set([`${prefix}${ACKED_MARKER}`, `${prefix}${PURGED_MARKER}`]);
  const keys: string[] = [];
  let bytes = 0;
  let cursor: string | undefined;
  do {
    const page = await bucket.list({ prefix, cursor, limit: 1000 });
    for (const object of page.objects) {
      if (kept.has(object.key)) continue;
      keys.push(object.key);
      bytes += object.size;
    }
    cursor = page.truncated ? page.cursor : undefined;
  } while (cursor !== undefined);
  return { keys, bytes };
}
