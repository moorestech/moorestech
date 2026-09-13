// 待機中だけマウントされる本体。説明文は任意で、どちらのボタンでも待機が解ける
// The body mounted only while waiting; the description is optional and either button releases the wait
import { useState } from "react";
import { Button, Group, Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import { DictionaryIndependentText, L, useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

// 応答待ちと失敗をテスターに見せるための状態。成功は待機解除でゲートごと消える
// The state that shows a pending response and a failure to the tester; success removes the gate itself
type RespondState = "idle" | "pending" | "failed";

export function CrashReportGateBody() {
  const { status, t } = useI18n();
  const [description, setDescription] = useState("");
  const [respondState, setRespondState] = useState<RespondState>("idle");

  // ゲートは辞書配信より前に出るため、未確定の間は t() の空文字ではなく辞書非依存の文言を描く
  // The gate precedes dictionary delivery, so until it is ready the copy comes from dictionary-independent literals
  const dictionaryReady = status === "ready";
  const body = dictionaryReady ? t(L.ui.playtest.crashGate.body) : DictionaryIndependentText.crashGateBody;
  const placeholder = dictionaryReady ? t(L.ui.playtest.crashGate.placeholder) : DictionaryIndependentText.crashGatePlaceholder;
  const sendLabel = dictionaryReady ? t(L.ui.playtest.crashGate.send) : DictionaryIndependentText.crashGateSend;
  const skipLabel = dictionaryReady ? t(L.ui.playtest.crashGate.skip) : DictionaryIndependentText.crashGateSkip;
  const respondFailedLabel = dictionaryReady ? t(L.ui.playtest.crashGate.respondFailed) : DictionaryIndependentText.crashGateRespondFailed;

  // 二度押しはC#側が already_responded で弾くが、そこまで届かせない。押した時点で両方を閉じる
  // C# rejects a second press with already_responded, but it never gets that far: one press closes both buttons
  async function respond(send: boolean) {
    setRespondState("pending");
    const accepted = await dispatchAction("playtest.crash_report.respond", { send, description: send ? description.trim() : "" });

    // 受理されたときは押下不可のまま保つ。ゲートは待機解除のtopic eventで消える
    // Keep the buttons disabled once accepted; the gate disappears on the waiting-released topic event
    if (accepted) return;

    // 押下が通らなければゲートは開いたままなので、押下可へ戻し理由を開発者ログにも残す
    // A rejected press leaves the gate open, so the buttons come back and the reason also reaches the developer log
    console.warn(`[playtest.crash_report.respond] rejected: send=${send}`);
    setRespondState("failed");
  }

  return (
    <Stack align="center" gap="md">
      <Text c="white">{body}</Text>
      <textarea
        className={styles.description}
        value={description}
        placeholder={placeholder}
        onChange={(e) => setDescription(e.currentTarget.value)}
        data-testid="crash-report-description"
      />
      <Group justify="center" gap="lg">
        <Button size="xl" disabled={respondState === "pending"} onClick={() => void respond(true)} data-testid="crash-report-send">
          {sendLabel}
        </Button>
        <Button size="xl" disabled={respondState === "pending"} onClick={() => void respond(false)} data-testid="crash-report-skip">
          {skipLabel}
        </Button>
      </Group>
      {/* トーストはゲートの下に隠れるため、押下が通らなかったことはこの1行だけが伝える */}
      {/* Toasts hide beneath the gate, so this single line is the only report that a press did not go through */}
      {respondState === "failed" && (
        <Text c="white" data-testid="crash-report-respond-failed">
          {respondFailedLabel}
        </Text>
      )}
    </Stack>
  );
}
