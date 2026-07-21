import { useState } from "react";
import { Group, Stack } from "@mantine/core";
import { Topics, useTopic } from "@/bridge";
import type { BlockInventoryOpen } from "@/bridge";
import { ItemSlot, ModeSwitch } from "@/shared/ui";
import { useI18n } from "@/shared/i18n";
import PowerRateText from "./PowerRateText";
import MachineInventoryBody from "./machine/MachineInventoryBody";
import MachineRecipeSelectionTab from "./machine/MachineRecipeSelectionTab";
import { buildMachineRecipeSelectionRows } from "./machine/machineRecipeSelectionLogic";

// 機械: レシピ有りはインベントリ/レシピ選択の2タブ、レシピ無しは従来スタック
// Machine: recipe-capable machines get inventory/recipe tabs; others keep the plain stack
export default function MachineSection({ data }: { data: BlockInventoryOpen }) {
  const machineRecipes = useTopic(Topics.machineRecipes);
  const [tab, setTab] = useState("inventory");
  const { t } = useI18n();
  if (!data.machine) return null;
  const machine = data.machine;

  const rows = buildMachineRecipeSelectionRows(
    machineRecipes?.recipes ?? [],
    machine.blockGuid,
    machine.selectedRecipeGuid,
  );
  // 電力率は稼働状態の常時視認のため、タブの外の共通フッタとして中央揃えで表示する
  // The power rate stays visible on both tabs as a centered common footer for at-a-glance status
  const powerRate = (
    <Group justify="center">
      <PowerRateText currentPower={machine.currentPower} requestPower={machine.requestPower} testId="machine-power-rate" />
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
          { value: "inventory", label: t("インベントリ"), testId: "machine-tab-inventory" },
          { value: "recipes", label: t("レシピ選択"), testId: "machine-tab-recipes" },
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
