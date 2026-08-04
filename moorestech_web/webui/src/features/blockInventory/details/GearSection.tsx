import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import LackHighlightText from "./LackHighlightText";
import { L, useI18n } from "@/shared/i18n";

// ギア: トルク/RPM の現在値と要求値（不足時赤）。uGUI SetGearText 準拠
// Gear: current vs required torque/RPM (red when lacking); mirrors uGUI SetGearText
export default function GearSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  if (!data.gear) return null;
  const torqueLack = data.gear.currentTorque < data.gear.baseTorque;
  const rpmLack = data.gear.currentRpm < data.gear.baseRpm;
  return (
    <Stack gap={2} data-testid="gear-section">
      <LackHighlightText insufficient={torqueLack} size="sm" testId="gear-torque">
        {t(L.ui.blockInventory.gearTorqueSummary, {
          current: data.gear.currentTorque.toFixed(1),
          required: data.gear.baseTorque.toFixed(1),
        })}
      </LackHighlightText>
      <LackHighlightText insufficient={rpmLack} size="sm" testId="gear-rpm">
        {t(L.ui.blockInventory.gearRpmSummary, {
          current: data.gear.currentRpm.toFixed(1),
          required: data.gear.baseRpm.toFixed(1),
        })}
      </LackHighlightText>
    </Stack>
  );
}
