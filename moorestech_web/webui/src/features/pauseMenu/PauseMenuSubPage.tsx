import { ScrollArea, Stack, Title } from "@mantine/core";
import type { ReactNode } from "react";
import { dispatchAction, PauseMenuPageNames } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { PanelActionButton } from "@/shared/ui";
import styles from "./style.module.css";

// 子画面の枠。見出しと戻るボタンは固定し、本文だけがスクロールする
// Frame for a sub-page: the heading and back button stay fixed while only the body scrolls
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
