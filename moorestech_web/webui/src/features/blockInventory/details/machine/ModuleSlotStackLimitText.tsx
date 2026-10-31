import { Text } from "@mantine/core";
import { Topics, useTopic } from "@/bridge";
import { L, useI18n } from "@/shared/i18n";

// モジュールスロット1つあたりの上限。配信が届くまでは数値を推測で出さない
// The per-module-slot limit; no number is guessed until the topic arrives
export default function ModuleSlotStackLimitText() {
  const { t } = useI18n();
  const upgrade = useTopic(Topics.machineUpgrade);
  if (upgrade === null) return null;
  return (
    <Text size="xs" c="dimmed" data-testid="machine-module-stack-limit">
      {t(L.ui.blockInventory.moduleSlotStackLimit, { limit: upgrade.moduleSlotStackLimit })}
    </Text>
  );
}
