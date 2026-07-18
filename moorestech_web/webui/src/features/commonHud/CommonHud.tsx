import { Paper, Text } from "@mantine/core";
import { Topics, useTopic } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import { tutorialAnchor } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

export function Crosshair() {
  const data = useTopic(Topics.crosshair);
  if (!data?.visible) return null;
  return <div className={styles.crosshair} {...tutorialAnchor("game.crosshair")} />;
}

export function KeyHintBar() {
  const data = useTopic(Topics.keyHints);
  const { t } = useI18n();
  if (!data || data.textKey.length === 0) return null;
  const text = t(data.textKey);
  return (
    <Paper className={styles.keyHints} {...tutorialAnchor("game.key-hints")}>
      <Text className={styles.keyHintText}>{text}</Text>
    </Paper>
  );
}
