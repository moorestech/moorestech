import { describe, expect, it, vi } from "vitest";
import { L } from "./generated/localizationKeys";
import { createTranslator, setDictionaries } from "./i18nStore";

describe("useI18n translation behavior", () => {
  it("current locale wins and interpolates named values", () => {
    const t = createTranslator({
      locale: "japanese",
      dictionary: { [L.ui.mainMenu.playLocally]: "こんにちは、{name}。残り{count}個" },
      fallbackDictionary: { [L.ui.mainMenu.playLocally]: "Hello, {name}" },
      sourceDictionary: { [L.ui.mainMenu.playLocally]: "Source {name}" },
    });
    expect(t(L.ui.mainMenu.playLocally, { name: "Moore", count: 3 })).toBe("こんにちは、Moore。残り3個");
  });

  it("uses the fallback locale when the current dictionary lacks a key", () => {
    const t = createTranslator({
      locale: "japanese",
      dictionary: {},
      fallbackDictionary: { [L.ui.mainMenu.playLocally]: "Play locally" },
      sourceDictionary: { [L.ui.mainMenu.playLocally]: "Source play" },
    });
    expect(t(L.ui.mainMenu.playLocally)).toBe("Play locally");
  });

  it("uses the source text when the current and fallback dictionaries lack a key", () => {
    const t = createTranslator({
      locale: "japanese",
      dictionary: {},
      fallbackDictionary: {},
      sourceDictionary: { [L.ui.mainMenu.playLocally]: "Play locally source" },
    });

    expect(t(L.ui.mainMenu.playLocally)).toBe("Play locally source");
  });

  it("treats an empty current translation as missing and uses fallback", () => {
    const t = createTranslator({
      locale: "japanese",
      dictionary: { [L.ui.mainMenu.playLocally]: "" },
      fallbackDictionary: { [L.ui.mainMenu.playLocally]: "Play locally" },
      sourceDictionary: { [L.ui.mainMenu.playLocally]: "Play locally source" },
    });

    expect(t(L.ui.mainMenu.playLocally)).toBe("Play locally");
  });

  it("treats empty current and fallback translations as missing and uses source", () => {
    const t = createTranslator({
      locale: "japanese",
      dictionary: { [L.ui.mainMenu.playLocally]: "" },
      fallbackDictionary: { [L.ui.mainMenu.playLocally]: "" },
      sourceDictionary: { [L.ui.mainMenu.playLocally]: "Play locally source" },
    });

    expect(t(L.ui.mainMenu.playLocally)).toBe("Play locally source");
  });

  it("shows a loud marker and warns when all three translation layers are empty", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const t = createTranslator({
      locale: "japanese",
      dictionary: { [L.ui.mainMenu.playLocally]: "" },
      fallbackDictionary: { [L.ui.mainMenu.playLocally]: "" },
      sourceDictionary: { [L.ui.mainMenu.playLocally]: "" },
    });

    expect(t(L.ui.mainMenu.playLocally)).toBe(`[!${L.ui.mainMenu.playLocally}]`);
    expect(warn).toHaveBeenCalledWith(`[i18n] Missing translation key: ${L.ui.mainMenu.playLocally}`);
    warn.mockRestore();
  });

  it("leaves unknown interpolation variables visible for diagnosis", () => {
    const t = createTranslator({
      locale: "english",
      dictionary: { [L.ui.mainMenu.playLocally]: "Hello {name}, {count}" },
      fallbackDictionary: {},
      sourceDictionary: {},
    });
    expect(t(L.ui.mainMenu.playLocally, { name: "Moore" })).toBe("Hello Moore, {count}");
  });

  it("warns once per missing key until dictionaries change", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const current = { locale: "japanese", dictionary: {}, fallbackDictionary: {}, sourceDictionary: {} };
    setDictionaries(current.locale, current.dictionary, current.fallbackDictionary, current.sourceDictionary);
    const first = createTranslator(current);
    const second = createTranslator(current);

    first(L.ui.mainMenu.playLocally);
    second(L.ui.mainMenu.playLocally);
    expect(warn).toHaveBeenCalledTimes(1);

    // 辞書更新後に警告を再許可する
    // Allow the warning again after dictionary updates
    setDictionaries("english", {}, {}, {});
    createTranslator({ locale: "english", dictionary: {}, fallbackDictionary: {}, sourceDictionary: {} })(
      L.ui.mainMenu.playLocally,
    );
    expect(warn).toHaveBeenCalledTimes(2);
    warn.mockRestore();
  });

  it("keeps missing-key warnings isolated between old and current translators", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    setDictionaries("japanese", {}, {}, {});
    const oldTranslator = createTranslator({
      locale: "japanese",
      dictionary: {},
      fallbackDictionary: {},
      sourceDictionary: {},
    });

    // 旧翻訳器と現行警告を分離する
    // Keep old translators isolated from current warnings
    setDictionaries("english", {}, {}, {});
    const currentTranslator = createTranslator({
      locale: "english",
      dictionary: {},
      fallbackDictionary: {},
      sourceDictionary: {},
    });
    oldTranslator(L.ui.mainMenu.playLocally);
    currentTranslator(L.ui.mainMenu.playLocally);

    expect(warn).toHaveBeenCalledTimes(2);
    warn.mockRestore();
  });
});
