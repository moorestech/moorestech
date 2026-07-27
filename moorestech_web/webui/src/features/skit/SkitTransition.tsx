import { Topics, useTopicSelector } from "@/bridge";
import styles from "./style.module.css";

// 暗転はレターボックス帯まで覆うためstage外(Portal)へ置き、会話窓より上に描く
// The blackout also covers the letterbox bars, so it renders outside the stage (portal) and above the window
export function SkitTransition() {
  const visible = useTopicSelector(Topics.skitPresentation, (data) =>
    Boolean(data && data.presentationState.mode !== "none" && data.presentationState.transitionVisible));
  if (!visible) return null;
  return <div className={styles.transition} data-testid="skit-transition" />;
}
