import { afterEach, describe, expect, it, vi } from "vitest";
import { ACKED_MARKER, ackedMarkerKey, declaredMarkerKey, READY_MARKER } from "../../src/bundleMarkers";
import type { Env } from "../../src/env";
import { bundlePrefix, pendingIndexKey } from "../../src/keys";
import { bearer, clean, declaration, handle, ID, noNetwork, prepare, putDirect, STEAM_ID, workerEnv } from "../support/uploadsFixture";

// 設定漏れ・DECLAREDのwrite-once・送信済みの除外・ACKの再確認（D4(1)/D6/D7）。基本経路は uploads.test.ts
// Misconfiguration, write-once DECLARED, skipping sent files and ACK re-checks (D4(1)/D6/D7); the basic paths live in uploads.test.ts
afterEach(() => {
  vi.restoreAllMocks();
  return clean();
});

type PrepareBody = { outcome: string; uploads: { path: string; url: string; bytes: number }[]; expiresInSeconds: number };

async function post(env: Env, action: "prepare" | "complete", body: string): Promise<Response> {
  return handle(new Request(`https://x/v1/uploads/report/${ID}/${action}`, { method: "POST", headers: { authorization: await bearer(), "content-type": "application/json" }, body }), env, noNetwork);
}

async function declaredText(): Promise<string | null> {
  return (await workerEnv.BUCKET.get(declaredMarkerKey("report", STEAM_ID, ID)))?.text() ?? null;
}

// ACKEDのheadを最初のchecks回だけ「無い」と答えるバケット。処理の途中でACKが届いた状況を作る
// A bucket whose ACKED head answers "absent" for the first `checks` calls, staging an ack that lands mid-request
function envWhereAckLandsAfter(checks: number): Env {
  let seen = 0;
  const real = workerEnv.BUCKET;
  const bucket = new Proxy(real, {
    get(target, property) {
      if (property === "head") {
        return async (key: string) => (key.endsWith(`/${ACKED_MARKER}`) && seen++ < checks ? null : target.head(key));
      }
      const value = Reflect.get(target, property);
      return typeof value === "function" ? value.bind(target) : value;
    },
  });
  return { ...workerEnv, BUCKET: bucket };
}

describe("prepare の設定漏れ", () => {
  it.each(["R2_ACCOUNT_ID", "R2_BUCKET_NAME", "R2_ACCESS_KEY_ID", "R2_SECRET_ACCESS_KEY"] as const)("%s が空なら500 server-misconfiguredでerrorを出し、何も書かない", async (name) => {
    const error = vi.spyOn(console, "error").mockImplementation(() => {});
    const response = await post({ ...workerEnv, [name]: "" }, "prepare", declaration({ a: 1 }));
    expect(response.status).toBe(500);
    expect(await response.json()).toEqual({ reason: "server-misconfigured" });
    expect(error).toHaveBeenCalledWith(expect.stringContaining(name));
    expect(await declaredText()).toBeNull();
  });
});

describe("DECLARED は write-once", () => {
  it("同じpath+bytes集合（順序違い）の再prepareはURLを出し直し、DECLAREDを書き換えない", async () => {
    await prepare("report", ID, declaration({ a: 1, "b/c.bin": 2 }));
    const before = await declaredText();
    const response = await prepare("report", ID, declaration({ "b/c.bin": 2, a: 1 }));
    expect(response.status).toBe(200);
    const body = (await response.json()) as PrepareBody;
    expect(body.outcome).toBe("prepared");
    expect(body.uploads.map((u) => u.path).sort()).toEqual(["a", "b/c.bin"]);
    expect(await declaredText()).toBe(before);
  });

  it.each([
    ["長さが違う", { a: 1, "b/c.bin": 3 }],
    ["ファイルが増えた", { a: 1, "b/c.bin": 2, d: 1 }],
    ["ファイルが増え別のファイルが減った", { a: 1, d: 2 }],
    ["パスが違う", { a: 1, "b/x.bin": 2 }],
  ])("宣言が%s再prepareは409 declaration-conflictでwarnし、DECLAREDは元のまま", async (_, entries) => {
    await prepare("report", ID, declaration({ a: 1, "b/c.bin": 2 }));
    const before = await declaredText();
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await prepare("report", ID, declaration(entries));
    expect(response.status).toBe(409);
    expect(await response.json()).toEqual({ reason: "declaration-conflict" });
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("adds or resizes files"));
    expect(await declaredText()).toBe(before);
  });
});

