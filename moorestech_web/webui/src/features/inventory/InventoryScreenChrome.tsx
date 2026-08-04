import { Button } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import { L, LocalizedShortcutHint, useI18n } from "@/shared/i18n";
import styles from "./InventoryScreenChrome.module.css";

// インベントリ画面固有の操作とキーヒントを所有する
// Own inventory-screen controls and key hints
export default function InventoryScreenChrome() {
  const { t } = useI18n();
  return (
    <>
      <div className={styles.topControls}>
        <Button className={styles.sortButton} variant="default" size="compact-sm" onClick={() => void dispatchAction("inventory.sort", {})}>
          {t(L.ui.inventory.sort)}
        </Button>
      </div>
      <div className={styles.keyHints} data-testid="key-hints">
        <div>
          <LocalizedShortcutHint shortcut="Tab/ESC" translationKey={L.ui.inventory.closeHint} />
        </div>
        <div>
          <LocalizedShortcutHint shortcut="R" translationKey={L.ui.inventory.researchHint} />
        </div>
      </div>
    </>
  );
}
