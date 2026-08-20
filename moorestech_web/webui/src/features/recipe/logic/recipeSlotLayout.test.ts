import { describe, expect, it } from "vitest";
import { MAX_SLOT_COLUMNS, recipeSlotLayout } from "./recipeSlotLayout";

// 列数が2で固定され行が伸びること自体が裁定の中身なので、点数ごとの出力文字列を明示値で押さえる
// The ruling is precisely that the column count stays at two while rows grow, so pin the emitted strings per count
describe("recipeSlotLayout", () => {
  it("列数は2で頭打ちになり、3点以上は行が増える", () => {
    expect(MAX_SLOT_COLUMNS).toBe(2);
    expect(recipeSlotLayout(1).gridTemplateColumns).toBe("repeat(1, auto)");
    expect(recipeSlotLayout(2).gridTemplateColumns).toBe("repeat(2, auto)");
    expect(recipeSlotLayout(3).gridTemplateColumns).toBe("repeat(2, auto)");
    expect(recipeSlotLayout(6).gridTemplateColumns).toBe("repeat(2, auto)");
  });

  // 0点でも不正CSS（repeat(0,…)・0除算）を出さない
  // A zero count must not emit invalid CSS (repeat(0, …) or a divide-by-zero)
  it("0点でも1列へ落として不正CSSを出さない", () => {
    const style = recipeSlotLayout(0) as Record<string, string>;
    expect(style.gridTemplateColumns).toBe("repeat(1, auto)");
    expect(style["--slot-size"]).not.toContain("/ 0)");
  });

  // 1列のときは列間隔を引かない。2列のときだけ1本ぶんのgapを差し引く
  // One column subtracts no gap; two columns subtract exactly one gap
  it("スロット寸法は列数ぶんのgapを差し引いて上限で頭打ちになる", () => {
    const one = recipeSlotLayout(1) as Record<string, string>;
    const two = recipeSlotLayout(4) as Record<string, string>;
    expect(one["--slot-size"]).toBe("min(var(--recipe-slot-size-max), calc((100cqw - 0 * var(--recipe-slot-gap)) / 1))");
    expect(two["--slot-size"]).toBe("min(var(--recipe-slot-size-max), calc((100cqw - 1 * var(--recipe-slot-gap)) / 2))");
  });
});
