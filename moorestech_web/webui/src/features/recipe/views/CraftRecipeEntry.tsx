import { useEffect } from "react";
import { Box, Button, Group, Stack, Text } from "@mantine/core";
import { dispatchAction } from "@/bridge";
import { ItemSlot, ProgressArrowGlyph } from "@/shared/ui";
import type { CraftRecipe } from "@/bridge";
import { craftable } from "../logic/craftLogic";
import { useHoldCraft } from "../logic/useHoldCraft";
import styles from "./RecipeBox.module.css";
import craftArrowStyles from "./craftArrow.module.css";
import { L, useI18n, useItemNameResolver } from "@/shared/i18n";

type Props = {
  recipe: CraftRecipe;
  counts: Map<number, number>;
  onSelect: (itemId: number) => void;
  // チュートリアルアンカーは重複禁止のため先頭エントリだけ親が注入する
  // The tutorial anchor must stay unique, so only the first entry receives it from the parent
  tutorialAnchorProps?: Record<string, string>;
};

// クラフトエントリ: 素材列 → 進捗矢印 → 結果のレシピ行と、下端の全幅長押しボタン（ADR 0011・uGUI CraftButton 準拠）
// Craft entry: material row → progress arrow → result, plus a full-width hold-to-craft button below (ADR 0011, mirrors uGUI CraftButton)
export default function CraftRecipeEntry({ recipe, counts, onSelect, tutorialAnchorProps }: Props) {
  const { t } = useI18n();
  const resolveItemName = useItemNameResolver();
  const isCraftable = craftable(recipe, counts);

  // 長押し1周ごとに1回クラフト要求を送る。素材チェックはサーバー側で行われる
  // Send one craft request per completed hold cycle; material checks happen server-side
  const { progress, isHolding, start, stop } = useHoldCraft(recipe.craftTime, isCraftable, () => {
    void dispatchAction("craft.execute", { recipeGuid: recipe.recipeGuid });
  });

  // レシピが差し替わったら進行中の長押しを打ち切る
  // Abort any in-progress hold when the recipe changes
  useEffect(() => stop, [recipe.recipeGuid, stop]);

  return (
    <Stack className={styles.recipeEntry} gap="xs" data-testid="craft-recipe-entry">
      {/* 正本は素材/矢印+時間/完成品の3カラムを固定配置する。space-betweenだと素材の点数で矢印列が押されて
          ズレるため、gridで列位置を内容量に依存させない */}
      {/* The reference fixes 3 columns (materials / arrow+time / result); space-between let the arrow column
          drift with material count, so a grid pins each column regardless of content size */}
      <div className={styles.recipeBox} data-testid="craft-recipe-box">
        <Group gap={0} className={styles.recipeMaterials}>
          {recipe.requiredItems.map((r, i) => (
            <Box className={styles.materialSlot} key={i}>
              {/* 所持数不足の素材は既存どおり40%透過にし、数値も赤で示す */}
              {/* Keep the existing 40% dimming for shortages and also mark the numeric count red */}
              <ItemSlot
                itemId={r.itemId}
                insufficient={(counts.get(r.itemId) ?? 0) < r.count}
                tooltip={<span style={{ whiteSpace: "pre-line" }}>{t(L.ui.recipe.materialTooltip, {
                  itemName: resolveItemName(r.itemId) ?? t(L.ui.common.itemFallback, { itemId: r.itemId }),
                  ownedCount: counts.get(r.itemId) ?? 0,
                  requiredCount: r.count,
                })}</span>}
                onLeftDown={() => onSelect(r.itemId)}
              />
              <Text className={styles.materialCount} data-lack={(counts.get(r.itemId) ?? 0) < r.count || undefined}>
                {t(L.ui.recipe.itemCountSummary, {
                  ownedCount: counts.get(r.itemId) ?? 0,
                  requiredCount: r.count,
                })}
              </Text>
            </Box>
          ))}
        </Group>
        {/* 素材と完成品の間に長押し進捗を矢印で表示する */}
        {/* Show hold progress as an arrow between materials and result */}
        <Box className={styles.recipeArrowCol}>
          <div className={craftArrowStyles.craftArrow}>
            <ProgressArrowGlyph value={isHolding ? progress : 0} testId="craft-progress-arrow" />
          </div>
        </Box>
        <Box className={styles.recipeResult}>
          <ItemSlot itemId={recipe.resultItemId} count={recipe.resultCount} />
        </Box>
      </div>
      <Button
        {...tutorialAnchorProps}
        className={styles.craftButton}
        fullWidth
        disabled={!isCraftable}
        title={t(L.ui.recipe.holdToCraft)}
        // 主ボタン（左クリック/主タッチ）以外では長押しを開始しない
        // Only the primary button/touch starts the hold; ignore right/middle clicks
        onPointerDown={(e) => { if (e.button === 0) start(); }}
        // 離す・ボタンから外れる・キャンセルのいずれでもクラフトを止め、経過時間をリセットする
        // Release, leaving the button, or cancel all stop the craft and reset the elapsed time
        onPointerUp={stop}
        onPointerLeave={stop}
        onPointerCancel={stop}
        // キーボード（Enter/Space）長押しでも連続クラフトできるようにする（ネイティブ onClick 喪失分の回復）
        // Keep keyboard (Enter/Space) hold working, restoring the craft path lost when native onClick was removed
        onKeyDown={(e) => { if (e.key === "Enter" || e.key === " ") { e.preventDefault(); start(); } }}
        onKeyUp={(e) => { if (e.key === "Enter" || e.key === " ") stop(); }}
        onBlur={stop}
      >
        {t(L.ui.recipe.craftButtonLabel, { seconds: recipe.craftTime })}
      </Button>
    </Stack>
  );
}
