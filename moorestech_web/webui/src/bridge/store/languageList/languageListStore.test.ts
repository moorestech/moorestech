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

const languagesJson = [
  { code: "english", displayName: "English" },
  { code: "japanese", displayName: "日本語" },
];

describe("ensureLanguageListLoaded", () => {
  it("初回成功で配信順のまま一覧がストアへ反映される", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({ ok: true, json: async () => languagesJson }));
    const { ensureLanguageListLoaded, useLanguageListStore } = await import("./languageListStore");

    ensureLanguageListLoaded();
    await vi.advanceTimersByTimeAsync(0);

    expect(useLanguageListStore.getState().entries?.map((entry) => entry.code)).toEqual(["english", "japanese"]);
  });

  it("HTTP失敗の後もマウントに依存せず自動再試行して反映される", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce({ ok: false })
      .mockResolvedValueOnce({ ok: true, json: async () => languagesJson });
    vi.stubGlobal("fetch", fetchMock);
    const { ensureLanguageListLoaded, useLanguageListStore } = await import("./languageListStore");

    ensureLanguageListLoaded();
    await vi.advanceTimersByTimeAsync(0);
    expect(useLanguageListStore.getState().entries).toBeNull();

    // リトライ間隔(3秒)経過で2回目のfetchが成功する
    // After the 3s retry interval the second fetch succeeds
    await vi.advanceTimersByTimeAsync(3000);
    expect(useLanguageListStore.getState().entries).toHaveLength(2);
  });

  it("選択肢ゼロ件は取り込まず再試行を続ける", async () => {
    const fetchMock = vi
      .fn()
      .mockResolvedValueOnce({ ok: true, json: async () => [] })
      .mockResolvedValueOnce({ ok: true, json: async () => languagesJson });
    vi.stubGlobal("fetch", fetchMock);
    const { ensureLanguageListLoaded, useLanguageListStore } = await import("./languageListStore");

    ensureLanguageListLoaded();
    await vi.advanceTimersByTimeAsync(0);
    expect(useLanguageListStore.getState().entries).toBeNull();

    await vi.advanceTimersByTimeAsync(3000);
    expect(useLanguageListStore.getState().entries).toHaveLength(2);
  });

  it("code/displayNameの欠けた要素は取り込まない", async () => {
    vi.stubGlobal("fetch", vi.fn().mockResolvedValue({
      ok: true,
      json: async () => [{ code: "english" }, ...languagesJson],
    }));
    const { ensureLanguageListLoaded, useLanguageListStore } = await import("./languageListStore");

    ensureLanguageListLoaded();
    await vi.advanceTimersByTimeAsync(0);

    expect(useLanguageListStore.getState().entries?.map((entry) => entry.code)).toEqual(["english", "japanese"]);
  });
});
