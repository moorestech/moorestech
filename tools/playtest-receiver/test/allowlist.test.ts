import { env } from "cloudflare:test";
import { beforeEach, describe, expect, it } from "vitest";
import { ALLOWLIST_KEY, readAllowlist, writeAllowlist } from "../src/allowlist";
import type { Env } from "../src/env";

const bucket = (env as unknown as Env).BUCKET;

describe("allowlist", () => {
  beforeEach(async () => {
    await bucket.delete(ALLOWLIST_KEY);
  });

  it("オブジェクトが無ければ空リストになる", async () => {
    expect(await readAllowlist(bucket)).toEqual([]);
  });

  it("書いた内容が読める", async () => {
    await writeAllowlist(bucket, ["76561198000000001", "76561198000000002"]);
    expect(await readAllowlist(bucket)).toEqual(["76561198000000001", "76561198000000002"]);
  });

  it("同じIDを渡しても重複しない", async () => {
    await writeAllowlist(bucket, ["76561198000000001", "76561198000000001"]);
    expect(await readAllowlist(bucket)).toEqual(["76561198000000001"]);
  });

  it("壊れたJSONは空リストとして扱う", async () => {
    await bucket.put(ALLOWLIST_KEY, "{ broken");
    expect(await readAllowlist(bucket)).toEqual([]);
  });
});
