import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { gearRpmTranslationKey, gearTorqueTranslationKey } from "./detailLogic";
import { useI18n } from "@/shared/i18n";

// ギア: 消費/発生トルクとRPMの現在値報告。不足の赤は網停止理由行（GearNetworkSection）だけが担う（ADR 0056）
// Gear: reports current consumed/generated torque and RPM; only the network stop-reason row carries the insufficient tone (ADR 0056)
export default function GearSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  if (!data.gear) return null;
  const gear = data.gear;
  return (
    <Stack gap={2} data-testid="gear-section">
      <Text size="sm" c="var(--text-default)" data-testid="gear-torque">
        {t(gearTorqueTranslationKey(gear.role), { value: gear.currentTorque.toFixed(1) })}
      </Text>
      <Text size="sm" c="var(--text-default)" data-testid="gear-rpm">
        {t(gearRpmTranslationKey(gear.role), { current: gear.currentRpm.toFixed(1), base: gear.baseRpm.toFixed(1), value: gear.currentRpm.toFixed(1) })}
      </Text>
    </Stack>
  );
}
