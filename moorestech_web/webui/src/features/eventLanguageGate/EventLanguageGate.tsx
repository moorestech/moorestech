import { useEffect, useState } from "react";
import { Button, Group, Overlay, Portal, Stack, Text, Title } from "@mantine/core";
import { dispatchAction, localizationLanguagesUrl, Topics, useTopicSelector } from "@/bridge";
import { DictionaryIndependentText } from "@/shared/i18n";

// 選ばせる対象が辞書そのものなのでt()を通さない。英語固定はADR 0040の裁定
// The dictionary itself is what gets chosen, so this bypasses t(); English-only is the ADR 0040 ruling
const HeadingText = "Select Language";

type LanguageEntry = {
  code: string;
  displayName: string;
};

type LanguageListState = {
  status: "loading" | "error" | "ready";
  entries: LanguageEntry[];
};

// 出展モードの開始ゲート。待機中だけ不透明な全画面で操作を塞ぎ、押下でゲームを始める
// The event-mode start gate; blocks input behind an opaque full screen while waiting and starts the game on press
export function EventLanguageGate() {
  const waiting = useTopicSelector(Topics.eventLanguageGate, (data) => data?.waiting ?? false);
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
          {languages.status === "error"
            ? (
              <Stack align="center" gap="sm">
                {/* 一覧取得の失敗は辞書に依存しないリテラルで伝える */}
                {/* Report the list failure with copy that does not depend on the dictionary */}
                <Text c="white" data-testid="event-language-gate-error">
                  {DictionaryIndependentText.languageListLoadFailed}
                </Text>
                <Button onClick={() => setReloadCount((count) => count + 1)} data-testid="event-language-gate-retry">
                  {DictionaryIndependentText.retry}
                </Button>
              </Stack>
            )
            : (
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
            )}
        </Stack>
      </Overlay>
    </Portal>
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
