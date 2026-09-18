import { AwsClient } from "aws4fetch";
import { UPLOAD_URL_TTL_SECONDS } from "../contract";
import type { Env } from "../env";

// aws4fetchは署名鍵の派生（HMAC 4段）をインスタンス単位でキャッシュする。1リクエストにつき1つだけ作って使い回す
// aws4fetch caches its derived signing key per instance; create exactly one per request and reuse it
export function createR2Client(env: Env): AwsClient {
  return new AwsClient({
    accessKeyId: env.R2_ACCESS_KEY_ID,
    secretAccessKey: env.R2_SECRET_ACCESS_KEY,
    service: "s3",
    region: "auto",
  });
}

// 署名に要る設定のうち空のものの名前。1つでもあれば署名は発行できない（壊れたURLを配らない）
// Names of the signing settings that are empty; any one of them means no URL can be issued (never hand out broken URLs)
export function missingR2SigningSettings(env: Env): string[] {
  const settings = { R2_ACCOUNT_ID: env.R2_ACCOUNT_ID, R2_BUCKET_NAME: env.R2_BUCKET_NAME, R2_ACCESS_KEY_ID: env.R2_ACCESS_KEY_ID, R2_SECRET_ACCESS_KEY: env.R2_SECRET_ACCESS_KEY };
  return Object.entries(settings).filter(([, value]) => !value).map(([name]) => name);
}

// R2のS3互換APIに対するPUT用の署名付きURL。Content-Lengthを署名ヘッダに含め、宣言と違う長さのPUTをR2に拒否させる
// A presigned PUT for R2's S3-compatible API; content-length is a signed header so R2 rejects a PUT whose length differs from the declaration
// If-None-Match: * も署名に含め、既にあるキー（送信済み・取り込み済みの原本）をこのURLで上書きさせない。既存キーにはR2が412を返す
// If-None-Match: * is signed too, so this URL can never overwrite an existing key (a sent or ingested original); R2 answers 412 for one
export async function presignPut(client: AwsClient, env: Env, key: string, contentLength: number, now: Date): Promise<string> {
  const encodedKey = key.split("/").map(encodeURIComponent).join("/");
  const url = new URL(`https://${env.R2_ACCOUNT_ID}.r2.cloudflarestorage.com/${env.R2_BUCKET_NAME}/${encodedKey}`);
  url.searchParams.set("X-Amz-Expires", String(UPLOAD_URL_TTL_SECONDS));
  const signed = await client.sign(
    new Request(url.toString(), { method: "PUT", headers: { "content-length": String(contentLength), "if-none-match": "*" } }),
    { aws: { signQuery: true, datetime: toAmzDate(now), allHeaders: true } },
  );
  return signed.url;
}

// SigV4のX-Amz-Date形式（YYYYMMDDTHHMMSSZ）。テストで固定時刻を渡せるよう引数で受ける
// SigV4's X-Amz-Date form (YYYYMMDDTHHMMSSZ); the instant is a parameter so tests can pin it
function toAmzDate(now: Date): string {
  return now.toISOString().replace(/[-:]/g, "").replace(/\.\d{3}Z$/, "Z");
}
