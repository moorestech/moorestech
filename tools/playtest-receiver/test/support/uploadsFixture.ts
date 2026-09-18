import { env } from "cloudflare:test";
import { handle } from "../../src/index";
import { signToken } from "../../src/token";
import type { Env } from "../../src/env";
import { bundlePrefix, type PlaytestKind } from "../../src/keys";

export { handle };

// uploads系のテストで共有する固定値とヘルパー。複数ファイルに分割したため重複させない
// Shared fixtures and helpers for the uploads tests; kept in one place since the tests are split across files
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

// 署名付きURLへの直接PUTは、テストではR2へ同じキーで置くことで模擬する（S3 APIはminiflareに無い）
// A direct PUT to the presigned URL is simulated by putting the same key into R2 (miniflare has no S3 API)
export async function putDirect(kind: PlaytestKind, steamId: string, id: string, path: string, body: string): Promise<void> {
  await workerEnv.BUCKET.put(`${bundlePrefix(kind, steamId, id)}/${path}`, body);
}

// 初回宣言（世代1）の本文。世代を指定したいテストはdeclarationOfGenerationを使う
// Body of a first declaration (generation 1); tests that need another generation use declarationOfGeneration
export function declaration(entries: Record<string, number>): string {
  return declarationOfGeneration(1, entries);
}

export function declarationOfGeneration(generation: number, entries: Record<string, number>): string {
  return JSON.stringify({ generation, files: Object.entries(entries).map(([path, bytes]) => ({ path, bytes })) });
}

export async function prepare(kind: string, id: string, body: string, steamId = STEAM_ID): Promise<Response> {
  return handle(new Request(`https://x/v1/uploads/${kind}/${id}/prepare`, { method: "POST", headers: { authorization: await bearer(steamId), "content-type": "application/json" }, body }), workerEnv, noNetwork);
}

export async function complete(kind: string, id: string, body = "{}", steamId = STEAM_ID): Promise<Response> {
  return handle(new Request(`https://x/v1/uploads/${kind}/${id}/complete`, { method: "POST", headers: { authorization: await bearer(steamId), "content-type": "application/json" }, body }), workerEnv, noNetwork);
}
