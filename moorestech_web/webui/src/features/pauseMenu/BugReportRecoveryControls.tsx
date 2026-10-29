// 送れず残った報告を救う操作。件数・状態はC#が配る
// Controls rescuing unsent reports; counts and state come from C#
import { Button } from "@mantine/core";
import { dispatchAction, type PauseMenuData } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";
import styles from "./style.module.css";

type Props = {
  // 未送信・送信不可の件数。読めないときnull
  // Pending and failed counts; null when unreadable
  counts: { pendingCount: number; failedCount: number } | null;
  resendAvailable: boolean;
  inventoryUnavailable: boolean;
  exportStatus: PauseMenuData["export"];
};

export function BugReportRecoveryControls({ counts, resendAvailable, inventoryUnavailable, exportStatus }: Props) {
  const { t } = useI18n();
  const exporting = exportStatus.kind === "running";

  // 件数が読めないときも書き出しだけは出す。読めない箱の救出が書き出しの役目のため
  // Export stays available even when counts are unreadable, because rescuing such boxes is its job
  const showResend = resendAvailable && counts !== null && counts.failedCount > 0;
  const showExport = inventoryUnavailable || (counts !== null && counts.pendingCount + counts.failedCount > 0);
  const statusLine = describeExport();

  return (
    <>
      {showResend && (
        <Button onClick={() => dispatchAction("bug_report.resend_failed", {})} disabled={exporting} data-testid="bug-report-resend-failed">
          {t(L.ui.bugReport.resendFailed)}
        </Button>
      )}
      {showExport && (
        <Button onClick={() => dispatchAction("bug_report.export_unsent", {})} disabled={exporting} data-testid="bug-report-export-unsent">
          {t(L.ui.bugReport.exportUnsent)}
        </Button>
      )}
      {statusLine && <span className={styles.status} data-testid="bug-report-export-status">{statusLine}</span>}
    </>
  );

  function describeExport(): string | null {
    switch (exportStatus.kind) {
      case "idle": return null;
      case "running": return t(L.ui.bugReport.exportRunning);
      case "failed": return t(L.ui.bugReport.exportFailed, { reason: exportStatus.reason });
      case "done": {
        const parts = [t(L.ui.bugReport.exportDone, { count: exportStatus.boxCount, path: exportStatus.zipPath })];
        if (exportStatus.missing.length > 0) parts.push(t(L.ui.bugReport.exportMissing, { count: exportStatus.missing.length, items: exportStatus.missing.map((m) => `${m.item} (${m.reason})`).join(", ") }));
        if (!exportStatus.revealed) parts.push(t(L.ui.bugReport.exportNotRevealed));
        return parts.join(" / ");
      }
    }
  }
}
