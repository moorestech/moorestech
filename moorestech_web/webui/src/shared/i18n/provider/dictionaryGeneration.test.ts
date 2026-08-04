import { afterEach, describe, expect, it, vi } from "vitest";

let dictionaryRevision = 7;
let effectCleanup: (() => void) | undefined;
let effectDependencies: readonly unknown[] | undefined;

vi.mock("react", () => ({
  useEffect(effect: () => void | (() => void), dependencies: readonly unknown[]) {
    // 同locale世代をReact比較で検出
    // Detect same-locale generation changes with React comparison
    if (effectDependencies?.length === dependencies.length &&
        effectDependencies.every((value, index) => Object.is(value, dependencies[index]))) return;

    effectCleanup?.();
    effectDependencies = dependencies;
    effectCleanup = effect() ?? undefined;
  },
}));

vi.mock("@/bridge", () => ({
  localizationDictionaryUrl: (locale: string, revision: number) =>
    `/api/i18n/${locale}?revision=${revision}`,
  Topics: { localization: "localization.current" },
  useTopic: () => ({ locale: "japanese", revision: dictionaryRevision }),
}));

import { I18nProvider } from "../I18nProvider";
import { getI18nSnapshot } from "../i18nStore";
import { L } from "../generated/localizationKeys";

describe("dictionary generation loading", () => {
  afterEach(() => {
    effectCleanup?.();
    effectCleanup = undefined;
    effectDependencies = undefined;
    dictionaryRevision = 7;
    vi.unstubAllGlobals();
    vi.restoreAllMocks();
  });

  it("publishes only the newest revision when the same locale is recomposed", async () => {
    vi.stubGlobal("document", { documentElement: { lang: "", dataset: {} } });
    const requests: Array<{
      url: string;
      signal: AbortSignal;
      resolve: (response: { ok: true; json: () => Promise<Record<string, string>> }) => void;
    }> = [];
    vi.stubGlobal("fetch", vi.fn((url: string, init: { signal: AbortSignal }) =>
      new Promise((resolve) => requests.push({ url, signal: init.signal, resolve }))));
    const initialGeneration = getI18nSnapshot().generation;

    I18nProvider({ children: null });
    await vi.waitFor(() => expect(requests).toHaveLength(3));
    dictionaryRevision = 8;
    I18nProvider({ children: null });
    await vi.waitFor(() => expect(requests).toHaveLength(6));

    // revision更新で旧3リクエストを破棄し、新3リクエストだけを完了させる
    // Abort the old three requests on revision change and complete only the new three
    expect(requests.slice(0, 3).every(({ signal }) => signal.aborted)).toBe(true);
    expect(requests.slice(3).map(({ url }) => url)).toEqual([
      "/api/i18n/english?revision=8",
      "/api/i18n/source?revision=8",
      "/api/i18n/japanese?revision=8",
    ]);
    requests.slice(3).forEach((request) =>
      request.resolve({ ok: true, json: async () => ({ [L.ui.mainMenu.playLocally]: "Revision 8" }) }));
    await vi.waitFor(() =>
      expect(currentTranslation(L.ui.mainMenu.playLocally)).toBe("Revision 8"));

    requests.slice(0, 3).forEach((request) =>
      request.resolve({ ok: true, json: async () => ({ [L.ui.mainMenu.playLocally]: "Revision 7" }) }));
    await Promise.resolve();
    await Promise.resolve();

    expect(currentTranslation(L.ui.mainMenu.playLocally)).toBe("Revision 8");
    expect(getI18nSnapshot().generation).toBe(initialGeneration + 1);
  });
});

// 現在辞書はloaded判別の内側にあるため、テスト側で1箇所だけ取り出す
// The current dictionary lives inside the loaded variant, so unwrap it in one place for the tests
function currentTranslation(key: string): string | undefined {
  const { dictionaries } = getI18nSnapshot();
  return dictionaries.kind === "loaded" ? dictionaries.dictionary[key] : undefined;
}
