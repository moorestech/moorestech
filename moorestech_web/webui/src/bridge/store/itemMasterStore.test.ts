import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

// モジュール変数(started)を各テストで初期化するため resetModules + 動的 import を使う
// Reset module-level state (started) per test via resetModules + dynamic import
beforeEach(() => {
  vi.useFakeTimers();
  vi.resetModules();
});
afterEach(() => {
  vi.useRealTimers();
  vi.unstubAllGlobals();
});

const masterJson = { items: [{ itemId: 1, name: "Wood", maxStack: 100 }] };

describe("ensureItemMasterLoaded", () => {
  it("初回成功で master がストアへ反映される", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => masterJson }));
    const { ensureItemMasterLoaded, useItemMasterStore } = await import("./itemMasterStore");
    ensureItemMasterLoaded();
    await vi.advanceTimersByTimeAsync(0);
    expect(useItemMasterStore.getState().master?.get(1)?.maxStack).toBe(100);
  });

  it("503 の後もマウントに依存せず自動再試行して反映される", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce({ ok: false })
      .mockResolvedValueOnce({ ok: true, json: async () => masterJson });
    vi.stubGlobal("fetch", fetchMock);
    const { ensureItemMasterLoaded, useItemMasterStore } = await import("./itemMasterStore");
    ensureItemMasterLoaded();
    await vi.advanceTimersByTimeAsync(0);
    expect(useItemMasterStore.getState().master).toBeNull();
    // リトライ間隔(3秒)経過で2回目のfetchが成功する
    // After the 3s retry interval the second fetch succeeds
    await vi.advanceTimersByTimeAsync(3000);
    expect(useItemMasterStore.getState().master?.get(1)?.name).toBe("Wood");
  });

  it("ネットワーク例外でも再試行する", async () => {
    const fetchMock = vi
      .fn()
      .mockRejectedValueOnce(new Error("net down"))
      .mockResolvedValueOnce({ ok: true, json: async () => masterJson });
    vi.stubGlobal("fetch", fetchMock);
    const { ensureItemMasterLoaded, useItemMasterStore } = await import("./itemMasterStore");
    ensureItemMasterLoaded();
    await vi.advanceTimersByTimeAsync(3000);
    expect(useItemMasterStore.getState().master?.get(1)?.name).toBe("Wood");
  });

  it("多重呼び出しでも fetch は1系列しか走らない", async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => masterJson });
    vi.stubGlobal("fetch", fetchMock);
    const { ensureItemMasterLoaded } = await import("./itemMasterStore");
    ensureItemMasterLoaded();
    ensureItemMasterLoaded();
    await vi.advanceTimersByTimeAsync(0);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });
});
