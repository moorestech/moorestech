import { useState } from "react";
import { Button, Group, Stack, Text } from "@mantine/core";
import { dispatchAction, useLanguageList } from "@/bridge";
import { DictionaryIndependentText } from "@/shared/i18n";

// 応答待ちと失敗を来場者に見せるための状態。成功は待機解除でゲートごと消える
// The state that shows a pending response and a failure to the visitor; success removes the gate itself
type SelectionState = "idle" | "pending" | "failed";

// 待機中だけマウントされる本体。一覧の取得もここで初めて起きる
// The body mounted only while waiting; the list fetch starts here for the first time
export function EventLanguageGateBody() {
  const languages = useLanguageList();
  const [selectionState, setSelectionState] = useState<SelectionState>("idle");

  // 一覧はローダーが3秒間隔で自動再試行するため、届くまでは読み込み中を出し続ける
  // The loader retries the list every 3s, so this keeps showing the loading line until entries arrive
  if (languages.status === "loading") {
    return (
      <Text c="white" data-testid="event-language-gate-loading">
        {DictionaryIndependentText.languageListLoading}
      </Text>
    );
  }

  async function selectLanguage(languageCode: string) {
    setSelectionState("pending");
    const accepted = await dispatchAction("event_mode.select_language", { locale: languageCode });

    // 受理されたときは押下不可のまま保つ。ゲートは待機解除のtopic eventで消える
    // Keep the buttons disabled once accepted; the gate disappears on the waiting-released topic event
    if (!accepted) setSelectionState("failed");
  }

  return (
    <Stack align="center" gap="md">
      <Group justify="center" gap="lg">
        {languages.entries.map((language) => (
          <Button
            key={language.code}
            size="xl"
            disabled={selectionState === "pending"}
            data-testid={`event-language-gate-option-${language.code}`}
            onClick={() => void selectLanguage(language.code)}
          >
            {language.displayName}
          </Button>
        ))}
      </Group>
      {/* トーストはゲートの下に隠れるため、押下が通らなかったことはこの1行だけが伝える */}
      {/* Toasts hide beneath the gate, so this single line is the only report that a press did not go through */}
      {selectionState === "failed" && (
        <Text c="white" data-testid="event-language-gate-select-failed">
          {DictionaryIndependentText.languageSelectFailed}
        </Text>
      )}
    </Stack>
  );
}
