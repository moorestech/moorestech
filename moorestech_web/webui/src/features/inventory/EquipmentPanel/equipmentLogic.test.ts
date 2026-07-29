import { describe, expect, it } from "vitest";
import { accumulateWheelSteps, cycleEquipment } from "./equipmentLogic";

describe("cycleEquipment", () => {
  it("下方向で0→1→2→空(-1)→0と循環する", () => {
    expect(cycleEquipment(0, 1, 3)).toBe(1);
    expect(cycleEquipment(2, 1, 3)).toBe(-1); // -1 = 素手（空選択）
    expect(cycleEquipment(-1, 1, 3)).toBe(0);
  });
  it("上方向で逆順に循環する", () => {
    expect(cycleEquipment(0, -1, 3)).toBe(-1);
    expect(cycleEquipment(-1, -1, 3)).toBe(2);
  });
  it("スロット数が変わっても周期が追随する", () => {
    expect(cycleEquipment(0, 1, 1)).toBe(-1);
    expect(cycleEquipment(4, 1, 5)).toBe(-1);
    expect(cycleEquipment(-1, -1, 5)).toBe(4);
  });
  it("周期を超える delta も1周として畳み込む", () => {
    expect(cycleEquipment(0, 4, 3)).toBe(0);
    expect(cycleEquipment(0, -4, 3)).toBe(0);
  });
});

describe("accumulateWheelSteps", () => {
  it("小さい入力は累積し閾値を越えるまで切り替えない", () => {
    expect(accumulateWheelSteps(0, 40)).toEqual({ remainder: 0.4, steps: 0 });
    expect(accumulateWheelSteps(0.4, 70)).toEqual({ remainder: 0.10000000000000009, steps: 1 });
  });

  it("標準1ノッチ(±100)でちょうど1段切り替わる", () => {
    expect(accumulateWheelSteps(0, 100)).toEqual({ remainder: 0, steps: 1 });
    expect(accumulateWheelSteps(0, -100)).toEqual({ remainder: 0, steps: -1 });
  });

  it("大きい負入力は複数段を返して端数を残す", () => {
    expect(accumulateWheelSteps(0, -250)).toEqual({ remainder: -0.5, steps: -2 });
  });
});
