import { Topics, useTopic, useTopicSelector } from "@/bridge";
import { LocalizedShortcutHint, challengeTutorialTextKey, useI18n } from "@/shared/i18n";
import { isKnownUiStateName, useBlockingSkitActive } from "@/shared/uiState";
import styles from "./keyControlHint.module.css";
import { tutorialElementKey } from "./tutorialElement";

// uiState一致keyControlのみ縦積み
// Stack only keyControl elements matching uiState
export function KeyControlHintHud() {
  const presentation = useTopic(Topics.tutorialPresentation);
  const uiStateName = useTopicSelector(Topics.uiState, (d) => d?.state ?? null);
  const blockingSkitActive = useBlockingSkitActive();
  const { t } = useI18n();
  if (blockingSkitActive || !presentation || !uiStateName) return null;

  // 文言解決まで済ませて畳む。辞書未着の空ヒントはkbdだけの断片になるため描かない
  // Resolve the text up front and drop empty hints; before the dictionary lands they would render as a lone kbd
  const hints = presentation.sessions.flatMap((session) => session.elements.flatMap((element) => {
    if (element.kind !== "keyControl" || !matchesUiState(element.uiState, uiStateName)) return [];
    if (!t(challengeTutorialTextKey(element.tutorialGuid))) return [];
    return [{ key: tutorialElementKey(session.tutorialSessionId, element.elementId), keyName: element.keyName, tutorialGuid: element.tutorialGuid }];
  }));
  if (hints.length === 0) return null;

  return (
    <div className={styles.hud} data-testid="key-control-hint-hud">
      {hints.map((hint) => (
        <div key={hint.key} className={`keyHintText ${styles.hint}`} data-testid="key-control-hint">
          <LocalizedShortcutHint layout="prefix" shortcut={hint.keyName} translationKey={challengeTutorialTextKey(hint.tutorialGuid)} />
        </div>
      ))}
    </div>
  );
}

// 語彙外のuiStateはどの画面とも一致させない。マスタ側の綴り違いを黙って常時表示にしないため
// A uiState outside the vocabulary matches no screen, so a master-side typo never turns into an always-on hint
function matchesUiState(uiState: string, uiStateName: string): boolean {
  if (!isKnownUiStateName(uiState)) {
    warnUnknownUiState(uiState);
    return false;
  }
  return uiState === uiStateName;
}

const warnedUiStates = new Set<string>();

function warnUnknownUiState(uiState: string): void {
  if (warnedUiStates.has(uiState)) return;
  warnedUiStates.add(uiState);
  console.warn(`[tutorial] Unknown keyControl uiState: ${uiState}`);
}
