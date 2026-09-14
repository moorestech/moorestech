// Workerのbindings。secretsはCLIで設定
// Bindings the Worker receives; secrets are provisioned with `wrangler secret put`
export interface Env {
  BUCKET: R2Bucket;
  STEAM_APP_ID: string;
  STEAM_WEB_API_KEY: string;
  SESSION_HMAC_SECRET: string;
  ADMIN_KEY: string;
}
