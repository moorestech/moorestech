import { useI18n } from "@/shared/i18n";
import styles from "./ResearchScreenChrome.module.css";

// 研究画面のキー操作ヒント（InventoryScreenChromeのkeyHints様式）
// Key hints for the research screen, following the InventoryScreenChrome style
export default function ResearchScreenChrome() {
  const { t } = useI18n();
  return (
    <div className={styles.keyHints} data-testid="research-key-hints">
      <div><kbd>{t("Tab")}</kbd>{t(": インベントリ")}</div>
      <div><kbd>{t("ESC/R")}</kbd>{t(": 閉じる")}</div>
    </div>
  );
}
