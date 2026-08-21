import { L, LocalizedShortcutHint } from "@/shared/i18n";
import styles from "./ResearchScreenChrome.module.css";

// 研究画面のキー操作ヒント（InventoryScreenChromeのkeyHints様式）
// Key hints for the research screen, following the InventoryScreenChrome style
export default function ResearchScreenChrome() {
  return (
    <div className={`keyHintText ${styles.keyHints}`} data-testid="research-key-hints">
      <div>
        <LocalizedShortcutHint layout="inline" shortcut="Tab" translationKey={L.ui.research.inventoryHint} />
      </div>
      <div>
        <LocalizedShortcutHint layout="inline" shortcut="ESC/R" translationKey={L.ui.research.closeHint} />
      </div>
    </div>
  );
}
