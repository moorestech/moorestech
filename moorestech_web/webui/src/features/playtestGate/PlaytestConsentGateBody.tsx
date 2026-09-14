// 待機中だけマウントされる本体。本文を読ませてから了解1回で待機が解ける
// The body mounted only while waiting; the body text is read, and a single acknowledgement releases the wait
import { Button, Stack, Text } from "@mantine/core";
import { DictionaryIndependentText, L, useI18n } from "@/shared/i18n";
import { useGateAnswer } from "./useGateAnswer";
import styles from "./style.module.css";

export function PlaytestConsentGateBody() {
  const { t } = useI18n();
  const { disabled, message, answer } = useGateAnswer("playtest.consent.acknowledge");

  // ゲートは辞書配信より前に出るため、第3引数の辞書非依存文言が未確定の間の表示になる
  // The gate precedes dictionary delivery, so the third argument's dictionary-independent copy is what shows until it arrives
  const body = t(L.ui.playtest.consent.body, {}, DictionaryIndependentText.playtestConsentBody);
  const agreeLabel = t(L.ui.playtest.consent.agree, {}, DictionaryIndependentText.playtestConsentAgree);

  return (
    <Stack align="center" gap="md">
      <Text c="white" ta="center" className={styles.body} data-testid="playtest-consent-body">{body}</Text>
      <Button size="xl" disabled={disabled} onClick={() => void answer({})} data-testid="playtest-consent-agree">
        {agreeLabel}
      </Button>
      {/* トーストはゲートの下に隠れるため、応答がどうなったかはこの1行だけが伝える */}
      {/* Toasts hide beneath the gate, so this single line is the only report of what became of the answer */}
      {message !== null && (
        <Text c="white" data-testid="playtest-consent-gate-status">
          {message}
        </Text>
      )}
    </Stack>
  );
}
