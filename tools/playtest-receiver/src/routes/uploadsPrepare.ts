import { isAcked } from "../bundleMarkers";
import { UPLOAD_URL_TTL_SECONDS } from "../contract";
import type { Env } from "../env";
import { fail, json } from "../http";
import { bundlePrefix, type PlaytestKind } from "../keys";
import { classifyRedeclaration, parseDeclaration, readDeclaration, writeDeclaration, type DeclaredFile } from "../uploads/bundleDeclaration";
import { createR2Client, missingR2SigningSettings, presignPut } from "../uploads/presign";
import { authorizeUpload } from "./uploadsAuthorize";
import { listBundleObjectSizes } from "./uploadsVerify";

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

  // DECLAREDは縮小だけ許すwrite-once。追加やbytes変更は箱の上限の迂回と旧宣言分の孤立を招くため拒否する
  // DECLARED is write-once except for shrinking; additions or byte changes would dodge the bundle limits and orphan old files, so refuse them
  const stored = await readDeclaration(env.BUCKET, kind, steamId, id);
  const redeclaration = stored === null ? null : classifyRedeclaration(stored, declaration.files);
  if (redeclaration === "conflict") {
    console.warn(`[upload] rejected a re-prepare of ${label} that adds or resizes files against the stored DECLARED`);
    return fail("declaration-conflict", 409);
  }
  if (redeclaration !== "same") {
    // 新規宣言か縮小（D3で見送ったファイルを外した再試行）。本文の読み取り中にACKされうるので書く直前に確かめ直す
    // A new declaration or a shrink (a retry that dropped files skipped under D3); an ack may land while the body is read, so re-check right before writing
    if (await isAcked(env.BUCKET, kind, steamId, id)) return ackedAnswer(label);
    if (redeclaration === "shrunk") console.warn(`[upload] ${label} shrank its declaration from ${stored?.length} to ${declaration.files.length} files`);
    await writeDeclaration(env.BUCKET, kind, steamId, id, declaration.files);
  }

  const uploads = await presignUnsentFiles(env, kind, steamId, id, declaration.files);
  // 署名中にACKされた箱へURLを返すと、取り込み後の原本へ送らせてしまう。返す直前に確かめ直す
  // Returning URLs for a box acked while signing would send into an ingested original; check again right before answering
  if (await isAcked(env.BUCKET, kind, steamId, id)) return ackedAnswer(label);
  return json({ outcome: "prepared", uploads, expiresInSeconds: UPLOAD_URL_TTL_SECONDS });
}

function ackedAnswer(label: string): Response {
  console.warn(`[upload] ignored prepare for an already acked bundle: ${label}`);
  return json({ outcome: "acked" });
}

// 宣言どおりの長さで既にR2にあるファイルにはURLを出さない（再送も上書きもさせない）。既存判定は箱のlist1回で済ませる
// Files already in R2 at their declared length get no URL (no resend, no overwrite); existence comes from a single list of the box
async function presignUnsentFiles(env: Env, kind: PlaytestKind, steamId: string, id: string, files: DeclaredFile[]): Promise<{ path: string; bytes: number; url: string }[]> {
  const existing = await listBundleObjectSizes(env.BUCKET, kind, steamId, id);
  const client = createR2Client(env);
  const now = new Date();
  const uploads: { path: string; bytes: number; url: string }[] = [];
  for (const file of files) {
    const actual = existing.get(file.path);
    if (actual === file.bytes) continue;
    if (actual !== undefined) {
      // 長さ違いの既存キーはIf-None-Matchで上書きできず、completeが欠損として返し続ける。原因を追えるよう残す
      // An existing key of the wrong length can't be overwritten under If-None-Match and complete keeps reporting it; log it so the cause is traceable
      console.warn(`[upload] ${steamId}/${id}/${file.path} already exists with ${actual} bytes instead of the declared ${file.bytes}`);
    }
    const key = `${bundlePrefix(kind, steamId, id)}/${file.path}`;
    uploads.push({ path: file.path, bytes: file.bytes, url: await presignPut(client, env, key, file.bytes, now) });
  }
  return uploads;
}
