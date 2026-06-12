import { useState } from "react";
import InventoryPanel from "./components/InventoryPanel";
import RecipeViewer from "./components/RecipeViewer";
import ItemListPanel from "./components/ItemListPanel";
import ToastHost from "./components/ToastHost";
import DebugActionButton from "./components/DebugActionButton";

// uGUI のインベントリ画面準拠の3カラム+下段ホットバーレイアウト
// Three-column layout with a bottom hotbar row, matching the uGUI inventory screen
export default function App() {
  // 右リストで選択、中央に表示
  // Picked in the right list, shown in the center
  const [selectedItemId, setSelectedItemId] = useState<number | null>(null);

  return (
    <div
      className="p-4 min-h-screen grid gap-6"
      style={{
        gridTemplateAreas: '"header header header" "inv viewer items" "hotbar hotbar hotbar"',
        gridTemplateColumns: "auto 1fr auto",
        gridTemplateRows: "auto 1fr auto",
      }}
    >
      <div className="flex items-center gap-4 [grid-area:header]">
        <h1 className="text-2xl font-bold">moorestech Web UI</h1>
        <DebugActionButton />
      </div>
      <InventoryPanel />
      <RecipeViewer itemId={selectedItemId} onSelect={setSelectedItemId} />
      <ItemListPanel selectedItemId={selectedItemId} onSelect={setSelectedItemId} />
      <ToastHost />
    </div>
  );
}
