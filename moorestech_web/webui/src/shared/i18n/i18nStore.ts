import { useSyncExternalStore } from "react";

export const FALLBACK_LOCALE = "english";

export type TranslationDictionary = Readonly<Record<string, string>>;
export type InterpolationValues = Readonly<Record<string, string | number>>;

export type I18nSnapshot = {
  locale: string;
  dictionary: TranslationDictionary;
  fallbackDictionary: TranslationDictionary;
};

let snapshot: I18nSnapshot = {
  locale: FALLBACK_LOCALE,
  dictionary: {},
  fallbackDictionary: {},
};
const listeners = new Set<() => void>();

export function setDictionaries(
  locale: string,
  dictionary: TranslationDictionary,
  fallbackDictionary: TranslationDictionary,
): void {
  snapshot = { locale, dictionary, fallbackDictionary };
  listeners.forEach((listener) => listener());
}

export function createTranslator(current: I18nSnapshot) {
  return (key: string, values: InterpolationValues = {}): string => {
    const template = current.dictionary[key] ?? current.fallbackDictionary[key];
    if (template === undefined) console.warn(`[i18n] Missing translation key: ${key}`);

    // 未登録keyもkey文字列をテンプレートとして補間する（移行期のkey=原文運用を成立させる）
    // Interpolate the key itself when unregistered so the transitional key-as-source-text style works
    return (template ?? key).replace(/\{([^{}]+)\}/g, (token, name: string) =>
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
