// ブロックUI群が許可済み共通部品と色トークンだけを使うことを固定する
// Locks block UI screens to approved shared components and color tokens
import { readFileSync } from "node:fs";
import { describe, expect, it } from "vitest";

const sources = {
  panel: read("./BlockInventoryPanel.tsx"),
  blockItemGrid: read("./BlockItemGrid.tsx"),
  generator: read("./details/GeneratorSection.tsx"),
  filterSplitter: read("./views/FilterSplitterInventory.tsx"),
  trainPlatform: read("./details/TrainPlatformSection.tsx"),
  electricToGear: read("./views/ElectricToGearInventory.tsx"),
  network: read("./details/NetworkSections.tsx"),
  miner: read("./details/MinerSection.tsx"),
};

const styles = {
  panel: read("./style.module.css"),
  gaugeBar: read("../../shared/ui/GaugeBar/style.module.css"),
  modeSwitch: read("../../shared/ui/ModeSwitch/style.module.css"),
  progressArrow: read("../../shared/ui/ProgressArrow/style.module.css"),
};

const appTokens = read("../../app/index.css");

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

  it("ブロックパネルをviewer領域へ上端揃えで配置する", () => {
    expect(styles.panel).toContain("grid-area: viewer");
    expect(styles.panel).toContain("justify-self: start");
    expect(styles.panel).toContain("align-self: start");
    expect(styles.panel).toContain("position: relative");
    expect(styles.panel).not.toContain("position: fixed");
    expect(styles.panel).not.toContain("translateX");
  });

  it("ブロックパネルだけに下部装飾の安全帯を確保する", () => {
    expect(appTokens).toContain("--block-panel-bottom-safe-area: 58px");
    expect(sources.panel).toContain('paddingBottom: "var(--block-panel-bottom-safe-area)"');
  });

  it("ゲージとモード切替を用途別に共通部品へ置換する", () => {
    expect(sources.generator).toContain("<GaugeBar");
    expect(sources.filterSplitter).toContain("<ModeSwitch");
    expect(sources.trainPlatform).toContain("<ModeSwitch");
    expect(sources.electricToGear).toContain("<ModeSwitch");
    expect(sources.electricToGear).toContain('orientation="vertical"');
    expect(sources.electricToGear).toContain("<GaugeBar");
  });

  it("ブロックスロットの列数を1列から9列の範囲で内容に追従させる", () => {
    expect(sources.blockItemGrid).toContain("cols={Math.min(9, Math.max(1, itemSlots.length))}");
  });

  it("フィルタ分岐器の方向を縦積みにする", () => {
    expect(sources.filterSplitter).toContain('<Stack gap="sm" data-testid="filter-splitter">');
    expect(sources.filterSplitter).not.toContain('<Group align="flex-start" gap="md" data-testid="filter-splitter">');
  });

  it("ゲージ系部品を寒色トークンで統一する", () => {
    expect(styles.gaugeBar).toContain("var(--gauge-outline-width)");
    expect(styles.gaugeBar).toContain("var(--bevel-c1)");
    expect(styles.progressArrow).toContain("var(--gauge-track)");
    expect(styles.progressArrow).toContain("var(--gauge-fill)");
    expect(styles.progressArrow).toContain("var(--gauge-outline-width)");
    expect(styles.progressArrow).not.toContain("--mantine-");
    expect(styles.progressArrow).not.toContain("green");
  });

  it("モード切替の間隔と選択面を専用トークンで制御する", () => {
    expect(styles.modeSwitch).toContain("var(--mode-switch-gap)");
    expect(styles.modeSwitch).toContain("var(--mode-switch-padding-block)");
    expect(styles.modeSwitch).toContain("var(--mode-switch-padding-inline)");
    expect(styles.modeSwitch).toContain("var(--mode-switch-selected-mix)");
  });

  it("MachineSectionはレシピ有りでタブ切替、電力率を共通フッタに置く", () => {
    const machineSection = read("./details/MachineSection.tsx");
    expect(machineSection).toContain("<ModeSwitch");
    expect(machineSection).toContain("<MachineRecipeSelectionTab");
    expect(machineSection).toContain("<MachineInventoryBody");
    expect(machineSection).toContain("<PowerRateText");
  });

  it("レシピ有り機械だけviewer〜items占有の大型パネルへ広げる", () => {
    expect(styles.panel).toContain("grid-column: viewer-start / items-end");
    expect(sources.panel).toContain("styles.panelLarge");
    expect(sources.panel).toContain("buildMachineRecipeSelectionRows");
  });

  it("レシピ選択タブは共通SlotGridの9列折返しで列挙する", () => {
    const recipeTab = read("./details/machine/MachineRecipeSelectionTab.tsx");
    expect(recipeTab).toContain("<SlotGrid cols={Math.min(9, Math.max(1, rows.length))}");
    expect(recipeTab).not.toMatch(/display:\s*grid/);
  });
});

function read(relativePath: string) {
  return readFileSync(new URL(relativePath, import.meta.url), "utf8");
}
