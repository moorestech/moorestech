// 開始ゲートの待機を購読し、今見せる1枚を決める。順序はC#がpayloadのprecedenceで配り、ここは比べるだけ
// Subscribes to the start gates' waiting and picks the one to show; C# ships the order as precedence and this only compares
import { Topics, useTopicSelector } from "@/bridge";

export type StartGate = "eventLanguage";

// topicは同形。precedenceが小さいほど先に答えさせる
// Topics share this shape; a smaller precedence is answered first
type StartGateWaiting = { waiting: boolean; precedence: number };

export function useFrontmostStartGate(): StartGate | null {
  const eventLanguage = useTopicSelector(Topics.eventLanguageGate, (data) => data);
  return pickFrontmostStartGate({ eventLanguage });
}

// 待機中のうちprecedence最小の1枚。ゲートは無条件マウントなので、同時に待っても重ねて描かせない
// The waiting gate with the smallest precedence; gates mount unconditionally, so simultaneous waits never stack
export function pickFrontmostStartGate(gates: Readonly<Record<StartGate, StartGateWaiting | null>>): StartGate | null {
  let frontmost: { gate: StartGate; precedence: number } | null = null;
  for (const gate of Object.keys(gates) as StartGate[]) {
    const data = gates[gate];
    if (data === null || !data.waiting) continue;
    if (frontmost === null || data.precedence < frontmost.precedence) frontmost = { gate, precedence: data.precedence };
  }
  return frontmost?.gate ?? null;
}
