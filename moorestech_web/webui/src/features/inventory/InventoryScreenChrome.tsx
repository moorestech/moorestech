import { lazy, Suspense } from "react";
import { Button } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import { useI18n } from "@/shared/i18n";
import styles from "./InventoryScreenChrome.module.css";

// 開発用操作は本番バンドルへ残さない
// Keep development controls out of the production bundle
const DebugActionButton = import.meta.env.DEV ? lazy(() => import("./DebugActionButton")) : null;

// インベントリ画面固有の操作とキーヒントを所有する
// Own inventory-screen controls and key hints
export default function InventoryScreenChrome() {
  const { t } = useI18n();
  return (
    <>
      <div className={styles.topControls}>
        <Button className={styles.sortButton} variant="default" size="compact-sm" onClick={() => void dispatchAction("inventory.sort", {})}>
          {t("整理")}
        </Button>
        {DebugActionButton ? (
          <Suspense fallback={null}>
            <DebugActionButton />
          </Suspense>
        ) : null}
      </div>
      <div className={styles.keyHints} data-testid="key-hints">
        <div><kbd>{t("Tab/ESC")}</kbd>{t(": インベントリを閉じる")}</div>
        <div><kbd>{t("R")}</kbd>{t(": リサーチツリー")}</div>
      </div>
    </>
  );
}
