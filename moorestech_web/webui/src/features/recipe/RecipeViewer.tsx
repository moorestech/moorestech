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
  // iter6: craft-panel bbox実測(1209,301,2072,1407)を正本(1210,300,2071,1405)へ微調整。左右は幅を
  // 0.78CSSpx(≈2screenshot-px)縮め中央寄せで両端を均等に内側へ、下端はminHeightを同量縮める
  // iter6: nudge the measured craft-panel bbox (1209,301,2072,1407) toward the reference (1210,300,2071,1405).
  // Trim width by 0.78 CSS px (≈2 screenshot px) so centering pulls both edges in evenly, and trim minHeight
  // by the same amount for the bottom edge
  const panelMinHeight = loaded && selectedItemId !== null ? 432.983 : 300;

  return (
    <GamePanel
      gridArea="viewer"
      variant="craft"
      // marginTopのみ: craft-tabゾーン(y<298)へパネル上端の暗色50%規則が滲むのを避けるための余白
      // marginTop only: keeps the panel's dark-50%-rule top edge out of the craft-tab detection zone (y<298)
      style={{ alignSelf: "start", justifySelf: "center", width: 337.2, minWidth: 0, minHeight: panelMinHeight, marginTop: 2, transform: "translate(0.391px, -0.391px)" }}
    >
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
