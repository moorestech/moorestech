import { Box, Button, Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import { ItemSlot } from "@/shared/ui";
import type { CraftRecipe } from "@/bridge";
import type { TutorialAnchorAttributes } from "@/shared/tutorialAnchor";
import { ownedCountOf } from "@/shared/ownedCounts";
import { craftable } from "../logic/craftLogic";
import { useHoldCraft } from "../logic/useHoldCraft";
import RecipeRow from "./RecipeRow";
import styles from "./RecipeBox.module.css";
import { L, useI18n } from "@/shared/i18n";
import { useMaterialTooltipText } from "@/shared/materialTooltipText";

type Props = {
  recipe: CraftRecipe;
  counts: Map<number, number>;
  onSelect: (itemId: number) => void;
  // testIdはレシピ単位で親が注入
  // Parent injects a per-recipe testId
  testId: string;
  // アンカーは対象クラフトのみ親が注入
  // Parent injects the anchor only on the chosen craft entry
  tutorialAnchorProps?: TutorialAnchorAttributes;
};

// 素材→矢印→結果の行と全幅長押しボタン
// Material-arrow-result row plus a full-width hold-to-craft button
export default function CraftRecipeEntry({ recipe, counts, onSelect, testId, tutorialAnchorProps }: Props) {
  const { t } = useI18n();
  const materialTooltipText = useMaterialTooltipText();
  const isCraftable = craftable(recipe, counts);

  // 長押し1周ごとにクラフト要求を送信
  // Sends one craft request per hold cycle
  const { progress, isHolding, start, stop } = useHoldCraft(recipe.craftTime, isCraftable, () => {
    void dispatchAction("craft.execute", { recipeGuid: recipe.recipeGuid });
  });

  return (
    <Stack className={styles.recipeEntry} gap="xs" data-testid={testId}>
      <RecipeRow
        testId={`craft-recipe-box-${recipe.recipeGuid}`}
        arrowValue={isHolding ? progress : 0}
        arrowTestId={`craft-progress-arrow-${recipe.recipeGuid}`}
        materials={recipe.requiredItems.map((r, i) => (
          <Box className={styles.materialSlot} key={i}>
            {/* 所持数不足の素材は既存どおり40%透過にし、数値も赤で示す */}
            {/* Keep the existing 40% dimming for shortages and also mark the numeric count red */}
            <ItemSlot
              itemId={r.itemId}
              insufficient={ownedCountOf(counts, r.itemId) < r.count}
              tooltip={<span style={{ whiteSpace: "pre-line" }}>
                {materialTooltipText(L.ui.recipe.materialTooltip, r.itemId, r.count, counts)}
              </span>}
              onLeftDown={() => onSelect(r.itemId)}
            />
            <Text className={styles.materialCount} data-lack={ownedCountOf(counts, r.itemId) < r.count || undefined}>
              {t(L.ui.recipe.itemCountSummary, { ownedCount: ownedCountOf(counts, r.itemId), requiredCount: r.count })}
            </Text>
          </Box>
        ))}
        result={<ItemSlot itemId={recipe.resultItemId} count={recipe.resultCount} />}
      />
      <Button
        {...tutorialAnchorProps}
        className={styles.craftButton}
        fullWidth
        disabled={!isCraftable}
        title={t(L.ui.recipe.holdToCraft)}
        // 主ボタン以外は長押し開始しない
        // Only the primary button/touch starts the hold
        onPointerDown={(e) => { if (e.button === 0) start(); }}
        // 離す/外れる/キャンセルで停止しリセット
        // Release, leave, or cancel: stop and reset elapsed time
        onPointerUp={stop}
        onPointerLeave={stop}
        onPointerCancel={stop}
        // キーボード（Enter/Space）長押しでも連続クラフトできるようにする（ネイティブ onClick 喪失分の回復）
        // Keep keyboard (Enter/Space) hold working, restoring the craft path lost when native onClick was removed
        onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); start(); } }}
        onKeyUp={(e) => { if (e.key === "Enter" || e.key === " ") stop(); }}
        onBlur={stop}
      >
        {/* 秒数の書式は ui.recipe.duration が唯一の出所 */}
        {/* ui.recipe.duration is the single source for the seconds format */}
        {t(L.ui.recipe.craftButtonLabel, { duration: t(L.ui.recipe.duration, { seconds: recipe.craftTime }) })}
      </Button>
    </Stack>
  );
}
