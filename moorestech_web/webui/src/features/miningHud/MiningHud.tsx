import { Paper, Progress, Stack, Text } from "@mantine/core";
import { Topics, useTopic } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

export function MiningHud() {
  const data = useTopic(Topics.miningHud);
  const { t } = useI18n();
  if (!data?.visible) return null;
  const target = t("Mining Target: {name}", { name: data.targetName });

  return (
    <Paper className={styles.panel} {...tutorialAnchor(TutorialAnchorIds.miningHud)}>
      <Stack gap="xs">
        <Text>{target}</Text>
        {data.mining && <Progress value={data.progress * 100} />}
      </Stack>
    </Paper>
  );
}
