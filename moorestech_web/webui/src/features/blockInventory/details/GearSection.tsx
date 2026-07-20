import { Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import LackHighlightText from "./LackHighlightText";
import { useI18n } from "@/shared/i18n";

// ギア: トルク/RPM の現在値と要求値（不足時赤）。uGUI SetGearText 準拠
// Gear: current vs required torque/RPM (red when lacking); mirrors uGUI SetGearText
export default function GearSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  if (!data.gear) return null;
  const torqueLack = data.gear.currentTorque < data.gear.baseTorque;
  const rpmLack = data.gear.currentRpm < data.gear.baseRpm;
  return (
    <Stack gap={2} data-testid="gear-section">
      <LackHighlightText label={t("トルク ")} current={data.gear.currentTorque.toFixed(1)} separator={t(" / ")} required={data.gear.baseTorque.toFixed(1)} insufficient={torqueLack} size="sm" testId="gear-torque" />
      <LackHighlightText label={t("RPM ")} current={data.gear.currentRpm.toFixed(1)} separator={t(" / ")} required={data.gear.baseRpm.toFixed(1)} insufficient={rpmLack} size="sm" testId="gear-rpm" />
    </Stack>
  );
}
