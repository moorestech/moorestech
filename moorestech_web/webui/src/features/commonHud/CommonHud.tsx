import { Topics, useTopic } from "@/bridge";
import { tutorialAnchor } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

export function Crosshair() {
  const data = useTopic(Topics.crosshair);
  if (!data?.visible) return null;
  return <div className={styles.crosshair} {...tutorialAnchor("game.crosshair")} />;
}
