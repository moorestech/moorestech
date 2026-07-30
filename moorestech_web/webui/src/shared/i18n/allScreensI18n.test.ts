import { readFileSync } from "node:fs";
import { afterEach, describe, expect, it, vi } from "vitest";
// @ts-expect-error -- The build script is intentionally a plain ESM module.
import { parseLocalizationCsv } from "../../../scripts/generate-localization-keys.mjs";
import { L } from "./index";

let currentLocale: string | null = "english";
let currentDictionaryRevision = 1;
let effectCleanup: (() => void) | undefined;
let effectDependencies: readonly unknown[] | undefined;
let rerender: (() => void) | undefined;
let unsubscribe: (() => void) | undefined;

vi.mock("react", () => ({
  useEffect(effect: () => void | (() => void), dependencies: readonly unknown[]) {
    // React依存比較で世代欠落を検出
    // Use React dependency comparison to detect missing generations
    if (effectDependencies?.length === dependencies.length &&
        effectDependencies.every((value, index) => Object.is(value, dependencies[index]))) return;

    effectCleanup?.();
    effectDependencies = dependencies;
    effectCleanup = effect() ?? undefined;
  },
  useSyncExternalStore(subscribe: (listener: () => void) => () => void, getSnapshot: () => unknown) {
    unsubscribe ??= subscribe(() => rerender?.());
    return getSnapshot();
  },
  useMemo<T>(createValue: () => T) {
    return createValue();
  },
}));

vi.mock("@/bridge", () => ({
  localizationDictionaryUrl: (locale: string, revision: number) =>
    `/api/i18n/${locale}?revision=${revision}`,
  Topics: { localization: "localization.current" },
  useTopic: () => currentLocale === null
    ? null
    : { locale: currentLocale, revision: currentDictionaryRevision },
}));

import { I18nProvider, useI18n } from "./index";

