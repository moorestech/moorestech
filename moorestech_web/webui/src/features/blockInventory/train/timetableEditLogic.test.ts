import { describe, expect, it } from "vitest";
import { addStop, canEnableAutoRun, moveStop, removeStop, stationKey, stationLabel, sameStopOrder, toReplacePayload, type Translator } from "./timetableEditLogic";

const a = { position: { x: 1, y: 0, z: 1 }, name: "A" };
const b = { position: { x: 2, y: 0, z: 2 }, name: "" };
const aStop = { ...a, side: "back" as const };
const bStop = { ...b, side: "back" as const };

describe("timetableEditLogic", () => {
  it("追加・削除・上下移動は新しいdraftを返し元を変えない", () => {
    const d0 = { stops: [] };
    const d1 = addStop(d0, a);
    const d2 = addStop(d1, b);
    expect(d0.stops).toHaveLength(0);
    expect(d2.stops.map((s) => stationKey(s.position))).toEqual(["1,0,1", "2,0,2"]);
    expect(moveStop(d2, 1, -1).stops[0]).toEqual(bStop);
    expect(moveStop(d2, 0, -1)).toBe(d2);
    expect(moveStop(d2, 1, 1)).toBe(d2);
    expect(removeStop(d2, 0).stops).toEqual([bStop]);
  });

  it("同じ駅を2回入れられる（循環で2度停まる時刻表を許す）", () => {
    const d = addStop(addStop({ stops: [] }, a), a);
    expect(d.stops).toHaveLength(2);
  });

  it("停車駅が無いと自動運転ONにできない", () => {
    expect(canEnableAutoRun([])).toBe(false);
    expect(canEnableAutoRun([aStop])).toBe(true);
  });

  it("適用ペイロードは座標と端を送る", () => {
    expect(toReplacePayload({ stops: [aStop, bStop] })).toEqual({
      stops: [
        { x: 1, y: 0, z: 1, side: "back" },
        { x: 2, y: 0, z: 2, side: "back" },
      ],
    });
  });

  it("adds new stops on the fixed UI side (back) and keeps existing sides", () => {
    const existing = { position: { x: 1, y: 0, z: 2 }, name: "A", side: "front" as const };
    const station = { position: { x: 5, y: 0, z: 6 }, name: "B" };
    const draft = addStop({ stops: [existing] }, station);
    expect(toReplacePayload(draft)).toEqual({
      stops: [
        { x: 1, y: 0, z: 2, side: "front" },
        { x: 5, y: 0, z: 6, side: "back" },
      ],
    });
  });
});

// 表示名は座標の識別性を保ち、同名でも別駅として扱う
// Display labels retain coordinates and distinguish stations with identical names
it("formats named and unnamed station labels with all coordinates", () => {
  const t: Translator = (key, values) => key.endsWith("timetableUnnamedStation")
    ? "駅"
    : `${values?.name} (${values?.x}, ${values?.y}, ${values?.z})`;
  expect(stationLabel(t, a)).toBe("A (1, 0, 1)");
  expect(stationLabel(t, b)).toBe("駅 (2, 0, 2)");
  expect(sameStopOrder([aStop, bStop], [bStop, aStop])).toBe(false);
  expect(sameStopOrder([aStop], [{ ...aStop, name: "renamed" }])).toBe(true);
  expect(sameStopOrder([aStop], [{ ...aStop, side: "front" }])).toBe(false);
});
