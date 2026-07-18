import { Group, Stack } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { ItemSlot, SlotGrid, ProgressArrow, FluidSlotRow } from "@/shared/ui";
import { useBlockSlotGestures } from "../useBlockSlotGestures";
import { itemsPerMinute, splitSlotIndices } from "./detailLogic";
import { useI18n } from "@/shared/i18n";
import PowerRateText from "./PowerRateText";

// 機械: 入力→出力→モジュールの分割グリッド + 進捗 + 電力率（uGUI MachineBlockInventoryView 準拠）
// Machine: split input→output→module grids, progress, and power rate (mirrors uGUI MachineBlockInventoryView)
export default function MachineSection({ data }: { data: BlockInventoryOpen }) {
  // ジェスチャ配線は BlockItemGrid と共通。分割グリッドでも右クリ/Shift/収集がフルに効く
  // Gesture wiring shared with BlockItemGrid; split grids get the full right-click/Shift/collect set
  const gestures = useBlockSlotGestures();
  const { t } = useI18n();
  if (!data.machine) return null;
  const machine = data.machine;
  const { input, output, module } = splitSlotIndices(machine.slotLayout, data.itemSlots.length);

  const slotAt = (i: number) => {
    const slot = data.itemSlots[i];
    return (
      <ItemSlot
        key={i}
        itemId={slot.itemId}
        count={slot.count}
        onLeftDown={(shiftKey) => gestures.onLeftDown(i, shiftKey)}
        onRightDown={() => gestures.onRightDown(i)}
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
      {/* 機械の流体行は従来どおり矢印なし（加工進捗は入出力グリッド間の矢印が担う） */}
      {/* The machine fluid row keeps no arrow; processing progress lives between the in/out grids */}
      <FluidSlotRow fluids={data.fluidSlots} testId="machine-fluid-slots" />
      <PowerRateText currentPower={data.machine.currentPower} requestPower={data.machine.requestPower} testId="machine-power-rate" />
      {machine.outputItems.map((output) => {
        const rate = itemsPerMinute(output.count, machine.recipeTime);
        return rate === null ? null : <div key={output.itemId} data-testid="machine-items-per-minute">{t("分間生産数")} <span>{rate}</span></div>;
      })}
    </Stack>
  );
}
