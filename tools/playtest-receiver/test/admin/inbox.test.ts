import { beforeEach, describe, expect, it, vi } from "vitest";
import { handle } from "../../src/index";
import type { PlaytestKind } from "../../src/keys";
import { STEAM_ID, clean, complete, declaration, noNetwork, prepare, putDirect, workerEnv } from "../support/uploadsFixture";

const ADMIN = { "x-admin-key": "test-admin-key" };

// prepare → 直接PUT（R2への直接putで模擬）→ complete の正規経路で1箱を上げる
// Uploads one box through the real path: prepare, a direct PUT (simulated by an R2 put), then complete
async function upload(kind: PlaytestKind, id: string, path: string, body: string): Promise<void> {
  await prepare(kind, id, declaration({ [path]: body.length }));
  await putDirect(kind, STEAM_ID, id, path, body);
  const response = await complete(kind, id, JSON.stringify({ manifest: null, skipped: [] }));
  expect(response.status).toBe(200);
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

  // ACKEDが正本（F28）。ack途中で索引だけ残っても一覧に出さず、古い索引を片付ける
  // ACKED is the source of truth (F28); a leftover index entry is hidden from the list and its stale key deleted
  it("ACKED済みの索引はinboxに出ず索引も消える", async () => {
    await upload("report", "20260913_120000_aaaa1111", "a.txt", "x");
    await workerEnv.BUCKET.put(`reports/${STEAM_ID}/20260913_120000_aaaa1111/ACKED`, "2026-09-15T00:00:00.000Z");
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const inbox = await handle(new Request("https://playtest.tar-atari.com/v1/inbox", { headers: ADMIN }), workerEnv, noNetwork);
    expect(((await inbox.json()) as { items: unknown[] }).items).toHaveLength(0);
    expect(await workerEnv.BUCKET.head(`index/pending/report/${STEAM_ID}/20260913_120000_aaaa1111`)).toBeNull();
    expect(warn).toHaveBeenCalled();
    warn.mockRestore();
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

  // at-least-onceの再送でackを2回叩いても失敗させない（冪等）。ack応答だけ取りこぼしても404にならない
  // A retried ack (at-least-once) must not fail; losing only the ack response and retrying must not 404
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
