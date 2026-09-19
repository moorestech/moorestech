// 開始ゲートのワイヤ契約テスト
// The start gate wire contract tests
import { describe, expect, it } from "vitest";
import { parseTopicPayload } from "./validators";
import { BENIGN_ERRORS, GATE_ALREADY_ANSWERED_ERRORS } from "../transport/actions";
import { Topics } from "../transport/protocol";

describe("start gate topic schema", () => {
  it("boolean の waiting と非負整数の precedence を受理する", () => {
    expect(parseTopicPayload(Topics.eventLanguageGate, { waiting: true, precedence: 0 }).valid).toBe(true);
    expect(parseTopicPayload(Topics.eventLanguageGate, { waiting: false, precedence: 0 }).valid).toBe(true);
    expect(parseTopicPayload(Topics.eventLanguageGate, {}).valid).toBe(false);
    expect(parseTopicPayload(Topics.eventLanguageGate, { waiting: "true", precedence: 0 }).valid).toBe(false);
  });

  // 順序を持たない待機は、同時に待ったとき見せる1枚を決められない
  // A wait without an order cannot decide which gate shows when several wait at once
  it("precedence の欠けた開始ゲートの待機を拒否する", () => {
    expect(parseTopicPayload(Topics.eventLanguageGate, { waiting: true }).valid).toBe(false);
    expect(parseTopicPayload(Topics.eventLanguageGate, { waiting: true, precedence: -1 }).valid).toBe(false);
  });
});

describe("start gate rejection codes", () => {
  // ゲートが受理扱いするコードをトーストすると、同じ拒否が受理と失敗の二重表示になる
  // Toasting a code the gate treats as accepted would report the same rejection as both accepted and failed
  it("全画面ゲートの応答済みコードは BENIGN_ERRORS の部分集合", () => {
    for (const [type, code] of Object.entries(GATE_ALREADY_ANSWERED_ERRORS)) {
      expect(BENIGN_ERRORS[type as keyof typeof GATE_ALREADY_ANSWERED_ERRORS]?.has(code), `${type}: ${code}`).toBe(true);
    }
  });
});
