import { useI18n } from "@/shared/i18n";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

export function DeleteModeWarningBands() {
  const { t } = useI18n();
  const label = t("Delete Mode");

  // 警告帯だけで削除モードを示す
  // Signal deletion mode only with the warning bands
  return (
    <div
      className={styles.deleteModeWarning}
      role="status"
      aria-label={label}
      data-testid="delete-mode-warning"
    >
      <div
        className={`${styles.deleteModeWarningBand} ${styles.deleteModeWarningBandTop}`}
        data-testid="delete-mode-warning-band"
        aria-hidden="true"
      />
      <div
        className={`${styles.deleteModeWarningBand} ${styles.deleteModeWarningBandBottom}`}
        data-testid="delete-mode-warning-band"
        {...tutorialAnchor(TutorialAnchorIds.deleteHud)}
      />
    </div>
  );
}
