// インベントリモード上部の選択中レシピ表示。クリックでレシピ選択モードへ戻る（ADR 0042 R2）
// Selected-recipe header atop the inventory mode; clicking returns to recipe selection (ADR 0042 R2)
import { Group, Text } from "@mantine/core";
import type { MachineRecipe } from "@/bridge";
import { HoverTooltip, ItemSlot } from "@/shared/ui";
import { L, useI18n, useItemNameResolver } from "@/shared/i18n";

type Props = { recipe: MachineRecipe; onChangeRecipe: () => void };

// 代表出力（先頭の生産物）はbuildMachineRecipeSelectionRowsが既にガードしているため、
// このレシピが渡ってくる時点で必ず存在する。フォールバックは持たない（C9）
// buildMachineRecipeSelectionRows already guards the representative output, so any
// recipe reaching here is guaranteed to have one; no fallback icon is kept here (C9)
export default function SelectedRecipeHeader({ recipe, onChangeRecipe }: Props) {
  const { t } = useI18n();
  const resolveItemName = useItemNameResolver();
  const outputItemId = recipe.outputItems[0].itemId;
  return (
    <HoverTooltip label={t(L.ui.blockInventory.changeRecipe)} disabled={false}>
      <Group justify="center" gap="xs" role="button" data-testid="machine-selected-recipe" style={{ cursor: "pointer" }} onClick={onChangeRecipe}>
        <ItemSlot itemId={outputItemId} />
        <Text data-testid="machine-selected-recipe-name">{resolveItemName(outputItemId)}</Text>
        <Text c="dimmed" size="sm" data-testid="machine-selected-recipe-time">{t(L.ui.blockInventory.recipeDuration, { seconds: recipe.time })}</Text>
      </Group>
    </HoverTooltip>
  );
}
