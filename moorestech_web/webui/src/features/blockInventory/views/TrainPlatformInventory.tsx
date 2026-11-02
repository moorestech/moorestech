import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import FluidInventoryContents from "../fluid/FluidInventoryContents";
import BlockItemGrid from "../BlockItemGrid";
import TrainPlatformSection from "../details/TrainPlatformSection";

import TrainStationNameSection from "../details/station/TrainStationNameSection";
import { allSlotIndices } from "../details/detailLogic";

const ITEM_SLOTS_TEST_ID = "train-platform-item-slots";
const FLUID_SLOTS_TEST_ID = "train-platform-fluid-slots";

export default function TrainPlatformInventory({ data }: { data: BlockInventoryOpen }) {
  const hasItemSlots = data.trainPlatform?.itemSlotCount !== undefined;

  return (
    <Stack gap="sm" data-testid="train-platform-view">
      <TrainStationNameSection data={data} />
      {hasItemSlots ? (
        <BlockItemGrid itemSlots={data.itemSlots} slotIndices={allSlotIndices(data.itemSlots.length)} testId={ITEM_SLOTS_TEST_ID} />
      ) : null}
      {data.fluidSlots.length > 0 ? (
        <FluidInventoryContents fluids={data.fluidSlots} testId={FLUID_SLOTS_TEST_ID} />
      ) : null}
      <TrainPlatformSection data={data} />
    </Stack>
  );
}
