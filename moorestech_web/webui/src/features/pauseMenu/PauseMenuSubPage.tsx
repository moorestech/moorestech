import { ScrollArea, Stack, Title } from "@mantine/core";
import type { ReactNode } from "react";
import { dispatchAction, PauseMenuPageNames } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { PanelActionButton } from "@/shared/ui";
import styles from "./style.module.css";

// 見出し・戻るは固定、本文のみスクロール
// Keep the heading and Back fixed; scroll only the body
export function PauseMenuSubPage({ title, children }: { title: string; children: ReactNode }) {
  const { t } = useI18n();
  const back = () => void dispatchAction("pause_menu.show_page", { page: PauseMenuPageNames.top });

  return (
    <Stack gap="md" className={styles.subPage}>
      <Title order={1}>{title}</Title>
      <ScrollArea className={styles.scroll} type="auto" data-testid="pause-menu-body">
        <Stack gap="md">{children}</Stack>
      </ScrollArea>
      <PanelActionButton onClick={back} testId="pause-menu-back" {...tutorialAnchor(TutorialAnchorIds.pauseBackToTop)}>
        {t(L.ui.pauseMenu.back)}
      </PanelActionButton>
    </Stack>
  );
}
