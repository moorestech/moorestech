import { afterEach, expect, it, vi } from "vitest";
import { MAX_BUNDLE_BYTES, MAX_FILE_BYTES } from "../../../src/contract";
import { parseTransferPlan, TRANSFER_INDEX } from "../../../src/uploads/commitTransfer";
import { ackedMarkerKey } from "../../../src/bundleMarkers";
import { bundlePrefix, pendingIndexKey } from "../../../src/keys";
import { clean, complete, declaration, ID, prepare, putDirect, STEAM_ID, workerEnv } from "../../support/uploadsFixture";

afterEach(async () => { vi.restoreAllMocks(); await clean(); });

const child = `${ID}/__attachments/000000`;
const part = "__attachments/000000/chunk";
const digest = "a".repeat(64);
const plan = [{ path: "repo/commits.bundle", bytes: 3, parts: [{ path: part, bytes: 3, sha256: digest }] }];

it("断片を独立公開せず、親は全断片が揃ってから公開し管理APIで読める", async () => {
  const raw = JSON.stringify(plan);
  expect((await prepare("report", child, declaration({ chunk: 3 }))).status).toBe(200);
  expect((await prepare("report", ID, declaration({ "manifest.json": 2, [TRANSFER_INDEX]: raw.length }))).status).toBe(200);
  await putDirect("report", STEAM_ID, ID, "manifest.json", "{}");
  await putDirect("report", STEAM_ID, ID, TRANSFER_INDEX, raw);
  expect((await complete("report", ID)).status).toBe(409);
  expect(await workerEnv.BUCKET.get(pendingIndexKey("report", STEAM_ID, ID))).toBeNull();
  await putDirect("report", STEAM_ID, child, "chunk", "abc");
  expect((await complete("report", child)).status).toBe(200);
  expect(await workerEnv.BUCKET.get(pendingIndexKey("report", STEAM_ID, child))).toBeNull();
  expect((await complete("report", ID)).status).toBe(200);
  expect((await complete("report", ID)).status).toBe(200);
  const ready = await workerEnv.BUCKET.get(`${bundlePrefix("report", STEAM_ID, ID)}/READY`);
  expect((await ready!.json<{ files: string[] }>()).files).toContain(part);
  expect(await workerEnv.BUCKET.get(pendingIndexKey("report", STEAM_ID, ID))).not.toBeNull();
});

it("親ACK後に断片を再送しても宣言・索引を復活させない", async () => {
  await workerEnv.BUCKET.put(ackedMarkerKey("report", STEAM_ID, ID), "done");
  expect(await (await prepare("report", child, declaration({ chunk: 3 }))).json()).toEqual({ outcome: "acked" });
  expect(await (await complete("report", child)).json()).toEqual({ ready: true });
  expect(await workerEnv.BUCKET.get(pendingIndexKey("report", STEAM_ID, child))).toBeNull();
});

it("単体上限と箱合計を超える資料でも各転送単位は既存上限を守れる", () => {
  const parts = Array.from({ length: 4 }, (_, index) => ({ path: `__attachments/${String(index).padStart(6, "0")}/chunk`, bytes: MAX_FILE_BYTES, sha256: digest }));
  expect(MAX_FILE_BYTES * 4).toBeGreaterThan(MAX_BUNDLE_BYTES);
  const result = parseTransferPlan([{ path: "repo/commits.bundle", bytes: MAX_FILE_BYTES * 4, parts }]);
  expect(result).toEqual(parts.map(({ path, bytes }) => ({ path, bytes })));
  expect(typeof parseTransferPlan([{ path: "repo/commits.bundle", bytes: MAX_FILE_BYTES + 1,
    parts: [{ path: part, bytes: MAX_FILE_BYTES + 1, sha256: digest }] }])).toBe("string");
});

it("任意パス・不一致・重複計画は親を公開しない", async () => {
  for (const invalid of ["bad", [{ ...plan[0], path: "../outside" }], [{ ...plan[0], bytes: 4 }], [plan[0], plan[0]]]) {
    expect(typeof parseTransferPlan(invalid)).toBe("string");
  }
  const raw = JSON.stringify([{ ...plan[0], path: "../outside" }]);
  await prepare("report", ID, declaration({ [TRANSFER_INDEX]: raw.length }));
  await putDirect("report", STEAM_ID, ID, TRANSFER_INDEX, raw);
  expect((await complete("report", ID)).status).toBe(409);
  expect(await workerEnv.BUCKET.get(pendingIndexKey("report", STEAM_ID, ID))).toBeNull();
  expect((await prepare("report", child, declaration({ other: 3 }))).status).toBe(400);
});
