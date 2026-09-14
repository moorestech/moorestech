// 全画面ゲートの応答1回ぶんの状態機械。押下不可の判断と、テスターに見せる1行をここ1本が持つ
// The state machine for one full-screen-gate answer; this single place owns the disabled decision and the line the tester reads
import { useEffect, useRef, useState } from "react";
import { dispatchActionOutcome, GATE_ALREADY_ANSWERED_ERRORS, type ActionPayloads, type GateAnswerActionType } from "@/bridge";
import { DictionaryIndependentText, L, useI18n } from "@/shared/i18n";

// 受理からゲートが閉じるまでの猶予。これを過ぎても閉じないのは待機解除のpublishが落ちた疑い
// The grace period from acceptance to the gate closing; past it the waiting-release publish is suspected lost
const CloseGraceMs = 10000;

// 失敗を1状態へ畳むと「もう一度押してください」が二重応答に対して必ず無効な指示になる
// Collapsing failures into one state makes "press again" an always-invalid instruction for a second answer
export type GateAnswerState = "idle" | "pending" | "awaitingClose" | "stalled" | "failed" | "disconnected";

export type GateAnswer<K extends GateAnswerActionType> = {
  state: GateAnswerState;
  disabled: boolean;
  message: string | null;
  answer: (payload: ActionPayloads[K]) => Promise<void>;
};

export function useGateAnswer<K extends GateAnswerActionType>(type: K): GateAnswer<K> {
  const { t } = useI18n();
  const [state, setState] = useState<GateAnswerState>("idle");
  const closeTimer = useRef<ReturnType<typeof setTimeout> | null>(null);

  // 閉じない見張りはゲートが消えると同時に止める。残すと消えた木へsetStateが走る
  // The not-closing watch stops with the gate; left running it would setState into an unmounted tree
  useEffect(() => () => clearCloseWatch(), []);

  return {
    state,
    // 受理済み・応答中は押させない。それ以外（拒否・切断・閉じない）はテスターが押し直せる
    // A pending or accepted answer blocks presses; every other state (refused, disconnected, not closing) stays pressable
    disabled: state === "pending" || state === "awaitingClose",
    message: describeState(),
    answer,
  };

  async function answer(payload: ActionPayloads[K]): Promise<void> {
    setState("pending");
    const outcome = await dispatchActionOutcome(type, payload);

    // 受理と「すでに応答済み」は同じ結末。サーバーは答えを持っているので待機解除を待つ
    // Acceptance and "already answered" end the same way: the server holds the answer, so this waits for the release
    if (outcome.kind === "accepted") {
      acceptAnswer();
      return;
    }
    if (outcome.kind === "rejected" && outcome.error === GATE_ALREADY_ANSWERED_ERRORS[type]) {
      console.warn(`[${type}] already answered: ${outcome.error}`);
      acceptAnswer();
      return;
    }

    // トーストはゲートの下に隠れるため、ここで理由を分けないとテスターへ届く情報が無くなる
    // Toasts hide beneath the gate, so failing to separate the reasons here leaves the tester with nothing
    clearCloseWatch();
    if (outcome.kind === "unreachable" && outcome.reason === "disconnected") {
      console.warn(`[${type}] not sent: disconnected`);
      setState("disconnected");
      return;
    }
    console.warn(`[${type}] rejected: ${outcome.kind === "rejected" ? outcome.error : outcome.reason}`);
    setState("failed");
  }

  function acceptAnswer(): void {
    setState("awaitingClose");
    startCloseWatch();
  }

  function startCloseWatch(): void {
    clearCloseWatch();
    closeTimer.current = setTimeout(() => {
      console.warn(`[${type}] answered but the gate did not close within ${CloseGraceMs}ms`);
      setState("stalled");
    }, CloseGraceMs);
  }

  function clearCloseWatch(): void {
    if (closeTimer.current === null) return;
    clearTimeout(closeTimer.current);
    closeTimer.current = null;
  }

  function describeState(): string | null {
    switch (state) {
      case "idle":
      case "pending":
        return null;
      case "awaitingClose":
        return t(L.ui.playtest.gate.answerAccepted, {}, DictionaryIndependentText.gateAnswerAccepted);
      case "stalled":
        return t(L.ui.playtest.gate.notClosed, {}, DictionaryIndependentText.gateNotClosed);
      case "disconnected":
        return t(L.ui.playtest.gate.disconnected, {}, DictionaryIndependentText.gateDisconnected);
      case "failed":
        return t(L.ui.playtest.gate.respondFailed, {}, DictionaryIndependentText.gateRespondFailed);
    }
  }
}
