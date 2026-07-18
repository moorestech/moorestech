/* eslint-disable local/no-jsx-visible-literal -- Server-localized challenge titles are dynamic data, not hard-coded copy. */
import { Paper, Text } from "@mantine/core";
import { Topics, useTopic } from "@/bridge";
import { tutorialAnchor } from "@/shared/tutorialAnchor";
import { useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

export default function CurrentChallengeHud() {
  const current = useTopic(Topics.challengeCurrent);
  const { t } = useI18n();
  if (!current || current.challenges.length === 0) return null;
  return (
    <Paper className={styles.hud} data-testid="challenge-hud" {...tutorialAnchor("challenge.current-hud")}>
      <Text fw={700}>{t("challenge.current")}</Text>
      {current.challenges.map((challenge) => <Text key={challenge.guid}>{challenge.title}</Text>)}
    </Paper>
  );
}
