import { Group, Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { ItemSlot, ProgressArrowBar } from "@/shared/ui";
import PowerRateText from "./rows/PowerRateText";
import PerMinuteRateRow from "./rows/PerMinuteRateRow";

// 採掘機: 採掘進捗 + 電力率 + 採掘中アイテムと分間数（uGUI MinerBlockInventoryView 準拠）
// Miner: mining progress, power rate, and currently mined items with per-minute rates (mirrors uGUI MinerBlockInventoryView)
export default function MinerSection({ data }: { data: BlockInventoryOpen }) {
  if (!data.miner) return null;
  // 歯車採掘機の不足赤は網の停止理由行が担う（ADR 0056）
  // A gear miner's shortage red belongs to the network stop-reason row (ADR 0056)
  const highlightPowerShortage = data.gear === undefined;
  return (
    <Stack gap="xs" data-testid="miner-section">
      <ProgressArrowBar value={data.progress ?? 0} />
      <PowerRateText currentPower={data.miner.currentPower} requestPower={data.miner.requestPower} highlightShortage={highlightPowerShortage} testId="miner-power-rate" />
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
