import { readFileSync } from "node:fs";
import { afterEach, describe, expect, it, vi } from "vitest";

let currentLocale = "english";
let effectCleanup: (() => void) | undefined;
let rerender: (() => void) | undefined;
let unsubscribe: (() => void) | undefined;

vi.mock("react", () => ({
  useEffect(effect: () => void | (() => void)) {
    effectCleanup?.();
    effectCleanup = effect() ?? undefined;
  },
  useSyncExternalStore(subscribe: (listener: () => void) => () => void, getSnapshot: () => unknown) {
    unsubscribe ??= subscribe(() => rerender?.());
    return getSnapshot();
  },
}));

vi.mock("@/bridge", () => ({
  localizationDictionaryUrl: (locale: string) => `/api/i18n/${locale}`,
  Topics: { localization: "localization.current" },
  useTopic: () => ({ locale: currentLocale }),
}));

import { I18nProvider, useI18n } from "./index";

describe("all-screen i18n propagation", () => {
  afterEach(() => {
    effectCleanup?.();
    effectCleanup = undefined;
    unsubscribe?.();
    unsubscribe = undefined;
    rerender = undefined;
    currentLocale = "english";
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("re-renders translated screen copy after localization.current changes with no legacy screen exemptions", async () => {
    vi.spyOn(console, "warn").mockImplementation(() => undefined);
    vi.stubGlobal("document", { documentElement: { lang: "", dataset: {} } });
    vi.stubGlobal("fetch", vi.fn(async (url: string) => ({
      ok: true,
      json: async () => url.endsWith("/japanese")
        ? { "画面タイトル": "日本語タイトル" }
        : { "画面タイトル": "English title" },
    })));

    let renderedCopy = "";
    let renderCount = 0;
    rerender = () => {
      renderCount += 1;
      renderedCopy = useI18n().t("画面タイトル");
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
});
