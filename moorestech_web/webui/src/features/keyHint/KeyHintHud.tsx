import { Topics, useTopic } from "@/bridge";
import { translateExternalKey, useI18n } from "@/shared/i18n";
import { useBlockingSkitActive } from "@/shared/uiState";
import styles from "./keyHint.module.css";

// 現画面のヒントをC#から受け取ってそのまま積む。画面名で内容を導出しない（ADR-0032）
// Stack the hints C# sends for the current screen as-is; never derive content from the screen name (ADR-0032)
export function KeyHintHud() {
  // 配列はセレクタで返さずtopic本体から読む（毎publishで参照が変わる値をセレクタに載せない規約）
  // Read the array from the topic itself rather than a selector; selectors carry primitives by convention
  const uiState = useTopic(Topics.uiState);
  const { t } = useI18n();
  const blockingSkitActive = useBlockingSkitActive();
  const hints = uiState?.keyHints ?? [];

  // 兄弟HUDと同じくblockingスキット中は退避する。topic未着(undefined)は別事由なので??で吸収したまま
  // Retreat during a blocking skit like the sibling HUDs; an undelivered topic is a separate cause and stays absorbed by ??
  if (blockingSkitActive || hints.length === 0) return null;

  // キー名も文言もホスト由来の外部キーなので、辞書に無ければ声高なplaceholderへ落とす
  // Both the key name and the text are host-supplied external keys, so an unknown one falls back to a loud placeholder
  return (
    <div className={`keyHintText ${styles.keyHints}`} data-testid="key-hints">
      {hints.map((hint) => (
        <div key={`${hint.keyNameKey}:${hint.textKey}`} className={styles.hint}>
          <kbd>{translateExternalKey(hint.keyNameKey, t, {})}</kbd>
          {translateExternalKey(hint.textKey, t, {})}
        </div>
      ))}
    </div>
  );
}
