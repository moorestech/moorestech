import { beforeEach, describe, expect, it, vi } from "vitest";
import { handle } from "../../src/index";
import { signToken } from "../../src/token";
import { STEAM_ID, clean, noNetwork, workerEnv } from "../support/uploadsFixture";

const ADMIN = { "x-admin-key": "test-admin-key" };

async function upload(kind: string, id: string, path: string, body: string): Promise<void> {
  const token = await signToken(workerEnv.SESSION_HMAC_SECRET, STEAM_ID, Math.floor(Date.now() / 1000));
  await handle(
    new Request(`https://playtest.tar-atari.com/v1/uploads/${kind}/${id}/${path}`, {
      method: "PUT",
      headers: { authorization: `Bearer ${token}`, "content-length": String(body.length) },
      body,
    }),
    workerEnv,
    noNetwork,
  );
  await handle(
    new Request(`https://playtest.tar-atari.com/v1/uploads/${kind}/${id}/complete`, {
      method: "POST",
      headers: { authorization: `Bearer ${token}` },
      body: JSON.stringify({ kind: "bug" }),
    }),
    workerEnv,
    noNetwork,
  );
}

// 認証の合否は admin/auth.test.ts が担う。ここでは admin キーが正しい前提の中身を検証する
// admin/auth.test.ts owns authentication pass/fail; this file assumes a valid admin key throughout
describe("admin api inbox", () => {
  beforeEach(clean);

  it("kindが不正なら400でwarnする", async () => {
    await upload("report", "20260913_120000_aaaa1111", "a.txt", "x");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/inbox/config/${STEAM_ID}/20260913_120000_aaaa1111/a.txt`, { headers: ADMIN }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(400);
    expect(await response.json()).toEqual({ reason: "bad-kind" });
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("completeした2件がinboxに出る", async () => {
    await upload("report", "20260913_120000_aaaa1111", "a.txt", "x");
    await upload("progress", "20260913_130000_bbbb2222", "record.json", "{}");
    const response = await handle(new Request("https://playtest.tar-atari.com/v1/inbox", { headers: ADMIN }), workerEnv, noNetwork);
    const body = (await response.json()) as { items: { kind: string; steamId: string; id: string; readyAt: string }[]; cursor: string | null };
    expect(body.items).toHaveLength(2);
    expect(body.items.map((item) => item.id).sort()).toEqual(["20260913_120000_aaaa1111", "20260913_130000_bbbb2222"]);
    expect(body.items[0]?.steamId).toBe(STEAM_ID);
    expect(Number.isNaN(Date.parse(body.items[0]?.readyAt ?? ""))).toBe(false);
  });

  it("inboxの個別ファイルを取れる。不在は404でwarnする", async () => {
    await upload("report", "20260913_120000_aaaa1111", "a.txt", "hello");
    const ok = await handle(
      new Request(`https://playtest.tar-atari.com/v1/inbox/report/${STEAM_ID}/20260913_120000_aaaa1111/a.txt`, { headers: ADMIN }),
      workerEnv,
      noNetwork,
    );
    // content-typeはoctet-streamなので.text()は使わず、workerdの診断警告を出さずに内容を検証する
    // content-type is octet-stream, so avoid .text() (which triggers a workerd diagnostic warning) to check the content
    expect(new TextDecoder().decode(await ok.arrayBuffer())).toBe("hello");

    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const missing = await handle(
      new Request(`https://playtest.tar-atari.com/v1/inbox/report/${STEAM_ID}/20260913_120000_aaaa1111/none.txt`, { headers: ADMIN }),
      workerEnv,
      noNetwork,
    );
    expect(missing.status).toBe(404);
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("101件あれば2ページ目にcursorで続きが取れる", async () => {
    // R2への直接putで索引だけ101件作る。アップロード経路を101回通す必要は無い
    // Write 101 index entries directly to R2; no need to drive the upload path 101 times
    for (let i = 0; i < 101; i++) {
      const id = `20260913_120000_${String(i).padStart(4, "0")}`;
      await workerEnv.BUCKET.put(`index/pending/report/${STEAM_ID}/${id}`, "");
    }
    const first = await handle(new Request("https://playtest.tar-atari.com/v1/inbox", { headers: ADMIN }), workerEnv, noNetwork);
    const firstBody = (await first.json()) as { items: unknown[]; cursor: string | null };
    expect(firstBody.items).toHaveLength(100);
    expect(firstBody.cursor).not.toBeNull();

    const second = await handle(
      new Request(`https://playtest.tar-atari.com/v1/inbox?cursor=${encodeURIComponent(firstBody.cursor ?? "")}`, { headers: ADMIN }),
      workerEnv,
      noNetwork,
    );
    const secondBody = (await second.json()) as { items: unknown[]; cursor: string | null };
    expect(secondBody.items).toHaveLength(1);
    expect(secondBody.cursor).toBeNull();
  });

  it("pendingでないidをackすると404でwarnする", async () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const response = await handle(
      new Request(`https://playtest.tar-atari.com/v1/inbox/report/${STEAM_ID}/20260913_999999_zzzz9999/ack`, { method: "POST", headers: ADMIN }),
      workerEnv,
      noNetwork,
    );
    expect(response.status).toBe(404);
    expect(await response.json()).toEqual({ reason: "not-found" });
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
  });

  it("ackするとinboxから消えACKEDが出来る", async () => {
    await upload("report", "20260913_120000_aaaa1111", "a.txt", "x");
    const acked = await handle(
      new Request(`https://playtest.tar-atari.com/v1/inbox/report/${STEAM_ID}/20260913_120000_aaaa1111/ack`, { method: "POST", headers: ADMIN }),
      workerEnv,
      noNetwork,
    );
    expect(acked.status).toBe(200);
    expect(await workerEnv.BUCKET.get(`reports/${STEAM_ID}/20260913_120000_aaaa1111/ACKED`)).not.toBeNull();
    const inbox = await handle(new Request("https://playtest.tar-atari.com/v1/inbox", { headers: ADMIN }), workerEnv, noNetwork);
    expect(((await inbox.json()) as { items: unknown[] }).items).toHaveLength(0);
  });

  // at-least-onceの再送でackを2回叩いても失敗させない（冪等）。plan Hの取り込みがack応答だけ
  // 取りこぼしてリトライしても404にならないことを保証する
  // A retried ack (under at-least-once semantics) must not fail; this guarantees plan H's ingest can
  // retry after losing only the ack response, without getting a 404
  it("ackを2回呼んでも200でACKEDは1つのまま", async () => {
    await upload("report", "20260913_120000_aaaa1111", "a.txt", "x");
    const request = () =>
      handle(
        new Request(`https://playtest.tar-atari.com/v1/inbox/report/${STEAM_ID}/20260913_120000_aaaa1111/ack`, { method: "POST", headers: ADMIN }),
        workerEnv,
        noNetwork,
      );

    const first = await request();
    expect(first.status).toBe(200);
    const second = await request();
    expect(second.status).toBe(200);
    expect(await second.json()).toEqual({ acked: true });

    const listed = await workerEnv.BUCKET.list({ prefix: `reports/${STEAM_ID}/20260913_120000_aaaa1111/` });
    expect(listed.objects.filter((object) => object.key.endsWith("/ACKED"))).toHaveLength(1);
  });
});
