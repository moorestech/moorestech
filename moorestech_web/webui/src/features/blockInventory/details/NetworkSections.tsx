import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { stopReasonText } from "./detailLogic";
import LackHighlightText from "./LackHighlightText";
import { useI18n } from "@/shared/i18n";

// 電力ネットワーク集約（uGUI ElectricNetworkInfoView 準拠。消費者0は需要なし表示）
// Electric network aggregate (mirrors uGUI ElectricNetworkInfoView; zero consumers shows a no-demand note)
export function ElectricNetworkSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  if (!data.electricNetwork) return null;
  const n = data.electricNetwork;
  return (
    <Stack gap={2} data-testid="electric-network-section">
      {n.consumerCount === 0 ? (
        <Text size="xs" c="var(--text-muted)">{t("需要なし")}</Text>
      ) : (
        <LackHighlightText
          label={t("発電 ")}
          current={n.totalGeneratePower.toFixed(0)}
          separator={t(" / 需要 ")}
          required={n.totalRequiredPower.toFixed(0)}
          suffix={t("（消費 {count}件, 供給率 {rate}%）", {
            count: n.consumerCount,
            rate: Math.round(n.powerRate * 100),
          })}
          insufficient={false}
          size="xs"
        />
      )}
    </Stack>
  );
}

// ギアネットワーク集約 + 停止理由（uGUI SetGearText の networkInfo 部準拠）
// Gear network aggregate with stop reason (mirrors the networkInfo part of uGUI SetGearText)
export function GearNetworkSection({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  if (!data.gearNetwork) return null;
  const n = data.gearNetwork;
  const reason = stopReasonText(n.stopReason);
  return (
    <Stack gap={2} data-testid="gear-network-section">
      <LackHighlightText label={t("供給 ")} current={n.totalGenerateGearPower.toFixed(0)} separator={t(" / 要求 ")} required={n.totalRequiredGearPower.toFixed(0)} insufficient={false} size="xs" />
      {reason !== "" && <Text size="xs" c="var(--text-insufficient)" data-testid="gear-stop-reason">{t(reason)}</Text>}
    </Stack>
  );
}
