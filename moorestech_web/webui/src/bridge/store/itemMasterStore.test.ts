import { describe, it, expect, vi, beforeEach, afterEach } from "vitest";

// モジュール変数(started)を各テストで初期化するため resetModules + 動的 import を使う
// Reset module-level state (started) per test via resetModules + dynamic import
beforeEach(() => {
  vi.useFakeTimers();
  vi.resetModules();
});
afterEach(() => {
  vi.clearAllTimers();
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

  it("成功後も再取得し、現在の MaxStack で Map を置き換える", async () => {
    const updatedMasterJson = { items: [{ itemId: 1, name: "Wood", maxStack: 200 }] };
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce({ ok: true, json: async () => masterJson })
      .mockResolvedValueOnce({ ok: true, json: async () => updatedMasterJson });
    vi.stubGlobal("fetch", fetchMock);
    const { ensureItemMasterLoaded, useItemMasterStore } = await import("./itemMasterStore");

    ensureItemMasterLoaded();
    await vi.advanceTimersByTimeAsync(0);
    const firstMaster = useItemMasterStore.getState().master;
    expect(firstMaster?.get(1)?.maxStack).toBe(100);

    // 成功後も同じ間隔で再取得し、Map 自体を置き換える
    // Refresh after the same interval even on success and replace the Map itself
    await vi.advanceTimersByTimeAsync(3000);
    const secondMaster = useItemMasterStore.getState().master;
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(secondMaster).not.toBe(firstMaster);
    expect(secondMaster?.get(1)?.maxStack).toBe(200);
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

  it("JSON 解析失敗の後も自動再試行して反映される", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce({ ok: true, json: async () => Promise.reject(new Error("invalid json")) })
      .mockResolvedValueOnce({ ok: true, json: async () => masterJson });
    vi.stubGlobal("fetch", fetchMock);
    const { ensureItemMasterLoaded, useItemMasterStore } = await import("./itemMasterStore");

    ensureItemMasterLoaded();
    await vi.advanceTimersByTimeAsync(0);
    expect(useItemMasterStore.getState().master).toBeNull();

    // JSON 解析失敗でも3秒後の取得を継続する
    // Continue fetching after three seconds even when JSON parsing fails
    await vi.advanceTimersByTimeAsync(3000);
    expect(fetchMock).toHaveBeenCalledTimes(2);
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
