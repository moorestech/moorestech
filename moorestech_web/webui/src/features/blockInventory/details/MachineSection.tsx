import { useState } from "react";
import { Group, Stack } from "@mantine/core";
import { Topics, useTopic } from "@/bridge";
import type { BlockInventoryOpen, MachineDetailData } from "@/bridge";
import { ItemSlot, ModeSwitch } from "@/shared/ui";
import { L, useI18n } from "@/shared/i18n";
import LackHighlightText from "./LackHighlightText";
import PowerRateText from "./PowerRateText";
import { machineStateDisplay } from "./detailLogic";
import MachineInventoryBody from "./machine/MachineInventoryBody";
import MachineRecipeSelectionTab from "./machine/MachineRecipeSelectionTab";
import { buildMachineRecipeSelectionRows, machineInitialTab } from "./machine/machineRecipeSelectionLogic";

// 機械: レシピ有りはインベントリ/レシピ選択の2タブ、レシピ無しは従来スタック
// Machine: recipe-capable machines get inventory/recipe tabs; others keep the plain stack
export default function MachineSection({ data, machine }: { data: BlockInventoryOpen; machine: MachineDetailData }) {
  const machineRecipes = useTopic(Topics.machineRecipes);
  const [tab, setTab] = useState<string>(() => machineInitialTab(machine.selectedRecipeGuid));
  const { t } = useI18n();

  const rows = buildMachineRecipeSelectionRows(
    machineRecipes?.recipes ?? [],
    machine.blockGuid,
    machine.selectedRecipeGuid,
  );
  // 状態ラベル+充足率を共通フッタに表示
  // The state label and satisfaction rate stay visible on both tabs as the shared footer (ADR 0010)
  const stateDisplay = machineStateDisplay(machine.currentState);
  const powerRate = (
    <Group justify="center" gap="xs">
      <LackHighlightText insufficient={stateDisplay.insufficient} size="sm" testId="machine-state-label">
        {t(stateDisplay.labelKey)}
      </LackHighlightText>
      {stateDisplay.showPowerRate && (
        <PowerRateText currentPower={machine.currentPower} requestPower={machine.requestPower} testId="machine-power-rate" />
      )}
    </Group>
  );

  if (rows.length === 0) {
    return (
      <Stack gap="xs" data-testid="machine-section">
        <MachineInventoryBody data={data} />
        {powerRate}
      </Stack>
    );
  }

  // 選択中レシピの生産物はインベントリタブでも1個表示する（個数バッジ無し）
  // The selected recipe's product also shows on the inventory tab as one badge-less slot
  const selectedRow = rows.find((row) => row.selected);

  return (
    <Stack gap="sm" data-testid="machine-section">
      <ModeSwitch
        value={tab}
        onChange={setTab}
        options={[
          { value: "recipes", label: t(L.ui.blockInventory.recipeSelectionTab), testId: "machine-tab-recipes" },
          { value: "inventory", label: t(L.ui.blockInventory.inventoryTab), testId: "machine-tab-inventory" },
        ]}
        testId="machine-tab-switch"
      />
      {tab === "inventory" ? (
        <>
          {selectedRow && (
            <Group justify="center" data-testid="machine-selected-product">
              <ItemSlot itemId={selectedRow.iconItemId} />
            </Group>
          )}
          <MachineInventoryBody data={data} />
        </>
      ) : (
        <MachineRecipeSelectionTab
          rows={rows}
          recipes={machineRecipes?.recipes ?? []}
          onSelected={() => setTab("inventory")}
        />
      )}
      {powerRate}
    </Stack>
  );
}
