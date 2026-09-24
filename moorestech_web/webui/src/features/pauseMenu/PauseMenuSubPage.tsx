import { Stack, Title } from "@mantine/core";
import type { ReactNode } from "react";
import { dispatchAction, PauseMenuPageNames } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { PanelActionButton } from "@/shared/ui";

// 子画面の枠。見出しと、トップへ戻るボタンを持つ
// Frame for a sub-page: a heading and a button back to the top
export function PauseMenuSubPage({ title, children }: { title: string; children: ReactNode }) {
  const { t } = useI18n();
  const back = () => void dispatchAction("pause_menu.show_page", { page: PauseMenuPageNames.top });

  return (
    <Stack gap="md">
      <Title order={1}>{title}</Title>
      {children}
      <PanelActionButton onClick={back} testId="pause-menu-back" {...tutorialAnchor(TutorialAnchorIds.pauseBackToTop)}>
        {t(L.ui.pauseMenu.back)}
      </PanelActionButton>
    </Stack>
  );
}
