import { Group, Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { ItemSlot, ProgressArrow } from "@/shared/ui";
import PowerRateText from "./PowerRateText";

// 採掘機: 採掘進捗 + 電力率 + 採掘中アイテムと分間数（uGUI MinerBlockInventoryView 準拠）
// Miner: mining progress, power rate, and currently mined items with per-minute rates (mirrors uGUI MinerBlockInventoryView)
export default function MinerSection({ data }: { data: BlockInventoryOpen }) {
  if (!data.miner) return null;
  return (
    <Stack gap="xs" data-testid="miner-section">
      <ProgressArrow value={data.progress ?? 0} />
      <PowerRateText currentPower={data.miner.currentPower} requestPower={data.miner.requestPower} testId="miner-power-rate" />
      <Group gap="xs" data-testid="miner-mining-items">
        {data.miner.miningItems.map((m, i) => (
          <Group key={`${m.itemId}-${i}`} gap={4}>
            <ItemSlot itemId={m.itemId} />
            <Text size="xs" c="dark.1">{m.itemsPerMinute.toFixed(1)}/分</Text>
          </Group>
        ))}
      </Group>
    </Stack>
  );
}
