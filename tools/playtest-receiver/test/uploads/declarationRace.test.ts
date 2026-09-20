import { afterEach, describe, expect, it, vi } from "vitest";
import { declaredMarkerKey } from "../../src/bundleMarkers";
import type { Env } from "../../src/env";
import { bearer, clean, complete, declaration, declarationOfGeneration, handle, ID, noNetwork, prepare, STEAM_ID, workerEnv } from "../support/uploadsFixture";

// DECLAREDの条件付き書き込み（同時prepareの競合）と読めないDECLAREDの拒否（F25）。世代規則の基本はreprepare.test.ts
// Conditional DECLARED writes under racing prepares and refusal of an unreadable DECLARED (F25); basic generation rules live in reprepare.test.ts
afterEach(() => {
  vi.restoreAllMocks();
  return clean();
});

const DECLARED_KEY = declaredMarkerKey("report", STEAM_ID, ID);

async function declaredJson(): Promise<unknown> {
  return (await workerEnv.BUCKET.get(DECLARED_KEY))?.json() ?? null;
}

async function storeDeclared(text: string): Promise<void> {
  await workerEnv.BUCKET.put(DECLARED_KEY, text);
}

// DECLAREDへの条件付きputの直前に、別のprepareの書き込み（competitorText）を割り込ませるバケット
// A bucket that slips another prepare's write (competitorText) in right before each conditional DECLARED put
function envWhereCompetitorWritesBeforeEachPut(competitorTexts: string[]): Env {
  const real = workerEnv.BUCKET;
  const bucket = new Proxy(real, {
    get(target, property) {
      if (property === "put") {
        return async (key: string, value: string, options?: R2PutOptions) => {
          const competitorText = key === DECLARED_KEY && options?.onlyIf !== undefined ? competitorTexts.shift() : undefined;
          if (competitorText !== undefined) await target.put(key, competitorText);
          return target.put(key, value, options);
        };
      }
      const value = Reflect.get(target, property);
      return typeof value === "function" ? value.bind(target) : value;
    },
  });
  return { ...workerEnv, BUCKET: bucket };
}

async function prepareWith(env: Env, body: string): Promise<Response> {
  return handle(new Request(`https://x/v1/uploads/report/${ID}/prepare`, { method: "POST", headers: { authorization: await bearer(), "content-type": "application/json" }, body }), env, noNetwork);
}

describe("同時prepareの競合", () => {
  it("集合の異なる初回prepareが同時に来ると片方だけが確定し、もう片方は409 declaration-conflict", async () => {
    vi.spyOn(console, "warn").mockImplementation(() => {});
    const bodies = [declaration({ a: 1, b: 2 }), declaration({ a: 1, c: 3 })];
    const responses = await Promise.all(bodies.map((body) => prepare("report", ID, body)));
    expect(responses.map((r) => r.status).sort()).toEqual([200, 409]);
    const loser = responses.find((r) => r.status === 409)!;
    expect(await loser.json()).toEqual({ reason: "declaration-conflict" });
    const winnerBody = JSON.parse(bodies[responses.findIndex((r) => r.status === 200)]!);
    expect(await declaredJson()).toEqual(winnerBody);
  });

  it("初回確定の直前に別の集合が書かれたら、上書きせず読み直して409 declaration-conflict", async () => {
    const competitor = declaration({ x: 5 });
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await prepareWith(envWhereCompetitorWritesBeforeEachPut([competitor]), declaration({ a: 1 }));
    expect(response.status).toBe(409);
    expect(await response.json()).toEqual({ reason: "declaration-conflict" });
    expect(await declaredJson()).toEqual(JSON.parse(competitor));
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("lost a conditional DECLARED write"));
  });

  it("初回確定の直前に同じ集合が書かれたら、読み直してsameとしてURLを出す", async () => {
    vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await prepareWith(envWhereCompetitorWritesBeforeEachPut([declaration({ a: 1 })]), declaration({ a: 1 }));
    expect(response.status).toBe(200);
    expect(((await response.json()) as { uploads: { path: string }[] }).uploads.map((u) => u.path)).toEqual(["a"]);
  });

  it("縮小のCASの直前に同世代の別の縮小が書かれたら、上書きせず409 declaration-conflict", async () => {
    await prepare("report", ID, declaration({ a: 1, b: 2, c: 3 }));
    const competitor = declarationOfGeneration(2, { b: 2 });
    vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await prepareWith(envWhereCompetitorWritesBeforeEachPut([competitor]), declarationOfGeneration(2, { a: 1 }));
    expect(response.status).toBe(409);
    expect(await declaredJson()).toEqual(JSON.parse(competitor));
  });

  it("条件付き書き込みに2回続けて負けたら409 declaration-conflictで止める", async () => {
    await prepare("report", ID, declaration({ a: 1, b: 2, c: 3 }));
    // R2のetagは内容のMD5なので、2回目の割り込みは順序だけ変えた別内容にしてetagを変える
    // R2 etags are the content MD5, so the second interloper reorders the files to change the etag
    const competitors = [declarationOfGeneration(2, { a: 1, b: 2 }), declarationOfGeneration(2, { b: 2, a: 1 })];
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await prepareWith(envWhereCompetitorWritesBeforeEachPut([...competitors]), declarationOfGeneration(3, { a: 1 }));
    expect(response.status).toBe(409);
    expect(await response.json()).toEqual({ reason: "declaration-conflict" });
    expect(await declaredJson()).toEqual(JSON.parse(competitors[1]!));
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("twice"));
  });
});

describe("読めないDECLARED", () => {
  it.each([
    ["JSONでない", "{broken"],
    ["世代が無い（旧形式）", JSON.stringify({ files: [{ path: "a", bytes: 1 }] })],
    ["宣言検査に通らない", JSON.stringify({ generation: 1, files: [{ path: "../a", bytes: 1 }] })],
  ])("DECLAREDが%sならprepareは409 declaration-unreadableでwarnし、DECLAREDを書き換えない", async (_, text) => {
    await storeDeclared(text);
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await prepare("report", ID, declaration({ a: 1 }));
    expect(response.status).toBe(409);
    expect(await response.json()).toEqual({ reason: "declaration-unreadable" });
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("refused prepare"));
    expect(await (await workerEnv.BUCKET.get(DECLARED_KEY))!.text()).toBe(text);
  });

  it("DECLAREDが読めなければcompleteも409 declaration-unreadableでwarnする", async () => {
    await storeDeclared("{broken");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await complete("report", ID);
    expect(response.status).toBe(409);
    expect(await response.json()).toEqual({ reason: "declaration-unreadable" });
    expect(warn).toHaveBeenCalledWith(expect.stringContaining("refused complete"));
  });
});
