// 縁取り様式を1箇所へ固定する（ADR 0033）
// Lock the outline style into a single place (ADR 0033)
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const read = (path: string) => readFileSync(new URL(path, import.meta.url), "utf8");

const tokens = read("./tokens.css");
const itemSlotTsx = read("../shared/ui/ItemSlot/index.tsx");
const itemSlotCss = read("../shared/ui/ItemSlot/style.module.css");
const craftRecipeEntryTsx = read("../features/recipe/views/CraftRecipeEntry.tsx");
const researchDetailTsx = read("../features/research/ResearchDetailPane.tsx");
const fluidSlotTsx = read("../shared/ui/FluidSlot/index.tsx");
const fluidSlotCss = read("../shared/ui/FluidSlot/style.module.css");
const hotbarTsx = read("../features/hotbar/HotbarPanel/index.tsx");
const hotbarCss = read("../features/hotbar/HotbarPanel/style.module.css");

describe("icon overlay text outline", () => {
  it("縁の太さと色を固定長トークンへ集約する", () => {
    expect(tokens).toContain("--icon-text-stroke-width: 2px;");
    expect(tokens).toContain("--icon-text-stroke-light: #fff;");
    expect(tokens).toContain("--icon-text-stroke-dark: #000;");
    expect(tokens).not.toMatch(/--icon-text-stroke-width:\s*[\d.]+(?:em|rem|%)/);
  });

  it("縁を真のストロークで描く共有クラスを2本だけ持つ", () => {
    expect(tokens).toContain(".iconTextOutlineLight)");
    expect(tokens).toContain(".iconTextOutlineDark)");
    expect(tokens.match(/paint-order: stroke fill;/g)).toHaveLength(2);
    expect(tokens.match(/-webkit-text-stroke:/g)).toHaveLength(2);
  });

  // 共有クラスと縁色トークンの対応を固定し白文字に白縁の取り違えを検知する
  // Pin each shared class to its own stroke token so a light/dark swap cannot pass
  it("共有クラスがそれぞれ反対色の縁トークンだけを引く", () => {
    expect(tokens).toMatch(/:where\(\.iconTextOutlineLight\)\s*\{[^}]*var\(--icon-text-stroke-light\)/);
    expect(tokens).toMatch(/:where\(\.iconTextOutlineDark\)\s*\{[^}]*var\(--icon-text-stroke-dark\)/);
    expect(tokens).not.toMatch(/:where\(\.iconTextOutlineLight\)\s*\{[^}]*--icon-text-stroke-dark/);
    expect(tokens).not.toMatch(/:where\(\.iconTextOutlineDark\)\s*\{[^}]*--icon-text-stroke-light/);
  });

  // 素材の所持/必要は3系統ともItemSlotのshortageへ集約済みで、縁の合成もそこ1箇所が持つ
  // All three material owned/required usages now live in ItemSlot's shortage, which composes the outline once
  it("黒文字が白縁の共有クラスを持ち、擬似縁を残さない", () => {
    expect(itemSlotTsx.match(/iconTextOutlineLight/g)).toHaveLength(2);
    expect(craftRecipeEntryTsx).not.toContain("iconTextOutlineLight");
    expect(researchDetailTsx).not.toContain("iconTextOutlineLight");
    expect(researchDetailTsx).toContain("shortage=");
    expect(itemSlotCss).not.toMatch(/\.count\s*\{[^}]*text-shadow/);
    expect(itemSlotCss).not.toMatch(/\.shortageCount\s*\{[^}]*text-shadow/);
  });

  it("白文字2系統が黒縁の共有クラスを持ち、擬似縁を残さない", () => {
    expect(fluidSlotTsx).toContain("iconTextOutlineDark");
    expect(hotbarTsx).toContain("iconTextOutlineDark");
    expect(fluidSlotCss).not.toMatch(/\.amount\s*\{[^}]*text-shadow/);
    expect(hotbarCss).not.toMatch(/\.num\s*\{[^}]*text-shadow/);
  });

  it("文字色は変更しない", () => {
    expect(tokens).toContain("--count-text: #111;");
    expect(fluidSlotCss).toContain("color: var(--mantine-color-white);");
    expect(hotbarCss).toContain("color: #e2e5ee;");
  });
});
