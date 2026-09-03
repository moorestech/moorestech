import { Text, Title } from "@mantine/core";
import { dispatchAction, Topics, useLanguageList, useTopic } from "@/bridge";
import { DictionaryIndependentText, L, useI18n } from "@/shared/i18n";
import { ModeSwitch } from "@/shared/ui";

// ポーズメニュー内の言語一覧。現在値はlocalization.currentトピックを正とする
// Locale list inside the pause menu; localization.current is authoritative for selection
export function LanguageSelect() {
  const { t } = useI18n();
  const currentLocale = useTopic(Topics.localization)?.locale ?? "";
  const languages = useLanguageList();

  const label = t(L.ui.settings.language);

  return (
    <section aria-label={label}>
      <Title order={2}>{label}</Title>
      {/* 取得失敗はストアが自動再試行するため、届くまで読み込み中のまま待つ */}
      {/* The store retries a failed load on its own, so this waits as loading until the list arrives */}
      {languages.status === "loading" && (
        <Text c="dimmed" data-testid="language-list-loading">
          {DictionaryIndependentText.languageListLoading}
        </Text>
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
