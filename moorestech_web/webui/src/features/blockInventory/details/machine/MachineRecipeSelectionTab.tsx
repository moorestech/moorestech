// 解放済みレシピを9列グリッドで列挙し、選択中レシピの材料詳細を表示するタブ
// Tab listing unlocked recipes in a 9-column grid with the selected recipe's ingredient detail
import { Group, Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { MachineRecipe } from "@/bridge";
import { ItemSlot, SlotGrid } from "@/shared/ui";
import { useI18n } from "@/shared/i18n";
import type { MachineRecipeSelectionRowData } from "./machineRecipeSelectionLogic";

type Props = {
  rows: MachineRecipeSelectionRowData[];
  recipes: readonly MachineRecipe[];
};

export default function MachineRecipeSelectionTab({ rows, recipes }: Props) {
  const { t } = useI18n();
  const selectedRow = rows.find((row) => row.selected);
  const selectedRecipe = selectedRow ? recipes.find((recipe) => recipe.recipeGuid === selectedRow.recipeGuid) : undefined;

  return (
    <Stack gap="xs" data-testid="machine-recipe-selection">
      <SlotGrid cols={Math.min(9, Math.max(1, rows.length))}>
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
      {/* 選択中レシピの詳細: 材料→出力と所要時間（MachineRecipeView様式） */}
      {/* Selected recipe detail: inputs→outputs and time, in the MachineRecipeView style */}
      {selectedRecipe && (
        <Stack gap="xs" data-testid="machine-recipe-detail">
          <Group gap={4} align="center" wrap="wrap">
            {selectedRecipe.inputItems.map((item, i) => (
              <ItemSlot key={i} itemId={item.itemId} count={item.count} />
            ))}
            <Text c="dimmed" mx="xs">{t("→")}</Text>
            {selectedRecipe.outputItems.map((item, i) => (
              <ItemSlot key={i} itemId={item.itemId} count={item.count} />
            ))}
          </Group>
          <div data-testid="machine-recipe-detail-time">{t("所要時間 {time}秒", { time: selectedRecipe.time })}</div>
        </Stack>
      )}
    </Stack>
  );
}
