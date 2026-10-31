import { ACKED_MARKER, ackedMarkerKey, DECLARED_MARKER, PURGED_MARKER, readPurged } from "../../bundleMarkers";
import { UPLOAD_URL_TTL_SECONDS } from "../../contract";
import type { Env } from "../../env";
import { fail, json } from "../../http";
import type { PlaytestKind } from "../../keys";
import { parseFileList, type FileListLimits } from "../../uploads/fileList";
import { missingR2SigningSettings } from "../../uploads/presign";
import { presignUnsentFiles } from "../../uploads/presignUnsent";

// zipの箱はREADYを持つので許し、取り込み・宣言・片付けの印だけを予約名にする
// A zip's box carries READY, so it is allowed; only the ingest, declaration and purge markers are reserved
const IMPORT_RESERVED_SEGMENTS: ReadonlySet<string> = new Set([ACKED_MARKER, DECLARED_MARKER, PURGED_MARKER]);
// zipの全ファイルをR2へ残すため、上限はR2単一PUTの上限（約5GiB）を下回る5GBだけにし、件数と合計は縛らない
// To keep every file of a zip in R2, only 5GB (under R2's ~5GiB single-PUT cap) limits a file; count and total are unbounded
const R2_SINGLE_PUT_MAX_BYTES = 5 * 1000 * 1000 * 1000;
const IMPORT_FILE_LIMITS: FileListLimits = { maxFileBytes: R2_SINGLE_PUT_MAX_BYTES, maxBundleFiles: Number.POSITIVE_INFINITY, maxBundleBytes: Number.POSITIVE_INFINITY };

// 受け口に届かなかった報告（テスターが書き出したzip）をMac miniからR2へ上げ直すための署名付きPUT URLを出す
// Issues presigned PUT URLs so the Mac mini can re-upload a report that never reached the receiver (a tester's exported zip)
export async function postImport(request: Request, env: Env, kind: PlaytestKind, steamId: string, id: string): Promise<Response> {
  const label = `${kind}/${steamId}/${id}`;
  const missingSettings = missingR2SigningSettings(env);
  if (missingSettings.length > 0) {
    console.error(`[import] cannot presign for ${label} because ${missingSettings.join(", ")} is empty`);
    return fail("server-misconfigured", 500);
  }
  if ((await readPurged(env.BUCKET, kind, steamId, id)).purged) return purgedAnswer(label);

  // 本文は外部入力のJSON（パース例外の境界）。壊れていれば400で理由をwarnする
  // The body is external JSON (a parse-exception boundary); a broken one is a 400 with the reason warned
  let body: unknown;
  try {
    body = await request.json();
  } catch {
    console.warn(`[import] import body of ${label} is not JSON`);
    return fail("bad-request", 400);
  }
  const checked = parseFileList((body as { files?: unknown })?.files, IMPORT_RESERVED_SEGMENTS, IMPORT_FILE_LIMITS);
  if (!checked.ok) {
    // 運用者がどのファイルのどの上限かを読めるよう、detailを応答にも載せる
    // detail is echoed in the reply so the operator can see which file hit which limit
    console.warn(`[import] rejected the file list of ${label}: ${checked.error} (${checked.detail})`);
    return json({ reason: checked.error, detail: checked.detail }, checked.status);
  }

  const { uploads, conflicts } = await presignUnsentFiles(env, kind, steamId, id, checked.files, "import");

  // ACKEDを書いて取り込み一覧に出さず、片付けのDELETEの対象にする。既にあれば最初の日時を保つ
  // Writing ACKED keeps the box out of the inbox and makes it purgeable; an existing one keeps its first timestamp
  // silent-ok: 既にACKEDがあれば条件付きputが書かずにnullを返すのが意図 / 取り込み済みの印は既にあるので誰も困らない
  await env.BUCKET.put(ackedMarkerKey(kind, steamId, id), new Date().toISOString(), { onlyIf: { etagDoesNotMatch: "*" } });

  // 署名中に片付けられた箱へURLを返すと削除済みの箱へ送らせてしまう。返す直前に確かめ直す
  // Returning URLs for a box purged while signing would send into a purged box; check again right before answering
  if ((await readPurged(env.BUCKET, kind, steamId, id)).purged) return purgedAnswer(label);
  return json({ uploads, conflicts, expiresInSeconds: UPLOAD_URL_TTL_SECONDS });
}

function purgedAnswer(label: string): Response {
  console.warn(`[import] refused to import into a purged bundle: ${label}`);
  return json({ reason: "purged" }, 409);
}
