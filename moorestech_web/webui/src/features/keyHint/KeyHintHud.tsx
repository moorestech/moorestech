import { Topics, useTopic } from "@/bridge";
import { LocalizedShortcutHint, useI18n, type TranslationKey } from "@/shared/i18n";
import styles from "./keyHint.module.css";

// 現画面のヒントをC#から受け取ってそのまま積む。画面名で内容を導出しない（ADR-0032）
// Stack the hints C# sends for the current screen as-is; never derive content from the screen name (ADR-0032)
export function KeyHintHud() {
  // 配列はセレクタで返さずtopic本体から読む（毎publishで参照が変わる値をセレクタに載せない規約）
  // Read the array from the topic itself rather than a selector; selectors carry primitives by convention
  const uiState = useTopic(Topics.uiState);
  const { t } = useI18n();
  const hints = uiState?.keyHints ?? [];
  if (hints.length === 0) return null;

  return (
    <div className={`keyHintText ${styles.keyHints}`} data-testid="key-hints">
      {hints.map((hint) => (
        <div key={`${hint.keyNameKey}:${hint.textKey}`} className={styles.hint}>
          <LocalizedShortcutHint
            layout="prefix"
            shortcut={t(hint.keyNameKey as TranslationKey)}
            translationKey={hint.textKey as TranslationKey}
          />
        </div>
      ))}
    </div>
  );
}
