// ポーズメニュー直置きのバグ報告欄。Escape時点の記録はC#側が確保済みで、ここは説明文と送信だけを担う
// Bug-report form placed directly in the pause menu; C# has secured the Escape-moment records, this only adds text and sends
import { Button } from "@mantine/core";
import { useState } from "react";
import { dispatchAction, readTopic, Topics, type PauseMenuData } from "@/bridge";
import { emitToast } from "@/features/toast";
import { L, useI18n } from "@/shared/i18n";
import { ModeSwitch } from "@/shared/ui";
import styles from "./style.module.css";

type Props = {
  status: PauseMenuData["bugReport"];
};

// 契約値はC#の PlaytestReportKind と同じ文字列。既定はバグ（ADR 0058）
// The contract strings match C#'s PlaytestReportKind; bug is the default (ADR 0058)
const KindBug = "bug";
const KindFeedback = "feedback";

export function BugReportForm({ status }: Props) {
  const { t } = useI18n();
  const [description, setDescription] = useState("");
  const [kind, setKind] = useState<string>(KindBug);
  const [sending, setSending] = useState(false);

  const trimmedDescription = description.trim();

  // 送信してよいかの判定はC#が持ち、結論が kind で届く。ここで条件を組み立て直すと判定が2本になる
  // C# owns whether a send may start and delivers the verdict as the kind; rebuilding the conditions here would make two rules
  const blocked = trimmedDescription.length === 0 || status.kind !== "ready" || sending;

  const send = async () => {
    // 二度押しは同じ確保から2箱を作り、同じ報告のdraft PRが2本出る
    // A second click makes two boxes from one capture and raises two draft PRs for one report
    if (blocked) return;
    setSending(true);
    // 失敗時のトーストは dispatchAction が出すため、ここでは書き出し成功だけを伝える
    // dispatchAction toasts the failure itself, so this path only reports a successful write
    const ok = await dispatchAction("bug_report.submit", { description: trimmedDescription, kind });
    setSending(false);
    if (!ok) return;

    // 欠損の判定はC#が持つ。押した時点の props は古いので、配信済みの最新値をその場で読む
    // C# owns the missing decision; the props captured at click time are stale, so the latest delivered value is read on the spot
    const missing = readTopic(Topics.pauseMenu)?.bugReport.missing ?? [];
    if (missing.length === 0) emitToast(t(L.ui.bugReport.sent), "info");
    else emitToast(t(L.ui.bugReport.missing, { items: missing.join(", ") }), "error");
    setDescription("");
  };

  const statusLine = describeStatus();

  return (
    <>
      <span className={styles.status}>{t(L.ui.playtest.reportKind.label)}</span>
      <ModeSwitch
        value={kind}
        options={[
          { value: KindBug, label: t(L.ui.playtest.reportKind.bug), testId: "bug-report-kind-bug" },
          { value: KindFeedback, label: t(L.ui.playtest.reportKind.feedback), testId: "bug-report-kind-feedback" },
        ]}
        onChange={setKind}
        testId="bug-report-kind"
      />
      <textarea
        className={styles.description}
        value={description}
        placeholder={t(L.ui.bugReport.placeholder)}
        onChange={(e) => setDescription(e.currentTarget.value)}
        data-testid="bug-report-description"
      />
      {statusLine && <span className={styles.status} data-testid="bug-report-status">{statusLine}</span>}
      {status.missing.length > 0 && (
        <span className={styles.status} data-testid="bug-report-status">{t(L.ui.bugReport.missing, { items: status.missing.join(", ") })}</span>
      )}
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
}
