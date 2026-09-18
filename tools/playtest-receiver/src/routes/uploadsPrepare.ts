import { isAcked } from "../bundleMarkers";
import { DECLARATION_CONFLICT_REASON, DECLARATION_UNREADABLE_REASON, PREPARE_OUTCOME_ACKED, PREPARE_OUTCOME_PREPARED, UPLOAD_URL_TTL_SECONDS } from "../contract";
import type { Env } from "../env";
import { fail, json } from "../http";
import { bundlePrefix, type PlaytestKind } from "../keys";
import { parseDeclaration, type DeclaredFile } from "../uploads/bundleDeclaration";
import { settleDeclaration } from "../uploads/declarationSettle";
import { createR2Client, missingR2SigningSettings, presignPut } from "../uploads/presign";
import { authorizeUpload } from "./uploadsAuthorize";
import { verifyDeclaredObjects, type MissingObject } from "./uploadsVerify";

export async function prepareUpload(request: Request, env: Env, kind: PlaytestKind, id: string): Promise<Response> {
  const steamId = await authorizeUpload(request, env);
  if (typeof steamId !== "string") return steamId;
  const label = `${steamId}/${id}`;

  // 署名設定が空のまま動くと、R2が必ず拒否するURLを200で配ってしまう。発行前に500で止める
  // Running with an empty signing setting would hand out URLs R2 always rejects with a 200; stop with a 500 before issuing
  const missingSettings = missingR2SigningSettings(env);
  if (missingSettings.length > 0) {
    console.error(`[upload] cannot presign for ${label} because ${missingSettings.join(", ")} is empty`);
    return fail("server-misconfigured", 500);
  }

  // 取り込み済みの箱へは宣言もURLも出さない。ACKED後の上書きで取り込み内容とR2がずれるのを防ぐ
  // An already acked box gets neither a declaration nor URLs, so R2 never drifts from what was ingested
  if (await isAcked(env.BUCKET, kind, steamId, id)) return ackedAnswer(label);

  // 本文は外部入力のJSON（パース例外の境界）。壊れていれば400で理由をwarnする
  // The body is external JSON (a parse-exception boundary); a broken one is a 400 with the reason warned
  let body: unknown;
  try {
    body = await request.json();
  } catch {
    console.warn(`[upload] prepare body of ${label} is not JSON`);
    return fail("bad-request", 400);
  }
  const declaration = parseDeclaration(body);
  if (!declaration.ok) {
    console.warn(`[upload] rejected the declaration of ${label}: ${declaration.error} (${declaration.detail})`);
    return fail(declaration.error, declaration.status);
  }

  // DECLAREDは世代付きのwrite-once。同世代は完全一致だけ、縮小は上の世代でだけ許す（箱の上限の迂回と旧宣言分の孤立を防ぐ）
  // DECLARED is generation-scoped write-once: a generation matches exactly, only a higher one may shrink (no dodging limits, no orphaned files)
  const settlement = await settleDeclaration(env.BUCKET, kind, steamId, id, { generation: declaration.generation, files: declaration.files });
  if (settlement.kind === "acked") return ackedAnswer(label);
  if (settlement.kind === "conflict") {
    console.warn(`[upload] rejected a re-prepare of ${label} against the stored DECLARED: ${settlement.detail}`);
    return fail(DECLARATION_CONFLICT_REASON, 409);
  }
  if (settlement.kind === "unreadable") {
    // 壊れたDECLAREDを無い扱いにするとガードが外れる。人が箱を調べるまで拒否し続ける
    // Treating a broken DECLARED as absent would drop the guard; keep refusing until a human inspects the box
    console.warn(`[upload] refused prepare of ${label} because its DECLARED ${settlement.reason}`);
    return fail(DECLARATION_UNREADABLE_REASON, 409);
  }

  const { uploads, conflicts } = await presignUnsentFiles(env, kind, steamId, id, declaration.files);
  // 署名中にACKされた箱へURLを返すと、取り込み後の原本へ送らせてしまう。返す直前に確かめ直す
  // Returning URLs for a box acked while signing would send into an ingested original; check again right before answering
  if (await isAcked(env.BUCKET, kind, steamId, id)) return ackedAnswer(label);
  return json({ outcome: PREPARE_OUTCOME_PREPARED, uploads, conflicts, expiresInSeconds: UPLOAD_URL_TTL_SECONDS });
}

function ackedAnswer(label: string): Response {
  console.warn(`[upload] ignored prepare for an already acked bundle: ${label}`);
  return json({ outcome: PREPARE_OUTCOME_ACKED });
}

// 宣言どおりの長さで既にR2にあるかの判定はcompleteと同じverifyDeclaredObjectsに任せ、未送信（キー無し）にだけURLを出す
// Whether a file already sits in R2 at its declared length is judged by the same verifyDeclaredObjects as complete; only unsent (absent) keys get a URL
async function presignUnsentFiles(env: Env, kind: PlaytestKind, steamId: string, id: string, files: DeclaredFile[]): Promise<{ uploads: { path: string; bytes: number; url: string }[]; conflicts: MissingObject[] }> {
  const { missing } = await verifyDeclaredObjects(env.BUCKET, kind, steamId, id, files);
  const client = createR2Client(env);
  const now = new Date();
  const uploads: { path: string; bytes: number; url: string }[] = [];
  const conflicts: MissingObject[] = [];
  for (const object of missing) {
    if (object.actualBytes !== null) {
      // 長さ違いの既存キーはIf-None-Matchで上書きできずURLを出しても送れない。クライアントが見送れるよう応答で返す
      // An existing key of the wrong length can't be overwritten under If-None-Match, so a URL is useless; report it so the client can give up on it
      console.warn(`[upload] ${steamId}/${id}/${object.path} already exists with ${object.actualBytes} bytes instead of the declared ${object.expectedBytes}`);
      conflicts.push(object);
      continue;
    }
    const key = `${bundlePrefix(kind, steamId, id)}/${object.path}`;
    uploads.push({ path: object.path, bytes: object.expectedBytes, url: await presignPut(client, env, key, object.expectedBytes, now) });
  }
  return { uploads, conflicts };
}
