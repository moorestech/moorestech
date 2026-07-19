import { describe, expect, it, vi } from "vitest";
import { createTranslator, setDictionaries } from "./i18nStore";

describe("useI18n translation behavior", () => {
  it("current locale wins and interpolates named values", () => {
    const t = createTranslator({
      locale: "japanese",
      dictionary: { greeting: "こんにちは、{name}。残り{count}個" },
      fallbackDictionary: { greeting: "Hello, {name}" },
    });
    expect(t("greeting", { name: "Moore", count: 3 })).toBe("こんにちは、Moore。残り3個");
  });

  it("uses the fallback locale when the current dictionary lacks a key", () => {
    const t = createTranslator({
      locale: "japanese",
      dictionary: {},
      fallbackDictionary: { menu: "Menu" },
    });
    expect(t("menu")).toBe("Menu");
  });

  it("shows and warns with the key when both dictionaries lack it", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const t = createTranslator({ locale: "japanese", dictionary: {}, fallbackDictionary: {} });
    expect(t("missing.key")).toBe("missing.key");
    expect(warn).toHaveBeenCalledWith("[i18n] Missing translation key: missing.key");
    warn.mockRestore();
  });

  it("leaves unknown interpolation variables visible for diagnosis", () => {
    const t = createTranslator({
      locale: "english",
      dictionary: { greeting: "Hello {name}, {count}" },
      fallbackDictionary: {},
    });
    expect(t("greeting", { name: "Moore" })).toBe("Hello Moore, {count}");
  });

  it("warns once per missing key until dictionaries change", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const current = { locale: "japanese", dictionary: {}, fallbackDictionary: {} };
    setDictionaries(current.locale, current.dictionary, current.fallbackDictionary);
    const first = createTranslator(current);
    const second = createTranslator(current);

    first("missing.key");
    second("missing.key");
    expect(warn).toHaveBeenCalledTimes(1);

    // 辞書更新後は新しい世代の欠落として再度一度だけ報告する
    // Report the missing key once again for the new dictionary generation
    setDictionaries("english", {}, {});
    createTranslator({ locale: "english", dictionary: {}, fallbackDictionary: {} })("missing.key");
    expect(warn).toHaveBeenCalledTimes(2);
    warn.mockRestore();
  });
});
