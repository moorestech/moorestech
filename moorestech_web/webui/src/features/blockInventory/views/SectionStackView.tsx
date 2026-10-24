import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { FluidSlotRow } from "@/shared/ui";
import FluidInventoryContents from "../fluid/FluidInventoryContents";
import BlockItemGrid from "../BlockItemGrid";
import styles from "../style.module.css";
import GearSection from "../details/GearSection";
import GearToElectricSection from "../details/GearToElectricSection";
import GeneratorSection from "../details/GeneratorSection";
import MachineSection from "../details/MachineSection";
import MinerSection from "../details/MinerSection";
import BeltThroughputSection from "../details/BeltThroughputSection";
import CleanRoomSection from "../details/cleanRoom/CleanRoomSection";
import CleanRoomYieldSection from "../details/cleanRoom/CleanRoomYieldSection";
import MachineInputStallSection from "../details/machineInputStall/MachineInputStallSection";
import PumpSection from "../details/pump/PumpSection";
import { ElectricNetworkSection, GearNetworkSection } from "../details/NetworkSections";

type SectionStackViewConfig = {
  itemGridTestId: string | null;
  fluidRowTestId: string | null;
  renderEmptyGrid: boolean;
  showFluidProgress: boolean;
  showFluidDetails: boolean;
};

const configByBlockType: Record<string, SectionStackViewConfig> = {
  FluidPipe: { itemGridTestId: null, fluidRowTestId: "fluid-pipe-fluid-slots", renderEmptyGrid: false, showFluidProgress: false, showFluidDetails: true },
  Chest: { itemGridTestId: "chest-grid", fluidRowTestId: null, renderEmptyGrid: true, showFluidProgress: true, showFluidDetails: false },
  ElectricMachine: { itemGridTestId: null, fluidRowTestId: "machine-fluid-slots", renderEmptyGrid: false, showFluidProgress: false, showFluidDetails: false },
  GearMachine: { itemGridTestId: null, fluidRowTestId: "machine-fluid-slots", renderEmptyGrid: false, showFluidProgress: false, showFluidDetails: false },
  ElectricGenerator: { itemGridTestId: "generator-fuel-grid", fluidRowTestId: null, renderEmptyGrid: true, showFluidProgress: true, showFluidDetails: false },
  FuelGearGenerator: { itemGridTestId: "generator-fuel-grid", fluidRowTestId: null, renderEmptyGrid: true, showFluidProgress: true, showFluidDetails: false },
  SimpleGearGenerator: { itemGridTestId: "generator-fuel-grid", fluidRowTestId: null, renderEmptyGrid: true, showFluidProgress: true, showFluidDetails: false },
  GearToElectricGenerator: { itemGridTestId: null, fluidRowTestId: null, renderEmptyGrid: false, showFluidProgress: false, showFluidDetails: false },
  ElectricMiner: { itemGridTestId: "miner-output-grid", fluidRowTestId: null, renderEmptyGrid: true, showFluidProgress: true, showFluidDetails: false },
  GearMiner: { itemGridTestId: "gear-miner-output-grid", fluidRowTestId: null, renderEmptyGrid: true, showFluidProgress: true, showFluidDetails: false },
  ElectricPump: { itemGridTestId: null, fluidRowTestId: "pump-fluid-slots", renderEmptyGrid: false, showFluidProgress: false, showFluidDetails: false },
  GearPump: { itemGridTestId: null, fluidRowTestId: "pump-fluid-slots", renderEmptyGrid: false, showFluidProgress: false, showFluidDetails: false },
  CleanRoomMachine: { itemGridTestId: null, fluidRowTestId: "machine-fluid-slots", renderEmptyGrid: false, showFluidProgress: false, showFluidDetails: false },
  CleanRoomAirFilter: { itemGridTestId: "clean-room-filter-grid", fluidRowTestId: null, renderEmptyGrid: true, showFluidProgress: false, showFluidDetails: false },
};

const genericConfig: SectionStackViewConfig = {
  itemGridTestId: "generic-block-grid",
  fluidRowTestId: "generic-block-fluids",
  renderEmptyGrid: false,
  showFluidProgress: true,
  showFluidDetails: false,
};

function resolveSectionStackViewConfig(blockType: string): SectionStackViewConfig {
  return configByBlockType[blockType] ?? genericConfig;
}

// 標準ブロック表示をデータ有無で合成し、固有UIだけをレジストリへ残す
// Compose standard block sections from available data, leaving only unique UIs in the registry
export default function SectionStackView({ data, fillsPanelHeight }: { data: BlockInventoryOpen; fillsPanelHeight: boolean }) {
  const config = resolveSectionStackViewConfig(data.blockType);
  const itemGridTestId = config.itemGridTestId;
  const showItemGrid = itemGridTestId !== null && (config.renderEmptyGrid || data.itemSlots.length > 0);

  return (
    // 高さ確定面でだけ伸ばす。高さ自動の面で伸ばすと子が内容高より縮む経路が開く
    // Stretch only on the height-determining panel; on auto-height panels it would let children shrink below their content
    <Stack className={fillsPanelHeight ? styles.fillPanelHeight : undefined} gap="sm">
      <CleanRoomSection data={data} />
      <CleanRoomYieldSection data={data} />
      <MachineInputStallSection data={data} />
      {showItemGrid ? <BlockItemGrid itemSlots={data.itemSlots} testId={itemGridTestId} /> : null}
      {data.machine ? <MachineSection data={data} machine={data.machine} fillsPanelHeight={fillsPanelHeight} /> : null}
      <MinerSection data={data} />
      <PumpSection data={data} />
      <GeneratorSection data={data} />
      <GearToElectricSection data={data} />
      <BeltThroughputSection data={data} />
      <GearSection data={data} />
      <ElectricNetworkSection data={data} />
      <GearNetworkSection data={data} />
      {config.showFluidDetails && config.fluidRowTestId ? <FluidInventoryContents fluids={data.fluidSlots} testId={config.fluidRowTestId} /> : null}
      {config.fluidRowTestId && !data.machine && !config.showFluidDetails ? <FluidSlotRow fluids={data.fluidSlots.map((fluid) => ({ fluid }))} progress={config.showFluidProgress ? data.progress : null} testId={config.fluidRowTestId} /> : null}
    </Stack>
  );
}
