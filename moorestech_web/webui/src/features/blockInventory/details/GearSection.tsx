import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { gearRowDisplay } from "./detailLogic";
import { useI18n } from "@/shared/i18n";

// ギア: 消費/発生トルクとRPMの現在値報告。不足の赤は網停止理由行（GearNetworkSection）だけが担う（ADR 0056）
// Gear: reports current consumed/generated torque and RPM; only the network stop-reason row carries the insufficient tone (ADR 0056)
export default function GearSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  if (!data.gear) return null;
  const row = gearRowDisplay(data.gear);
  return (
    <Stack gap={2} data-testid="gear-section">
      <Text size="sm" c="var(--text-default)" data-testid="gear-torque">
        {t(row.torqueKey, row.torqueParams)}
      </Text>
      <Text size="sm" c="var(--text-default)" data-testid="gear-rpm">
        {t(row.rpmKey, row.rpmParams)}
      </Text>
    </Stack>
  );
}
