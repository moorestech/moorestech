// 縁取り様式を1箇所へ固定する（ADR 0033）
// Lock the outline style into a single place (ADR 0033)
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const read = (path: string) => readFileSync(new URL(path, import.meta.url), "utf8");

const tokens = read("../../app/tokens.css");
const itemSlotTsx = read("./ItemSlot/index.tsx");
const itemSlotCss = read("./ItemSlot/style.module.css");
const craftRecipeEntryTsx = read("../../features/recipe/views/CraftRecipeEntry.tsx");
const recipeBoxCss = read("../../features/recipe/views/RecipeBox.module.css");
const researchDetailTsx = read("../../features/research/ResearchDetailPane.tsx");
const researchCss = read("../../features/research/style.module.css");
const fluidSlotTsx = read("./FluidSlot/index.tsx");
const fluidSlotCss = read("./FluidSlot/style.module.css");
const hotbarTsx = read("../../features/hotbar/HotbarPanel/index.tsx");
const hotbarCss = read("../../features/hotbar/HotbarPanel/style.module.css");

describe("icon overlay text outline", () => {
  it("縁の太さと色を固定長トークンへ集約する", () => {
    expect(tokens).toContain("--icon-text-stroke-width: 2px;");
    expect(tokens).toContain("--icon-text-stroke-light:");
    expect(tokens).toContain("--icon-text-stroke-dark:");
    expect(tokens).not.toMatch(/--icon-text-stroke-width:\s*[\d.]+(?:em|rem|%)/);
  });

  it("縁を真のストロークで描く共有クラスを2本だけ持つ", () => {
    expect(tokens).toContain(".iconTextOutlineLight)");
    expect(tokens).toContain(".iconTextOutlineDark)");
    expect(tokens).toContain("paint-order: stroke fill;");
    expect(tokens.match(/-webkit-text-stroke:/g)).toHaveLength(2);
  });

  it("黒文字3系統が白縁の共有クラスを持ち、擬似縁を残さない", () => {
    expect(itemSlotTsx).toContain("iconTextOutlineLight");
    expect(craftRecipeEntryTsx).toContain("iconTextOutlineLight");
    expect(researchDetailTsx).toContain("iconTextOutlineLight");
    expect(itemSlotCss).not.toContain("text-shadow");
    expect(researchCss).not.toContain("text-shadow");
    // .craftButton の擬似太字は対象外
    // .craftButton's faux bold is out of scope
    expect(recipeBoxCss.match(/text-shadow:/g)).toHaveLength(1);
    expect(recipeBoxCss).toContain("rgb(0 40 80 / 55%)");
  });

  it("白文字2系統が黒縁の共有クラスを持ち、擬似縁を残さない", () => {
    expect(fluidSlotTsx).toContain("iconTextOutlineDark");
    expect(hotbarTsx).toContain("iconTextOutlineDark");
    expect(fluidSlotCss).not.toContain("text-shadow");
    expect(hotbarCss).not.toContain("text-shadow");
  });

  it("文字色は変更しない", () => {
    expect(tokens).toContain("--count-text: #111;");
    expect(fluidSlotCss).toContain("color: var(--mantine-color-white);");
    expect(hotbarCss).toContain("color: #e2e5ee;");
  });
});
