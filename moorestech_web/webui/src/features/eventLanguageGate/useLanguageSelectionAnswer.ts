// 言語選択ゲートの応答1回ぶんの状態機械。押下不可の判断と、テスターに見せる1行をここ1本が持つ
// The state machine for one language-gate answer; this single place owns the disabled decision and the line the tester reads
import { useEffect, useRef, useState } from "react";
import { dispatchActionOutcome, EVENT_LANGUAGE_ALREADY_SELECTED, type ActionPayloads } from "@/bridge";

// 応答するactionは言語選択1本。ログの前置きにも同じ名前を使う
// The answer dispatches one action alone, and the log prefix reuses the same name
const AnswerActionType = "event_mode.select_language";

// 受理からゲートが閉じるまでの猶予。これを過ぎても閉じないのは待機解除のpublishが落ちた疑い
// The grace period from acceptance to the gate closing; past it the waiting-release publish is suspected lost
const CloseGraceMs = 10000;

// 失敗を1状態へ畳むと「もう一度押してください」が二重応答に対して必ず無効な指示になる
// Collapsing failures into one state makes "press again" an always-invalid instruction for a second answer
type GateAnswerState = "idle" | "pending" | "awaitingClose" | "stalled" | "failed" | "disconnected";

// 結末ごとの1行。辞書経由か辞書非依存かはゲート側が決めて解決済みの文字列で渡す
// One line per outcome; the gate decides between dictionary and dictionary-independent copy and passes resolved strings
export type GateAnswerCopy = {
  answerAccepted: string;
  notClosed: string;
  disconnected: string;
  respondFailed: string;
};

type GateAnswer = {
  disabled: boolean;
  message: string | null;
  answer: (payload: ActionPayloads[typeof AnswerActionType]) => Promise<void>;
};

export function useLanguageSelectionAnswer(copy: GateAnswerCopy): GateAnswer {
  const [state, setState] = useState<GateAnswerState>("idle");
  const closeTimer = useRef<ReturnType<typeof setTimeout> | null>(null);
  const mounted = useRef(false);

  // 受理でゲートが消えるのは正常経路。消えた後に解決した応答が見張りを張り直さないよう生存を持つ
  // Unmounting on acceptance is the normal path; tracking liveness keeps an answer settling afterwards from re-arming the watch
  useEffect(() => {
    mounted.current = true;
    return () => {
      mounted.current = false;
      clearCloseWatch();
    };
  }, []);

  return {
    // 受理済み・応答中は押させない。それ以外（拒否・切断・閉じない）はテスターが押し直せる
    // A pending or accepted answer blocks presses; every other state (refused, disconnected, not closing) stays pressable
    disabled: state === "pending" || state === "awaitingClose",
    message: describeState(),
    answer,
  };

  async function answer(payload: ActionPayloads[typeof AnswerActionType]): Promise<void> {
    setState("pending");
    const outcome = await dispatchActionOutcome(AnswerActionType, payload);

    // ゲートが先に閉じたら結末を描く先が無い。見張りも張らない
    // If the gate closed first there is nowhere to show the outcome, and no watch is armed
    if (!mounted.current) {
      console.info(`[${AnswerActionType}] gate closed before the answer settled: ${outcome.kind}`);
      return;
    }

    // 受理と「すでに応答済み」は同じ結末。サーバーは答えを持っているので待機解除を待つ
    // Acceptance and "already answered" end the same way: the server holds the answer, so this waits for the release
    if (outcome.kind === "accepted") {
      acceptAnswer();
      return;
    }
    if (outcome.kind === "rejected" && outcome.error === EVENT_LANGUAGE_ALREADY_SELECTED) {
      console.warn(`[${AnswerActionType}] already answered: ${outcome.error}`);
      acceptAnswer();
      return;
    }

    // トーストはゲートの下に隠れるため、ここで理由を分けないとテスターへ届く情報が無くなる
    // Toasts hide beneath the gate, so failing to separate the reasons here leaves the tester with nothing
    clearCloseWatch();
    if (outcome.kind === "unreachable" && outcome.reason === "disconnected") {
      console.warn(`[${AnswerActionType}] not sent: disconnected`);
      setState("disconnected");
      return;
    }
    console.warn(`[${AnswerActionType}] rejected: ${outcome.kind === "rejected" ? outcome.error : outcome.reason}`);
    setState("failed");
  }

  function acceptAnswer(): void {
    setState("awaitingClose");
    startCloseWatch();
  }

  function startCloseWatch(): void {
    clearCloseWatch();
    closeTimer.current = setTimeout(() => {
      console.warn(`[${AnswerActionType}] answered but the gate did not close within ${CloseGraceMs}ms`);
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
        return copy.answerAccepted;
      case "stalled":
        return copy.notClosed;
      case "disconnected":
        return copy.disconnected;
      case "failed":
        return copy.respondFailed;
    }
  }
}
