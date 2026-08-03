import { useMemo, useSyncExternalStore } from "react";
import {
  VanillaLocalizationKeys,
  type VanillaLocalizationKey,
} from "./generated/localizationKeys";
import type { ContentLocalizationKey } from "./contentKeys";

export const FALLBACK_LOCALE = "english";

export type TranslationDictionary = Readonly<Record<string, string>>;
export type InterpolationValues = Readonly<Record<string, string | number>>;
export type TranslationKey = VanillaLocalizationKey | ContentLocalizationKey;
export type I18nStatus = "loading" | "ready" | "error";

export type I18nSnapshot = {
  status: I18nStatus;
  locale: string;
  requestedLocale: string;
  generation: number;
  dictionary: TranslationDictionary;
  fallbackDictionary: TranslationDictionary;
  sourceDictionary: TranslationDictionary;
};

let snapshot: I18nSnapshot = {
  status: "loading",
  locale: FALLBACK_LOCALE,
  requestedLocale: FALLBACK_LOCALE,
  generation: 0,
  dictionary: {},
  fallbackDictionary: {},
  sourceDictionary: {},
};
const listeners = new Set<() => void>();
let warnedMissingTranslationKeys = new Set<TranslationKey>();
let warnedUnknownExternalKeys = new Set<string>();
const translationKeys = new Set<string>(VanillaLocalizationKeys);

// 導出キーは宣言表から生成される`<ns>.<uuid>.<field>`書式で、有限の生成済み一覧には載らない
// Derived keys follow the generated `<ns>.<uuid>.<field>` shape and never appear in the finite generated list
const CONTENT_KEY_RE = /^[a-z][a-zA-Z]*\.[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}\.[a-zA-Z]+$/;

export function isTranslationKey(value: string): value is TranslationKey {
  return translationKeys.has(value) || CONTENT_KEY_RE.test(value);
}

// ホスト側の位置パラメータを{p0}補間値へ変換する（通知・tooltip共通の規約）
// Convert host-side positional params into {p0} interpolation values (shared by notifications and tooltips)
export function buildPositionalInterpolationValues(values: readonly string[]): InterpolationValues {
  return Object.fromEntries(values.map((value, index) => [`p${index}`, value]));
}

export function setDictionaries(
  locale: string,
  dictionary: TranslationDictionary,
  fallbackDictionary: TranslationDictionary,
  sourceDictionary: TranslationDictionary,
): void {
  snapshot = {
    status: "ready",
    locale,
    requestedLocale: locale,
    generation: snapshot.generation + 1,
    dictionary,
    fallbackDictionary,
    sourceDictionary,
  };
  warnedMissingTranslationKeys = new Set<TranslationKey>();
  warnedUnknownExternalKeys = new Set<string>();
  notifyListeners();
}

export function setDictionaryLoading(requestedLocale: string): void {
  snapshot = { ...snapshot, status: "loading", requestedLocale };
  notifyListeners();
}

export function setDictionaryLoadError(requestedLocale: string): void {
  if (snapshot.requestedLocale !== requestedLocale) return;
  snapshot = { ...snapshot, status: "error" };
  notifyListeners();
}

export function getI18nSnapshot(): I18nSnapshot {
  return snapshot;
}

export function createTranslator(current: I18nSnapshot) {
  const warnedKeysForGeneration = warnedMissingTranslationKeys;
  return (key: TranslationKey, values: InterpolationValues = {}): string => {
    if (current.generation === 0) return "";

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

export function translateExternalKey(
  key: string,
  translate: (key: TranslationKey, values: InterpolationValues) => string,
  values: InterpolationValues,
): string {
  if (isTranslationKey(key)) return translate(key, values);
  if (!warnedUnknownExternalKeys.has(key)) {
    warnedUnknownExternalKeys.add(key);
    console.warn(`[i18n] Unknown localized external key: ${key}`);
  }
  return `[!${key}]`;
}

export function useI18n() {
  const current = useSyncExternalStore(subscribe, getSnapshot, getSnapshot);
  const t = useMemo(() => createTranslator(current), [current]);
  return {
    status: current.status,
    locale: current.locale,
    requestedLocale: current.requestedLocale,
    t,
  };
}

function subscribe(listener: () => void): () => void {
  listeners.add(listener);
  return () => listeners.delete(listener);
}

function getSnapshot(): I18nSnapshot {
  return snapshot;
}

function notifyListeners(): void {
  listeners.forEach((listener) => listener());
}

function nonEmptyTranslation(value: string | undefined): string | undefined {
  return value === undefined || value.length === 0 ? undefined : value;
}
