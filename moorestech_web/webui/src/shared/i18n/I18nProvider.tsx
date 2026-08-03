import { useEffect, type ReactNode } from "react";
import { localizationDictionaryUrl, Topics, useTopic } from "@/bridge";
import {
  FALLBACK_LOCALE,
  setDictionaries,
  setDictionaryLoadError,
  setDictionaryLoading,
  SOURCE_LOCALE,
  type TranslationDictionary,
} from "./i18nStore";

const RETRY_DELAY_MS = 5000;

// 復旧可能性でHTTP失敗を分類する（再試行の可否は呼び出し側が決める）
// Classify HTTP failures by recoverability; the caller decides whether to retry
type DictionaryFetchFailure = "unavailable" | "staleRevision" | "transient";

class DictionaryFetchError extends Error {
  readonly failure: DictionaryFetchFailure;

  constructor(failure: DictionaryFetchFailure, message: string) {
    super(message);
    this.failure = failure;
  }
}

export function I18nProvider({ children }: { children: ReactNode }) {
  const localization = useTopic(Topics.localization);
  const locale = localization?.locale;
  const revision = localization?.revision;

  useEffect(() => {
    // 辞書準備topicまで初回取得待機
    // Wait for the dictionary-ready topic before initial loading
    if (locale === undefined || revision === undefined) return;

    const abort = new AbortController();
    let retryTimer: ReturnType<typeof setTimeout> | undefined;

    const startLoad = (retriesLeft: number) => {
      void loadDictionaries(locale, revision, abort.signal).catch((error: unknown) => {
        // HTTP/JSONは外部境界のため、切替失敗を画面全体の未処理rejectionへ波及させない
        // HTTP/JSON is an external boundary; do not turn a switch failure into an unhandled rejection
        if (abort.signal.aborted) return;
        console.error(`[i18n] Failed to switch locale to '${locale}'`, error);

        // 409は新revisionのpushが同じ経路で取り直すため、ここでは再試行も失敗表示もしない
        // A 409 is refetched by the next revision push, so neither retry nor surface it here
        const failure = classifyFetchFailure(error);
        if (failure === "staleRevision") return;

        // 一時障害だけを5秒後に1回だけ再試行し、それでも駄目ならUIへ失敗を出す
        // Retry only transient failures once after five seconds, then surface the failure to the UI
        if (failure === "transient" && retriesLeft > 0) {
          retryTimer = setTimeout(() => startLoad(retriesLeft - 1), RETRY_DELAY_MS);
          return;
        }
        setDictionaryLoadError(locale);
      });
    };

    setDictionaryLoading(locale);
    startLoad(1);
    return () => {
      abort.abort();
      clearTimeout(retryTimer);
    };
  }, [locale, revision]);

  return children;
}

async function loadDictionaries(locale: string, revision: number, signal: AbortSignal): Promise<void> {
  const fallbackPromise = fetchDictionary(FALLBACK_LOCALE, revision, signal);
  const sourcePromise = fetchDictionary(SOURCE_LOCALE, revision, signal);
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
  if (!response.ok) {
    throw new DictionaryFetchError(
      classifyResponseStatus(response.status),
      `Failed to load locale '${locale}': HTTP ${response.status}`,
    );
  }
  return response.json() as Promise<TranslationDictionary>;
}

function classifyResponseStatus(status: number): DictionaryFetchFailure {
  // 400/404は要求自体が不正・不在で、待っても直らない
  // 400/404 mean the request itself is invalid or absent and waiting cannot fix it
  if (status === 400 || status === 404) return "unavailable";
  if (status === 409) return "staleRevision";
  return "transient";
}

function classifyFetchFailure(error: unknown): DictionaryFetchFailure {
  // 通信断・JSON破損はDictionaryFetchError以外で届くため一時障害として扱う
  // Network drops and broken JSON arrive as other error types, so treat them as transient
  return error instanceof DictionaryFetchError ? error.failure : "transient";
}
