// バグ報告画面の報告欄。書きかけは親のパネルが持ち、ここは入力と送信だけを担う
// The report form on the bug-report page; the parent panel holds the draft, this only handles input and sending
import { Button } from "@mantine/core";
import { useState } from "react";
import { dispatchActionOutcome, PauseMenuReportKinds, type PauseMenuData, type PauseMenuReportKind } from "@/bridge";
import { emitToast } from "@/features/toast";
import { L, useI18n } from "@/shared/i18n";
import { ModeSwitch } from "@/shared/ui";
import type { BugReportDraft } from "./useBugReportDraft";
import styles from "./style.module.css";

type Props = {
  status: PauseMenuData["bugReport"];
  draft: BugReportDraft;
};

export function BugReportForm({ status, draft }: Props) {
  const { t } = useI18n();
  const [sending, setSending] = useState(false);

  const trimmedDescription = draft.description.trim();

  // 送信してよいかの判定はC#が持ち、結論が kind で届く。ここで条件を組み立て直すと判定が2本になる
  // C# owns whether a send may start and delivers the verdict as the kind; rebuilding the conditions here would make two rules
  const blocked = trimmedDescription.length === 0 || status.kind !== "ready" || sending;

  const send = async () => {
    // 二度押しは同じ確保から2箱を作り、同じ報告のdraft PRが2本出る
    // A second click makes two boxes from one capture and raises two draft PRs for one report
    if (blocked) return;
    setSending(true);
    // 失敗時のトーストは dispatchActionOutcome が出すため、ここでは書き出し成功だけを伝える
    // dispatchActionOutcome toasts the failure itself, so this path only reports a successful write
    const result = await dispatchActionOutcome("bug_report.submit", { description: trimmedDescription, kind: draft.kind });
    setSending(false);
    if (result.kind !== "accepted") return;

    // 再確保後のtopicではなく、送った箱に確定した欠損をaction結果から読む
    // Read the gaps settled for the sent bundle from the action result, not the topic after recapture
    const missing = parseSubmittedMissing(result.payload);
    if (missing.length === 0) emitToast(t(L.ui.bugReport.sent), "info");
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
        disabled={sending}
        testId="bug-report-kind"
      />
      <textarea
        className={styles.description}
        value={draft.description}
        placeholder={t(L.ui.bugReport.placeholder)}
        disabled={sending}
        onChange={(e) => draft.setDescription(e.currentTarget.value)}
        data-testid="bug-report-description"
      />
      {statusLine && <span className={styles.status} data-testid="bug-report-status">{statusLine}</span>}
      <Button onClick={send} disabled={blocked} data-testid="bug-report-send">{t(L.ui.bugReport.send)}</Button>
    </>
  );

  // 送れない理由はC#の kind 1本から引く。押下中だけは応答が届くまでの手元の状態を優先する
  // The reason a send is blocked comes from C#'s single kind; only the click's own round trip is read locally
  function describeStatus(): string | null {
    if (sending) return t(L.ui.bugReport.sending);
    switch (status.kind) {
      case "noSession": return t(L.ui.bugReport.noSession);
      case "capturing": return t(L.ui.bugReport.capturePending);
      case "submitting": return t(L.ui.bugReport.sending);
      case "submitted": return t(L.ui.bugReport.sent);
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

function parseSubmittedMissing(payload: unknown): string[] {
  if (typeof payload !== "object" || payload === null || !("missing" in payload)) return [];
  const missing = payload.missing;
  return Array.isArray(missing) && missing.every((item) => typeof item === "string") ? missing : [];
}
