// 開始を止める全画面ゲートの外殻。不透明面・Portal・z層と「見せないなら何も描かない」だけを持つ
// The shell of a full-screen start gate; it owns only the opaque face, portal, z layer and the render-nothing-when-hidden rule
// どのゲートを見せるか（待機の購読と優先順位）はapp層が決めて visible で渡す
// Which gate shows (waiting subscriptions and precedence) is decided in the app layer and passed as visible
import type { ReactElement, ReactNode } from "react";
import { Overlay, Portal, Stack, Title } from "@mantine/core";
import styles from "./style.module.css";

type Props = {
  visible: boolean;
  testId: string;
  title: ReactNode;
  children: ReactNode;
};

export default function FullScreenGate({ visible, testId, title, children }: Props): ReactElement | null {
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
        data-testid={testId}
      >
        <Stack align="center" gap="xl">
          <Title order={1} c="white" ta="center" className={styles.title} data-testid={`${testId}-title`}>{title}</Title>
          {children}
        </Stack>
      </Overlay>
    </Portal>
  );
}
