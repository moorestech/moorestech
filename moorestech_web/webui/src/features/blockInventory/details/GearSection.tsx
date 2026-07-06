import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/contract/payloadTypes";

// ギア: トルク/RPM の現在値と要求値（不足時赤）。uGUI SetGearText 準拠
// Gear: current vs required torque/RPM (red when lacking); mirrors uGUI SetGearText
export default function GearSection({ data }: { data: BlockInventoryOpen }) {
  if (!data.gear) return null;
  const torqueLack = data.gear.currentTorque < data.gear.baseTorque;
  const rpmLack = data.gear.currentRpm < data.gear.baseRpm;
  return (
    <Stack gap={2} data-testid="gear-section">
      <Text size="sm" c={torqueLack ? "red.5" : "dark.1"} data-testid="gear-torque">
        トルク {data.gear.currentTorque.toFixed(1)} / {data.gear.baseTorque.toFixed(1)}
      </Text>
      <Text size="sm" c={rpmLack ? "red.5" : "dark.1"} data-testid="gear-rpm">
        RPM {data.gear.currentRpm.toFixed(1)} / {data.gear.baseRpm.toFixed(1)}
      </Text>
    </Stack>
  );
}
