// 待機中は全画面で操作を塞ぐ外殻。本体は待機中だけマウントする（CrashReportGate/EventLanguageGateと同型）
// The shell that blocks input full-screen while waiting; the body mounts only while waiting (same shape as CrashReportGate/EventLanguageGate)
import { Overlay, Portal, Stack, Title } from "@mantine/core";
import { Topics, useTopicSelector } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { PlaytestConsentGateBody } from "./PlaytestConsentGateBody";

export function PlaytestConsentGate() {
  const { t } = useI18n();
  const waiting = useTopicSelector(Topics.consentGate, (data) => data?.waiting ?? false);

  if (!waiting) return null;

  return (
    <Portal>
      <Overlay
        fixed
        center
        backgroundOpacity={1}
        color="var(--playtest-gate-face)"
        zIndex="var(--z-portal-playtest-gate)"
        data-testid="playtest-consent-gate"
      >
        <Stack align="center" gap="xl">
          <Title order={1} c="white">{t(L.ui.playtest.consent.title)}</Title>
          <PlaytestConsentGateBody />
        </Stack>
      </Overlay>
    </Portal>
  );
}
