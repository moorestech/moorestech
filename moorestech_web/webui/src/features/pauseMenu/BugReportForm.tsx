// ポーズメニュー直置きのバグ報告欄。Escape時点の記録はC#側が確保済みで、ここは説明文と送信だけを担う
// Bug-report form placed directly in the pause menu; C# has secured the Escape-moment records, this only adds text and sends
import { useRef, useState } from "react";
import { dispatchAction, UiStateNames, type PauseMenuData } from "@/bridge";
import { emitToast } from "@/features/toast";
import { L, useI18n } from "@/shared/i18n";
import { PanelActionButton } from "@/shared/ui";
import styles from "./style.module.css";

type Props = {
  status: PauseMenuData["bugReport"];
};

export function BugReportForm({ status }: Props) {
  const { t } = useI18n();
  const [description, setDescription] = useState("");
  const [sending, setSending] = useState(false);

  // 送信後の欠損はC#が配り直す。押した時点の props は古いので、最新の配信値を読むために ref で持つ
  // C# republishes the missing list after a send; the props captured at click time are stale, so the latest delivery is read through a ref
  const statusRef = useRef(status);
  statusRef.current = status;

  const trimmedDescription = description.trim();
  const blocked = trimmedDescription.length === 0 || status.capturePending || !status.hasSession || sending;

  const send = async () => {
    // 二度押しは同じ確保から2箱を作り、同じ報告のdraft PRが2本出る
    // A second click makes two boxes from one capture and raises two draft PRs for one report
    if (blocked) return;
    setSending(true);
    // 失敗時のトーストは dispatchAction が出すため、ここでは書き出し成功だけを伝える
    // dispatchAction toasts the failure itself, so this path only reports a successful write
    const ok = await dispatchAction("bug_report.submit", { description: trimmedDescription });
    setSending(false);
    if (!ok) return;

    // 欠損の判定はC#が持つ。Web側は再判断せず、配られた missing で文言だけを分ける
    // C# owns the missing decision; the Web re-judges nothing and only picks the wording from what was delivered
    const missing = statusRef.current.missing;
    if (missing.length === 0) emitToast(t(L.ui.bugReport.sent), "info");
    else emitToast(t(L.ui.bugReport.missing, { items: missing.join(", ") }), "error");
    setDescription("");
    void dispatchAction("ui_state.request", { state: UiStateNames.gameScreen });
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
      {!status.hasSession && <span className={styles.status} data-testid="bug-report-status">{t(L.ui.bugReport.noSession)}</span>}
      {sending && <span className={styles.status} data-testid="bug-report-status">{t(L.ui.bugReport.sending)}</span>}
      {status.missing.length > 0 && (
        <span className={styles.status} data-testid="bug-report-status">{t(L.ui.bugReport.missing, { items: status.missing.join(", ") })}</span>
      )}
      <PanelActionButton onClick={send} disabled={blocked} testId="bug-report-send">{t(L.ui.bugReport.send)}</PanelActionButton>
    </>
  );
}
