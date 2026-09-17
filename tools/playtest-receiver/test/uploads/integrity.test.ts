import { beforeEach, describe, expect, it, vi } from "vitest";
import { handle } from "../../src/index";
import { pendingIndexKey } from "../../src/keys";
import { bearer, clean, ID, noNetwork, STEAM_ID, workerEnv } from "../support/uploadsFixture";

const BASE = `https://playtest.moores.tech/v1/uploads/report/${ID}`;

async function putFile(path: string, body: string, contentLength: string): Promise<Response> {
  return handle(
    new Request(`${BASE}/${path}`, { method: "PUT", headers: { authorization: await bearer(), "content-length": contentLength }, body }),
    workerEnv,
    noNetwork,
  );
}

async function complete(): Promise<Response> {
  return handle(new Request(`${BASE}/complete`, { method: "POST", headers: { authorization: await bearer() }, body: "{}" }), workerEnv, noNetwork);
}

// 実バイト数と宣言長の一致（F27）、ACKED後の書き込み抑止（F28）、トークン検証の設定欠落（F29）
// Actual vs declared length (F27), no writes after ACKED (F28), and token verification misconfiguration (F29)
describe("uploads integrity", () => {
  beforeEach(clean);

  it("本文が宣言したContent-Lengthより短ければ400 length-mismatchで何も残さない", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await putFile("short.txt", "hello", "10");
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ reason: "length-mismatch" });
    expect(await workerEnv.BUCKET.head(`reports/${STEAM_ID}/${ID}/short.txt`)).toBeNull();
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("本文が宣言したContent-Lengthより長ければ400 length-mismatchで何も残さない", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await putFile("long.txt", "hello world", "5");
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ reason: "length-mismatch" });
    expect(await workerEnv.BUCKET.head(`reports/${STEAM_ID}/${ID}/long.txt`)).toBeNull();
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("長さが合っていてもR2への保存が落ちたら400ではなく再試行できる503 storage-unavailable", async () => {
    // 400を返すとクライアントはそのファイルを恒久に見送り、箱だけUPLOADEDになってファイルが失われる
    // A 400 would make the client drop the file for good while the box still completes, silently losing it
    const failingBucket = new Proxy(workerEnv.BUCKET, {
      get(target, property) {
        if (property === "put") return () => Promise.reject(new Error("r2 is down"));
        const value: unknown = Reflect.get(target, property);
        return typeof value === "function" ? value.bind(target) : value;
      },
    });
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request(`${BASE}/ok.txt`, { method: "PUT", headers: { authorization: await bearer(), "content-length": "5" }, body: "hello" }),
      { ...workerEnv, BUCKET: failingBucket },
      noNetwork,
    );
    expect(response.status).toBe(503);
    expect(await response.json()).toEqual({ reason: "storage-unavailable" });
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("could not be stored"));
    warn.mockRestore();
  });

  it("ACKED済みバンドルへのPUTは書かずに200でwarnする", async () => {
    await putFile("a.txt", "first", "5");
    await workerEnv.BUCKET.put(`reports/${STEAM_ID}/${ID}/ACKED`, "2026-09-15T00:00:00.000Z");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await putFile("a.txt", "second", "6");
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ stored: "a.txt" });
    expect(await (await workerEnv.BUCKET.get(`reports/${STEAM_ID}/${ID}/a.txt`))?.text()).toBe("first");
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("already acked"));
    warn.mockRestore();
  });

  it("ACKED済みバンドルへのcompleteは索引を作り直さず200でwarnする", async () => {
    await workerEnv.BUCKET.put(`reports/${STEAM_ID}/${ID}/ACKED`, "2026-09-15T00:00:00.000Z");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await complete();
    expect(response.status).toBe(200);
    expect(await response.json()).toEqual({ ready: true });
    expect(await workerEnv.BUCKET.head(pendingIndexKey("report", STEAM_ID, ID))).toBeNull();
    expect(await workerEnv.BUCKET.head(`reports/${STEAM_ID}/${ID}/READY`)).toBeNull();
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("already acked"));
    warn.mockRestore();
  });

  it("SESSION_HMAC_SECRETが無ければアップロードは401ではなく500 server-misconfigured", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request(`${BASE}/a.txt`, { method: "PUT", headers: { authorization: await bearer(), "content-length": "1" }, body: "x" }),
      { ...workerEnv, SESSION_HMAC_SECRET: "" },
      noNetwork,
    );
    expect(response.status).toBe(500);
    expect(await response.json()).toEqual({ reason: "server-misconfigured" });
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });
});
