import { describe, expect, it, vi } from "vitest";
import { L } from "../index";
import { createTranslator, getI18nSnapshot, setDictionaries, type TranslationDictionary } from "../i18nStore";

const readySnapshotState = { status: "ready" as const, requestedLocale: "english", generation: 1 };
function loadedDictionaries(dictionary: TranslationDictionary, fallbackDictionary: TranslationDictionary, sourceDictionary: TranslationDictionary) {
  return { kind: "loaded" as const, dictionary, fallbackDictionary, sourceDictionary };
}

describe("i18n generation diagnostics", () => {
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
  it("deduplicates each key and placeholder across renders and isolates generations", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => undefined);
    const dictionary = { [L.ui.tooltip.placeWireCost]: "{count}/{count}/{unit}", [L.ui.tooltip.requiredItems]: "{count}" };
    setDictionaries("english", dictionary, {}, {});
    const oldTranslator = createTranslator(getI18nSnapshot());
    expect(oldTranslator(L.ui.tooltip.placeWireCost, { count: 0, unit: "" })).toBe("0/0/");
    expect(warn).not.toHaveBeenCalled();
    expect(oldTranslator(L.ui.tooltip.placeWireCost)).toBe("{count}/{count}/{unit}");
    createTranslator(getI18nSnapshot())(L.ui.tooltip.placeWireCost);
    expect(warn).toHaveBeenCalledTimes(2);
    oldTranslator(L.ui.tooltip.requiredItems);
    expect(warn).toHaveBeenCalledTimes(3);
    setDictionaries("english", dictionary, {}, {});
    oldTranslator(L.ui.tooltip.placeWireCost);
    expect(warn).toHaveBeenCalledTimes(3);
    createTranslator(getI18nSnapshot())(L.ui.tooltip.placeWireCost);
    expect(warn).toHaveBeenCalledTimes(5);
    warn.mockRestore();
  });
});
