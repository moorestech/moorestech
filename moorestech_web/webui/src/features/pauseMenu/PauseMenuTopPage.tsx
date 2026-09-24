import { Stack, Text, Title } from "@mantine/core";
import { dispatchAction, PauseMenuPageNames, type PauseMenuPageName } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { PanelActionButton } from "@/shared/ui";

// ポーズメニューのトップ。セーブ系2つと、子画面への入口2つを並べる（ADR 0069）
// The pause-menu top: two save actions plus two entries to the sub-pages (ADR 0069)
export function PauseMenuTopPage({ disconnected }: { disconnected: boolean }) {
  const { t } = useI18n();
  const disconnectColor = "red";
  const save = () => void dispatchAction("pause_menu.save", {});
  const quit = () => void dispatchAction("pause_menu.save_and_quit", {});
  const open = (page: PauseMenuPageName) => void dispatchAction("pause_menu.show_page", { page });

  return (
    <Stack gap="md">
      <Title order={1}>{t(L.ui.pauseMenu.title)}</Title>
      {disconnected && <Text c={disconnectColor}>{t(L.ui.pauseMenu.disconnected)}</Text>}
      <PanelActionButton onClick={save} data-testid="pause-menu-save" {...tutorialAnchor(TutorialAnchorIds.pauseSave)}>
        {t(L.ui.game.saveGame)}
      </PanelActionButton>
      <PanelActionButton onClick={quit} data-testid="pause-menu-save-and-quit" {...tutorialAnchor(TutorialAnchorIds.pauseBack)}>
        {t(L.ui.game.saveAndQuit)}
      </PanelActionButton>
      <PanelActionButton onClick={() => open(PauseMenuPageNames.settings)} data-testid="pause-menu-open-settings" {...tutorialAnchor(TutorialAnchorIds.pauseSettings)}>
        {t(L.ui.pauseMenu.settings)}
      </PanelActionButton>
      <PanelActionButton onClick={() => open(PauseMenuPageNames.bugReport)} data-testid="pause-menu-open-bug-report" {...tutorialAnchor(TutorialAnchorIds.pauseBugReport)}>
        {t(L.ui.pauseMenu.bugReport)}
      </PanelActionButton>
    </Stack>
  );
}
