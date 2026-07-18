/* eslint-disable local/no-jsx-visible-literal -- Branch counts are runtime train state; all player-facing copy is translated before JSX. */
import { Paper, Stack, Text, Title } from "@mantine/core";
import { Topics, useTopic } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import { tutorialAnchor } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

export function TrainRidingHud() {
  const riding = useTopic(Topics.trainRiding);
  const { t } = useI18n();
  if (!riding?.riding) return null;
  const title = t("Riding Train");
  const controls = t("W/S: Drive  A/D: Select branch  E: Dismount  Esc: Menu");
  const branchSelection = `${t("Selected branch")} ${riding.selectedBranchIndex + 1}/${riding.branchCandidateCount}`;
  const showBranchSelection = riding.branchCandidateCount > 1;

  return (
    <Paper className={styles.hud} data-testid="train-riding-hud" {...tutorialAnchor("train-hud.status")}>
      <Stack gap={4}>
        <Title order={2} size="h4">{title}</Title>
        <Text>{controls}</Text>
        {showBranchSelection && (
          <Text data-testid="train-branch-selection">
            {branchSelection}
          </Text>
        )}
      </Stack>
    </Paper>
  );
}
