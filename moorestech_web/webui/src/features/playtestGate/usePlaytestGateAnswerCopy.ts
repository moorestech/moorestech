import type { GateAnswerCopy } from "@/shared/ui";
import { L, useI18n } from "@/shared/i18n";

// 同意・前回異常終了の2ゲートは同じキー族（ui.playtest.gate.*）を共有する。辞書未着の間はi18nの辞書前文言が出る
// The consent and previous-crash gates share one key family (ui.playtest.gate.*); before the dictionary, i18n's pre-dictionary copy shows
export function usePlaytestGateAnswerCopy(): GateAnswerCopy {
  const { t } = useI18n();
  return {
    answerAccepted: t(L.ui.playtest.gate.answerAccepted),
    notClosed: t(L.ui.playtest.gate.notClosed),
    disconnected: t(L.ui.playtest.gate.disconnected),
    respondFailed: t(L.ui.playtest.gate.respondFailed),
  };
}
