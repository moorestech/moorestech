import { AwsClient } from "aws4fetch";
import { UPLOAD_URL_TTL_SECONDS } from "./contract";
import type { Env } from "./env";

// R2のS3互換APIに対するPUT用の署名付きURL。Content-Lengthを署名ヘッダに含め、宣言と違う長さのPUTをR2に拒否させる
// A presigned PUT for R2's S3-compatible API; content-length is a signed header so R2 rejects a PUT whose length differs from the declaration
export async function presignPut(env: Env, key: string, contentLength: number, now: Date): Promise<string> {
  const client = new AwsClient({
    accessKeyId: env.R2_ACCESS_KEY_ID,
    secretAccessKey: env.R2_SECRET_ACCESS_KEY,
    service: "s3",
    region: "auto",
  });
  const encodedKey = key.split("/").map(encodeURIComponent).join("/");
  const url = new URL(`https://${env.R2_ACCOUNT_ID}.r2.cloudflarestorage.com/${env.R2_BUCKET_NAME}/${encodedKey}`);
  url.searchParams.set("X-Amz-Expires", String(UPLOAD_URL_TTL_SECONDS));
  const signed = await client.sign(
    new Request(url.toString(), { method: "PUT", headers: { "content-length": String(contentLength) } }),
    { aws: { signQuery: true, datetime: toAmzDate(now), allHeaders: true } },
  );
  return signed.url;
}

// SigV4のX-Amz-Date形式（YYYYMMDDTHHMMSSZ）。テストで固定時刻を渡せるよう引数で受ける
// SigV4's X-Amz-Date form (YYYYMMDDTHHMMSSZ); the instant is a parameter so tests can pin it
function toAmzDate(now: Date): string {
  return now.toISOString().replace(/[-:]/g, "").replace(/\.\d{3}Z$/, "Z");
}
