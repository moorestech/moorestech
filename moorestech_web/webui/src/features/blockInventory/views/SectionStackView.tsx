import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { FluidSlotRow } from "@/shared/ui";
import BlockItemGrid from "../BlockItemGrid";
import GearSection from "../details/GearSection";
import GeneratorSection from "../details/GeneratorSection";
import MachineSection from "../details/MachineSection";
import MinerSection from "../details/MinerSection";
import { ElectricNetworkSection, GearNetworkSection } from "../details/NetworkSections";

type SectionStackViewConfig = {
  itemGridTestId: string | null;
  fluidRowTestId: string | null;
  renderEmptyGrid: boolean;
  showFluidProgress: boolean;
};

const configByBlockType: Record<string, SectionStackViewConfig> = {
  Chest: { itemGridTestId: "chest-grid", fluidRowTestId: null, renderEmptyGrid: true, showFluidProgress: true },
  ElectricMachine: { itemGridTestId: null, fluidRowTestId: "machine-fluid-slots", renderEmptyGrid: false, showFluidProgress: false },
  GearMachine: { itemGridTestId: null, fluidRowTestId: "machine-fluid-slots", renderEmptyGrid: false, showFluidProgress: false },
  ElectricGenerator: { itemGridTestId: "generator-fuel-grid", fluidRowTestId: null, renderEmptyGrid: true, showFluidProgress: true },
  FuelGearGenerator: { itemGridTestId: "generator-fuel-grid", fluidRowTestId: null, renderEmptyGrid: true, showFluidProgress: true },
  SimpleGearGenerator: { itemGridTestId: "generator-fuel-grid", fluidRowTestId: null, renderEmptyGrid: true, showFluidProgress: true },
  ElectricMiner: { itemGridTestId: "miner-output-grid", fluidRowTestId: null, renderEmptyGrid: true, showFluidProgress: true },
  GearMiner: { itemGridTestId: "gear-miner-output-grid", fluidRowTestId: null, renderEmptyGrid: true, showFluidProgress: true },
};

const genericConfig: SectionStackViewConfig = {
  itemGridTestId: "generic-block-grid",
  fluidRowTestId: "generic-block-fluids",
  renderEmptyGrid: false,
  showFluidProgress: true,
};

export function resolveSectionStackViewConfig(blockType: string): SectionStackViewConfig {
  return configByBlockType[blockType] ?? genericConfig;
}

// 標準ブロック表示をデータ有無で合成し、固有UIだけをレジストリへ残す
// Compose standard block sections from available data, leaving only unique UIs in the registry
export default function SectionStackView({ data }: { data: BlockInventoryOpen }) {
  const config = resolveSectionStackViewConfig(data.blockType);
  const itemGridTestId = config.itemGridTestId;
  const showItemGrid = itemGridTestId !== null && (config.renderEmptyGrid || data.itemSlots.length > 0);

  return (
    <Stack gap="sm">
      {showItemGrid ? <BlockItemGrid itemSlots={data.itemSlots} testId={itemGridTestId} /> : null}
      <MachineSection data={data} />
      <MinerSection data={data} />
      <GeneratorSection data={data} />
      <GearSection data={data} />
      <ElectricNetworkSection data={data} />
      <GearNetworkSection data={data} />
      {config.fluidRowTestId && !data.machine ? <FluidSlotRow fluids={data.fluidSlots} progress={config.showFluidProgress ? data.progress : null} testId={config.fluidRowTestId} /> : null}
    </Stack>
  );
}
