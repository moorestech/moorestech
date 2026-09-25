import { describe, it, expect } from "vitest";

import { parseTopicPayload } from "./validators";
import { loadFixture } from "./wireFixtures.test-helper";
import { Topics } from "../transport/protocol";

// 列車時刻表の取得状態の wire 契約。C# 側 WireContractTrainTimetableTest が同じフィクスチャを照合する
// Wire contract for the train timetable fetch state; the C# WireContractTrainTimetableTest matches the same fixtures
describe("train timetable fetch state (shared with C#)", () => {
  it("読み込み中と取得済みのフィクスチャを受理する", () => {
    expect(parseTopicPayload(Topics.blockInventory, loadFixture("train_inventory.json")).valid).toBe(true);
    expect(parseTopicPayload(Topics.blockInventory, loadFixture("train_inventory_timetable_ready.json")).valid).toBe(true);
  });

  it("取得状態ごとの形だけを受理する", () => {
    const train = loadFixture("train_inventory.json") as Record<string, unknown>;
    expect(parseTopicPayload(Topics.blockInventory, { ...train, timetable: { kind: "unavailable" } }).valid).toBe(true);
    // 取得済み以外は data を持てず、取得済みは data 必須、状態の省略は不可
    // Only ready may carry data, ready requires it, and the state cannot be omitted
    expect(parseTopicPayload(Topics.blockInventory, { ...train, timetable: { kind: "ready" } }).valid).toBe(false);
    expect(parseTopicPayload(Topics.blockInventory, { ...train, timetable: { kind: "loading", data: {} } }).valid).toBe(false);
    expect(parseTopicPayload(Topics.blockInventory, { ...train, timetable: undefined }).valid).toBe(false);
  });
});
