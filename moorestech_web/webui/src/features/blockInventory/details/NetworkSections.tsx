import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/contract/payloadTypes";
import { stopReasonText } from "./detailLogic";

// 電力ネットワーク集約（uGUI ElectricNetworkInfoView 準拠。消費者0は需要なし表示）
// Electric network aggregate (mirrors uGUI ElectricNetworkInfoView; zero consumers shows a no-demand note)
export function ElectricNetworkSection({ data }: { data: BlockInventoryOpen }) {
  if (!data.electricNetwork) return null;
  const n = data.electricNetwork;
  return (
    <Stack gap={2} data-testid="electric-network-section">
      {n.consumerCount === 0 ? (
        <Text size="xs" c="dark.2">需要なし</Text>
      ) : (
        <Text size="xs" c="dark.1">
          発電 {n.totalGeneratePower.toFixed(0)} / 需要 {n.totalRequiredPower.toFixed(0)}（消費 {n.consumerCount}件, 供給率 {Math.round(n.powerRate * 100)}%）
        </Text>
      )}
    </Stack>
  );
}

// ギアネットワーク集約 + 停止理由（uGUI SetGearText の networkInfo 部準拠）
// Gear network aggregate with stop reason (mirrors the networkInfo part of uGUI SetGearText)
export function GearNetworkSection({ data }: { data: BlockInventoryOpen }) {
  if (!data.gearNetwork) return null;
  const n = data.gearNetwork;
  const reason = stopReasonText(n.stopReason);
  return (
    <Stack gap={2} data-testid="gear-network-section">
      <Text size="xs" c="dark.1">
        供給 {n.totalGenerateGearPower.toFixed(0)} / 要求 {n.totalRequiredGearPower.toFixed(0)}
      </Text>
      {reason !== "" && <Text size="xs" c="red.5" data-testid="gear-stop-reason">{reason}</Text>}
    </Stack>
  );
}
