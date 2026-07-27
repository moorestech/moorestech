import { Paper, Text } from "@mantine/core";
import { Topics, useTopic, useTopicSelector } from "@/bridge";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

export default function CurrentChallengeHud() {
  const current = useTopic(Topics.challengeCurrent);
  // HUDはPortal層で会話窓より上に来るため、blockingスキット中は演出を専有させて引っ込む
  // The HUD paints above the dialogue window from the portal layer, so it withdraws and lets a blocking skit own the screen
  const skitMode = useTopicSelector(Topics.skitPresentation, (value) => value?.presentationState.mode ?? "none");
  const { t } = useI18n();
  if (skitMode === "blocking") return null;
  if (!current || current.challenges.length === 0) return null;
  return (
    <Paper className={styles.hud} data-testid="challenge-hud" {...tutorialAnchor(TutorialAnchorIds.challengeCurrentHud)}>
      <Text fw={700}>{t("challenge.current")}</Text>
      {current.challenges.map((challenge) => <Text key={challenge.guid}>{challenge.title}</Text>)}
    </Paper>
  );
}
