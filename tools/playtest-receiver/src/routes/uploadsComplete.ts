import { isAcked, READY_MARKER } from "../bundleMarkers";
import { DECLARATION_UNREADABLE_REASON } from "../contract";
import type { Env } from "../env";
import { fail, json } from "../http";
import { bundlePrefix, pendingIndexKey, type PlaytestKind } from "../keys";
import { readDeclaration } from "../uploads/bundleDeclaration";
import { authorizeUpload } from "./uploadsAuthorize";
import { verifyDeclaredObjects } from "./uploadsVerify";

export async function completeUpload(request: Request, env: Env, kind: PlaytestKind, id: string): Promise<Response> {
  const steamId = await authorizeUpload(request, env);
  if (typeof steamId !== "string") return steamId;

  // ACKED後のcompleteで索引を作り直すと取り込み済みが未ACKへ戻る。書かずに冪等の成功を返す
  // Re-creating the index after ACKED would resurrect an ingested bundle as pending, so answer an idempotent success without writing
  if (await isAcked(env.BUCKET, kind, steamId, id)) {
    console.warn(`[upload] ignored complete for an already acked bundle: ${steamId}/${id}`);
    return json({ ready: true });
  }
  const declared = await readDeclaration(env.BUCKET, kind, steamId, id);
  if (declared.state === "absent") {
    console.warn(`[upload] complete without a declaration: ${steamId}/${id}`);
    return fail("not-prepared", 409);
  }
  if (declared.state === "unreadable") {
    // 壊れたDECLAREDでは照合元が無い。無い扱いにせず、人が箱を調べるまで拒否する
    // A broken DECLARED leaves nothing to verify against; refuse rather than treat it as absent until a human inspects the box
    console.warn(`[upload] refused complete of ${steamId}/${id} because its DECLARED ${declared.reason}`);
    return fail(DECLARATION_UNREADABLE_REASON, 409);
  }
  const verified = await verifyDeclaredObjects(env.BUCKET, kind, steamId, id, declared.declaration.files);
  if (verified.missing.length > 0) {
    console.warn(`[upload] ${steamId}/${id} is incomplete: ${verified.missing.map((m) => `${m.path}(${m.actualBytes ?? "absent"}/${m.expectedBytes})`).join(", ")}`);
    return json({ reason: "incomplete", missing: verified.missing }, 409);
  }

  // ファイル一覧は照合済みの側を使い、クライアントの申告は使わない。本文はmanifest原文とskippedの補足だけ
  // The file list comes from the verified side, never the client's claim; the body only supplements the raw manifest and skipped
  const supplement = await readCompleteSupplement(request, `${steamId}/${id}`);
  // 照合と本文読み取りの間にACKされうる。READYを書く直前に確かめ直し、取り込み済みを未ACKへ戻さない
  // An ack may land during verification and body reading; check again right before writing READY so an ingested box never returns to pending
  if (await isAcked(env.BUCKET, kind, steamId, id)) {
    console.warn(`[upload] ${steamId}/${id} was acked during complete; READY is not written`);
    return json({ ready: true });
  }
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
