import type { ReactNode } from "react";
import { Group, Text } from "@mantine/core";
import { L, useI18n } from "@/shared/i18n";

// アイコンと分間レートの組。採掘機とポンプが同じ行を共有し、表記の変更が片側だけに効かないようにする
// An icon paired with its per-minute rate; the miner and the pump share it so a wording change can never land on one side only
export default function PerMinuteRateRow({ amountPerMinute, children }: { amountPerMinute: number; children: ReactNode }) {
  const { t } = useI18n();
  return (
    <Group gap={4}>
      {children}
      <Text size="xs" c="var(--text-default)">
        {t(L.ui.blockInventory.itemsPerMinute, { itemsPerMinute: amountPerMinute.toFixed(1) })}
      </Text>
    </Group>
  );
}
