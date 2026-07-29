import { Topics, useTopic } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { FadeRule, GamePanel } from "@/shared/ui";
import styles from "./style.module.css";

export function PlacementModeHud() {
  const data = useTopic(Topics.placementMode);
  const { t } = useI18n();
  if (!data) return null;

  const headingId = "placement-mode-hud-heading";
  const title = t("Placement Mode");
  const selected = t("Selected: {name}", { name: data.selectedName });
  const height = t("Height: {height}", { height: data.height });

  // 配置情報をクラフト枠で表示する
  // Show placement information in the craft frame
  return (
    <section
      className={styles.placementHud}
      aria-labelledby={headingId}
      data-testid="placement-mode-hud"
      {...tutorialAnchor(TutorialAnchorIds.placementHud)}
    >
      <GamePanel variant="craft">
        <h2 id={headingId} className={styles.label} data-testid="operation-mode-label">{title}</h2>
        <FadeRule />
        <div className={styles.details}>
          <p className={styles.detail} data-testid="operation-mode-detail">{selected}</p>
          <p className={styles.detail} data-testid="operation-mode-detail">{height}</p>
          {data.unavailableReason.length > 0 && (
            <p className={styles.warning} data-testid="operation-mode-warning">{data.unavailableReason}</p>
          )}
        </div>
      </GamePanel>
    </section>
  );
}
