import { ackedMarkerKey, readPurged } from "../../bundleMarkers";
import { DECLARATION_UNREADABLE_REASON } from "../../contract";
import type { Env } from "../../env";
import { fail, json } from "../../http";
import { pendingIndexKey, type PlaytestKind } from "../../keys";
import { readImportDeclaration } from "../../uploads/importDeclaration";
import { verifyDeclaredObjects } from "../uploadsVerify";
import { purgedImportAnswer } from "./adminImport";

// zip取り込みの確定。importで保存した宣言の全ファイルが宣言どおりの長さでR2にあるときだけACKEDを書く
// Settles a zip import; ACKED is written only when every file of the stored import declaration is in R2 at its declared length
export async function postImportComplete(env: Env, kind: PlaytestKind, steamId: string, id: string): Promise<Response> {
  const label = `${kind}/${steamId}/${id}`;
  // importとcompleteの間に片付けのDELETEが走った箱は、揃っていても取り込み済みにしない
  // A box purged between import and complete is never settled, even if its files look complete
  if ((await readPurged(env.BUCKET, kind, steamId, id)).purged) return purgedImportAnswer(label);

  const declared = await readImportDeclaration(env.BUCKET, kind, steamId, id);
  if (declared.state === "absent") {
    console.warn(`[import] import/complete without a prior import: ${label}`);
    return fail("not-imported", 409);
  }
  if (declared.state === "unreadable") {
    console.warn(`[import] refused import/complete of ${label} because its IMPORT_DECLARED ${declared.reason}`);
    return fail(DECLARATION_UNREADABLE_REASON, 409);
  }

  // 不足（actualBytes=null）と長さの食い違いを一覧で返し、ACKEDは書かない
  // Missing files (actualBytes=null) and length mismatches are listed back, and ACKED is not written
  const verified = await verifyDeclaredObjects(env.BUCKET, kind, steamId, id, declared.files);
  if (verified.missing.length > 0) {
    console.warn(`[import] ${label} is incomplete: ${verified.missing.map((m) => `${m.path}(${m.actualBytes ?? "absent"}/${m.expectedBytes})`).join(", ")}`);
    return json({ reason: "incomplete", missing: verified.missing }, 409);
  }
  if ((await readPurged(env.BUCKET, kind, steamId, id)).purged) return purgedImportAnswer(label);

  // 既にACKEDがあれば最初の日時を保つ。受け口経由で残っていた未ACK索引も消す
  // An existing ACKED keeps its first timestamp; any pending index left from the receiver path is dropped
  // silent-ok: 既にACKEDがあれば条件付きputが書かずにnullを返すのが冪等の意図 / 取り込み済みの印は既にあるので誰も困らない
  await env.BUCKET.put(ackedMarkerKey(kind, steamId, id), new Date().toISOString(), { onlyIf: { etagDoesNotMatch: "*" } });
  await env.BUCKET.delete(pendingIndexKey(kind, steamId, id));
  return json({ acked: true, fileCount: verified.present.length });
}
