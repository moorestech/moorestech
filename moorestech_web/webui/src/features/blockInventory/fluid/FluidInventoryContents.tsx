import { Group, Stack, Text } from "@mantine/core";
import type { FluidSlotData } from "@/bridge";
import { fluidNameKey, useI18n } from "@/shared/i18n";
import { FluidSlotRow } from "@/shared/ui";
import { formatSlotAmount } from "@/shared/ui/slotAmountFormat";

// 閲覧専用の液体在庫は名前と容量を常時表示する
// Read-only fluid inventories always display their names and capacities
export default function FluidInventoryContents({ fluids, testId }: { fluids: FluidSlotData[]; testId: string }) {
  const { t } = useI18n();
  if (fluids.length === 0) return null;
  return (
    <Stack gap="xs" data-testid={testId}>
      {fluids.map((fluid, index) => (
        <Group key={index} gap="sm" align="center" wrap="nowrap">
          <FluidSlotRow fluids={[{ fluid }]} progress={null} testId={`${testId}-slot-${index}`} />
          {fluid.kind === "filled" ? (
            <Stack gap={0}>
              <Text size="sm">{t(fluidNameKey(fluid.fluidGuid))}</Text>
              <Text size="sm">{formatSlotAmount(fluid.amount)} / {formatSlotAmount(fluid.capacity)}</Text>
            </Stack>
          ) : null}
        </Group>
      ))}
    </Stack>
  );
}
