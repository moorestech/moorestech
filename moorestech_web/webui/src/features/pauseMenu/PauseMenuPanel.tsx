import { Button, Stack, Text, Title } from "@mantine/core";
import { dispatchAction, Topics, useTopic } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { LanguageSelect } from "@/features/settings";
import styles from "./style.module.css";

export function PauseMenuPanel() {
  const data = useTopic(Topics.pauseMenu);
  const { locale, t } = useI18n();
  const title = t(L.ui.pauseMenu.title);
  const disconnected = t(L.ui.pauseMenu.disconnected);
  const saveLabel = t(L.ui.game.saveGame);
  const quitLabel = t(L.ui.game.saveAndQuit);
  const disconnectColor = "red";
  const save = () => void dispatchAction("pause_menu.save", {});
  const quit = () => void dispatchAction("pause_menu.save_and_quit", {});

  return (
    <section className={styles.panel} data-testid="pause-menu" {...tutorialAnchor(TutorialAnchorIds.pauseMenu)}>
      <Stack gap="md" data-testid={`pause-menu-locale-${locale}`}>
        <Title order={1}>{title}</Title>
        {data?.disconnected && <Text c={disconnectColor}>{disconnected}</Text>}
        <Button {...tutorialAnchor(TutorialAnchorIds.pauseSave)} onClick={save}>
          {saveLabel}
        </Button>
        <Button {...tutorialAnchor(TutorialAnchorIds.pauseBack)} onClick={quit}>
          {quitLabel}
        </Button>
        <LanguageSelect />
      </Stack>
    </section>
  );
}
