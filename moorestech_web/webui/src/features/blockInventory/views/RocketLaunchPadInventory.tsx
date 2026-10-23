import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import RocketLaunchPadSection from "../details/rocket/RocketLaunchPadSection";

export default function RocketLaunchPadInventory({ data }: { data: BlockInventoryOpen }) {
  return (
    <Stack gap="sm" data-testid="rocket-launch-pad-view">
      <RocketLaunchPadSection data={data} />
    </Stack>
  );
}
