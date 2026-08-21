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

const WATER_GUID = "54000000-0000-4000-8000-000000000001";
const REPAINTED_WATER_GUID = "54000000-0000-4000-8000-000000000002";
const masterJson = { fluids: [{ fluidId: 1, fluidGuid: WATER_GUID, color: "#2A6FE0" }] };

describe("ensureFluidMasterLoaded", () => {
  it("初回成功で master がストアへ反映される", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => masterJson }));
    const { ensureFluidMasterLoaded, useFluidMasterStore } = await import("./fluidMasterStore");
    ensureFluidMasterLoaded();
    await vi.advanceTimersByTimeAsync(0);
    expect(useFluidMasterStore.getState().master?.get(WATER_GUID)?.color).toBe("#2A6FE0");
  });

  it("成功後は再取得せず同じ master 参照を保つ", async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => masterJson });
    vi.stubGlobal("fetch", fetchMock);
    const { ensureFluidMasterLoaded, useFluidMasterStore } = await import("./fluidMasterStore");

    ensureFluidMasterLoaded();
    await vi.advanceTimersByTimeAsync(0);
    const firstMaster = useFluidMasterStore.getState().master;
    expect(firstMaster?.get(WATER_GUID)?.color).toBe("#2A6FE0");

    await vi.advanceTimersByTimeAsync(3000);
    const secondMaster = useFluidMasterStore.getState().master;
    expect(fetchMock).toHaveBeenCalledTimes(1);
    expect(secondMaster).toBe(firstMaster);
  });

  it("503 の後もマウントに依存せず自動再試行して反映される", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce({ ok: false })
      .mockResolvedValueOnce({ ok: true, json: async () => masterJson });
    vi.stubGlobal("fetch", fetchMock);
    const { ensureFluidMasterLoaded, useFluidMasterStore } = await import("./fluidMasterStore");
    ensureFluidMasterLoaded();
    await vi.advanceTimersByTimeAsync(0);
    expect(useFluidMasterStore.getState().master).toBeNull();
    // リトライ間隔(3秒)経過で2回目のfetchが成功する
    // After the 3s retry interval the second fetch succeeds
    await vi.advanceTimersByTimeAsync(3000);
    expect(useFluidMasterStore.getState().master?.get(WATER_GUID)?.color).toBe("#2A6FE0");
  });

  it("ネットワーク例外でも再試行する", async () => {
    const fetchMock = vi
      .fn()
      .mockRejectedValueOnce(new Error("net down"))
      .mockResolvedValueOnce({ ok: true, json: async () => masterJson });
    vi.stubGlobal("fetch", fetchMock);
    const { ensureFluidMasterLoaded, useFluidMasterStore } = await import("./fluidMasterStore");
    ensureFluidMasterLoaded();
    await vi.advanceTimersByTimeAsync(3000);
    expect(useFluidMasterStore.getState().master?.get(WATER_GUID)?.color).toBe("#2A6FE0");
  });

  // HTTP 応答の形状不正は取り込まず、次の取得機会を保つ
  // Ignore malformed HTTP payloads while preserving the next retry opportunity
  it.each([
    ["fluids キー欠落", {}],
    ["fluids が配列でない", { fluids: "invalid" }],
    ["fluidId が number でない", { fluids: [{ fluidId: "1", fluidGuid: WATER_GUID, color: "#2A6FE0" }] }],
    ["fluidGuid が string でない", { fluids: [{ fluidId: 1, fluidGuid: null, color: "#2A6FE0" }] }],
    ["color が string でない", { fluids: [{ fluidId: 1, fluidGuid: WATER_GUID, color: 123 }] }],
  ])("不正 shape（%s）の後も自動再試行して反映される", async (_label, invalidData) => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce({ ok: true, json: async () => invalidData })
      .mockResolvedValueOnce({ ok: true, json: async () => masterJson });
    vi.stubGlobal("fetch", fetchMock);
    const { ensureFluidMasterLoaded, useFluidMasterStore } = await import("./fluidMasterStore");

    ensureFluidMasterLoaded();
    await vi.advanceTimersByTimeAsync(0);
    expect(useFluidMasterStore.getState().master).toBeNull();

    await vi.advanceTimersByTimeAsync(3000);
    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(useFluidMasterStore.getState().master?.get(WATER_GUID)?.color).toBe("#2A6FE0");
  });

  it("多重呼び出しでも fetch は1系列しか走らない", async () => {
    const fetchMock = vi.fn().mockResolvedValue({ ok: true, json: async () => masterJson });
    vi.stubGlobal("fetch", fetchMock);
    const { ensureFluidMasterLoaded } = await import("./fluidMasterStore");
    ensureFluidMasterLoaded();
    ensureFluidMasterLoaded();
    await vi.advanceTimersByTimeAsync(0);
    expect(fetchMock).toHaveBeenCalledTimes(1);
  });

  it("WS 再接続開始時に成功済み master を再取得する", async () => {
    const refreshedJson = { fluids: [{ fluidId: 1, fluidGuid: REPAINTED_WATER_GUID, color: "#00AAFF" }] };
    const fetchMock = vi.fn()
      .mockResolvedValueOnce({ ok: true, json: async () => masterJson })
      .mockResolvedValueOnce({ ok: true, json: async () => refreshedJson });
    vi.stubGlobal("fetch", fetchMock);
    const { ensureFluidMasterLoaded, useFluidMasterStore } = await import("./fluidMasterStore");
    const { useTopicStore } = await import("./topicStore");

    ensureFluidMasterLoaded();
    await vi.advanceTimersByTimeAsync(0);
    useTopicStore.getState().setStatus("reconnecting");
    useTopicStore.getState().setStatus("restoring");
    await vi.advanceTimersByTimeAsync(0);

    expect(fetchMock).toHaveBeenCalledTimes(2);
    expect(useFluidMasterStore.getState().master?.get(REPAINTED_WATER_GUID)?.color).toBe("#00AAFF");
  });
});
