// ブロックUI群が許可済み共通部品と色トークンだけを使うことを固定する
// Locks block UI screens to approved shared components and color tokens
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const sources = {
  panel: read("./BlockInventoryPanel.tsx"),
  generator: read("./details/GeneratorSection.tsx"),
  filterSplitter: read("./views/FilterSplitterInventory.tsx"),
  trainPlatform: read("./details/TrainPlatformSection.tsx"),
  electricToGear: read("./views/ElectricToGearInventory.tsx"),
  network: read("./details/NetworkSections.tsx"),
  miner: read("./details/MinerSection.tsx"),
};

describe("block inventory design whitelist", () => {
  it.each(Object.entries(sources))("%sから禁止Mantine部品とテーマ色を除去する", (_, source) => {
    expect(source).not.toMatch(/<(?:Paper|Button|CloseButton|SegmentedControl|Radio(?:\.Group)?|Progress|Slider)\b/);
    expect(source).not.toMatch(/(?:c|color|bg)="(?:dark\.\d|red(?:\.\d)?|orange)"/);
  });

  it("パネル面と閉じる操作をshared uiへ集約する", () => {
    expect(sources.panel).toContain("<GamePanel");
    expect(sources.panel).toContain('variant="default"');
    expect(sources.panel).toContain("<PanelCloseButton");
  });

  it("ゲージとモード切替を用途別に共通部品へ置換する", () => {
    expect(sources.generator).toContain("<GaugeBar");
    expect(sources.filterSplitter).toContain("<ModeSwitch");
    expect(sources.trainPlatform).toContain("<ModeSwitch");
    expect(sources.electricToGear).toContain("<ModeSwitch");
    expect(sources.electricToGear).toContain('orientation="vertical"');
    expect(sources.electricToGear).toContain("<GaugeBar");
  });

  it("MachineSectionへレシピ選択行を組み込む", () => {
    const machineSection = read("./details/MachineSection.tsx");
    expect(machineSection).toContain("<MachineRecipeSelectionRow");
  });
});

function read(relativePath: string) {
  return readFileSync(new URL(relativePath, import.meta.url), "utf8");
}
