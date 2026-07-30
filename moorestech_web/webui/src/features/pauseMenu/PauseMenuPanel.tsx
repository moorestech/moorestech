import { Button, Stack, Text, Title } from "@mantine/core";
import { dispatchAction, Topics, useTopic } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

export function PauseMenuPanel() {
  const data = useTopic(Topics.pauseMenu);
  const { t } = useI18n();
  const title = t(L.ui.pauseMenu.title);
  const disconnected = t(L.ui.pauseMenu.disconnected);
  const saveLabel = t(L.ui.game.saveGame);
  const backLabel = t(L.ui.game.saveAndBackToMainMenu);
  const disconnectColor = "red";
  const save = () => void dispatchAction("pause_menu.save", {});
  const back = () => void dispatchAction("pause_menu.back_to_main_menu", {});

  return (
    <section className={styles.panel} data-testid="pause-menu" {...tutorialAnchor(TutorialAnchorIds.pauseMenu)}>
      <Stack gap="md">
        <Title order={1}>{title}</Title>
        {data?.disconnected && <Text c={disconnectColor}>{disconnected}</Text>}
        <Button {...tutorialAnchor(TutorialAnchorIds.pauseSave)} onClick={save}>
          {saveLabel}
        </Button>
        <Button {...tutorialAnchor(TutorialAnchorIds.pauseBack)} onClick={back}>
          {backLabel}
        </Button>
      </Stack>
    </section>
  );
}
