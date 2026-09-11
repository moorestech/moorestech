// ポーズメニュー直置きのバグ報告欄。Escape時点の記録はC#側が確保済みで、ここは説明文と送信だけを担う
// Bug-report form placed directly in the pause menu; C# has secured the Escape-moment records, this only adds text and sends
import { useState } from "react";
import { dispatchAction, type PauseMenuData } from "@/bridge";
import { emitToast } from "@/features/toast";
import { L, useI18n } from "@/shared/i18n";
import { PanelActionButton } from "@/shared/ui";
import styles from "./style.module.css";

type Props = {
  status: PauseMenuData["bugReport"];
  onSent: () => void;
};

export function BugReportForm({ status, onSent }: Props) {
  const { t } = useI18n();
  const [description, setDescription] = useState("");

  const send = async () => {
    const trimmed = description.trim();
    if (trimmed.length === 0) return;
    // 失敗時のトーストは dispatchAction が出すため、ここでは書き出し成功だけを伝える
    // dispatchAction toasts the failure itself, so this path only reports a successful write
    const ok = await dispatchAction("bug_report.submit", { description: trimmed });
    if (!ok) return;
    emitToast(t(L.ui.bugReport.sent), "info");
    setDescription("");
    onSent();
  };

  return (
    <>
      <textarea
        className={styles.description}
        value={description}
        placeholder={t(L.ui.bugReport.placeholder)}
        onChange={(e) => setDescription(e.currentTarget.value)}
        data-testid="bug-report-description"
      />
      {status.capturePending && <span className={styles.status} data-testid="bug-report-status">{t(L.ui.bugReport.capturePending)}</span>}
      {status.missing.length > 0 && (
        <span className={styles.status} data-testid="bug-report-status">{t(L.ui.bugReport.missing, { items: status.missing.join(", ") })}</span>
      )}
      <PanelActionButton onClick={send} testId="bug-report-send">{t(L.ui.bugReport.send)}</PanelActionButton>
    </>
  );
}
