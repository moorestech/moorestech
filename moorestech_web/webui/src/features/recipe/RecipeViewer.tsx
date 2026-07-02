import { useState } from "react";
import { Stack, Tabs, Text, Title } from "@mantine/core";
import { useTopic, Topics } from "@/bridge";
import { useItemMaster } from "@/bridge/useItemMaster";
import { useUiStore } from "@/app/uiStore";
import { ItemIcon } from "@/shared/ui";
import styles from "./RecipeViewer.module.css";
import type {
  CraftRecipesData,
  MachineRecipe,
  MachineRecipesData,
  PlayerInventoryData,
  ItemMasterEntry,
} from "@/bridge/payloadTypes";
import { buildOwnedCounts } from "./craftLogic";
import ItemHeader from "./ItemHeader";
import CraftRecipeView from "./CraftRecipeView";
import MachineRecipeView from "./MachineRecipeView";

// 中央カラム: 選択アイテムのクラフトレシピと機械レシピを表示する（uGUI の RecipeViewer 相当）
// Center column: shows craft and machine recipes for the selected item, like uGUI's RecipeViewer
export default function RecipeViewer() {
  const selectedItemId = useUiStore((s) => s.selectedItemId);
  const onSelect = useUiStore((s) => s.setSelectedItem);
  const recipes = useTopic(Topics.craftRecipes);
  const machineRecipes = useTopic(Topics.machineRecipes);
  const inventory = useTopic(Topics.inventory);
  const itemMaster = useItemMaster();

  const loaded = recipes !== null && machineRecipes !== null && inventory !== null;

  return (
    <Stack gap="sm" style={{ gridArea: "viewer", minWidth: 0 }}>
      <Title order={2} size="h4">Recipe</Title>
      {!loaded ? (
        <Text size="sm" c="dimmed">connecting...</Text>
      ) : selectedItemId === null ? (
        <Text size="sm" c="dimmed">右のアイテムリストからアイテムを選択してください</Text>
      ) : (
        // key={selectedItemId} の再マウントで tabKey/recipeIndex をリセットする契約は維持
        // Keep the contract: remount via key={selectedItemId} resets tabKey/recipeIndex
        <RecipeContent
          key={selectedItemId}
          itemId={selectedItemId}
          recipes={recipes}
          machineRecipes={machineRecipes}
          inventory={inventory}
          itemMaster={itemMaster}
          onSelect={onSelect}
        />
      )}
    </Stack>
  );
}

type ContentProps = {
  itemId: number;
  recipes: CraftRecipesData;
  machineRecipes: MachineRecipesData;
  inventory: PlayerInventoryData;
  itemMaster: Map<number, ItemMasterEntry> | null;
  onSelect: (itemId: number) => void;
};

// タブ定義。blockItemId が null ならクラフトレシピのタブ
// Tab descriptor; blockItemId null means the craft recipe tab
type Tab = { key: string; label: string; blockItemId: number | null };

// 選択アイテムのレシピ本体。key={itemId} で再マウントされタブ・ページ状態がリセットされる
// Recipe body for the selected item; remounted via key={itemId} so tab/page state resets
function RecipeContent({ itemId, recipes, machineRecipes, inventory, itemMaster, onSelect }: ContentProps) {
  // 生産レシピを抽出し機械別に集約
  // Collect producing recipes, grouped per machine
  const craftRecipes = recipes.recipes.filter((r) => r.resultItemId === itemId);
  const machineGroups = new Map<number, MachineRecipe[]>();
  machineRecipes.recipes
    .filter((r) => r.outputItems.some((o) => o.itemId === itemId))
    .forEach((r) => {
      const group = machineGroups.get(r.blockItemId) ?? [];
      group.push(r);
      machineGroups.set(r.blockItemId, group);
    });

  // タブ一覧: クラフトタブ + 機械ごとに1タブ（uGUI の RecipeTabView 相当）
  // Tab list: a craft tab plus one tab per machine, like uGUI's RecipeTabView
  const tabs: Tab[] = [];
  if (craftRecipes.length > 0) tabs.push({ key: "craft", label: "クラフト", blockItemId: null });
  machineGroups.forEach((group, blockItemId) => tabs.push({ key: `m${blockItemId}`, label: group[0].blockName, blockItemId }));

  const [tabKey, setTabKey] = useState(tabs[0]?.key ?? "");
  const [recipeIndex, setRecipeIndex] = useState(0);
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

  // サーバーの OneClickCraft は main+hotbar のみ参照するため、grab は所持数に含めない
  // The server's OneClickCraft only consults main+hotbar, so grab is excluded from the tally
  const counts = buildOwnedCounts(inventory);

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
