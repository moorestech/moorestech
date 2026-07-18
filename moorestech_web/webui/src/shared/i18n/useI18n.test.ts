import { describe, expect, it, vi } from "vitest";
import { createTranslator } from "./i18nStore";

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
});
