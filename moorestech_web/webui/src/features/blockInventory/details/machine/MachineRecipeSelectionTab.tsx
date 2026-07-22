// 上段に詳細プレビュー（ホバー優先）、罫線区切りの下に9列グリッドを中央揃えで並べるタブ
// Tab with a detail preview on top (hover first) and a centered 9-column grid under a rule divider
import { useState } from "react";
import { Group, Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import type { MachineRecipe } from "@/bridge";
import { FadeRule, ItemSlot, SlotGrid } from "@/shared/ui";
import { useI18n } from "@/shared/i18n";
import type { MachineRecipeSelectionRowData } from "./machineRecipeSelectionLogic";
import styles from "./machineRecipeSelection.module.css";

type Props = {
  rows: MachineRecipeSelectionRowData[];
  recipes: readonly MachineRecipe[];
  // レシピを選択した直後にインベントリタブへ戻すための通知
  // Fired right after selecting a recipe so the section can jump back to the inventory tab
  onSelected: () => void;
};

export default function MachineRecipeSelectionTab({ rows, recipes, onSelected }: Props) {
  const { t } = useI18n();
  const [hoveredRecipeGuid, setHoveredRecipeGuid] = useState<string | null>(null);

  // 詳細プレビューはホバー中レシピ優先、無ければ選択中レシピ
  // The detail preview prefers the hovered recipe, falling back to the selected one
  const selectedRow = rows.find((row) => row.selected);
  const previewGuid = hoveredRecipeGuid ?? selectedRow?.recipeGuid;
  const previewRecipe = previewGuid !== undefined ? recipes.find((recipe) => recipe.recipeGuid === previewGuid) : undefined;

  return (
    <Stack gap="sm" align="center" data-testid="machine-recipe-selection">
      <div className={styles.detailArea}>
        {previewRecipe ? (
          <Group gap="xs" align="center" justify="center" wrap="wrap" data-testid="machine-recipe-detail">
            {previewRecipe.inputItems.map((item, i) => (
              <ItemSlot key={i} itemId={item.itemId} count={item.count} />
            ))}
            <Stack gap={0} align="center" mx="xs">
              <Text c="dimmed">{t("→")}</Text>
              <Text c="dimmed" size="sm" data-testid="machine-recipe-detail-time">{t("{time}秒", { time: previewRecipe.time })}</Text>
            </Stack>
            {previewRecipe.outputItems.map((item, i) => (
              <ItemSlot key={i} itemId={item.itemId} count={item.count} />
            ))}
          </Group>
        ) : (
          <Text c="dimmed" data-testid="machine-recipe-detail-empty">{t("レシピをホバーで詳細、クリックで選択")}</Text>
        )}
      </div>
      <FadeRule />
      <SlotGrid cols={Math.min(9, Math.max(1, rows.length))}>
        {rows.map((row) => (
          <ItemSlot
            key={row.recipeGuid}
            itemId={row.iconItemId}
            count={row.iconCount}
            selected={row.selected}
            testId={`machine-recipe-${row.recipeGuid}`}
            onHoverChange={(hovering) => {
              setHoveredRecipeGuid((prev) => (hovering ? row.recipeGuid : prev === row.recipeGuid ? null : prev));
            }}
            onLeftDown={() => {
              void dispatchAction("machine_recipe.select", { operation: "set", recipeGuid: row.recipeGuid });
              onSelected();
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
