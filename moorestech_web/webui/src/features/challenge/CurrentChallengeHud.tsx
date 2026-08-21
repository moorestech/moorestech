import { Topics, useTopic } from "@/bridge";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { challengeTitleKey, L, useI18n } from "@/shared/i18n";
import { FadeRule, GamePanel } from "@/shared/ui";
import { useBlockingSkitActive } from "@/shared/uiState";
import styles from "./CurrentChallengeHud.module.css";

export default function CurrentChallengeHud() {
  const current = useTopic(Topics.challengeCurrent);
  // 会話中は演出を優先してHUDを隠す
  // Withdraw the HUD during blocking skits so the dialogue presentation owns the screen
  const blockingSkitActive = useBlockingSkitActive();
  const { t } = useI18n();
  if (blockingSkitActive) return null;
  if (!current || current.challenges.length === 0) return null;

  // 面はhud variantが供給
  // Face comes from the hud variant
  const label = t(L.ui.challenge.currentTitle);
  return (
    <section
      className={styles.hud}
      aria-label={label}
      data-testid="challenge-hud"
      {...tutorialAnchor(TutorialAnchorIds.challengeCurrentHud)}
    >
      <GamePanel variant="hud">
        <div className={styles.label} data-testid="challenge-hud-label">{label}</div>
        <div className={styles.rule}>
          <FadeRule />
        </div>
        <div className={styles.objectives}>
          {current.challenges.map((challenge) => (
            <div key={challenge.guid} className={styles.objective} data-testid="challenge-objective">
              {t(challengeTitleKey(challenge.guid))}
            </div>
          ))}
        </div>
      </GamePanel>
    </section>
  );
}
