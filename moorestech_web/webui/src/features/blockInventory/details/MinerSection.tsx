import { Group, Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import { ItemSlot, ProgressArrow } from "@/shared/ui";
import { useBlockInteraction } from "../blockInteractionContext";
import { computePowerRate } from "./detailLogic";

// 採掘機: 採掘進捗 + 電力率 + 採掘中アイテムと分間数（uGUI MinerBlockInventoryView 準拠）
// Miner: mining progress, power rate, and currently mined items with per-minute rates (mirrors uGUI MinerBlockInventoryView)
export default function MinerSection({ data }: { data: BlockInventoryOpen }) {
  const { resolveName } = useBlockInteraction();
  if (!data.miner) return null;
  const powerRate = computePowerRate(data.miner.currentPower, data.miner.requestPower);
  const lacking = powerRate < 1;
  return (
    <Stack gap="xs" data-testid="miner-section">
      <ProgressArrow value={data.progress ?? 0} />
      <Text size="sm" c={lacking ? "red.5" : "dark.1"} data-testid="miner-power-rate">
        電力 {Math.round(powerRate * 100)}% ({data.miner.currentPower}/{data.miner.requestPower})
      </Text>
      <Group gap="xs" data-testid="miner-mining-items">
        {data.miner.miningItems.map((m) => (
          <Group key={m.itemId} gap={4}>
            <ItemSlot itemId={m.itemId} name={resolveName(m.itemId)} />
            <Text size="xs" c="dark.1">{m.itemsPerMinute.toFixed(1)}/分</Text>
          </Group>
        ))}
      </Group>
    </Stack>
  );
}
