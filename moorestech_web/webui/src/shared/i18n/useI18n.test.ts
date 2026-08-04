import { describe, expect, it, vi } from "vitest";
import { isTranslationKey, L } from "./index";
import {
  createTranslator,
  getI18nSnapshot,
  setDictionaries,
  setDictionaryLoadError,
  setDictionaryLoading,
  translateExternalKey,
  type DictionaryContent,
  type TranslationDictionary,
} from "./i18nStore";

const readySnapshotState = {
  status: "ready" as const,
  requestedLocale: "english",
  generation: 1,
};

function loadedDictionaries(
  dictionary: TranslationDictionary,
  fallbackDictionary: TranslationDictionary,
  sourceDictionary: TranslationDictionary,
): DictionaryContent {
  return { kind: "loaded", dictionary, fallbackDictionary, sourceDictionary };
}

// storeはモジュール状態のため、辞書ゼロを見る4件は辞書投入より前に置く
// The store keeps module state, so the four dictionary-less cases must run before any dictionary is set
describe("useI18n translation behavior", () => {
  it("starts uninitialized with no dictionary content", () => {
    expect(getI18nSnapshot()).toMatchObject({ status: "uninitialized", dictionaries: { kind: "none" } });
    expect(createTranslator(getI18nSnapshot())(L.ui.mainMenu.playLocally)).toBe("");
  });

  it("does not warn or show a missing marker before the first dictionary generation is ready", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    setDictionaryLoading("english");

    expect(createTranslator(getI18nSnapshot())(L.ui.mainMenu.playLocally)).toBe("");
    expect(getI18nSnapshot().status).toBe("loading");
    expect(warn).not.toHaveBeenCalled();
    warn.mockRestore();
  });

  it("exposes an error status when the very first dictionary load fails", () => {
    setDictionaryLoadError("english");

    expect(getI18nSnapshot()).toMatchObject({ status: "error", requestedLocale: "english" });
  });

  it("keeps translations empty and silent after the very first load failed", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);

    // 辞書ゼロのままerrorへ落ちても、全画面が[!key]で埋まらないこと
    // Even when the failure lands with no dictionary, the screen must not fill up with [!key] markers
    expect(getI18nSnapshot().dictionaries.kind).toBe("none");
    expect(createTranslator(getI18nSnapshot())(L.ui.mainMenu.playLocally)).toBe("");
    expect(createTranslator(getI18nSnapshot())(L.ui.settings.language)).toBe("");
    expect(warn).not.toHaveBeenCalled();
    warn.mockRestore();
  });

  it("keeps the last ready generation while exposing a later load failure", () => {
    setDictionaries(
      "english",
      { [L.ui.mainMenu.playLocally]: "Ready title" },
      { [L.ui.mainMenu.playLocally]: "Ready title" },
      { [L.ui.mainMenu.playLocally]: "Source title" },
    );
    setDictionaryLoading("japanese");
    setDictionaryLoadError("japanese");

    expect(getI18nSnapshot()).toMatchObject({
      status: "error",
      locale: "english",
      requestedLocale: "japanese",
    });
    expect(createTranslator(getI18nSnapshot())(L.ui.mainMenu.playLocally)).toBe("Ready title");
  });

  it("warns once per generation for an unknown external localized key", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const translate = vi.fn(() => "unused");

    translateExternalKey("ui.external.unknown", translate, {});
    translateExternalKey("ui.external.unknown", translate, {});
    expect(warn).toHaveBeenCalledOnce();

    setDictionaries("english", {}, {}, {});
    translateExternalKey("ui.external.unknown", translate, {});
    expect(warn).toHaveBeenCalledTimes(2);
    warn.mockRestore();
  });

  it("narrows only generated keys at an external string boundary", () => {
    expect(isTranslationKey(L.ui.inventory.title)).toBe(true);
    expect(isTranslationKey("持ち物")).toBe(false);
    expect(isTranslationKey("ui.inventory.missing")).toBe(false);
  });

  it("current locale wins and interpolates named values", () => {
    const t = createTranslator({
      ...readySnapshotState,
      locale: "japanese",
      dictionaries: loadedDictionaries(
        { [L.ui.mainMenu.playLocally]: "こんにちは、{name}。残り{count}個" },
        { [L.ui.mainMenu.playLocally]: "Hello, {name}" },
        { [L.ui.mainMenu.playLocally]: "Source {name}" },
      ),
    });
    expect(t(L.ui.mainMenu.playLocally, { name: "Moore", count: 3 })).toBe("こんにちは、Moore。残り3個");
  });

  it("uses the fallback locale when the current dictionary lacks a key", () => {
    const t = createTranslator({
      ...readySnapshotState,
      locale: "japanese",
      dictionaries: loadedDictionaries(
        {},
        { [L.ui.mainMenu.playLocally]: "Play locally" },
        { [L.ui.mainMenu.playLocally]: "Source play" },
      ),
    });
    expect(t(L.ui.mainMenu.playLocally)).toBe("Play locally");
  });

  it("uses the source text when the current and fallback dictionaries lack a key", () => {
    const t = createTranslator({
      ...readySnapshotState,
      locale: "japanese",
      dictionaries: loadedDictionaries({}, {}, { [L.ui.mainMenu.playLocally]: "Play locally source" }),
    });

    expect(t(L.ui.mainMenu.playLocally)).toBe("Play locally source");
  });

  it("treats an empty current translation as missing and uses fallback", () => {
    const t = createTranslator({
      ...readySnapshotState,
      locale: "japanese",
      dictionaries: loadedDictionaries(
        { [L.ui.mainMenu.playLocally]: "" },
        { [L.ui.mainMenu.playLocally]: "Play locally" },
        { [L.ui.mainMenu.playLocally]: "Play locally source" },
      ),
    });

    expect(t(L.ui.mainMenu.playLocally)).toBe("Play locally");
  });

  it("treats empty current and fallback translations as missing and uses source", () => {
    const t = createTranslator({
      ...readySnapshotState,
      locale: "japanese",
      dictionaries: loadedDictionaries(
        { [L.ui.mainMenu.playLocally]: "" },
        { [L.ui.mainMenu.playLocally]: "" },
        { [L.ui.mainMenu.playLocally]: "Play locally source" },
      ),
    });

    expect(t(L.ui.mainMenu.playLocally)).toBe("Play locally source");
  });

  it("shows a loud marker and warns when all three translation layers are empty", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const t = createTranslator({
      ...readySnapshotState,
      locale: "japanese",
      dictionaries: loadedDictionaries(
        { [L.ui.mainMenu.playLocally]: "" },
        { [L.ui.mainMenu.playLocally]: "" },
        { [L.ui.mainMenu.playLocally]: "" },
      ),
    });

    expect(t(L.ui.mainMenu.playLocally)).toBe(`[!${L.ui.mainMenu.playLocally}]`);
    expect(warn).toHaveBeenCalledWith(`[i18n] Missing translation key: ${L.ui.mainMenu.playLocally}`);
    warn.mockRestore();
  });

  it("leaves unknown interpolation variables visible for diagnosis", () => {
    const t = createTranslator({
      ...readySnapshotState,
      locale: "english",
      dictionaries: loadedDictionaries({ [L.ui.mainMenu.playLocally]: "Hello {name}, {count}" }, {}, {}),
    });
    expect(t(L.ui.mainMenu.playLocally, { name: "Moore" })).toBe("Hello Moore, {count}");
  });

  it("warns once per missing key until dictionaries change", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const current = {
      ...readySnapshotState,
      locale: "japanese",
      dictionaries: loadedDictionaries({}, {}, {}),
    };
    setDictionaries(current.locale, {}, {}, {});
    const first = createTranslator(current);
    const second = createTranslator(current);

    first(L.ui.mainMenu.playLocally);
    second(L.ui.mainMenu.playLocally);
    expect(warn).toHaveBeenCalledTimes(1);

    // 辞書更新後に警告を再許可する
    // Allow the warning again after dictionary updates
    setDictionaries("english", {}, {}, {});
    createTranslator({
      ...readySnapshotState,
      locale: "english",
      dictionaries: loadedDictionaries({}, {}, {}),
    })(
      L.ui.mainMenu.playLocally,
    );
    expect(warn).toHaveBeenCalledTimes(2);
    warn.mockRestore();
  });

  it("keeps missing-key warnings isolated between old and current translators", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    setDictionaries("japanese", {}, {}, {});
    const oldTranslator = createTranslator({
      ...readySnapshotState,
      locale: "japanese",
      dictionaries: loadedDictionaries({}, {}, {}),
    });

    // 旧翻訳器と現行警告を分離する
    // Keep old translators isolated from current warnings
    setDictionaries("english", {}, {}, {});
    const currentTranslator = createTranslator({
      ...readySnapshotState,
      locale: "english",
      dictionaries: loadedDictionaries({}, {}, {}),
    });
    oldTranslator(L.ui.mainMenu.playLocally);
    currentTranslator(L.ui.mainMenu.playLocally);

    expect(warn).toHaveBeenCalledTimes(2);
    warn.mockRestore();
  });
});
