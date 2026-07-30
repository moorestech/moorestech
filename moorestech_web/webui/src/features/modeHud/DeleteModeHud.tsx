import { Topics, useTopic } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { FadeRule, GamePanel } from "@/shared/ui";
import styles from "./style.module.css";

export function DeleteModeHud() {
  const data = useTopic(Topics.deleteMode);
  const { t } = useI18n();
  if (!data) return null;

  const headingId = "delete-mode-hud-heading";
  const title = t(L.ui.modeHud.deleteModeTitle);
  const guide = t(L.ui.modeHud.deleteModeGuide);

  // 削除案内をクラフト枠で表示する
  // Show deletion guidance in the craft frame
  return (
    <section
      className={styles.modeHud}
      aria-labelledby={headingId}
      data-testid="delete-mode-hud"
      {...tutorialAnchor(TutorialAnchorIds.deleteHud)}
    >
      <GamePanel variant="craft">
        <h2 id={headingId} className={styles.label} data-testid="operation-mode-label">{title}</h2>
        <FadeRule />
        <div className={styles.details}>
          <p className={styles.detail} data-testid="operation-mode-detail">{guide}</p>
          {data.unavailableReason.length > 0 && (
            <p className={styles.warning} data-testid="operation-mode-warning">{data.unavailableReason}</p>
          )}
        </div>
      </GamePanel>
    </section>
  );
}
