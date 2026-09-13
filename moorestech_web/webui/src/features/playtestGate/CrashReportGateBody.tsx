// 待機中だけマウントされる本体。説明文は任意で、どちらのボタンでも待機が解ける
// The body mounted only while waiting; the description is optional and either button releases the wait
import { useState } from "react";
import { Button, Group, Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

export function CrashReportGateBody() {
  const { t } = useI18n();
  const [description, setDescription] = useState("");
  const [responded, setResponded] = useState(false);

  // 二度押しはC#側が already_responded で弾くが、そこまで届かせない。押した時点で両方を閉じる
  // C# rejects a second press with already_responded, but it never gets that far: one press closes both buttons
  async function respond(send: boolean) {
    if (responded) return;
    setResponded(true);
    await dispatchAction("playtest.crash_report.respond", { send, description: send ? description.trim() : "" });
  }

  return (
    <Stack align="center" gap="md">
      <Text c="white">{t(L.ui.playtest.crashGate.body)}</Text>
      <textarea
        className={styles.description}
        value={description}
        placeholder={t(L.ui.playtest.crashGate.placeholder)}
        onChange={(e) => setDescription(e.currentTarget.value)}
        data-testid="crash-report-description"
      />
      <Group justify="center" gap="lg">
        <Button size="xl" disabled={responded} onClick={() => void respond(true)} data-testid="crash-report-send">
          {t(L.ui.playtest.crashGate.send)}
        </Button>
        <Button size="xl" disabled={responded} onClick={() => void respond(false)} data-testid="crash-report-skip">
          {t(L.ui.playtest.crashGate.skip)}
        </Button>
      </Group>
    </Stack>
  );
}
