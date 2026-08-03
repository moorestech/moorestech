import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";

let effectCleanup: (() => void) | undefined;

vi.mock("react", () => ({
  useEffect(effect: () => void | (() => void)) {
    effectCleanup?.();
    effectCleanup = effect() ?? undefined;
  },
}));

vi.mock("@/bridge", () => ({
  localizationDictionaryUrl: (locale: string, revision: number) =>
    `/api/i18n/${locale}?revision=${revision}`,
  Topics: { localization: "localization.current" },
  useTopic: () => ({ locale: "japanese", revision: 3 }),
}));

import { I18nProvider } from "../I18nProvider";
import { createTranslator, getI18nSnapshot, setDictionaries } from "../i18nStore";
import { L } from "../generated/localizationKeys";

const RETRY_DELAY_MS = 5000;
const readyKey = L.ui.mainMenu.playLocally;

describe("dictionary load retry", () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.stubGlobal("document", { documentElement: { lang: "", dataset: {} } });
    vi.spyOn(console, "error").mockImplementation(() => undefined);
    vi.spyOn(console, "warn").mockImplementation(() => undefined);
  });

  afterEach(() => {
    effectCleanup?.();
    effectCleanup = undefined;
    vi.useRealTimers();
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("retries a transient failure once after five seconds and then reports the error", async () => {
    const fetchMock = vi.fn(() => Promise.reject(new TypeError("network down")));
    vi.stubGlobal("fetch", fetchMock);

    I18nProvider({ children: null });
    await flushPendingWork();
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(getI18nSnapshot().status).not.toBe("error");

    await vi.advanceTimersByTimeAsync(RETRY_DELAY_MS);
    await flushPendingWork();
    expect(fetchMock).toHaveBeenCalledTimes(6);
    expect(getI18nSnapshot().status).toBe("error");

    // 再試行は1回限りで、以降は時間を進めても再取得しない
    // The retry happens once only; later time advances must not refetch
    await vi.advanceTimersByTimeAsync(RETRY_DELAY_MS * 4);
    await flushPendingWork();
    expect(fetchMock).toHaveBeenCalledTimes(6);
  });

  it("retries a 5xx once and keeps the previously ready dictionary while failing", async () => {
    setDictionaries("english", { [readyKey]: "Ready title" }, {}, {});
    const fetchMock = vi.fn(() => Promise.resolve({ ok: false, status: 500 }));
    vi.stubGlobal("fetch", fetchMock);

    I18nProvider({ children: null });
    await flushPendingWork();
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(getI18nSnapshot().status).toBe("loading");

    await vi.advanceTimersByTimeAsync(RETRY_DELAY_MS);
    await flushPendingWork();
    expect(fetchMock).toHaveBeenCalledTimes(6);

    // 失敗中も直前のready辞書とlocaleは置き換えない
    // A failure must not replace the previously ready dictionary or locale
    expect(getI18nSnapshot()).toMatchObject({ status: "error", locale: "english" });
    expect(createTranslator(getI18nSnapshot())(readyKey)).toBe("Ready title");
  });

  it("reports 404 immediately without retrying", async () => {
    const fetchMock = vi.fn(() => Promise.resolve({ ok: false, status: 404 }));
    vi.stubGlobal("fetch", fetchMock);

    I18nProvider({ children: null });
    await flushPendingWork();
    expect(getI18nSnapshot().status).toBe("error");

    await vi.advanceTimersByTimeAsync(RETRY_DELAY_MS);
    await flushPendingWork();
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it("reports 400 immediately without retrying", async () => {
    const fetchMock = vi.fn(() => Promise.resolve({ ok: false, status: 400 }));
    vi.stubGlobal("fetch", fetchMock);

    I18nProvider({ children: null });
    await flushPendingWork();
    expect(getI18nSnapshot().status).toBe("error");

    await vi.advanceTimersByTimeAsync(RETRY_DELAY_MS);
    await flushPendingWork();
    expect(fetchMock).toHaveBeenCalledTimes(3);
  });

  it("leaves a stale-revision 409 to the next revision push without retrying or erroring", async () => {
    setDictionaries("english", {}, {}, {});
    const fetchMock = vi.fn(() => Promise.resolve({ ok: false, status: 409 }));
    vi.stubGlobal("fetch", fetchMock);

    I18nProvider({ children: null });
    await flushPendingWork();
    await vi.advanceTimersByTimeAsync(RETRY_DELAY_MS * 2);
    await flushPendingWork();

    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(getI18nSnapshot().status).toBe("loading");
  });

  it("cancels the pending retry timer when the effect is cleaned up", async () => {
    setDictionaries("english", {}, {}, {});
    const fetchMock = vi.fn(() => Promise.reject(new TypeError("network down")));
    vi.stubGlobal("fetch", fetchMock);

    I18nProvider({ children: null });
    await flushPendingWork();
    effectCleanup?.();
    effectCleanup = undefined;

    await vi.advanceTimersByTimeAsync(RETRY_DELAY_MS * 2);
    await flushPendingWork();
    expect(fetchMock).toHaveBeenCalledTimes(3);
    expect(getI18nSnapshot().status).not.toBe("error");
  });
});

// 偽タイマー下でPromise連鎖を進める（waitForはタイマーを勝手に進めるため使わない）
// Drain promise chains under fake timers; vi.waitFor is avoided because it advances timers
async function flushPendingWork(): Promise<void> {
  for (let step = 0; step < 20; step += 1) await Promise.resolve();
}
