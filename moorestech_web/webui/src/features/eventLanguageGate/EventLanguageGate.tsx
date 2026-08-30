import { Overlay, Portal, Stack, Title } from "@mantine/core";
import { Topics, useTopicSelector } from "@/bridge";
import { EventLanguageGateBody } from "./EventLanguageGateBody";

// 選ばせる対象が辞書そのものなのでt()を通さない。英語固定はADR 0040の裁定
// The dictionary itself is what gets chosen, so this bypasses t(); English-only is the ADR 0040 ruling
const HeadingText = "Select Language";

// 待機中は全画面で操作を塞ぐ外殻。本体は待機中だけマウントし通常起動では一覧を取りに行かない
// The shell that blocks input full-screen while waiting; the body mounts only while waiting, so a normal boot fetches nothing
export function EventLanguageGate() {
  const waiting = useTopicSelector(Topics.eventLanguageGate, (data) => data?.waiting ?? false);

  if (!waiting) return null;

  return (
    <Portal>
      <Overlay
        fixed
        center
        backgroundOpacity={1}
        color="var(--event-language-gate-face)"
        zIndex="var(--z-portal-event-language-gate)"
        data-testid="event-language-gate"
      >
        <Stack align="center" gap="xl">
          <Title order={1} c="white">{HeadingText}</Title>
          <EventLanguageGateBody />
        </Stack>
      </Overlay>
    </Portal>
  );
}
