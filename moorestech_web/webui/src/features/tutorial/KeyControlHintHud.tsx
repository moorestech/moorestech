import { Topics, useTopic, useTopicSelector } from "@/bridge";
import { challengeTutorialTextKey, useI18n } from "@/shared/i18n";
import { useBlockingSkitActive } from "@/shared/uiState";
import styles from "./keyControlHint.module.css";

// uiState一致keyControlのみ縦積み
// Stack only keyControl elements matching uiState
export function KeyControlHintHud() {
  const presentation = useTopic(Topics.tutorialPresentation);
  const uiStateName = useTopicSelector(Topics.uiState, (d) => d?.state ?? null);
  const blockingSkitActive = useBlockingSkitActive();
  const { t } = useI18n();
  if (blockingSkitActive || !presentation || !uiStateName) return null;

  const hints = presentation.sessions.flatMap((session) => session.elements.flatMap((element) =>
    element.kind === "keyControl" && element.uiState === uiStateName
      ? [{ key: `${session.tutorialSessionId}:${element.elementId}`, keyName: element.keyName, tutorialGuid: element.tutorialGuid }]
      : []));
  if (hints.length === 0) return null;

  return (
    <div className={styles.hud} data-testid="key-control-hint-hud">
      {hints.map((hint) => (
        <div key={hint.key} className={styles.hint} data-testid="key-control-hint">
          <kbd>{hint.keyName}</kbd>
          <span>{t(challengeTutorialTextKey(hint.tutorialGuid))}</span>
        </div>
      ))}
    </div>
  );
}
