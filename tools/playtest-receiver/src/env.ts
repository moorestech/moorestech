// Workerのbindings。secretsはCLIで設定
// Bindings the Worker receives; secrets are provisioned with `wrangler secret put`
export interface Env {
  BUCKET: R2Bucket;
  STEAM_APP_ID: string;
  STEAM_WEB_API_KEY: string;
  SESSION_HMAC_SECRET: string;
  ADMIN_KEY: string;
  // 署名URLはS3互換API。ID/バケットはvars、鍵はsecrets
  // Presigned URLs via S3-compatible API; id/bucket are vars, keys are secrets
  R2_ACCOUNT_ID: string;
  R2_BUCKET_NAME: string;
  R2_ACCESS_KEY_ID: string;
  R2_SECRET_ACCESS_KEY: string;
}
