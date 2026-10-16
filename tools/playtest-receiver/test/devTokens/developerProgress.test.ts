import { afterEach, beforeEach, expect, it, vi } from "vitest";
import { clean, complete, declaration, ID, prepare, putDirect, STEAM_ID, workerEnv } from "../support/uploadsFixture";
import { admin, developerSession, upload } from "../support/developerFixture";

beforeEach(clean);
afterEach(() => vi.restoreAllMocks());

it("開発機の進行記録をprepareとcompleteで拒否し理由をwarnする", async () => {
  const token = await developerSession();
  const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
  for (const action of ["prepare", "complete"]) {
    const response = await upload(action, "progress", token, declaration({ a: 1 }));
    expect(response.status).toBe(403);
    expect(await response.json()).toEqual({ reason: "developer-progress-not-accepted" });
  }
  expect(warn).toHaveBeenCalledTimes(2);
  expect(warn).toHaveBeenCalledWith(expect.stringContaining("progress records are not accepted"));
});

it("失効は発行済みセッションのprepareとcompleteにも即時に効く", async () => {
  const token = await developerSession();
  expect((await upload("prepare", "report", token, declaration({ a: 1 }))).status).toBe(200);
  await putDirect("report", "dev-macbook", ID, "a", "x");
  await admin("DELETE", "/macbook", "", workerEnv.ADMIN_KEY);
  const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
  for (const action of ["prepare", "complete"]) {
    const response = await upload(action, "report", token, declaration({ a: 1 }));
    expect(response.status).toBe(401);
    expect(await response.json()).toEqual({ reason: "developer-token-revoked" });
  }
  expect(warn).toHaveBeenCalledTimes(2);
  expect(await workerEnv.BUCKET.head(`reports/dev-macbook/${ID}/READY`)).toBeNull();
});

it("Steamの進行記録は引き続き受理する", async () => {
  expect((await prepare("progress", ID, declaration({ a: 1 }))).status).toBe(200);
  await putDirect("progress", STEAM_ID, ID, "a", "x");
  expect((await complete("progress", ID)).status).toBe(200);
});
