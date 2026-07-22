import { Paper, Stack, Text, Title } from "@mantine/core";
import { Topics, useTopic } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

export function PlacementModeHud() {
  const data = useTopic(Topics.placementMode);
  const { t } = useI18n();
  if (!data) return null;

  const title = t("Placement Mode");
  const selected = t("Selected: {name}", { name: data.selectedName });
  const height = t("Height: {height}", { height: data.height });
  const energized = t("Energized Range");
  const unavailableColor = "red";

  return (
    <Paper className={styles.modePanel} {...tutorialAnchor(TutorialAnchorIds.placementHud)}>
      <Stack gap="xs">
        <Title order={2} size="h4">{title}</Title>
        <Text>{selected}</Text>
        <Text>{height}</Text>
        {data.energizedRangeVisible && <Text>{energized}</Text>}
        {data.unavailableReason.length > 0 && <Text c={unavailableColor}>{data.unavailableReason}</Text>}
      </Stack>
    </Paper>
  );
}
