import { Group, Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge/payloadTypes";
import { ItemSlot, SlotGrid, ProgressArrow, FluidSlot } from "@/shared/ui";
import { dispatchAction } from "@/bridge";
import { useBlockInteraction } from "../blockInteractionContext";
import { pickUpPayload, placePayload } from "../blockLogic";
import { computePowerRate, splitSlotIndices } from "./detailLogic";

// 機械: 入力→出力→モジュールの分割グリッド + 進捗 + 電力率（uGUI MachineBlockInventoryView 準拠）
// Machine: split input→output→module grids, progress, and power rate (mirrors uGUI MachineBlockInventoryView)
export default function MachineSection({ data }: { data: BlockInventoryOpen }) {
  const { grabCount, resolveName } = useBlockInteraction();
  if (!data.machine) return null;
  const { input, output, module } = splitSlotIndices(data.machine.slotLayout, data.itemSlots.length);
  const powerRate = computePowerRate(data.machine.currentPower, data.machine.requestPower);
  const lacking = powerRate < 1;

  // grab 保持時は置く、空かつ中身ありなら拾う。表示更新は topic event 駆動に委ねる
  // Place while holding grab; pick up when empty and the slot has items; rendering follows topic events
  const slotAt = (i: number) => {
    const slot = data.itemSlots[i];
    const onLeftDown = () => {
      if (grabCount > 0) {
        void dispatchAction("block_inventory.move_item", placePayload(i, grabCount));
        return;
      }
      if (slot.count === 0) return;
      void dispatchAction("block_inventory.move_item", pickUpPayload(i, slot.count));
    };
    return (
      <ItemSlot key={i} itemId={slot.itemId} count={slot.count} name={resolveName(slot.itemId)} onLeftDown={onLeftDown} />
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
