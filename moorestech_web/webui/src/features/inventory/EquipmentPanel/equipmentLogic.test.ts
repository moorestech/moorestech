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
    expect(accumulateWheelSteps(0, 40, 0)).toEqual({ remainder: 0.4, steps: 0 });
    expect(accumulateWheelSteps(0.4, 70, 0)).toEqual({ remainder: 0, steps: 1 });
  });

  it("標準1ノッチ(±100)でちょうど1段切り替わる", () => {
    expect(accumulateWheelSteps(0, 100, 0)).toEqual({ remainder: 0, steps: 1 });
    expect(accumulateWheelSteps(0, -100, 0)).toEqual({ remainder: 0, steps: -1 });
  });

  it("スクロール加速で膨らんだ1ノッチでも1段しか進まない", () => {
    expect(accumulateWheelSteps(0, 300, 0)).toEqual({ remainder: 0, steps: 1 });
    expect(accumulateWheelSteps(0, -250, 0)).toEqual({ remainder: 0, steps: -1 });
  });

  it("逆回転では順方向の端数を捨ててから判定する", () => {
    expect(accumulateWheelSteps(0.9, -20, 0)).toEqual({ remainder: -0.2, steps: 0 });
    expect(accumulateWheelSteps(0.9, -100, 0)).toEqual({ remainder: 0, steps: -1 });
  });

  it("line/page単位のdeltaModeでも1ノッチが1段になる", () => {
    expect(accumulateWheelSteps(0, 3, 1)).toEqual({ remainder: 0, steps: 1 });
    expect(accumulateWheelSteps(0, -1, 2)).toEqual({ remainder: 0, steps: -1 });
  });
});
