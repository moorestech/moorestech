import { Topics, useTopic, useTopicSelector } from "@/bridge";
import { LocalizedShortcutHint, challengeTutorialTextKey } from "@/shared/i18n";
import { useBlockingSkitActive } from "@/shared/uiState";
import styles from "./keyControlHint.module.css";
import { tutorialElementKey } from "./tutorialElement";

// uiState一致keyControlのみ縦積み
// Stack only keyControl elements matching uiState
export function KeyControlHintHud() {
  const presentation = useTopic(Topics.tutorialPresentation);
  const uiStateName = useTopicSelector(Topics.uiState, (d) => d?.state ?? null);
  const blockingSkitActive = useBlockingSkitActive();
  if (blockingSkitActive || !presentation || !uiStateName) return null;

  const hints = presentation.sessions.flatMap((session) => session.elements.flatMap((element) =>
    element.kind === "keyControl" && element.uiState === uiStateName
      ? [{ key: tutorialElementKey(session.tutorialSessionId, element.elementId), keyName: element.keyName, tutorialGuid: element.tutorialGuid }]
      : []));
  if (hints.length === 0) return null;

  return (
    <div className={styles.hud} data-testid="key-control-hint-hud">
      {hints.map((hint) => (
        <div key={hint.key} className={styles.hint} data-testid="key-control-hint">
          <LocalizedShortcutHint shortcut={hint.keyName} translationKey={challengeTutorialTextKey(hint.tutorialGuid)} />
        </div>
      ))}
    </div>
  );
}
