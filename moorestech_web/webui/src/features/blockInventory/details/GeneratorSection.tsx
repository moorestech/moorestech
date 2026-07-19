import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { GaugeBar } from "@/shared/ui";
import { fuelRatio } from "./detailLogic";
import { useI18n } from "@/shared/i18n";

// 発電機: 残燃料バー + 稼働率（uGUI GeneratorBlockInventoryView 準拠。燃料スロットはビュー側グリッドが描画）
// Generator: remaining-fuel bar and operating rate (mirrors uGUI GeneratorBlockInventoryView; fuel slots render in the view grid)
export default function GeneratorSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  if (!data.generator) return null;
  const ratio = fuelRatio(data.generator.remainingFuelTime, data.generator.currentFuelTime);
  return (
    <Stack gap="xs" data-testid="generator-section">
      <GaugeBar value={ratio} testId="generator-fuel-progress" />
      <Text size="sm" c="var(--text-default)" data-testid="generator-operating-rate">
        {t("稼働率 {rate}%", { rate: Math.round(data.generator.operatingRate * 100) })}
      </Text>
    </Stack>
  );
}
