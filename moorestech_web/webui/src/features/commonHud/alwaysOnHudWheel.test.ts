// 常時表示HUDが装備ホイールを殺さない条件を固定する
// Pins the conditions under which always-on HUDs never kill the equipment wheel
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

// ゲーム層に出しっぱなしのHUDは「入力を取らない(pointer-events:none)」か「ホイールを素通す印」のどちらかを必ず持つ
// Every HUD left on the game layer must either take no input (pointer-events:none) or carry the wheel pass-through mark
const passiveHuds: [string, string, string][] = [
  ["クロスヘア", "./style.module.css", ".crosshair"],
  ["現在チャレンジ", "../challenge/CurrentChallengeHud.module.css", ".hud"],
  ["配置モード", "../modeHud/style.module.css", ".placementHud"],
  ["削除モード警告", "../modeHud/style.module.css", ".deleteModeWarning"],
  ["列車搭乗", "../trainHud/style.module.css", ".hud"],
  ["ホットバー帯", "../hotbar/HotbarPanel/style.module.css", ".hotbarArea"],
  ["進捗バー", "../progress/style.module.css", ".wrapper"],
  ["カーソルツールチップ", "../../shared/tooltip/style.module.css", ".tooltip"],
];

describe("always-on HUD wheel contract", () => {
  it.each(passiveHuds)("%s は入力を奪わない", (_, path, selector) => {
    expect(ruleOf(read(path), selector)).toContain("pointer-events: none");
  });

  it("クリックを持つスロット列だけが入力を取り戻し、ホイールは素通しの印を持つ", () => {
    const css = read("../hotbar/HotbarPanel/style.module.css");
    const source = read("../hotbar/HotbarPanel/index.tsx");

    expect(ruleOf(css, ".hotbarFrame")).toContain("pointer-events: auto");
    // 印は実際のスロット列だけに付ける。全幅の帯へ戻すと何も無い場所まで実UI扱いになる
    // The mark belongs to the actual slot row; moving it back to the full-width band makes empty space count as real UI
    expect(source).toMatch(/className=\{styles\.hotbarFrame\}[^>]*data-wheel-passthrough/);
    expect(source).not.toMatch(/className=\{styles\.hotbarArea\}[^>]*data-wheel-passthrough/);
  });

  it("装備HUDは実UIのままホイールだけ素通す", () => {
    const source = read("../inventory/EquipmentPanel/index.tsx");

    expect(source).toMatch(/className=\{styles\.equipmentArea\}[^>]*data-wheel-passthrough/);
  });
});

function read(relativePath: string) {
  return readFileSync(new URL(relativePath, import.meta.url), "utf8");
}

// セレクタ単位で宣言を切り出し、別ルールの pointer-events を誤検出しない
// Slice declarations per selector so another rule's pointer-events cannot be mistaken for this one's
function ruleOf(css: string, selector: string) {
  const match = new RegExp(`\\${selector}\\s*\\{([^}]*)\\}`).exec(css);
  expect(match, `${selector} rule not found`).not.toBeNull();
  return match![1];
}
