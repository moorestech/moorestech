// Workerのbindings。secretsはCLIで設定
// Bindings the Worker receives; secrets are provisioned with `wrangler secret put`
export interface Env {
  BUCKET: R2Bucket;
  STEAM_APP_ID: string;
  STEAM_WEB_API_KEY: string;
  SESSION_HMAC_SECRET: string;
  ADMIN_KEY: string;
  // 署名付きURLはS3互換APIで作る。アカウントIDとバケット名はvars、アクセスキーはsecrets
  // Presigned URLs come from the S3-compatible API; account id and bucket name are vars, the access key pair is secrets
  R2_ACCOUNT_ID: string;
  R2_BUCKET_NAME: string;
  R2_ACCESS_KEY_ID: string;
  R2_SECRET_ACCESS_KEY: string;
}
