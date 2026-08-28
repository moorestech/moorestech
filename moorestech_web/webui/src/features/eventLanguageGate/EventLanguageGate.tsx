import { Button, Group, Overlay, Portal, Stack, Text, Title } from "@mantine/core";
import { dispatchAction, Topics, useTopicSelector } from "@/bridge";
import { DictionaryIndependentText, type LanguageListState, useLanguageList } from "@/shared/i18n";

// 選ばせる対象が辞書そのものなのでt()を通さない。英語固定はADR 0040の裁定
// The dictionary itself is what gets chosen, so this bypasses t(); English-only is the ADR 0040 ruling
const HeadingText = "Select Language";

// 待機中は全画面で操作を塞ぎ、押下でゲーム開始するゲート
// Blocks input full-screen while waiting and starts the game on press
export function EventLanguageGate() {
  const waiting = useTopicSelector(Topics.eventLanguageGate, (data) => data?.waiting ?? false);
  const { languages, reload } = useLanguageList();

  if (!waiting) return null;

  return (
    <Portal>
      <Overlay
        fixed
        center
        backgroundOpacity={1}
        color="#000"
        zIndex="var(--z-portal-event-language-gate)"
        data-testid="event-language-gate"
      >
        <Stack align="center" gap="xl">
          <Title order={1} c="white">{HeadingText}</Title>
          {renderBody(languages, reload)}
        </Stack>
      </Overlay>
    </Portal>
  );
}

function renderBody(languages: LanguageListState, reload: () => void) {
  switch (languages.status) {
    case "loading":
      return (
        <Text c="white" data-testid="event-language-gate-loading">
          {DictionaryIndependentText.languageListLoading}
        </Text>
      );
    case "error":
      return (
        <Stack align="center" gap="sm">
          {/* 一覧取得の失敗と選択肢ゼロを同じ扱いにし、辞書非依存リテラルで伝える */}
          {/* Treat a load failure and zero entries alike, reported with copy that does not depend on the dictionary */}
          <Text c="white" data-testid="event-language-gate-error">
            {DictionaryIndependentText.languageListLoadFailed}
          </Text>
          <Button onClick={reload} data-testid="event-language-gate-retry">
            {DictionaryIndependentText.retry}
          </Button>
        </Stack>
      );
    case "ready":
      return (
        <Group justify="center" gap="lg">
          {languages.entries.map((language) => (
            <Button
              key={language.code}
              size="xl"
              data-testid={`event-language-gate-option-${language.code}`}
              onClick={() => void dispatchAction("event_mode.select_language", { locale: language.code })}
            >
              {language.displayName}
            </Button>
          ))}
        </Group>
      );
    default: {
      const exhaustive: never = languages;
      return exhaustive;
    }
  }
}
