import { describe, expect, it } from "vitest";
import { NestedPauseSubStateNames } from "@/bridge";
import { GraphicsQualityDataSchema, NotificationDataSchema, PauseMenuDataSchema, SprintInputModeDataSchema, UiStateDataSchema } from "../ui";

describe("NotificationDataSchema", () => {
  it("itemId: nullをomittedと同様に受理する（シリアライザ揺れ耐性）", () => {
    const parsed = NotificationDataSchema.parse({
      seq: 1, category: "achievement", messageId: "achievement.researchCompleted", messageParams: [], itemId: null,
    });
    expect("itemId" in parsed ? parsed.itemId ?? null : null).toBeNull();
  });

  it("snapshotの空オブジェクトを受理する", () => {
    expect(NotificationDataSchema.safeParse({}).success).toBe(true);
  });

  it("itemEarnedはitemIdとcountの欠損を弾く", () => {
    const base = { seq: 1, category: "itemEarned", messageId: "itemEarned.mined", messageParams: [] };
    expect(NotificationDataSchema.safeParse({ ...base, itemId: 5, count: 8 }).success).toBe(true);
    expect(NotificationDataSchema.safeParse({ ...base, itemId: 5 }).success).toBe(false);
    expect(NotificationDataSchema.safeParse({ ...base, count: 8 }).success).toBe(false);
    expect(NotificationDataSchema.safeParse({ ...base, itemId: 5, count: 0 }).success).toBe(false);
  });
});

// ヒント配列はC#のUIStateが宣言した内容をそのまま運ぶ（ADR-0032）
// The hint array carries exactly what C#'s UIState declared (ADR-0032)
describe("UiStateDataSchema", () => {
  it("keyHintsを受理する", () => {
    const parsed = UiStateDataSchema.safeParse({
      state: "GameScreen",
      keyHints: [{ keyNameKey: "ui.keyHint.key.tab", textKey: "ui.keyHint.text.inventory" }],
    });
    expect(parsed.success).toBe(true);
  });

  it("入れ子stateは既知語彙も未知語彙も受理する（未知値の解釈はscreenForUiStateのfail-safe）", () => {
    expect(UiStateDataSchema.safeParse({ state: "Story", subState: NestedPauseSubStateNames.gameScreen, keyHints: [] }).success).toBe(true);
    expect(UiStateDataSchema.safeParse({ state: "Story", subState: NestedPauseSubStateNames.pauseMenuScreen, keyHints: [] }).success).toBe(true);
    expect(UiStateDataSchema.safeParse({ state: "Story", subState: "UnknownSubState", keyHints: [] }).success).toBe(true);
  });

  it("keyHints未着のペイロードは契約破れとして弾く", () => {
    const parsed = UiStateDataSchema.safeParse({ state: "GameScreen" });
    expect(parsed.success).toBe(false);
  });
});

describe("PauseMenuDataSchema", () => {
  it("bugReport を持つ", () => {
    const parsed = PauseMenuDataSchema.parse({ disconnected: false, delivery: { kind: "allSent" }, export: { kind: "idle" }, bugReport: { kind: "capturing", missing: ["video"] }, page: "top" });
    expect(parsed.bugReport.missing).toEqual(["video"]);
  });

  // 送信可否はC#が出した結論そのものを受け取る。組み立て直せる形にすると判定が2本になる
  // The send-permission verdict arrives exactly as C# decided it; a re-assemblable shape would make two rules
  it("未知の kind と kind 抜きの確保状態を弾く", () => {
    expect(PauseMenuDataSchema.safeParse({ disconnected: false, delivery: { kind: "allSent" }, export: { kind: "idle" }, bugReport: { kind: "sending", missing: [] }, page: "top" }).success).toBe(false);
    expect(PauseMenuDataSchema.safeParse({ disconnected: false, delivery: { kind: "allSent" }, export: { kind: "idle" }, bugReport: { hasSession: true, capturePending: false, missing: [] }, page: "top" }).success).toBe(false);
  });
});

