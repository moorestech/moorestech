// 待機中だけマウントされる本体。説明文は任意で、どちらのボタンでも待機が解ける
// The body mounted only while waiting; the description is optional and either button releases the wait
import { useState } from "react";
import { Button, Group, Stack, Text } from "@mantine/core";
import { DictionaryIndependentText, L, useI18n } from "@/shared/i18n";
import { useGateAnswer } from "./useGateAnswer";
import styles from "./style.module.css";

export function CrashReportGateBody() {
  const { t } = useI18n();
  const [description, setDescription] = useState("");
  const { disabled, message, answer } = useGateAnswer("playtest.crash_report.respond");

  // ゲートは辞書配信より前に出るため、第3引数の辞書非依存文言が未確定の間の表示になる
  // The gate precedes dictionary delivery, so the third argument's dictionary-independent copy is what shows until it arrives
  const body = t(L.ui.playtest.crashGate.body, {}, DictionaryIndependentText.crashGateBody);
  const placeholder = t(L.ui.playtest.crashGate.placeholder, {}, DictionaryIndependentText.crashGatePlaceholder);
  const sendLabel = t(L.ui.playtest.crashGate.send, {}, DictionaryIndependentText.crashGateSend);
  const skipLabel = t(L.ui.playtest.crashGate.skip, {}, DictionaryIndependentText.crashGateSkip);

  return (
    <Stack align="center" gap="md">
      <Text c="white" ta="center" className={styles.body}>{body}</Text>
      <textarea
        className={styles.description}
        value={description}
        placeholder={placeholder}
        onChange={(e) => setDescription(e.currentTarget.value)}
        data-testid="crash-report-description"
      />
      <Group justify="center" gap="lg">
        <Button size="xl" disabled={disabled} onClick={() => void respond(true)} data-testid="crash-report-send">
          {sendLabel}
        </Button>
        <Button size="xl" disabled={disabled} onClick={() => void respond(false)} data-testid="crash-report-skip">
          {skipLabel}
        </Button>
      </Group>
      {/* トーストはゲートの下に隠れるため、応答がどうなったかはこの1行だけが伝える */}
      {/* Toasts hide beneath the gate, so this single line is the only report of what became of the answer */}
      {message !== null && (
        <Text c="white" data-testid="crash-report-gate-status">
          {message}
        </Text>
      )}
    </Stack>
  );

  // 送らないを選んだときは書きかけの説明文を送らない。送る意思が無い記述を箱へ入れない
  // Choosing not to send withholds the half-written description: text the tester never meant to send stays out of the box
  async function respond(send: boolean): Promise<void> {
    await answer({ send, description: send ? description.trim() : "" });
  }
}
