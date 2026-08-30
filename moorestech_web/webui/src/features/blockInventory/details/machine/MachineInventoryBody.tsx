import { Stack, Text } from "@mantine/core";
import type { BlockInventoryOpen, MachineRecipe } from "@/bridge";
import { ItemSlot, SlotGrid, ProgressArrowGlyph, FluidSlotRow } from "@/shared/ui";
import { useBlockSlotGestures } from "../../useBlockSlotGestures";
import { itemsPerMinute, splitSlotIndices } from "../detailLogic";
import { buildMachineSlotView, type GhostItem } from "./machineSlotGhosts";
import { L, useI18n } from "@/shared/i18n";
import styles from "./machineInventoryBody.module.css";

// 機械インベントリ: 入力→出力→モジュールの分割グリッド + 進捗 + 分間生産数
// Machine inventory: split input→output→module grids, progress, and items per minute
// recipe が null ならレシピ0件機械として全スロットを描く従来経路（ADR 0042 R7/R8）
// A null recipe falls back to drawing every slot, the legacy path for recipe-less machines (ADR 0042 R7/R8)
export default function MachineInventoryBody({ data, recipe }: { data: BlockInventoryOpen; recipe: MachineRecipe | null }) {
  // ジェスチャ配線は BlockItemGrid と共通。分割グリッドでも右クリ/Shift/収集がフルに効く
  // Gesture wiring shared with BlockItemGrid; split grids get the full right-click/Shift/collect set
  const gestures = useBlockSlotGestures();
  const { t } = useI18n();
  if (!data.machine) return null;
  const machine = data.machine;
  const { module } = splitSlotIndices(machine.slotLayout, data.itemSlots.length);
  const view = buildMachineSlotView(recipe, machine.slotLayout, data.itemSlots.length, machine.slotLayout.inputTank, data.fluidSlots.length);
  const inputSlots = view.inputs;
  const outputSlots = view.outputs;
  const fluids = view.fluidIndices.map((i) => data.fluidSlots[i]);

  const slotAt = ({ index, ghost }: { index: number; ghost: GhostItem | undefined }) => {
    const slot = data.itemSlots[index];
    return (
      <ItemSlot
        key={index}
        itemId={slot.itemId}
        count={slot.count}
        ghost={ghost}
        onLeftDown={(shiftKey) => gestures.onLeftDown(index, shiftKey)}
        onRightDown={() => gestures.onRightDown(index)}
        onDoubleClick={() => gestures.onDoubleClick(index)}
      />
    );
  };

  return (
    <Stack gap="xs" align="center" data-testid="machine-inventory-body">
      <div className={styles.processRow}>
        <div className={styles.inputSide}>
          <SlotGrid cols={Math.max(1, inputSlots.length)} testId="machine-input-slots">{inputSlots.map(slotAt)}</SlotGrid>
        </div>
        <ProgressArrowGlyph value={data.progress ?? 0} testId="machine-progress-arrow" />
        <div className={styles.outputSide}>
          <SlotGrid cols={Math.max(1, outputSlots.length)} testId="machine-output-slots">{outputSlots.map(slotAt)}</SlotGrid>
        </div>
      </div>
      {/* モジュールスロットは加工行から1段下げ、ラベルを添えて入出力と区別する */}
      {/* Drop the module slots one level below the process row and label them to distinguish from input/output */}
      {module.length > 0 && (
        <Stack gap={4} align="center" mt="xs">
          <Text size="sm" c="dimmed" data-testid="machine-module-label">
            {t(L.ui.blockInventory.upgradeSlots)}
          </Text>
          <SlotGrid cols={module.length} testId="machine-module-slots">{module.map((i) => slotAt({ index: i, ghost: undefined }))}</SlotGrid>
        </Stack>
      )}
      {/* 機械の流体行は従来どおり矢印なし（加工進捗は入出力グリッド間の矢印が担う） */}
      {/* The machine fluid row keeps no arrow; processing progress lives between the in/out grids */}
      <FluidSlotRow fluids={fluids} ghosts={view.fluidGhosts} testId="machine-fluid-slots" />
      {machine.outputItems.map((output) => {
        const rate = itemsPerMinute(output.count, machine.recipeTime);
        return rate === null ? null : (
          <div key={output.itemId} data-testid="machine-items-per-minute">
            {t(L.ui.blockInventory.productionPerMinute)} <span>{rate}</span>
          </div>
        );
      })}
    </Stack>
  );
}
