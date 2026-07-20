import { useEffect, type ReactNode } from "react";
import { localizationDictionaryUrl, Topics, useTopic } from "@/bridge";
import { FALLBACK_LOCALE, setDictionaries, type TranslationDictionary } from "./i18nStore";

export function I18nProvider({ children }: { children: ReactNode }) {
  const localization = useTopic(Topics.localization);
  const locale = localization?.locale ?? FALLBACK_LOCALE;

  useEffect(() => {
    const abort = new AbortController();
    void loadDictionaries(locale, abort.signal).catch((error: unknown) => {
      // HTTP/JSONは外部境界のため、切替失敗を画面全体の未処理rejectionへ波及させない
      // HTTP/JSON is an external boundary; do not turn a switch failure into an unhandled rejection
      if (!abort.signal.aborted) console.error(`[i18n] Failed to switch locale to '${locale}'`, error);
    });
    return () => abort.abort();
  }, [locale]);

  return children;
}

async function loadDictionaries(locale: string, signal: AbortSignal): Promise<void> {
  const fallbackPromise = fetchDictionary(FALLBACK_LOCALE, signal);
  const dictionaryPromise = locale === FALLBACK_LOCALE ? fallbackPromise : fetchDictionary(locale, signal);
  const [dictionary, fallbackDictionary] = await Promise.all([dictionaryPromise, fallbackPromise]);
  if (signal.aborted) return;

  document.documentElement.lang = locale;
  document.documentElement.dataset.locale = locale;
  setDictionaries(locale, dictionary, fallbackDictionary);
}

async function fetchDictionary(locale: string, signal: AbortSignal): Promise<TranslationDictionary> {
  const response = await fetch(localizationDictionaryUrl(locale), { signal });
  if (!response.ok) throw new Error(`Failed to load locale '${locale}': HTTP ${response.status}`);
  return response.json() as Promise<TranslationDictionary>;
}
