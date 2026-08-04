import { useEffect, useState } from "react";
import { Button, Stack, Text, Title } from "@mantine/core";
import { dispatchAction, localizationLanguagesUrl, Topics, useTopic } from "@/bridge";
import { DictionaryIndependentText, L, useI18n } from "@/shared/i18n";
import { ModeSwitch } from "@/shared/ui";

type LanguageEntry = {
  code: string;
  displayName: string;
};

type LanguageListState = {
  status: "loading" | "error" | "ready";
  entries: LanguageEntry[];
};

// ポーズメニュー内の言語一覧。現在値はlocalization.currentトピックを正とする
// Locale list inside the pause menu; localization.current is authoritative for selection
export function LanguageSelect() {
  const { t } = useI18n();
  const currentLocale = useTopic(Topics.localization)?.locale ?? "";
  const [languages, setLanguages] = useState<LanguageListState>({ status: "loading", entries: [] });
  const [reloadCount, setReloadCount] = useState(0);

  useEffect(() => {
    // HTTP境界の失敗はerrorとして持ち、unmount時は遅延応答を破棄する
    // Keep HTTP boundary failures as an error state and discard late responses after unmount
    const abort = new AbortController();
    setLanguages({ status: "loading", entries: [] });
    void fetch(localizationLanguagesUrl, { signal: abort.signal })
      .then((response) => response.ok
        ? response.json() as Promise<unknown>
        : Promise.reject(new Error(`Failed to load languages: HTTP ${response.status}`)))
      .then((data) => {
        if (!abort.signal.aborted) setLanguages({ status: "ready", entries: toLanguageEntries(data) });
      })
      .catch(() => {
        if (!abort.signal.aborted) setLanguages({ status: "error", entries: [] });
      });
    return () => abort.abort();
  }, [reloadCount]);

  const label = t(L.ui.settings.language);
  // 配信順を維持して共通の択一UIへ射影する
  // Preserve server order while projecting entries into the shared exclusive selector
  const options = languages.entries.map((language) => ({
    value: language.code,
    label: language.displayName,
    testId: `language-select-option-${language.code}`,
  }));

  return (
    <section aria-label={label}>
      <Title order={2}>{label}</Title>
      {languages.status === "error"
        ? (
          <Stack align="flex-start" gap="xs">
            {/* 一覧取得の失敗は辞書に依存しないリテラルで伝える */}
            {/* Report the list failure with copy that does not depend on the dictionary */}
            <Text c="dimmed" data-testid="language-list-error">
              {DictionaryIndependentText.languageListLoadFailed}
            </Text>
            <Button onClick={() => setReloadCount((count) => count + 1)} data-testid="language-list-retry">
              {DictionaryIndependentText.retry}
            </Button>
          </Stack>
        )
        : (
          <ModeSwitch
            value={currentLocale}
            options={options}
            onChange={(locale) => void dispatchAction("localization.setLocale", { locale })}
            orientation="vertical"
            testId="language-select"
          />
        )}
    </section>
  );
}

function toLanguageEntries(data: unknown): LanguageEntry[] {
  // 外部JSONは完全なcode/displayName組だけを表示候補として受理する
  // Accept only complete code/displayName pairs from external JSON as display candidates
  if (!Array.isArray(data)) return [];
  return data.filter((entry): entry is LanguageEntry =>
    typeof entry === "object"
    && entry !== null
    && "code" in entry
    && typeof entry.code === "string"
    && "displayName" in entry
    && typeof entry.displayName === "string");
}
