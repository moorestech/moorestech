import type { Env } from "../env";
import { bundlePrefix, type PlaytestKind } from "../keys";
import { verifyDeclaredObjects, type MissingObject } from "../routes/uploadsVerify";
import type { DeclaredFile } from "./fileList";
import { createR2Client, presignPut } from "./presign";

export interface PresignedUpload {
  path: string;
  bytes: number;
  url: string;
}

// 宣言どおりの長さで既にR2にあるかの判定はcompleteと同じverifyDeclaredObjectsに任せ、未送信（キー無し）にだけURLを出す（prepareとimportが共有）
// Presence at the declared length is judged by the same verifyDeclaredObjects as complete, and only unsent (absent) keys get a URL (shared by prepare and import)
export async function presignUnsentFiles(env: Env, kind: PlaytestKind, steamId: string, id: string, files: DeclaredFile[], logTag: string): Promise<{ uploads: PresignedUpload[]; conflicts: MissingObject[] }> {
  const { missing } = await verifyDeclaredObjects(env.BUCKET, kind, steamId, id, files);
  const client = createR2Client(env);
  const now = new Date();
  const uploads: PresignedUpload[] = [];
  const conflicts: MissingObject[] = [];
  for (const object of missing) {
    if (object.actualBytes !== null) {
      // 長さ違いの既存キーはIf-None-Matchで上書きできずURLを出しても送れない。呼び出し元が見送れるよう応答で返す
      // An existing key of the wrong length can't be overwritten under If-None-Match, so a URL is useless; report it so the caller can give up on it
      console.warn(`[${logTag}] ${steamId}/${id}/${object.path} already exists with ${object.actualBytes} bytes instead of the declared ${object.expectedBytes}`);
      conflicts.push(object);
      continue;
    }
    const key = `${bundlePrefix(kind, steamId, id)}/${object.path}`;
    uploads.push({ path: object.path, bytes: object.expectedBytes, url: await presignPut(client, env, key, object.expectedBytes, now) });
  }
  return { uploads, conflicts };
}
