import { Stack, Text, Title } from "@mantine/core";
import { useTopic, Topics } from "@/bridge";
import { useItemMaster } from "@/bridge/useItemMaster";
import { useUiStore } from "@/app/uiStore";
import RecipeContent from "./RecipeContent";

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
