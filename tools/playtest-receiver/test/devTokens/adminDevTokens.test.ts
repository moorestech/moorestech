import { beforeEach, expect, it } from "vitest";
import { clean, workerEnv } from "../support/uploadsFixture";
import { admin, issue } from "../support/developerFixture";

beforeEach(clean);

it.each(["GET", "POST", "DELETE", "PATCH"])("管理キーは経路やメソッドより先に検査: %s", async method => {
  for (const suffix of ["", "/macbook", "/malformed/extra"]) {
    for (const key of ["", "wrong"]) {
      const response = await admin(method, suffix, "{}", key);
      expect(response.status).toBe(401);
      expect(await response.json()).toEqual({ reason: "unauthorized" });
    }
  }
});

it.each(["Mac Book", "", "-a", "a".repeat(33)])("機材名を検査する: %s", async machine => {
  const response = await admin("POST", "", JSON.stringify({ machine }), workerEnv.ADMIN_KEY);
  expect(response.status).toBe(400);
  expect(await response.json()).toEqual({ reason: "bad-machine" });
});

it("同名の二重発行を拒否する", async () => {
  await issue("macbook");
  const response = await admin("POST", "", '{"machine":"macbook"}', workerEnv.ADMIN_KEY);
  expect(response.status).toBe(409);
  expect(await response.json()).toEqual({ reason: "machine-already-has-token" });
});

it("一覧は機材名順で作成日時を含み、生値を保存しない", async () => {
  const tokens = [await issue("windows"), await issue("macbook")];
  const response = await admin("GET", "", "", workerEnv.ADMIN_KEY);
  expect(response.status).toBe(200);
  const body = await response.json() as { machines: { machine: string; createdAt: string }[] };
  expect(body.machines.map(row => row.machine)).toEqual(["macbook", "windows"]);
  for (const row of body.machines) expect(new Date(row.createdAt).toISOString()).toBe(row.createdAt);
  const listed = await workerEnv.BUCKET.list();
  expect(listed.objects).toHaveLength(4);
  for (const entry of listed.objects) {
    const text = await (await workerEnv.BUCKET.get(entry.key))!.text();
    for (const token of tokens) expect(text).not.toContain(token);
  }
});

it("存在しない機材の失効は404", async () => {
  const response = await admin("DELETE", "/unknown", "", workerEnv.ADMIN_KEY);
  expect(response.status).toBe(404);
  expect(await response.json()).toEqual({ reason: "not-found" });
});

it("失効すると両方の索引が消え再発行できる", async () => {
  const original = await issue("macbook");
  await admin("DELETE", "/macbook", "", workerEnv.ADMIN_KEY);
  expect((await workerEnv.BUCKET.list()).objects.map(row => row.key)).toEqual(["devtokens/by-machine/macbook"]);
  const listing = await admin("GET", "", "", workerEnv.ADMIN_KEY);
  expect(await listing.json()).toEqual({ machines: [] });
  expect(await issue("macbook")).not.toBe(original);
});
