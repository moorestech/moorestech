import { L, LocalizedShortcutHint } from "@/shared/i18n";
import styles from "./InventoryScreenChrome.module.css";

// インベントリ画面固有のキーヒントを所有する（整理操作はInventoryPanelのタイトル行が持つ）
// Owns the inventory screen's key hints; the sort action lives in InventoryPanel's title row
export default function InventoryScreenChrome() {
  return (
    <div className={`keyHintText ${styles.keyHints}`} data-testid="key-hints">
      <div>
        <LocalizedShortcutHint layout="inline" shortcut="Tab/ESC" translationKey={L.ui.inventory.closeHint} />
      </div>
      <div>
        <LocalizedShortcutHint layout="inline" shortcut="R" translationKey={L.ui.inventory.researchHint} />
      </div>
    </div>
  );
}
