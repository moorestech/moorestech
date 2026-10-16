// バグ報告画面の報告欄。書きかけは親のパネルが持ち、ここは入力と送信だけを担う
// The report form on the bug-report page; the parent panel holds the draft, this only handles input and sending
import { Button } from "@mantine/core";
import { useState } from "react";
import { dispatchAction, PauseMenuReportKinds, type PauseMenuData, type PauseMenuReportKind } from "@/bridge";
import { emitToast } from "@/features/toast";
import { L, useI18n } from "@/shared/i18n";
import { ModeSwitch } from "@/shared/ui";
import type { BugReportDraft } from "./useBugReportDraft";
import styles from "./style.module.css";

type Props = {
  status: PauseMenuData["bugReport"];
  delivery: PauseMenuData["delivery"];
  draft: BugReportDraft;
};

export function BugReportForm({ status, delivery, draft }: Props) {
  const { t } = useI18n();
  const [sending, setSending] = useState(false);

  const trimmedDescription = draft.description.trim();

  // 送信してよいかの判定はC#が持ち、結論が kind で届く。ここで条件を組み立て直すと判定が2本になる
  // C# owns whether a send may start and delivers the verdict as the kind; rebuilding the conditions here would make two rules
  const blocked = trimmedDescription.length === 0 || status.kind !== "ready" || sending;
  // 応答待ち終了後もホスト送信中は種別を保つ
  // Preserve the kind while the host writes after local waiting ends
  const inputLocked = sending || status.kind === "submitting";

  const send = async () => {
    // 二度押しは同じ確保から2箱を作り、同じ報告のdraft PRが2本出る
    // A second click makes two boxes from one capture and raises two draft PRs for one report
    if (blocked) return;
    const missing = [...status.missing];
    const kind = draft.kind;
    setSending(true);
    // 失敗時のトーストは dispatchAction が出すため、ここでは書き出し成功だけを伝える
    // dispatchAction toasts the failure itself, so this path only reports a successful write
    const accepted = await dispatchAction("bug_report.submit", { description: trimmedDescription, kind });
    setSending(false);
    if (!accepted) return;

    // 押した時点の確保状態で出す。書き出し中の欠損はmanifestにだけ残す
    // Use capture state at click time; gaps during writing remain only in the manifest
    if (missing.length === 0) emitToast(t(reportLabels(kind).sent), "info");
    else emitToast(t(L.ui.bugReport.missing, { items: missing.join(", ") }), "error");

    draft.reset();
  };

  // 状態行は1行だけ出す。送れない理由と欠損は同時に出すと同じ testid が2つ描かれる
  // Only one status line renders: showing the blocked reason and the missing list at once would draw the same testid twice
  const statusLine = describeStatus() ?? describeMissing();

  return (
    <>
      <span className={styles.fieldLabel}>{t(L.ui.playtest.reportKind.label)}</span>
      <ModeSwitch
        value={draft.kind}
        options={[
          { value: PauseMenuReportKinds.bug, label: t(L.ui.playtest.reportKind.bug), testId: "bug-report-kind-bug" },
          { value: PauseMenuReportKinds.feedback, label: t(L.ui.playtest.reportKind.feedback), testId: "bug-report-kind-feedback" },
        ]}
        onChange={(value) => draft.setKind(value as PauseMenuReportKind)}
        disabled={inputLocked}
        testId="bug-report-kind"
      />
      <textarea
        className={styles.description}
        value={draft.description}
        placeholder={t(L.ui.bugReport.placeholder)}
        disabled={inputLocked}
        onChange={(e) => draft.setDescription(e.currentTarget.value)}
        data-testid="bug-report-description"
      />
      {statusLine && <span className={styles.status} data-testid="bug-report-status">{statusLine}</span>}
      <span className={styles.status} data-testid="bug-report-delivery">{describeDelivery()}{describeFailedDelivery()}</span>
      <Button onClick={send} disabled={blocked} data-testid="bug-report-send">{t(reportLabels(draft.kind).send)}</Button>
    </>
  );

  function describeFailedDelivery(): string {
    if (delivery.kind === "allSent" || delivery.inventory.kind !== "known" || delivery.inventory.failedCount === 0) return "";
    return ` / ${t(L.ui.bugReport.deliveryFailed, { count: delivery.inventory.failedCount })}`;
  }

  // 送信可否とは独立した配送状況を伝える
  // Delivery status is independent of permission to submit
  function describeDelivery(): string {
    if (delivery.kind === "allSent") return t(L.ui.bugReport.deliveryAllSent);
    if (delivery.inventory.kind === "unavailable") return t(L.ui.bugReport.deliveryInventoryUnavailable, { reason: delivery.inventory.reason });
    switch (delivery.kind) {
      case "pending": return t(L.ui.bugReport.deliveryPending, { count: delivery.inventory.pendingCount });
      case "refused": return t(L.ui.bugReport.deliveryRefused, { count: delivery.inventory.pendingCount });
      case "localOnly": {
        const keys = {
          developerTokenMissing: L.ui.bugReport.deliveryLocalOnlyTokenMissing,
          developerTokenUnreadable: L.ui.bugReport.deliveryLocalOnlyTokenUnreadable,
          distributionUnavailable: L.ui.bugReport.deliveryLocalOnlyDistributionUnavailable,
        };
        return t(keys[delivery.reason], { count: delivery.inventory.pendingCount });
      }
    }
  }

  // 種別別の表示キーをここに集め、完了通知は送信開始時の種別を使う
  // Keep kind-specific display keys together; completion uses the kind captured at submission
  function reportLabels(kind: PauseMenuReportKind) {
    return kind === PauseMenuReportKinds.feedback ? L.ui.feedbackReport : L.ui.bugReport;
  }

  // 送れない理由はC#の kind 1本から引く。押下中だけは応答が届くまでの手元の状態を優先する
  // The reason a send is blocked comes from C#'s single kind; only the click's own round trip is read locally
  function describeStatus(): string | null {
    if (sending) return t(reportLabels(draft.kind).sending);
    switch (status.kind) {
      case "noSession": return t(L.ui.bugReport.noSession);
      case "capturing": return t(L.ui.bugReport.capturePending);
      case "submitting": return t(reportLabels(draft.kind).sending);
      case "ready": return null;
    }
  }

  // 送れる状態でも取りこぼした記録があるなら、その一覧を同じ1行で伝える
  // When a send is possible but some records were missed, the same single line lists them
  function describeMissing(): string | null {
    if (status.missing.length === 0) return null;
    return t(L.ui.bugReport.missing, { items: status.missing.join(", ") });
  }
}
