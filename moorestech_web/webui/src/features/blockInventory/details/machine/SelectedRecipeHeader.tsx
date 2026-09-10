// インベントリモード上部の選択中レシピ表示。クリックでレシピ選択モードへ戻る（ADR 0042 R2）
// Selected-recipe header atop the inventory mode; clicking returns to recipe selection (ADR 0042 R2)
import { Group, Text } from "@mantine/core";
import type { MachineRecipe } from "@/bridge";
import { FluidIcon, HoverTooltip, ItemSlot } from "@/shared/ui";
import { L, useI18n } from "@/shared/i18n";
import { useRecipeDisplayName, type RecipeDisplaySubject } from "./machineRecipeSelectionLogic";

type Props = { recipe: MachineRecipe; subject: RecipeDisplaySubject; onChangeRecipe: () => void };

// 代表出力（アイテム優先、無ければ液体）はbuildMachineRecipeSelectionRowsが既にガードしているため、
// このレシピが渡ってくる時点で必ず存在する（D2）
// The representative output (item first, fluid otherwise) is already guarded by buildMachineRecipeSelectionRows,
// so any recipe reaching here is guaranteed to have one (D2)
export default function SelectedRecipeHeader({ recipe, subject, onChangeRecipe }: Props) {
  const { t } = useI18n();
  const name = useRecipeDisplayName(subject);

  return (
    <HoverTooltip label={t(L.ui.blockInventory.changeRecipe)}>
      <Group justify="center" gap="xs" role="button" data-testid="machine-selected-recipe" style={{ cursor: "pointer" }} onClick={onChangeRecipe}>
        {subject.kind === "item" ? <ItemSlot itemId={subject.itemId} /> : <FluidIcon fluidGuid={subject.fluidGuid} />}
        <Text data-testid="machine-selected-recipe-name">{name}</Text>
        <Text c="dimmed" size="sm" data-testid="machine-selected-recipe-time">{t(L.ui.blockInventory.recipeDuration, { seconds: recipe.time })}</Text>
      </Group>
    </HoverTooltip>
  );
}
