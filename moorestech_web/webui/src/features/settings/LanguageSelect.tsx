import { Button, Stack, Text, Title } from "@mantine/core";
import { dispatchAction, Topics, useTopic } from "@/bridge";
import { DictionaryIndependentText, L, useI18n, useLanguageList } from "@/shared/i18n";
import { ModeSwitch } from "@/shared/ui";

// ポーズメニュー内の言語一覧。現在値はlocalization.currentトピックを正とする
// Locale list inside the pause menu; localization.current is authoritative for selection
export function LanguageSelect() {
  const { t } = useI18n();
  const currentLocale = useTopic(Topics.localization)?.locale ?? "";
  const { languages, reload } = useLanguageList();

  const label = t(L.ui.settings.language);

  return (
    <section aria-label={label}>
      <Title order={2}>{label}</Title>
      {languages.status === "loading" && (
        <Text c="dimmed" data-testid="language-list-loading">
          {DictionaryIndependentText.languageListLoading}
        </Text>
      )}
      {languages.status === "error" && (
        <Stack align="flex-start" gap="xs">
          {/* 一覧取得の失敗と選択肢ゼロを同じ扱いにし、辞書非依存リテラルで伝える */}
          {/* Treat a load failure and zero entries alike, reported with copy that does not depend on the dictionary */}
          <Text c="dimmed" data-testid="language-list-error">
            {DictionaryIndependentText.languageListLoadFailed}
          </Text>
          <Button onClick={reload} data-testid="language-list-retry">
            {DictionaryIndependentText.retry}
          </Button>
        </Stack>
      )}
      {languages.status === "ready" && (
        <ModeSwitch
          value={currentLocale}
          // 配信順を維持して共通の択一UIへ射影する
          // Preserve server order while projecting entries into the shared exclusive selector
          options={languages.entries.map((language) => ({
            value: language.code,
            label: language.displayName,
            testId: `language-select-option-${language.code}`,
          }))}
          onChange={(locale) => void dispatchAction("localization.setLocale", { locale })}
          orientation="vertical"
          testId="language-select"
        />
      )}
    </section>
  );
}
