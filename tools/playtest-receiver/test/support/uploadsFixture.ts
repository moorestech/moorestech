import { env } from "cloudflare:test";
import { signToken } from "../../src/token";
import type { Env } from "../../src/env";

// uploads系のテストで共有する固定値とヘルパー。2ファイルに分割したため重複させない
// Shared fixtures and helpers for the uploads tests; kept in one place since the tests are split across two files
export const workerEnv = env as unknown as Env;
export const STEAM_ID = "76561198000000001";
export const ID = "20260913_120000_aaaa1111";
export const noNetwork: typeof fetch = (async () => {
  throw new Error("tests must not reach the network");
}) as unknown as typeof fetch;

export async function bearer(steamId = STEAM_ID): Promise<string> {
  return `Bearer ${await signToken(workerEnv.SESSION_HMAC_SECRET, steamId, Math.floor(Date.now() / 1000))}`;
}

export async function clean(): Promise<void> {
  const listed = await workerEnv.BUCKET.list({ limit: 1000 });
  await Promise.all(listed.objects.map((object) => workerEnv.BUCKET.delete(object.key)));
}
