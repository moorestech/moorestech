import { useSyncExternalStore } from "react";
import {
  VanillaLocalizationKeys,
  type VanillaLocalizationKey,
} from "./generated/localizationKeys";

export const FALLBACK_LOCALE = "english";

export type TranslationDictionary = Readonly<Record<string, string>>;
export type InterpolationValues = Readonly<Record<string, string | number>>;
export type TranslationKey = VanillaLocalizationKey;

export type I18nSnapshot = {
  locale: string;
  dictionary: TranslationDictionary;
  fallbackDictionary: TranslationDictionary;
  sourceDictionary: TranslationDictionary;
};

let snapshot: I18nSnapshot = {
  locale: FALLBACK_LOCALE,
  dictionary: {},
  fallbackDictionary: {},
  sourceDictionary: {},
};
const listeners = new Set<() => void>();
let warnedMissingTranslationKeys = new Set<TranslationKey>();
const translationKeys = new Set<string>(VanillaLocalizationKeys);

export function isTranslationKey(value: string): value is TranslationKey {
  return translationKeys.has(value);
}

export function setDictionaries(
  locale: string,
  dictionary: TranslationDictionary,
  fallbackDictionary: TranslationDictionary,
  sourceDictionary: TranslationDictionary,
): void {
  snapshot = { locale, dictionary, fallbackDictionary, sourceDictionary };
  warnedMissingTranslationKeys = new Set<TranslationKey>();
  listeners.forEach((listener) => listener());
}

export function createTranslator(current: I18nSnapshot) {
  const warnedKeysForGeneration = warnedMissingTranslationKeys;
  return (key: TranslationKey, values: InterpolationValues = {}): string => {
    const template =
      nonEmptyTranslation(current.dictionary[key]) ??
      nonEmptyTranslation(current.fallbackDictionary[key]) ??
      nonEmptyTranslation(current.sourceDictionary[key]);

    // 同じ辞書世代では欠落キーごとの警告を一度に抑える
    // Warn only once per missing key within the same dictionary generation
    if (template === undefined && !warnedKeysForGeneration.has(key)) {
      warnedKeysForGeneration.add(key);
      console.warn(`[i18n] Missing translation key: ${key}`);
    }

    // 欠落キーは目立つプレースホルダで露出させる
    // Surface missing keys with a loud placeholder
    return (template ?? `[!${key}]`).replace(/\{([^{}]+)\}/g, (token, name: string) =>
      Object.hasOwn(values, name) ? String(values[name]) : token);
  };
}

export function useI18n() {
  const current = useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
  return { locale: current.locale, t: createTranslator(current) };
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function getSnapshot(): I18nSnapshot {
  return snapshot;
}

function nonEmptyTranslation(value: string | undefined): string | undefined {
  return value === undefined || value.length === 0 ? undefined : value;
}
