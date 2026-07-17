import { Text } from "@mantine/core";
import { useTopic, Topics, useItemMaster } from "@/bridge";
import { GamePanel } from "@/shared/ui";
import { useItemSelectionStore } from "./selectionStore";
import RecipeContent from "./views/RecipeContent";

// 中央カラム: 選択アイテムのクラフトレシピと機械レシピを表示する（uGUI の RecipeViewer 相当）
// Center column: shows craft and machine recipes for the selected item, like uGUI's RecipeViewer
export default function RecipeViewer() {
  const selectedItemId = useItemSelectionStore((s) => s.selectedItemId);
  const onSelect = useItemSelectionStore((s) => s.setSelectedItem);
  const recipes = useTopic(Topics.craftRecipes);
  const machineRecipes = useTopic(Topics.machineRecipes);
  const inventory = useTopic(Topics.inventory);
  const itemMaster = useItemMaster();

  const loaded = recipes !== null && machineRecipes !== null && inventory !== null;

  // 選択時は左右パネルと等高、未選択時は縦の空箱を避け低めにする
  // Match the side panels when selected; keep the empty box short when nothing is selected
  const panelMinHeight = loaded && selectedItemId !== null ? 445 : 300;

  return (
    <GamePanel gridArea="viewer" variant="craft" style={{ alignSelf: "start", justifySelf: "center", width: 336, minWidth: 0, minHeight: panelMinHeight }}>
      {!loaded ? (
        <Text size="sm" c="dimmed" m="auto">connecting...</Text>
      ) : selectedItemId === null ? (
        <Text size="sm" c="dimmed" ta="center" m="auto">右のリストからアイテムを選択してください</Text>
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
    </GamePanel>
  );
}