describe("all-screen i18n propagation", () => {
  afterEach(() => {
    effectCleanup?.();
    effectCleanup = undefined;
    effectDependencies = undefined;
    unsubscribe?.();
    unsubscribe = undefined;
    rerender = undefined;
    currentLocale = "english";
    currentDictionaryRevision = 1;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("re-renders translated screen copy after localization.current changes with no legacy screen exemptions", async () => {
    vi.spyOn(console, "warn").mockImplementation(() => undefined);
    vi.stubGlobal("document", { documentElement: { lang: "", dataset: {} } });
    vi.stubGlobal("fetch", vi.fn(async (url: string) => ({
      ok: true,
      json: async () => url.startsWith("/api/i18n/japanese?")
        ? { [L.ui.mainMenu.playLocally]: "日本語タイトル" }
        : { [L.ui.mainMenu.playLocally]: "English title" },
    })));

    let renderedCopy = "";
    let renderCount = 0;
    rerender = () => {
      renderCount += 1;
      renderedCopy = useI18n().t(L.ui.mainMenu.playLocally);
    };

    rerender();
    I18nProvider({ children: null });
    await vi.waitFor(() => expect(renderedCopy).toBe("English title"));

    currentLocale = "japanese";
    I18nProvider({ children: null });
    await vi.waitFor(() => expect(renderedCopy).toBe("日本語タイトル"));
    expect(renderCount).toBeGreaterThanOrEqual(3);

    const eslintConfig = readFileSync(new URL("../../../eslint.config.mjs", import.meta.url), "utf8");
    const allowlistBody = eslintConfig.match(/const legacyUnlocalizedFiles = \[([\s\S]*?)\];/)?.[1];
    expect(allowlistBody?.trim()).toBe("");
  });

  it("fetches fallback only once while also loading source for the fallback locale", async () => {
    vi.stubGlobal("document", { documentElement: { lang: "", dataset: {} } });
    const fetchMock = vi.fn(async (_url: string, _init: { signal: AbortSignal }) =>
      ({ ok: true, json: async () => ({}) }));
    vi.stubGlobal("fetch", fetchMock);

    I18nProvider({ children: null });
    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(2));

    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      "/api/i18n/english?revision=1",
      "/api/i18n/source?revision=1",
    ]);
  });

  it("waits for the localization topic before the first dictionary fetch", async () => {
    vi.stubGlobal("document", { documentElement: { lang: "", dataset: {} } });
    const fetchMock = vi.fn(async (_url: string, _init: { signal: AbortSignal }) =>
      ({ ok: true, json: async () => ({}) }));
    vi.stubGlobal("fetch", fetchMock);
    currentLocale = null;

    I18nProvider({ children: null });
    await Promise.resolve();
    expect(fetchMock).not.toHaveBeenCalled();

    currentLocale = "japanese";
    I18nProvider({ children: null });
    await vi.waitFor(() => expect(fetchMock).toHaveBeenCalledTimes(3));
    expect(fetchMock.mock.calls.map(([url]) => url)).toEqual([
      "/api/i18n/english?revision=1",
      "/api/i18n/source?revision=1",
      "/api/i18n/japanese?revision=1",
    ]);
  });

  it("keeps the newest locale when StrictMode cleanup aborts an older dictionary generation", async () => {
    vi.spyOn(console, "warn").mockImplementation(() => undefined);
    vi.stubGlobal("document", { documentElement: { lang: "", dataset: {} } });
    const requests: Array<{
      url: string;
      signal: AbortSignal;
      resolve: (response: { ok: true; json: () => Promise<Record<string, string>> }) => void;
    }> = [];
    vi.stubGlobal("fetch", vi.fn((url: string, init: { signal: AbortSignal }) =>
      new Promise((resolve) => requests.push({ url, signal: init.signal, resolve }))));

    let renderedCopy = "";
    rerender = () => {
      renderedCopy = useI18n().t(L.ui.mainMenu.playLocally);
    };
    rerender();
    I18nProvider({ children: null });
    await vi.waitFor(() => expect(requests).toHaveLength(2));

    currentLocale = "japanese";
    I18nProvider({ children: null });
    await vi.waitFor(() => expect(requests).toHaveLength(5));
    expect(requests.slice(0, 2).every(({ signal }) => signal.aborted)).toBe(true);

    for (const request of requests.slice(2)) {
      const text = request.url.startsWith("/api/i18n/japanese?")
        ? "日本語タイトル"
        : request.url.startsWith("/api/i18n/english?") ? "English title" : "Source title";
      request.resolve({ ok: true, json: async () => ({ [L.ui.mainMenu.playLocally]: text }) });
    }
    await vi.waitFor(() => expect(renderedCopy).toBe("日本語タイトル"));

    for (const request of requests.slice(0, 2)) {
      request.resolve({ ok: true, json: async () => ({ [L.ui.mainMenu.playLocally]: "Stale title" }) });
    }
    await Promise.resolve();
    await Promise.resolve();

    expect(renderedCopy).toBe("日本語タイトル");
    expect(document.documentElement.lang).toBe("japanese");
  });

  it("marks a failed requested locale without replacing the previous ready dictionary", async () => {
    vi.stubGlobal("document", { documentElement: { lang: "", dataset: {} } });
    vi.spyOn(console, "error").mockImplementation(() => undefined);
    vi.stubGlobal("fetch", vi.fn(async (url: string) => {
      if (url.startsWith("/api/i18n/japanese?")) return { ok: false, status: 500 };
      return {
        ok: true,
        json: async () => ({ [L.ui.mainMenu.playLocally]: "English title" }),
      };
    }));

    I18nProvider({ children: null });
    await vi.waitFor(() => expect(useI18n().status).toBe("ready"));
    currentLocale = "japanese";
    I18nProvider({ children: null });
    await vi.waitFor(() => expect(useI18n().status).toBe("error"));

    expect(useI18n().locale).toBe("english");
    expect(useI18n().t(L.ui.mainMenu.playLocally)).toBe("English title");
  });

  it("requires non-empty Source and every language cell for every generated key", () => {
    const csvPath = new URL("../../../../../Localization/localization.csv", import.meta.url);
    const csv = parseLocalizationCsv(readFileSync(csvPath, "utf8"));

    for (const row of csv.rows) {
      row.texts.forEach((text: string, index: number) => {
        expect(text, `key '${row.key}' is missing '${csv.languageCodes[index]}'`).not.toBe("");
      });
      expect(row.source, `key '${row.key}' is missing Source`).not.toBe("");
    }
  });
});
