import { Paper, Stack, Text, Title } from "@mantine/core";
import { Topics, useTopic } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

export function DeleteModeHud() {
  const data = useTopic(Topics.deleteMode);
  const { t } = useI18n();
  if (!data) return null;

  const title = t("Delete Mode");
  const guide = t("Drag to select objects to delete");
  const unavailableColor = "red";

  return (
    <Paper className={styles.modePanel} {...tutorialAnchor(TutorialAnchorIds.deleteHud)}>
      <Stack gap="xs">
        <Title order={2} size="h4">{title}</Title>
        <Text>{guide}</Text>
        {data.unavailableReason.length > 0 && <Text c={unavailableColor}>{data.unavailableReason}</Text>}
      </Stack>
    </Paper>
  );
}
