// 出展モードの言語選択ゲートのワイヤ契約テスト
// The wire contract tests of event mode's language gate
import { describe, expect, it } from "vitest";
import { parseTopicPayload } from "./validators";
import { BENIGN_ERRORS, EVENT_LANGUAGE_ALREADY_SELECTED } from "../transport/actions";
import { Topics } from "../transport/protocol";

describe("event language gate topic schema", () => {
  it("boolean の waiting を受理し、欠損と型違いを拒否する", () => {
    expect(parseTopicPayload(Topics.eventLanguageGate, { waiting: true }).valid).toBe(true);
    expect(parseTopicPayload(Topics.eventLanguageGate, { waiting: false }).valid).toBe(true);
    expect(parseTopicPayload(Topics.eventLanguageGate, {}).valid).toBe(false);
    expect(parseTopicPayload(Topics.eventLanguageGate, { waiting: "true" }).valid).toBe(false);
  });
});

describe("event language gate rejection codes", () => {
  // ゲートが受理扱いするコードをトーストすると、同じ拒否が受理と失敗の二重表示になる
  // Toasting a code the gate treats as accepted would report the same rejection as both accepted and failed
  it("応答済みコードは BENIGN_ERRORS の部分集合", () => {
    expect(BENIGN_ERRORS["event_mode.select_language"]?.has(EVENT_LANGUAGE_ALREADY_SELECTED)).toBe(true);
  });
});
