import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { FluidSlotRow } from "@/shared/ui";
import BlockItemGrid from "../BlockItemGrid";
import TrainPlatformSection from "../details/TrainPlatformSection";

const ITEM_SLOTS_TEST_ID = "train-platform-item-slots";
const FLUID_SLOTS_TEST_ID = "train-platform-fluid-slots";

export default function TrainPlatformInventory({ data }: { data: BlockInventoryOpen }) {
  const hasItemSlots = data.trainPlatform?.itemSlotCount !== undefined;

  return (
    <Stack gap="sm" data-testid="train-platform-view">
      {hasItemSlots ? (
        <BlockItemGrid itemSlots={data.itemSlots} testId={ITEM_SLOTS_TEST_ID} />
      ) : null}
      {data.fluidSlots.length > 0 ? (
        <FluidSlotRow fluids={data.fluidSlots} progress={null} testId={FLUID_SLOTS_TEST_ID} />
      ) : null}
      <TrainPlatformSection data={data} />
    </Stack>
  );
}
