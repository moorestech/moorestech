import { Group, Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/contract/payloadTypes";
import { ItemSlot, SlotGrid, ProgressArrow, FluidSlot } from "@/shared/ui";
import { useBlockInteraction } from "../blockInteractionContext";
import { useBlockSlotGestures } from "../useBlockSlotGestures";
import { computePowerRate, splitSlotIndices } from "./detailLogic";

// 機械: 入力→出力→モジュールの分割グリッド + 進捗 + 電力率（uGUI MachineBlockInventoryView 準拠）
// Machine: split input→output→module grids, progress, and power rate (mirrors uGUI MachineBlockInventoryView)
export default function MachineSection({ data }: { data: BlockInventoryOpen }) {
  const { resolveName } = useBlockInteraction();
  // ジェスチャ配線は BlockItemGrid と共通。分割グリッドでも右クリ/Shift/収集がフルに効く
  // Gesture wiring shared with BlockItemGrid; split grids get the full right-click/Shift/collect set
  const gestures = useBlockSlotGestures();
  if (!data.machine) return null;
  const { input, output, module } = splitSlotIndices(data.machine.slotLayout, data.itemSlots.length);
  const powerRate = computePowerRate(data.machine.currentPower, data.machine.requestPower);
  const lacking = powerRate < 1;

  const slotAt = (i: number) => {
    const slot = data.itemSlots[i];
    return (
      <ItemSlot
        key={i}
        itemId={slot.itemId}
        count={slot.count}
        name={resolveName(slot.itemId)}
        onLeftDown={(shiftKey) => gestures.onLeftDown(i, slot, shiftKey)}
        onRightDown={() => gestures.onRightDown(i, slot)}
        onDoubleClick={() => gestures.onDoubleClick(i)}
      />
    );
  };

  return (
    <Stack gap="xs" data-testid="machine-section">
      <Group align="center" gap="md">
        <SlotGrid cols={Math.max(1, input.length)} testId="machine-input-slots">{input.map(slotAt)}</SlotGrid>
        <ProgressArrow value={data.progress ?? 0} />
        <SlotGrid cols={Math.max(1, output.length)} testId="machine-output-slots">{output.map(slotAt)}</SlotGrid>
      </Group>
      {module.length > 0 && <SlotGrid cols={module.length} testId="machine-module-slots">{module.map(slotAt)}</SlotGrid>}
      {data.fluidSlots.length > 0 && (
        <Group gap="xs" data-testid="machine-fluid-slots">
          {data.fluidSlots.map((f, i) => <FluidSlot key={i} fluid={f} />)}
        </Group>
      )}
      <Text size="sm" c={lacking ? "red.5" : "dark.1"} data-testid="machine-power-rate">
        電力 {Math.round(powerRate * 100)}% ({data.machine.currentPower}/{data.machine.requestPower})
      </Text>
    </Stack>
  );
}