// 復旧情報を欠損させた正常表示を境界で拒否する
// Reject normal-looking payloads that omit recovery information
it("走り設定は復旧状態を必須で運ぶ", () => {
  expect(SprintInputModeDataSchema.safeParse({ mode: "toggle" }).success).toBe(false);
  expect(SprintInputModeDataSchema.safeParse({ mode: "toggle", recoveredInvalidPreference: true }).success).toBe(true);
  expect(SprintInputModeDataSchema.safeParse({ mode: "hold", recoveredInvalidPreference: false }).success).toBe(true);
});

// 不要なフィールドや未知の理由は表示契約違反として拒否する
// Reject redundant fields and unknown reasons as display contract violations
it("deliveryの各状態だけを受理し、欠損と不正値を拒否する", () => {
  const base = { disconnected: false, export: { kind: "idle" }, bugReport: { kind: "ready", missing: [] }, page: "top" };
  for (const delivery of [{ kind: "allSent" }, { kind: "pending", inventory: { kind: "known", pendingCount: 0, failedCount: 0 } }, { kind: "refused", inventory: { kind: "known", pendingCount: 2, failedCount: 0 } }, { kind: "localOnly", reason: "distributionUnavailable", inventory: { kind: "known", pendingCount: 1, failedCount: 0 } }]) {
    expect(PauseMenuDataSchema.safeParse({ ...base, delivery }).success).toBe(true);
  }
  for (const delivery of [{ kind: "allSent", inventory: { kind: "known", pendingCount: 0, failedCount: 0 } }, { kind: "localOnly", reason: "unknown", inventory: { kind: "known", pendingCount: 1, failedCount: 0 } }, { kind: "pending", inventory: { kind: "known", pendingCount: -1, failedCount: 0 } }, { kind: "pending", inventory: { kind: "known", pendingCount: 0.5, failedCount: 0 } }]) {
    expect(PauseMenuDataSchema.safeParse({ ...base, delivery }).success).toBe(false);
  }
  expect(PauseMenuDataSchema.safeParse(base).success).toBe(false);
});

it("件数不明を4状態内で受理し、未知と件数の混在を拒否する", () => {
  const base = { disconnected: false, export: { kind: "idle" }, bugReport: { kind: "ready", missing: [] }, page: "top" };
  for (const status of [{ kind: "pending" }, { kind: "refused" }, { kind: "localOnly", reason: "developerTokenMissing" }]) {
    const inventory = { kind: "unavailable", reason: "denied" };
    expect(PauseMenuDataSchema.safeParse({ ...base, delivery: { ...status, inventory } }).success).toBe(true);
    expect(PauseMenuDataSchema.safeParse({ ...base, delivery: { ...status, inventory: { ...inventory, pendingCount: 0 } } }).success).toBe(false);
  }
});

it("GraphicsQualityDataSchemaは定義済みの値だけを受ける", () => {
  const settings = {
    drawDistanceMeters: 300, shadows: true, renderScalePercent: 100, ssao: true, postProcessing: true,
    effects: true, mapObjects: true, terrainDetail: "fine", ocean: true,
  };
  expect(GraphicsQualityDataSchema.safeParse({ preset: "custom", settings, recoveredInvalidPreference: true }).success).toBe(true);
  expect(GraphicsQualityDataSchema.safeParse({ preset: "medium", settings }).success).toBe(false);
  expect(GraphicsQualityDataSchema.safeParse({ preset: "medium", settings, recoveredInvalidPreference: "true" }).success).toBe(false);
  expect(GraphicsQualityDataSchema.safeParse({ preset: "ultra", settings, recoveredInvalidPreference: false }).success).toBe(false);
  expect(GraphicsQualityDataSchema.safeParse({ preset: "low", recoveredInvalidPreference: false, settings: { ...settings, drawDistanceMeters: 777 } }).success).toBe(false);
  expect(GraphicsQualityDataSchema.safeParse({ preset: "low", recoveredInvalidPreference: false, settings: { ...settings, terrainDetail: "ultra" } }).success).toBe(false);
});
