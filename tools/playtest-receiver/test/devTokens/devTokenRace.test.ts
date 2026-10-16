import { afterEach, beforeEach, expect, it, vi } from "vitest";
import type { Env } from "../../src/env";
import { findMachineByToken, isRevokedDeveloper, issueDevToken, listDevTokens, revokeDevToken } from "../../src/devTokens/devTokenStore";
import { clean, handle, noNetwork, workerEnv } from "../support/uploadsFixture";

const MACHINE_KEY = "devtokens/by-machine/macbook";
const NOW = new Date("2026-10-16T00:00:00Z");
beforeEach(clean);
afterEach(() => vi.restoreAllMocks());

function gate() {
  let release!: () => void;
  const promise = new Promise<void>(resolve => { release = resolve; });
  return { promise, release };
}

function withBucket(overrides: Record<string, unknown>): Env {
  const bucket = new Proxy(workerEnv.BUCKET, {
    get(target, property) {
      if (typeof property === "string" && property in overrides) return overrides[property];
      const value = Reflect.get(target, property);
      return typeof value === "function" ? value.bind(target) : value;
    },
  });
  return { ...workerEnv, BUCKET: bucket };
}

function postIssue(env: Env): Promise<Response> {
  return handle(new Request("https://x/v1/admin/dev-tokens", {
    method: "POST", headers: { "x-admin-key": env.ADMIN_KEY }, body: '{"machine":"macbook"}',
  }), env, noNetwork);
}

it("両方が空索引を読んでも、先に予約した発行だけ201で後発は409", async () => {
  const bothRead = gate();
  const firstReserved = gate();
  let reads = 0;
  let puts = 0;
  const env = withBucket({
    get: async (key: string) => {
      const object = await workerEnv.BUCKET.get(key);
      if (key === MACHINE_KEY && ++reads <= 2) {
        expect(object).toBeNull();
        if (reads === 2) bothRead.release();
        await bothRead.promise;
      }
      return object;
    },
    put: async (key: string, value: string, options: R2PutOptions) => {
      if (key !== MACHINE_KEY) return workerEnv.BUCKET.put(key, value, options);
      if (++puts === 1) {
        const result = await workerEnv.BUCKET.put(key, value, options);
        firstReserved.release();
        return result;
      }
      await firstReserved.promise;
      return workerEnv.BUCKET.put(key, value, options);
    },
  });
  const responses = await Promise.all([postIssue(env), postIssue(env)]);
  expect(responses.map(row => row.status).sort()).toEqual([201, 409]);
  expect(await responses.find(row => row.status === 409)!.json()).toEqual({ reason: "machine-already-has-token" });
  const { token } = await responses.find(row => row.status === 201)!.json() as { token: string };
  expect(await findMachineByToken(workerEnv, token)).toEqual({ machine: "macbook", credentialHash: expect.stringMatching(/^[0-9a-f]{64}$/) });
  await revokeDevToken(workerEnv, "macbook");
  expect(await findMachineByToken(workerEnv, token)).toBeNull();
});

it("予約後の書込失敗は有効にならず、失効して再発行できる", async () => {
  const error = vi.spyOn(console, "error").mockImplementation(() => {});
  const env = withBucket({
    put: async (key: string, value: string, options: R2PutOptions) => {
      if (key.startsWith("devtokens/by-hash/")) throw new Error("R2 unavailable");
      return workerEnv.BUCKET.put(key, value, options);
    },
  });
  await expect(issueDevToken(env, "macbook", NOW)).rejects.toThrow("R2 unavailable");
  expect(error).toHaveBeenCalledWith(expect.stringContaining("revoke before retrying"), expect.any(Error));
  expect(await isRevokedDeveloper(workerEnv, "dev-macbook", (await (await workerEnv.BUCKET.get(MACHINE_KEY))!.json<{ hash: string }>()).hash)).toBe(true);
  expect(await issueDevToken(workerEnv, "macbook", NOW)).toEqual({ kind: "exists" });
  await revokeDevToken(workerEnv, "macbook");
  expect((await issueDevToken(workerEnv, "macbook", NOW)).kind).toBe("issued");
});

