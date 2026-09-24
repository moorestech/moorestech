import { useState } from "react";
import { PauseMenuReportKinds, type PauseMenuReportKind } from "@/bridge";

// バグ報告の書きかけ。ポーズメニューのパネルが持ち、ポーズを閉じてパネルが外れると一緒に消える（ADR 0069）
// The bug-report draft; the pause-menu panel holds it and it vanishes with the panel when the pause closes (ADR 0069)
export type BugReportDraft = {
  description: string;
  kind: PauseMenuReportKind;
  setDescription: (value: string) => void;
  setKind: (value: PauseMenuReportKind) => void;
  reset: () => void;
};

export function useBugReportDraft(): BugReportDraft {
  const [description, setDescription] = useState("");
  const [kind, setKind] = useState<PauseMenuReportKind>(PauseMenuReportKinds.bug);

  // 種別も既定へ戻す。残すと次の1件が前回の種別のまま箱詰めされる（ADR 0058）
  // Reset the kind too: leaving it boxes the next report under the previous kind (ADR 0058)
  const reset = () => {
    setDescription("");
    setKind(PauseMenuReportKinds.bug);
  };

  return { description, kind, setDescription, setKind, reset };
}
