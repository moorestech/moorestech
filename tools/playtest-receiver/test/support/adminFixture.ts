import { STEAM_ID, handle, noNetwork, workerEnv } from "./uploadsFixture";

// 管理APIの箱単位操作を叩く共有ヘルパー。箱は report/STEAM_ID/BOX_ID に固定する
// Shared helpers for box-level admin calls; the box is pinned to report/STEAM_ID/BOX_ID
export const ADMIN_HEADERS = { "x-admin-key": "test-admin-key" };
export const BOX_ID = "20260913_120000_aaaa1111";
export const BOX_PREFIX = `reports/${STEAM_ID}/${BOX_ID}`;
export const BOX_URL = `https://playtest.moores.tech/v1/inbox/report/${STEAM_ID}/${BOX_ID}`;

export function adminRequest(method: string, suffix: string, headers: Record<string, string>, body: string | null): Promise<Response> {
  return handle(new Request(`${BOX_URL}${suffix}`, { method, headers: { ...ADMIN_HEADERS, ...headers }, ...(body === null ? {} : { body }) }), workerEnv, noNetwork);
}

export async function putBoxObject(path: string, body: string): Promise<void> {
  await workerEnv.BUCKET.put(`${BOX_PREFIX}/${path}`, body);
}

export async function boxKeys(): Promise<string[]> {
  const listed = await workerEnv.BUCKET.list({ prefix: `${BOX_PREFIX}/` });
  return listed.objects.map((object) => object.key.slice(BOX_PREFIX.length + 1)).sort();
}

export function decode(buffer: ArrayBuffer): string {
  return new TextDecoder().decode(buffer);
}