it("失効後に遅れて旧hashが書かれても、再発行後も交換できない", async () => {
  const reachedHash = gate();
  const writeHash = gate();
  const env = withBucket({
    put: async (key: string, value: string, options: R2PutOptions) => {
      if (key.startsWith("devtokens/by-hash/")) {
        reachedHash.release();
        await writeHash.promise;
      }
      return workerEnv.BUCKET.put(key, value, options);
    },
  });
  const pending = issueDevToken(env, "macbook", NOW);
  await reachedHash.promise;
  await revokeDevToken(workerEnv, "macbook");
  writeHash.release();
  const old = await pending;
  expect(old.kind).toBe("issued");
  if (old.kind !== "issued") throw new Error("expected issuance");
  expect(await findMachineByToken(workerEnv, old.token)).toBeNull();
  expect((await workerEnv.BUCKET.list({ prefix: "devtokens/by-hash/" })).objects).toHaveLength(0);
  const newer = await issueDevToken(workerEnv, "macbook", NOW);
  expect(newer.kind).toBe("issued");
  expect(await findMachineByToken(workerEnv, old.token)).toBeNull();
  await revokeDevToken(workerEnv, "macbook");
  expect(await findMachineByToken(workerEnv, old.token)).toBeNull();
});

it("失効のhash削除失敗後も拒否し、再失効で回収できる", async () => {
  const issued = await issueDevToken(workerEnv, "macbook", NOW);
  if (issued.kind !== "issued") throw new Error("expected issuance");
  const error = vi.spyOn(console, "error").mockImplementation(() => {});
  const env = withBucket({ delete: async () => { throw new Error("delete failed"); } });
  await expect(revokeDevToken(env, "macbook")).rejects.toThrow("delete failed");
  expect(error).toHaveBeenCalledWith(expect.stringContaining("retry revoke"), expect.any(Error));
  expect(await findMachineByToken(workerEnv, issued.token)).toBeNull();
  expect(await isRevokedDeveloper(workerEnv, "dev-macbook", (await (await workerEnv.BUCKET.get(MACHINE_KEY))!.json<{ hash: string }>()).hash)).toBe(true);
  expect(await listDevTokens(workerEnv)).toEqual([]);
  expect(await revokeDevToken(workerEnv, "macbook")).toEqual({ kind: "revoked" });
  expect((await workerEnv.BUCKET.list({ prefix: "devtokens/by-hash/" })).objects).toHaveLength(0);
});

it("遅れた失効CASが、新しく発行した機材索引を破壊しない", async () => {
  const first = await issueDevToken(workerEnv, "macbook", NOW);
  if (first.kind !== "issued") throw new Error("expected issuance");
  const reachedCas = gate();
  const continueCas = gate();
  const env = withBucket({
    put: async (key: string, value: string, options: R2PutOptions) => {
      reachedCas.release();
      await continueCas.promise;
      return workerEnv.BUCKET.put(key, value, options);
    },
  });
  const delayed = handle(new Request("https://x/v1/admin/dev-tokens/macbook", {
    method: "DELETE", headers: { "x-admin-key": env.ADMIN_KEY },
  }), env, noNetwork);
  await reachedCas.promise;
  await revokeDevToken(workerEnv, "macbook");
  const second = await issueDevToken(workerEnv, "macbook", NOW);
  if (second.kind !== "issued") throw new Error("expected issuance");
  continueCas.release();
  const response = await delayed;
  expect(response.status).toBe(409);
  expect(await response.json()).toEqual({ reason: "revocation-superseded" });
  expect(await findMachineByToken(workerEnv, first.token)).toBeNull();
  expect(await findMachineByToken(workerEnv, second.token)).toEqual({ machine: "macbook", credentialHash: expect.stringMatching(/^[0-9a-f]{64}$/) });
});
