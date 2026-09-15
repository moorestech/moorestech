import { Button, Group, Stack, Text } from "@mantine/core";
import { useLanguageList } from "@/bridge";
import { DictionaryIndependentText } from "@/shared/i18n";
import { useGateAnswer, type GateAnswerCopy } from "@/shared/ui";

// 選ばせる対象が辞書そのものなので、結末の1行も辞書を通さない（ADR 0040）
// The dictionary itself is what gets chosen, so the outcome line bypasses the dictionary as well (ADR 0040)
const LanguageGateAnswerCopy: GateAnswerCopy = {
  answerAccepted: DictionaryIndependentText.languageSelectAccepted,
  notClosed: DictionaryIndependentText.languageSelectNotClosed,
  disconnected: DictionaryIndependentText.languageSelectDisconnected,
  respondFailed: DictionaryIndependentText.languageSelectFailed,
};

// 待機中だけマウントされる本体。一覧の取得もここで初めて起きる
// The body mounted only while waiting; the list fetch starts here for the first time
export function EventLanguageGateBody() {
  const languages = useLanguageList();
  const { disabled, message, answer } = useGateAnswer("event_mode.select_language", LanguageGateAnswerCopy);

  // 一覧はローダーが3秒間隔で自動再試行するため、届くまでは読み込み中を出し続ける
  // The loader retries the list every 3s, so this keeps showing the loading line until entries arrive
  if (languages.status === "loading") {
    return (
      <Text c="white" data-testid="event-language-gate-loading">
        {DictionaryIndependentText.languageListLoading}
      </Text>
    );
  }

  return (
    <Stack align="center" gap="md">
      <Group justify="center" gap="lg">
        {languages.entries.map((language) => (
          <Button
            key={language.code}
            size="xl"
            disabled={disabled}
            data-testid={`event-language-gate-option-${language.code}`}
            onClick={() => void answer({ locale: language.code })}
          >
            {language.displayName}
          </Button>
        ))}
      </Group>
      {/* トーストはゲートの下に隠れるため、応答がどうなったかはこの1行だけが伝える */}
      {/* Toasts hide beneath the gate, so this single line is the only report of what became of the answer */}
      {message !== null && (
        <Text c="white" data-testid="event-language-gate-status">
          {message}
        </Text>
      )}
    </Stack>
  );
}
