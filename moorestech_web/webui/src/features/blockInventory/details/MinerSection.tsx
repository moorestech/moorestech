import { Group, Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { ItemSlot, ProgressArrowBar } from "@/shared/ui";
import PowerRateText from "./rows/PowerRateText";
import PerMinuteRateRow from "./rows/PerMinuteRateRow";

// 採掘機: 採掘進捗 + 電力率 + 採掘中アイテムと分間数（uGUI MinerBlockInventoryView 準拠）
// Miner: mining progress, power rate, and currently mined items with per-minute rates (mirrors uGUI MinerBlockInventoryView)
export default function MinerSection({ data }: { data: BlockInventoryOpen }) {
  if (!data.miner) return null;
  return (
    <Stack gap="xs" data-testid="miner-section">
      <ProgressArrowBar value={data.progress ?? 0} />
      <PowerRateText currentPower={data.miner.currentPower} requestPower={data.miner.requestPower} testId="miner-power-rate" />
      <Group gap="xs" data-testid="miner-mining-items">
        {data.miner.miningItems.map((m, i) => (
          <PerMinuteRateRow key={`${m.itemId}-${i}`} amountPerMinute={m.itemsPerMinute}>
            <ItemSlot itemId={m.itemId} />
          </PerMinuteRateRow>
        ))}
      </Group>
    </Stack>
  );
}
