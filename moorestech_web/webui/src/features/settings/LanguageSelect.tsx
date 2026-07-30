import { useEffect, useState } from "react";
import { Title } from "@mantine/core";
import { dispatchAction, localizationLanguagesUrl, Topics, useTopic } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { ModeSwitch } from "@/shared/ui";

type LanguageEntry = {
  code: string;
  displayName: string;
};

// ポーズメニュー内の言語一覧。現在値はlocalization.currentトピックを正とする
// Locale list inside the pause menu; localization.current is authoritative for selection
export function LanguageSelect() {
  const { t } = useI18n();
  const currentLocale = useTopic(Topics.localization)?.locale ?? "";
  const [languages, setLanguages] = useState<LanguageEntry[]>([]);

  useEffect(() => {
    // HTTP境界の失敗は空一覧として扱い、unmount時は遅延応答を破棄する
    // Treat HTTP boundary failures as an empty list and discard late responses after unmount
    const abort = new AbortController();
    void fetch(localizationLanguagesUrl, { signal: abort.signal })
      .then((response) => response.ok ? response.json() as Promise<unknown> : [])
      .then((data) => {
        if (!abort.signal.aborted) setLanguages(toLanguageEntries(data));
      })
      .catch(() => undefined);
    return () => abort.abort();
  }, []);

  const label = t(L.ui.settings.language);
  // 配信順を維持して共通の択一UIへ射影する
  // Preserve server order while projecting entries into the shared exclusive selector
  const options = languages.map((language) => ({
    value: language.code,
    label: language.displayName,
    testId: `language-select-option-${language.code}`,
  }));

  return (
    <section aria-label={label}>
      <Title order={2}>{label}</Title>
      <ModeSwitch
        value={currentLocale}
        options={options}
        onChange={(locale) => void dispatchAction("localization.setLocale", { locale })}
        orientation="vertical"
        testId="language-select"
      />
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