describe("宣言の縮小（契約補正 2'）", () => {
  it("既存宣言の部分集合（bytes一致）の再prepareは受け付け、DECLAREDをその部分集合へ縮めてURLを出す", async () => {
    await prepare("report", ID, declaration({ a: 1, "b/c.bin": 2, d: 3 }));
    vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await prepare("report", ID, declaration({ d: 3, a: 1 }));
    expect(response.status).toBe(200);
    const body = (await response.json()) as PrepareBody;
    expect(body.uploads.map((u) => u.path).sort()).toEqual(["a", "d"]);
    expect(JSON.parse((await declaredText())!)).toEqual({ files: [{ path: "d", bytes: 3 }, { path: "a", bytes: 1 }] });
  });

  it("縮めた後で元の宣言へ戻す（追加になる）再prepareは409", async () => {
    await prepare("report", ID, declaration({ a: 1, d: 3 }));
    vi.spyOn(console, "warn").mockImplementation(() => {});
    await prepare("report", ID, declaration({ a: 1 }));
    const response = await prepare("report", ID, declaration({ a: 1, d: 3 }));
    expect(response.status).toBe(409);
    expect(JSON.parse((await declaredText())!)).toEqual({ files: [{ path: "a", bytes: 1 }] });
  });

  it("縮小の途中（DECLAREDを書く直前）にACKされた箱は縮めずackedを返す", async () => {
    await prepare("report", ID, declaration({ a: 1, d: 3 }));
    const before = await declaredText();
    await workerEnv.BUCKET.put(ackedMarkerKey("report", STEAM_ID, ID), "");
    vi.spyOn(console, "warn").mockImplementation(() => {});
    expect(await (await post(envWhereAckLandsAfter(1), "prepare", declaration({ a: 1 }))).json()).toEqual({ outcome: "acked" });
    expect(await declaredText()).toBe(before);
  });
});

describe("再prepareは送信済みを除外する", () => {
  it("宣言どおりの長さで既にあるファイルはuploadsに載らない", async () => {
    await prepare("report", ID, declaration({ "manifest.json": 2, "a.bin": 3 }));
    await putDirect("report", STEAM_ID, ID, "manifest.json", "{}");
    const body = (await (await prepare("report", ID, declaration({ "manifest.json": 2, "a.bin": 3 }))).json()) as PrepareBody;
    expect(body.uploads.map((u) => u.path)).toEqual(["a.bin"]);
  });

  it("全部送信済みならuploadsは空配列（outcomeはprepared）", async () => {
    await prepare("report", ID, declaration({ "a.bin": 1 }));
    await putDirect("report", STEAM_ID, ID, "a.bin", "a");
    expect(await (await prepare("report", ID, declaration({ "a.bin": 1 }))).json()).toEqual({ outcome: "prepared", uploads: [], expiresInSeconds: 3600 });
  });

  it("長さ違いで既にあるファイルはURLを出したうえで原因をwarnする", async () => {
    await putDirect("report", STEAM_ID, ID, "a.bin", "abcd");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const body = (await (await prepare("report", ID, declaration({ "a.bin": 3 }))).json()) as PrepareBody;
    expect(body.uploads.map((u) => u.path)).toEqual(["a.bin"]);
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("already exists with 4 bytes"));
  });
});

describe("書き込み直前のACK再確認", () => {
  it("prepareの途中（DECLAREDを書く直前）にACKされた箱はDECLAREDを書かずackedを返す", async () => {
    await workerEnv.BUCKET.put(ackedMarkerKey("report", STEAM_ID, ID), "");
    vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await post(envWhereAckLandsAfter(1), "prepare", declaration({ a: 1 }));
    expect(await response.json()).toEqual({ outcome: "acked" });
    expect(await declaredText()).toBeNull();
  });

  it("署名の途中（URLを返す直前）にACKされた箱はURLを返さずackedを返す", async () => {
    await workerEnv.BUCKET.put(ackedMarkerKey("report", STEAM_ID, ID), "");
    vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await post(envWhereAckLandsAfter(2), "prepare", declaration({ a: 1 }));
    expect(await response.json()).toEqual({ outcome: "acked" });
  });

  it("completeの途中（READYを書く直前）にACKされた箱はREADYも索引も書かず冪等成功する", async () => {
    await prepare("report", ID, declaration({ "a.bin": 1 }));
    await putDirect("report", STEAM_ID, ID, "a.bin", "a");
    await workerEnv.BUCKET.put(ackedMarkerKey("report", STEAM_ID, ID), "");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await post(envWhereAckLandsAfter(1), "complete", "{}");
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ ready: true });
    expect(await workerEnv.BUCKET.get(`${bundlePrefix("report", STEAM_ID, ID)}/${READY_MARKER}`)).toBeNull();
    expect(await workerEnv.BUCKET.get(pendingIndexKey("report", STEAM_ID, ID))).toBeNull();
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("acked during complete"));
  });
});
