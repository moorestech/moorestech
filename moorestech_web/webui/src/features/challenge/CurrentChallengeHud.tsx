import { Topics, useTopic, useTopicSelector } from "@/bridge";
import { tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import { useI18n } from "@/shared/i18n";
import { FadeRule } from "@/shared/ui";
import styles from "./CurrentChallengeHud.module.css";

export default function CurrentChallengeHud() {
  const current = useTopic(Topics.challengeCurrent);
  // HUDはPortal層で会話窓より上に来るため、blockingスキット中は演出を専有させて引っ込む
  // The HUD paints above the dialogue window from the portal layer, so it withdraws and lets a blocking skit own the screen
  const skitMode = useTopicSelector(Topics.skitPresentation, (value) => value?.presentationState.mode ?? "none");
  const { t } = useI18n();
  if (skitMode === "blocking") return null;
  if (!current || current.challenges.length === 0) return null;

  // 見出し・罫線・目標だけで世界上の情報階層を作り、カード面は持たせない
  // Build the world-overlay hierarchy from a label, rule, and objectives without a card face
  const label = t("現在のチャレンジ");
  return (
    <section
      className={styles.hud}
      aria-label={label}
      data-testid="challenge-hud"
      {...tutorialAnchor(TutorialAnchorIds.challengeCurrentHud)}
    >
      <div className={styles.label}>{label}</div>
      <FadeRule />
      <div className={styles.objectives}>
        {current.challenges.map((challenge) => (
          <div key={challenge.guid} className={styles.objective} data-testid="challenge-objective">
            {challenge.title}
          </div>
        ))}
      </div>
    </section>
  );
}
