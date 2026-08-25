import { Paper, Stack, Text, Title } from "@mantine/core";
import { Topics, useTopic } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

export function TrainRidingHud() {
  const riding = useTopic(Topics.trainRiding);
  const { t } = useI18n();
  if (!riding?.riding) return null;
  const title = t(L.ui.trainHud.title);
  const branchSelection = t(L.ui.trainHud.branchSelection, {
    current: riding.selectedBranchIndex + 1,
    count: riding.branchCandidateCount,
  });
  const showBranchSelection = riding.branchCandidateCount > 1;

  return (
    <Paper className={styles.hud} data-testid="train-riding-hud" {...tutorialAnchor(TutorialAnchorIds.trainHudStatus)}>
      <Stack gap={4}>
        <Title order={2} size="h4">{title}</Title>
        {showBranchSelection && (
          <Text data-testid="train-branch-selection">
            {branchSelection}
          </Text>
        )}
      </Stack>
    </Paper>
  );
}
