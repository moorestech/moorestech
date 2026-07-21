import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen } from "@/bridge";
import { ItemSlot, SlotGrid, ProgressArrow, FluidSlotRow } from "@/shared/ui";
import { useBlockSlotGestures } from "../../useBlockSlotGestures";
import { itemsPerMinute, splitSlotIndices } from "../detailLogic";
import { useI18n } from "@/shared/i18n";
import styles from "./machineInventoryBody.module.css";

// 機械インベントリ: 入力→出力→モジュールの分割グリッド + 進捗 + 分間生産数
// Machine inventory: split input→output→module grids, progress, and items per minute
export default function MachineInventoryBody({ data }: { data: BlockInventoryOpen }) {
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
    <Stack gap="xs" align="center" data-testid="machine-inventory-body">
      <div className={styles.processRow}>
        <div className={styles.inputSide}>
          <SlotGrid cols={Math.max(1, input.length)} testId="machine-input-slots">{input.map(slotAt)}</SlotGrid>
        </div>
        <ProgressArrow value={data.progress ?? 0} />
        <div className={styles.outputSide}>
          <SlotGrid cols={Math.max(1, output.length)} testId="machine-output-slots">{output.map(slotAt)}</SlotGrid>
        </div>
      </div>
      {/* モジュールスロットは加工行から1段下げ、ラベルを添えて入出力と区別する */}
      {/* Drop the module slots one level below the process row and label them to distinguish from input/output */}
      {module.length > 0 && (
        <Stack gap={4} align="center" mt="xs">
          <Text size="sm" c="dimmed" data-testid="machine-module-label">{t("アップグレードスロット")}</Text>
          <SlotGrid cols={module.length} testId="machine-module-slots">{module.map(slotAt)}</SlotGrid>
        </Stack>
      )}
      {/* 機械の流体行は従来どおり矢印なし（加工進捗は入出力グリッド間の矢印が担う） */}
      {/* The machine fluid row keeps no arrow; processing progress lives between the in/out grids */}
      <FluidSlotRow fluids={data.fluidSlots} testId="machine-fluid-slots" />
      {machine.outputItems.map((output) => {
        const rate = itemsPerMinute(output.count, machine.recipeTime);
        return rate === null ? null : <div key={output.itemId} data-testid="machine-items-per-minute">{t("分間生産数")} <span>{rate}</span></div>;
      })}
    </Stack>
  );
}
