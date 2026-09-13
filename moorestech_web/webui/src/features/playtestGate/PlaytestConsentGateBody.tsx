// 待機中だけマウントされる本体。本文を読ませてから了解1回で待機が解ける
// The body mounted only while waiting; the body text is read, and a single acknowledgement releases the wait
import { useState } from "react";
import { Button, Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";

// 応答待ちと失敗をテスターに見せるための状態。成功は待機解除でゲートごと消える
// The state that shows a pending response and a failure to the tester; success removes the gate itself
type AcknowledgeState = "idle" | "pending" | "failed";

export function PlaytestConsentGateBody() {
  const { t } = useI18n();
  const [acknowledgeState, setAcknowledgeState] = useState<AcknowledgeState>("idle");

  // 二度押しはC#側が already_acknowledged で弾くが、そこまで届かせない。押した時点でボタンを閉じる
  // C# rejects a second press with already_acknowledged, but it never gets that far: one press closes the button
  async function acknowledge() {
    setAcknowledgeState("pending");
    const accepted = await dispatchAction("playtest.consent.acknowledge", {});

    // 受理されたときは押下不可のまま保つ。ゲートは待機解除のtopic eventで消える
    // Keep the button disabled once accepted; the gate disappears on the waiting-released topic event
    if (accepted) return;

    // 押下が通らなければゲートは開いたままなので、押下可へ戻し理由を開発者ログにも残す
    // A rejected press leaves the gate open, so the button comes back and the reason also reaches the developer log
    console.warn("[playtest.consent.acknowledge] rejected");
    setAcknowledgeState("failed");
  }

  return (
    <Stack align="center" gap="md">
      <Text c="white" data-testid="playtest-consent-body">{t(L.ui.playtest.consent.body)}</Text>
      <Button size="xl" disabled={acknowledgeState === "pending"} onClick={() => void acknowledge()} data-testid="playtest-consent-agree">
        {t(L.ui.playtest.consent.agree)}
      </Button>
      {/* トーストはゲートの下に隠れるため、押下が通らなかったことはこの1行だけが伝える */}
      {/* Toasts hide beneath the gate, so this single line is the only report that a press did not go through */}
      {acknowledgeState === "failed" && (
        <Text c="white" data-testid="playtest-consent-failed">
          {t(L.ui.playtest.consent.failed)}
        </Text>
      )}
    </Stack>
  );
}
