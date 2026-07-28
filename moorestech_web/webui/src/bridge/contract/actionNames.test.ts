import { describe, it, expect } from "vitest";

import { loadFixture } from "./wireFixtures.test-helper";
import { ACTION_TYPES } from "../transport/protocol";

// action 名の C#⇔TS パリティ。C# 側 NUnit(WireContractActionNamesTest) が同じフィクスチャを照合する
// C#⇔TS parity for action names; the C# NUnit (WireContractActionNamesTest) matches the same fixture
describe("action names shared source (action_names.json)", () => {
  it("ACTION_TYPES が共有フィクスチャと一致する", () => {
    const shared = (loadFixture("action_names.json") as { actions: string[] }).actions;
    expect(new Set(shared).size).toBe(shared.length);
    expect([...shared].sort()).toEqual([...ACTION_TYPES].sort());
  });
});
