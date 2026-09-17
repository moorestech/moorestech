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
    expect(await readAllowlist(bucket)).toEqual({ kind: "ok", steamIds: [] });
  });

  it("書いた内容が読める", async () => {
    await writeAllowlist(bucket, ["76561198000000001", "76561198000000002"]);
    expect(await readAllowlist(bucket)).toEqual({ kind: "ok", steamIds: ["76561198000000001", "76561198000000002"] });
  });

  it("同じIDを渡しても重複せず、保存した配列を返す", async () => {
    const stored = await writeAllowlist(bucket, ["76561198000000001", "76561198000000001"]);
    expect(stored).toEqual(["76561198000000001"]);
    expect(await readAllowlist(bucket)).toEqual({ kind: "ok", steamIds: ["76561198000000001"] });
  });

  // 破損を空リストへ畳まない（F24）。空と区別できないとGET→全置換PUTで許可リストを消し飛ばす
  // Corruption is not folded into an empty list (F24); otherwise GET -> full PUT could wipe the list
  it("壊れたJSONはcorruptになる", async () => {
    await bucket.put(ALLOWLIST_KEY, "{ broken");
    expect((await readAllowlist(bucket)).kind).toBe("corrupt");
  });

  it("steamIdsが配列でなければcorruptになる", async () => {
    await bucket.put(ALLOWLIST_KEY, JSON.stringify({ steamIds: "76561198000000001" }));
    expect(await readAllowlist(bucket)).toEqual({ kind: "corrupt", reason: "steamIds-not-array" });
  });

  it("JSONのnullや文字列以外の要素もcorruptになる", async () => {
    await bucket.put(ALLOWLIST_KEY, "null");
    expect(await readAllowlist(bucket)).toEqual({ kind: "corrupt", reason: "steamIds-not-array" });
    await bucket.put(ALLOWLIST_KEY, JSON.stringify({ steamIds: ["76561198000000001", 1] }));
    expect(await readAllowlist(bucket)).toEqual({ kind: "corrupt", reason: "steamIds-has-non-string" });
  });
});
