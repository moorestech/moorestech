import { Button, Group, Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import { ItemSlot } from "@/shared/ui";
import { useBlockInteraction } from "../blockInteractionContext";
import { filterSlotClickAction, modeLabel, nextMode } from "./filterSplitterLogic";

// 方向別のフィルタ操作ビュー
// Per-direction filter operation view
export default function FilterSplitterInventory({ data }: { data: BlockInventoryOpen }) {
  const { grabCount, resolveName } = useBlockInteraction();
  if (!data.filterSplitter) return null;

  // uGUI と同じ空 grab 判定
  // Applies the same empty-grab branch as uGUI
  const sendFilterItemAction = (directionIndex: number, slotIndex: number, clear: boolean) => {
    const action = filterSlotClickAction(grabCount, clear);
    if (action === "noop") return;
    void dispatchAction("filter_splitter.set_filter_item", { directionIndex, slotIndex, clear: action === "clear" });
  };

  return (
    <Group align="flex-start" gap="md" data-testid="filter-splitter">
      {data.filterSplitter.directions.map((direction, dirIndex) => (
        <Stack key={dirIndex} gap="xs" data-testid={`filter-direction-${dirIndex}`}>
          <Text size="sm" c="dark.1">出力 {dirIndex + 1}</Text>
          <Button
            size="compact-sm"
            variant="default"
            data-testid={`filter-mode-${dirIndex}`}
            onClick={() => void dispatchAction("filter_splitter.set_mode", { directionIndex: dirIndex, mode: nextMode(direction.mode) })}
          >
            {modeLabel[direction.mode]}
          </Button>
          <Group gap={4}>
            {direction.filterItemIds.map((itemId, slotIndex) => (
              <ItemSlot
                key={slotIndex}
                itemId={itemId}
                name={resolveName(itemId)}
                onLeftDown={() => sendFilterItemAction(dirIndex, slotIndex, false)}
                onRightDown={() => sendFilterItemAction(dirIndex, slotIndex, true)}
              />
            ))}
          </Group>
        </Stack>
      ))}
    </Group>
  );
}
