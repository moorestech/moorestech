import type { ReactElement } from "react";
import { Overlay, Portal, Stack, Title } from "@mantine/core";
import { EventLanguageGateBody } from "./EventLanguageGateBody";
import styles from "./style.module.css";

// 選ばせる対象が辞書そのものなのでt()を通さない。英語固定はADR 0040の裁定
// The dictionary itself is what gets chosen, so this bypasses t(); English-only is the ADR 0040 ruling
const HeadingText = "Select Language";

// 見せるかはapp層が決める。本体は見せる間だけマウントする
// The app layer decides visibility; the body mounts only while shown
export function EventLanguageGate({ visible }: { visible: boolean }): ReactElement | null {
  if (!visible) return null;

  return (
    <Portal>
      <Overlay
        fixed
        center
        backgroundOpacity={1}
        color="var(--full-screen-gate-face)"
        zIndex="var(--z-portal-full-screen-gate)"
        className={styles.overlay}
        data-testid="event-language-gate"
      >
        <Stack align="center" gap="xl">
          <Title order={1} c="white" ta="center" className={styles.title} data-testid="event-language-gate-title">
            {HeadingText}
          </Title>
          <EventLanguageGateBody />
        </Stack>
      </Overlay>
    </Portal>
  );
}
