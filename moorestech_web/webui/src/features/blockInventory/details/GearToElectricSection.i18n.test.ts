import { readFileSync } from "node:fs";
import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, expect, it, vi } from "vitest";
import type { BlockInventoryOpen } from "@/bridge";
import { setDictionaries } from "@/shared/i18n/i18nStore";
// CSV生成器のパーサーでLocalization正本を読む
// Read the canonical Localization CSV with its generator's parser
// @ts-expect-error -- The build script is intentionally a plain ESM module.
import { parseLocalizationCsv } from "../../../../scripts/generate-localization-keys.mjs";

vi.mock("@mantine/core", () => ({
  Stack: ({ children, ...props }: { children: unknown }) => createElement("mock-stack", props, children as never),
  Text: ({ children, ...props }: { children: unknown }) => createElement("mock-text", props, children as never),
}));

import GearToElectricSection from "./GearToElectricSection";

describe("GearToElectricSection with the Japanese dictionary", () => {
  it("shows the specified supplied-power sentence after real i18n interpolation", () => {
    const csvPath = new URL("../../../../../../Localization/localization.csv", import.meta.url);
    const csv = parseLocalizationCsv(readFileSync(csvPath, "utf8"));
    const japaneseIndex = csv.languageCodes.indexOf("japanese");
    const englishIndex = csv.languageCodes.indexOf("english");
    expect(japaneseIndex).toBeGreaterThanOrEqual(0);
    expect(englishIndex).toBeGreaterThanOrEqual(0);
    const japanese = Object.fromEntries(csv.rows.map((row: { key: string; texts: string[] }) => [row.key, row.texts[japaneseIndex]]));
    const english = Object.fromEntries(csv.rows.map((row: { key: string; texts: string[] }) => [row.key, row.texts[englishIndex]]));
    const source = Object.fromEntries(csv.rows.map((row: { key: string; source: string }) => [row.key, row.source]));
    setDictionaries("japanese", japanese, english, source);

    // 実コンポーネントに数値を渡し、実辞書と補間器の結果を読む
    // Render the real component and read the result of the real dictionary and interpolator
    const data = {
      open: true, source: "block", blockType: "GearToElectricGenerator", identifier: "block:1",
      blockGuid: "g", itemSlots: [], fluidSlots: [],
      gearToElectric: { generationState: "generating", suppliedPower: 72.4, maxGeneratedPower: 100 },
    } as unknown as BlockInventoryOpen;
    let tree!: ReturnType<typeof create>;
    act(() => { tree = create(createElement(GearToElectricSection, { data })); });
    const power = tree.root.findByProps({ "data-testid": "gear-to-electric-supplied-power" });
    expect(power.props.children).toBe("供給電力 72 / 最大 100");
  });
});
