import { Button, Group, Stack, Text } from "@mantine/core";
import { dispatchAction, readTopic, Topics } from "@/bridge";
import type { BlockInventoryOpen } from "@/bridge";
import { ItemSlot } from "@/shared/ui";
import { filterSlotClickAction, modeLabel, nextMode } from "../filterSplitterLogic";
import { useI18n } from "@/shared/i18n";

// 方向別のフィルタ操作ビュー
// Per-direction filter operation view
export default function FilterSplitterInventory({ data }: { data: BlockInventoryOpen }) {
  const { t } = useI18n();
  if (!data.filterSplitter) return null;

  // uGUI と同じ空 grab 判定
  // Applies the same empty-grab branch as uGUI
  const sendFilterItemAction = (directionIndex: number, slotIndex: number, clear: boolean) => {
    const action = filterSlotClickAction(readTopic(Topics.inventory)?.grab.count ?? 0, clear);
    if (action === "noop") return;
    void dispatchAction("filter_splitter.set_filter_item", { directionIndex, slotIndex, clear: action === "clear" });
  };

  return (
    <Group align="flex-start" gap="md" data-testid="filter-splitter">
      {data.filterSplitter.directions.map((direction, dirIndex) => (
        <Stack key={dirIndex} gap="xs" data-testid={`filter-direction-${dirIndex}`}>
          <Text size="sm" c="dark.1">{t("出力 {index}", { index: dirIndex + 1 })}</Text>
          <Button
            size="compact-sm"
            variant="default"
            data-testid={`filter-mode-${dirIndex}`}
            onClick={() => void dispatchAction("filter_splitter.set_mode", { directionIndex: dirIndex, mode: nextMode(direction.mode) })}
          >
            {t(modeLabel[direction.mode])}
          </Button>
          <Group gap={4}>
            {direction.filterItemIds.map((itemId, slotIndex) => (
              <ItemSlot
                key={slotIndex}
                itemId={itemId}
                onLeftDown={() => sendFilterItemAction(dirIndex, slotIndex, false)}
                onRightDown={() => sendFilterItemAction(dirIndex, slotIndex, true)}
                testId={`filter-slot-${dirIndex}-${slotIndex}`}
              />
            ))}
          </Group>
        </Stack>
      ))}
    </Group>
  );
}
