import { describe, it, expect } from "vitest";

import { loadFixture } from "./wireFixtures.test-helper";
import { ACTION_TYPES } from "../transport/protocol";

type ActionNamesFixture = { actions: string[]; excludedFromWebContract: string[] };

// action 名の C#⇔TS パリティ。C# 側 NUnit(WireContractActionNamesTest) が同じフィクスチャを照合する
// C#⇔TS parity for action names; the C# NUnit (WireContractActionNamesTest) matches the same fixture
describe("action names shared source (action_names.json)", () => {
  it("ACTION_TYPES が共有フィクスチャと一致する", () => {
    const shared = (loadFixture("action_names.json") as ActionNamesFixture).actions;
    expect(new Set(shared).size).toBe(shared.length);
    expect([...shared].sort()).toEqual([...ACTION_TYPES].sort());
  });

  it("web 契約外 action が ACTION_TYPES に混入していない", () => {
    const excluded = (loadFixture("action_names.json") as ActionNamesFixture).excludedFromWebContract;
    // 除外は C# 実装との照合用（playtest 等）。本番契約へ載せる時は actions へ移す
    // The excluded list exists to match C# implementations (playtest etc.); move entries to actions when they join the production contract
    expect(excluded.length).toBeGreaterThan(0);
    for (const name of excluded) expect(ACTION_TYPES).not.toContain(name);
  });
});
