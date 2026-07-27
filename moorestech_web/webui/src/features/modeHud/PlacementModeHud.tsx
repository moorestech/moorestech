import { Topics, useTopic } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { FadeRule } from "@/shared/ui";
import styles from "./style.module.css";

export function PlacementModeHud() {
  const data = useTopic(Topics.placementMode);
  const { t } = useI18n();
  if (!data) return null;

  const headingId = "placement-mode-hud-heading";
  const title = t("Placement Mode");
  const selected = t("Selected: {name}", { name: data.selectedName });
  const height = t("Height: {height}", { height: data.height });

  // 配置情報を面なしで表示する
  // Show placement information without a face
  return (
    <section
      className={styles.modeHud}
      aria-labelledby={headingId}
      data-testid="placement-mode-hud"
      {...tutorialAnchor(TutorialAnchorIds.placementHud)}
    >
      <h2 id={headingId} className={styles.label} data-testid="operation-mode-label">{title}</h2>
      <FadeRule />
      <div className={styles.details}>
        <p className={styles.detail} data-testid="operation-mode-detail">{selected}</p>
        <p className={styles.detail} data-testid="operation-mode-detail">{height}</p>
        {data.unavailableReason.length > 0 && (
          <p className={styles.warning} data-testid="operation-mode-warning">{data.unavailableReason}</p>
        )}
      </div>
    </section>
  );
}
