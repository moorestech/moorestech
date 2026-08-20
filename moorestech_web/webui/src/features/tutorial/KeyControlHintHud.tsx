import { Topics, useTopic } from "@/bridge";
import { challengeTutorialTextKey, useI18n } from "@/shared/i18n";
import { useBlockingSkitActive } from "@/shared/uiState";
import styles from "./keyControlHint.module.css";

// keyControl要素のうち現在のUI状態に一致するものだけを下中央HUDへ縦積みする
// Stack only the keyControl elements matching the current UI state in the bottom-center HUD
export function KeyControlHintHud() {
  const presentation = useTopic(Topics.tutorialPresentation);
  const uiState = useTopic(Topics.uiState);
  const blockingSkitActive = useBlockingSkitActive();
  const { t } = useI18n();
  if (blockingSkitActive || !presentation || !uiState) return null;

  const hints = presentation.sessions.flatMap((session) => session.elements.flatMap((element) =>
    element.kind === "keyControl" && element.uiState === uiState.state
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
