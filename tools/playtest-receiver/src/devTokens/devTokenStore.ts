import type { Env } from "../env";
import { DEVELOPER_SUBJECT_PREFIX, isDeveloperSubject } from "./developerSubject";

const BY_HASH = "devtokens/by-hash/";
const BY_MACHINE = "devtokens/by-machine/";
export const DEV_TOKEN_PATTERN = /^mdt_[0-9a-f]{64}$/;
export type IssueResult = { kind: "issued"; token: string } | { kind: "exists" };
type MachineRecord = { hash: string; createdAt: string };

// 生値は応答だけに使い、台帳にはSHA-256を保存する
// Return the raw token only in the response; persist its SHA-256 in the ledger
export async function issueDevToken(env: Env, machine: string, now: Date): Promise<IssueResult> {
  if ((await env.BUCKET.head(BY_MACHINE + machine)) !== null) return { kind: "exists" };
  const token = `mdt_${toHex(crypto.getRandomValues(new Uint8Array(32)))}`;
  const hash = await sha256Hex(token);
  const createdAt = now.toISOString();
  await env.BUCKET.put(BY_HASH + hash, JSON.stringify({ machine, createdAt }));
  await env.BUCKET.put(BY_MACHINE + machine, JSON.stringify({ hash, createdAt }));
  return { kind: "issued", token };
}

export async function findMachineByToken(env: Env, token: string): Promise<string | null> {
  const record = await readJson<{ machine?: unknown }>(env, BY_HASH + await sha256Hex(token));
  return record !== null && typeof record.machine === "string" ? record.machine : null;
}

// 一覧は機材索引を読み、表示順を固定する
// Read the machine index and return a stable display order
export async function listDevTokens(env: Env): Promise<readonly { machine: string; createdAt: string }[]> {
  const rows: { machine: string; createdAt: string }[] = [];
  let cursor: string | undefined;
  do {
    const listed = await env.BUCKET.list({ prefix: BY_MACHINE, cursor });
    for (const entry of listed.objects) {
      const record = await readJson<MachineRecord>(env, entry.key);
      // silent-ok: 一覧と失効の競合では表示用日時を空にする / 管理者は次の一覧で消えたことに気づく
      rows.push({ machine: entry.key.slice(BY_MACHINE.length), createdAt: record === null ? "" : record.createdAt });
    }
    cursor = listed.truncated ? listed.cursor : undefined;
  } while (cursor !== undefined);
  return rows.sort((a, b) => a.machine.localeCompare(b.machine));
}

// 両索引を削除し、既存セッションにも失効を効かせる
// Remove both indexes so revocation also applies to existing sessions
export async function revokeDevToken(env: Env, machine: string): Promise<boolean> {
  const record = await readJson<MachineRecord>(env, BY_MACHINE + machine);
  if (record === null) return false;
  await env.BUCKET.delete(BY_HASH + record.hash);
  await env.BUCKET.delete(BY_MACHINE + machine);
  return true;
}

export async function isRevokedDeveloper(env: Env, sub: string): Promise<boolean> {
  if (!isDeveloperSubject(sub)) return false;
  if (await env.BUCKET.head(BY_MACHINE + sub.slice(DEVELOPER_SUBJECT_PREFIX.length)) !== null) return false;
  console.warn(`[uploads] refused: developer token revoked for ${sub}`);
  return true;
}

async function readJson<T>(env: Env, key: string): Promise<T | null> {
  const object = await env.BUCKET.get(key);
  return object === null ? null : await object.json<T>();
}

function toHex(bytes: Uint8Array): string {
  return [...bytes].map((byte) => byte.toString(16).padStart(2, "0")).join("");
}

async function sha256Hex(text: string): Promise<string> {
  return toHex(new Uint8Array(await crypto.subtle.digest("SHA-256", new TextEncoder().encode(text))));
}
