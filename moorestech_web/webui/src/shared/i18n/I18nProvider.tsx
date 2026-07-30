import { useEffect, type ReactNode } from "react";
import { localizationDictionaryUrl, Topics, useTopic } from "@/bridge";
import {
  FALLBACK_LOCALE,
  setDictionaries,
  setDictionaryLoadError,
  setDictionaryLoading,
  type TranslationDictionary,
} from "./i18nStore";

export function I18nProvider({ children }: { children: ReactNode }) {
  const localization = useTopic(Topics.localization);
  const locale = localization?.locale;
  const revision = localization?.revision;

  useEffect(() => {
    // 辞書準備topicまで初回取得待機
    // Wait for the dictionary-ready topic before initial loading
    if (locale === undefined || revision === undefined) return;

    const abort = new AbortController();
    setDictionaryLoading(locale);
    void loadDictionaries(locale, revision, abort.signal).catch((error: unknown) => {
      // HTTP/JSONは外部境界のため、切替失敗を画面全体の未処理rejectionへ波及させない
      // HTTP/JSON is an external boundary; do not turn a switch failure into an unhandled rejection
      if (!abort.signal.aborted) {
        setDictionaryLoadError(locale);
        console.error(`[i18n] Failed to switch locale to '${locale}'`, error);
      }
    });
    return () => abort.abort();
  }, [locale, revision]);

  return children;
}

async function loadDictionaries(locale: string, revision: number, signal: AbortSignal): Promise<void> {
  const fallbackPromise = fetchDictionary(FALLBACK_LOCALE, revision, signal);
  const sourcePromise = fetchDictionary("source", revision, signal);
  const dictionaryPromise = locale === FALLBACK_LOCALE
    ? fallbackPromise
    : fetchDictionary(locale, revision, signal);
  const [dictionary, fallbackDictionary, sourceDictionary] =
    await Promise.all([dictionaryPromise, fallbackPromise, sourcePromise]);
  if (signal.aborted) return;

  document.documentElement.lang = locale;
  document.documentElement.dataset.locale = locale;
  setDictionaries(locale, dictionary, fallbackDictionary, sourceDictionary);
}

async function fetchDictionary(
  locale: string,
  revision: number,
  signal: AbortSignal,
): Promise<TranslationDictionary> {
  const response = await fetch(localizationDictionaryUrl(locale, revision), { signal });
  if (!response.ok) throw new Error(`Failed to load locale '${locale}': HTTP ${response.status}`);
  return response.json() as Promise<TranslationDictionary>;
}
