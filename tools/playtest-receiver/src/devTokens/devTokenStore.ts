import type { Env } from "../env";
import { DEVELOPER_SUBJECT_PREFIX, isDeveloperSubject } from "./developerSubject";

const BY_HASH = "devtokens/by-hash/";
const BY_MACHINE = "devtokens/by-machine/";
export const DEV_TOKEN_PATTERN = /^mdt_[0-9a-f]{64}$/;
type IssueResult = { kind: "issued"; token: string } | { kind: "exists" };
type MachineRecord = { hash: string; createdAt: string; revoked?: true };

// 生値は応答だけに使い、台帳にはSHA-256を保存する
// Return the raw token only in the response; persist its SHA-256 in the ledger
export async function issueDevToken(env: Env, machine: string, now: Date): Promise<IssueResult> {
  const previous = await env.BUCKET.get(BY_MACHINE + machine);
  const record = previous === null ? null : await previous.json<MachineRecord>();
  if (record !== null && record.revoked !== true) return alreadyExists(machine);
  const token = `mdt_${toHex(crypto.getRandomValues(new Uint8Array(32)))}`;
  const hash = await sha256Hex(token);
  const createdAt = now.toISOString();
  // 機材索引を原子的に予約し、同名の並行発行を排他する
  // Atomically reserve the machine index to exclude concurrent issuance for the same name
  // R2は外部I/O境界。予約残骸を管理者へ報告し、成功応答にしない
  // R2 is an external I/O boundary; report reservation remnants to admins and never report success
  try {
    const reserved = await env.BUCKET.put(BY_MACHINE + machine, JSON.stringify({ hash, createdAt }), {
      onlyIf: previous === null ? { etagDoesNotMatch: "*" } : { etagMatches: previous.etag },
    });
    if (reserved === null) return alreadyExists(machine);
    await env.BUCKET.put(BY_HASH + hash, JSON.stringify({ machine, createdAt }));
  } catch (error) {
    console.error(`[dev-tokens] issuance failed for ${machine}; inspect the ledger and revoke before retrying`, error);
    throw error;
  }
  return { kind: "issued", token };
}

export async function findMachineByToken(env: Env, token: string): Promise<string | null> {
  const hash = await sha256Hex(token);
  const record = await readJson<{ machine?: unknown }>(env, BY_HASH + hash);
  if (record === null || typeof record.machine !== "string") return null;
  const machine = await readJson<MachineRecord>(env, BY_MACHINE + record.machine);
  if (machine === null || machine.revoked === true || machine.hash !== hash) {
    console.warn("[dev-tokens] rejected: token indexes do not match an active machine");
    await removeStaleHash(env, hash);
    return null;
  }
  return record.machine;
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
      if (record?.revoked === true) continue; // silent-ok: 失効済みは一覧対象外 / 管理者は一覧から消えたことで失効を確認する
      // silent-ok: 一覧と失効の競合では表示用日時を空にする / 管理者は次の一覧で消えたことに気づく
      rows.push({ machine: entry.key.slice(BY_MACHINE.length), createdAt: record === null ? "" : record.createdAt });
    }
    cursor = listed.truncated ? listed.cursor : undefined;
  } while (cursor !== undefined);
  return rows.sort((a, b) => a.machine.localeCompare(b.machine));
}

// 墓標を先に確定し、既存セッションにも失効を効かせる
// Commit the tombstone first so revocation also applies to existing sessions
export async function revokeDevToken(env: Env, machine: string): Promise<boolean> {
  const object = await env.BUCKET.get(BY_MACHINE + machine);
  if (object === null) return false;
  const record = await object.json<MachineRecord>();
  if (record.revoked !== true) {
    // 墓標をCASで確定し、遅れた失効が新しい発行を消さないようにする
    // Commit a tombstone with CAS so a delayed revoke cannot delete a newer issuance
    const revoked = await env.BUCKET.put(BY_MACHINE + machine, JSON.stringify({ ...record, revoked: true }), {
      onlyIf: { etagMatches: object.etag },
    });
    if (revoked === null) {
      console.warn(`[dev-tokens] revocation superseded by another operation for ${machine}`);
      return true;
    }
  }
  // 墓標は次の発行がCASで置換する。再試行は旧hashだけを掃除する
  // The next issuance replaces the tombstone with CAS; retries clean only the old hash
  // R2削除は外部I/O境界。失敗時も墓標で拒否を保ち、再失効で回収する
  // R2 deletion is an external I/O boundary; the tombstone denies access and another revoke retries cleanup
  try {
    await env.BUCKET.delete(BY_HASH + record.hash);
  } catch (error) {
    console.error(`[dev-tokens] revocation cleanup failed for ${machine}; retry revoke to remove the old hash`, error);
    throw error;
  }
  return true;
}

export async function isRevokedDeveloper(env: Env, sub: string): Promise<boolean> {
  if (!isDeveloperSubject(sub)) return false;
  const machine = sub.slice(DEVELOPER_SUBJECT_PREFIX.length);
  const record = await readJson<MachineRecord>(env, BY_MACHINE + machine);
  const reverse = record === null || record.revoked === true ? null
    : await readJson<{ machine: string }>(env, BY_HASH + record.hash);
  if (reverse?.machine === machine) return false;
  console.warn(`[uploads] refused: developer token revoked or incomplete for ${sub}`);
  return true;
}

function alreadyExists(machine: string): IssueResult {
  console.warn(`[dev-tokens] rejected: ${machine} already has a token or issuance reservation`);
  return { kind: "exists" };
}

// 失効後に遅れて届いた孤立hashは、提示時に安全に回収する
// Safely reclaim an orphan hash written after revocation when it is presented
async function removeStaleHash(env: Env, hash: string): Promise<void> {
  // R2削除は外部I/O境界。回収失敗を報告しても認証拒否は維持する
  // R2 deletion is an external I/O boundary; report cleanup failures while preserving denial
  try {
    await env.BUCKET.delete(BY_HASH + hash);
  } catch (error) {
    console.error(`[dev-tokens] orphan cleanup failed for hash ${hash}; remove this by-hash key from R2`, error);
  }
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
