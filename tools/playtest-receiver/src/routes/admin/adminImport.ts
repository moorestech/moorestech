import { readPurged } from "../../bundleMarkers";
import { UPLOAD_URL_TTL_SECONDS } from "../../contract";
import type { Env } from "../../env";
import { fail, json } from "../../http";
import type { PlaytestKind } from "../../keys";
import { parseImportFiles, writeImportDeclaration } from "../../uploads/importDeclaration";
import { missingR2SigningSettings } from "../../uploads/presign";
import { presignUnsentFiles } from "../../uploads/presignUnsent";

// 受け口に届かなかった報告（テスターが書き出したzip）をMac miniからR2へ上げ直すための署名付きPUT URLを出す
// ACKEDはここでは書かない。全ファイルが揃ったことをimport/completeが確かめてから書く
// Issues presigned PUT URLs so the Mac mini can re-upload a report that never reached the receiver (a tester's exported zip)
// ACKED is not written here; import/complete writes it after confirming every file arrived
export async function postImport(request: Request, env: Env, kind: PlaytestKind, steamId: string, id: string): Promise<Response> {
  const label = `${kind}/${steamId}/${id}`;
  const missingSettings = missingR2SigningSettings(env);
  if (missingSettings.length > 0) {
    console.error(`[import] cannot presign for ${label} because ${missingSettings.join(", ")} is empty`);
    return fail("server-misconfigured", 500);
  }
  if ((await readPurged(env.BUCKET, kind, steamId, id)).purged) return purgedImportAnswer(label);

  // 本文は外部入力のJSON（パース例外の境界）。壊れていれば400で理由をwarnする
  // The body is external JSON (a parse-exception boundary); a broken one is a 400 with the reason warned
  let body: unknown;
  try {
    body = await request.json();
  } catch {
    console.warn(`[import] import body of ${label} is not JSON`);
    return fail("bad-request", 400);
  }
  const checked = parseImportFiles((body as { files?: unknown })?.files);
  if (!checked.ok) {
    // 運用者がどのファイルのどの上限かを読めるよう、detailを応答にも載せる
    // detail is echoed in the reply so the operator can see which file hit which limit
    console.warn(`[import] rejected the file list of ${label}: ${checked.error} (${checked.detail})`);
    return json({ reason: checked.error, detail: checked.detail }, checked.status);
  }

  await writeImportDeclaration(env.BUCKET, kind, steamId, id, checked.files);
  const { uploads, conflicts } = await presignUnsentFiles(env, kind, steamId, id, checked.files, "import");

  // 署名中に片付けられた箱へURLを返すと削除済みの箱へ送らせてしまう。返す直前に確かめ直す
  // Returning URLs for a box purged while signing would send into a purged box; check again right before answering
  if ((await readPurged(env.BUCKET, kind, steamId, id)).purged) return purgedImportAnswer(label);
  return json({ uploads, conflicts, expiresInSeconds: UPLOAD_URL_TTL_SECONDS });
}

export function purgedImportAnswer(label: string): Response {
  console.warn(`[import] refused to import into a purged bundle: ${label}`);
  return json({ reason: "purged" }, 409);
}
