// 機械に対応する解放済みレシピを選択・解除できるスロット行
// Slot row for selecting and clearing unlocked recipes supported by a machine
import { Stack, Text } from "@mantine/core";
import { dispatchAction, Topics, useTopic } from "@/bridge";
import type { BlockInventoryOpen } from "@/bridge";
import { ItemSlot, SlotGrid } from "@/shared/ui";
import { useI18n } from "@/shared/i18n";
import { buildMachineRecipeSelectionRows } from "./machineRecipeSelectionLogic";

export default function MachineRecipeSelectionRow({ data }: { data: BlockInventoryOpen }) {
  const machineRecipes = useTopic(Topics.machineRecipes);
  const { t } = useI18n();
  const machine = data.machine;
  if (!machine) return null;

  const rows = buildMachineRecipeSelectionRows(
    machineRecipes?.recipes ?? [],
    machine.blockGuid,
    machine.selectedRecipeGuid,
  );
  if (rows.length === 0) return null;

  return (
    <Stack gap="xs" data-testid="machine-recipe-selection">
      <Text size="xs" c="var(--text-default)">{t("レシピ選択")}</Text>
      <SlotGrid cols={rows.length}>
        {rows.map((row) => (
          <ItemSlot
            key={row.recipeGuid}
            itemId={row.iconItemId}
            count={row.iconCount}
            selected={row.selected}
            testId={`machine-recipe-${row.recipeGuid}`}
            onLeftDown={() => {
              void dispatchAction("machine_recipe.select", { operation: "set", recipeGuid: row.recipeGuid });
            }}
            onRightDown={() => {
              if (!row.selected) return;
              void dispatchAction("machine_recipe.select", { operation: "clear" });
            }}
          />
        ))}
      </SlotGrid>
    </Stack>
  );
}
