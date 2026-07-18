import { lazy, Suspense } from "react";
import { Button } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import styles from "./InventoryScreenChrome.module.css";

// 開発用操作は本番バンドルへ残さない
// Keep development controls out of the production bundle
const DebugActionButton = import.meta.env.DEV ? lazy(() => import("./DebugActionButton")) : null;

// インベントリ画面固有の操作とキーヒントを所有する
// Own inventory-screen controls and key hints
export default function InventoryScreenChrome() {
  return (
    <>
      <div className={styles.topControls}>
        <Button className={styles.sortButton} variant="default" size="compact-sm" onClick={() => void dispatchAction("inventory.sort", {})}>
          整理
        </Button>
        {DebugActionButton ? (
          <Suspense fallback={null}>
            <DebugActionButton />
          </Suspense>
        ) : null}
      </div>
      <div className={styles.keyHints} data-testid="key-hints">
        <div><kbd>Tab/ESC</kbd>: インベントリを閉じる</div>
        <div><kbd>R</kbd>: リサーチツリー</div>
      </div>
    </>
  );
}
