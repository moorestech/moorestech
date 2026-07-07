import { useEffect, useMemo, useState } from "react";
import { Stack, Tabs, Text } from "@mantine/core";
import { ItemIcon } from "@/shared/ui";
import { buildOwnedCounts } from "@/shared/ownedCounts";
import styles from "../RecipeViewer.module.css";
import type {
  CraftRecipesData,
  MachineRecipesData,
  PlayerInventoryData,
  ItemMasterEntry,
} from "@/bridge/contract/payloadTypes";
import {
  selectCraftRecipes,
  groupMachineRecipesByBlock,
  buildRecipeTabs,
} from "../craftLogic";
import ItemHeader from "./ItemHeader";
import CraftRecipeView from "./CraftRecipeView";
import MachineRecipeView from "./MachineRecipeView";

type Props = {
  itemId: number;
  recipes: CraftRecipesData;
  machineRecipes: MachineRecipesData;
  inventory: PlayerInventoryData;
  itemMaster: Map<number, ItemMasterEntry> | null;
  onSelect: (itemId: number) => void;
};

// 選択アイテムのレシピ本体。key={itemId} で再マウントされタブ・ページ状態がリセットされる
// Recipe body for the selected item; remounted via key={itemId} so tab/page state resets
export default function RecipeContent({ itemId, recipes, machineRecipes, inventory, itemMaster, onSelect }: Props) {
  // 導出は純関数＋useMemo。入力 topic が変わらない限り再計算しない
  // Derivations are pure functions + useMemo; no recompute unless the input topics change
  const craftRecipes = useMemo(() => selectCraftRecipes(recipes, itemId), [recipes, itemId]);
  const machineGroups = useMemo(() => groupMachineRecipesByBlock(machineRecipes, itemId), [machineRecipes, itemId]);
  const tabs = useMemo(() => buildRecipeTabs(craftRecipes, machineGroups), [craftRecipes, machineGroups]);
  // サーバーの OneClickCraft は main+hotbar のみ参照するため、grab は所持数に含めない
  // The server's OneClickCraft only consults main+hotbar, so grab is excluded from the tally
  const counts = useMemo(() => buildOwnedCounts([...inventory.mainSlots, ...inventory.hotbarSlots]), [inventory]);

  const [tabKey, setTabKey] = useState(tabs[0]?.key ?? "");
  const [recipeIndex, setRecipeIndex] = useState(0);
  // topic 更新でタブ構成が変わって先頭タブへフォールバックする際、ページ位置の持ち越しを防ぐ
  // When a topic update drops the active tab and we fall back to the first one, reset the page index too
  useEffect(() => {
    if (tabs.length === 0) return;
    if (!tabs.some((t) => t.key === tabKey)) {
      setTabKey(tabs[0].key);
      setRecipeIndex(0);
    }
  }, [tabs, tabKey]);

  // topic 更新でタブ構成が変わった場合は先頭タブへフォールバック
  // Fall back to the first tab if a topic update changed the tab set
  const activeTab = tabs.find((t) => t.key === tabKey) ?? tabs[0] ?? null;

  const itemName = itemMaster?.get(itemId)?.name ?? `item ${itemId}`;

  if (activeTab === null) {
    return (
      <Stack gap="sm">
        <ItemHeader itemId={itemId} name={itemName} />
        <Text size="sm" c="dimmed">このアイテムのレシピはありません</Text>
      </Stack>
    );
  }

  return (
    <Stack gap="sm">
      <ItemHeader itemId={itemId} name={itemName} />
      {tabs.length > 1 ? (
        <Tabs
          variant="pills"
          value={activeTab.key}
          onChange={(v) => {
            if (v === null) return;
            setTabKey(v);
            setRecipeIndex(0);
          }}
        >
          <Tabs.List>
            {tabs.map((t) => (
              <Tabs.Tab
                key={t.key}
                value={t.key}
                leftSection={t.blockItemId !== null ? <ItemIcon itemId={t.blockItemId} className={styles.tabIcon} /> : undefined}
              >
                {t.label}
              </Tabs.Tab>
            ))}
          </Tabs.List>
        </Tabs>
      ) : null}
      {activeTab.blockItemId === null ? (
        <CraftRecipeView
          recipes={craftRecipes}
          recipeIndex={recipeIndex}
          setRecipeIndex={setRecipeIndex}
          counts={counts}
          itemMaster={itemMaster}
          onSelect={onSelect}
        />
      ) : (
        <MachineRecipeView
          recipes={machineGroups.get(activeTab.blockItemId)!}
          recipeIndex={recipeIndex}
          setRecipeIndex={setRecipeIndex}
          itemMaster={itemMaster}
          onSelect={onSelect}
        />
      )}
    </Stack>
  );
}
