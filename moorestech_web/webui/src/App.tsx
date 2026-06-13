import InventoryPanel from "./components/InventoryPanel";
import RecipeViewer from "./components/RecipeViewer";
import ItemListPanel from "./components/ItemListPanel";
import ToastHost from "./components/ToastHost";
import DebugActionButton from "./components/DebugActionButton";

// uGUI のインベントリ画面準拠の3カラム+下段ホットバーレイアウト
// Three-column layout with a bottom hotbar row, matching the uGUI inventory screen
export default function App() {
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
      <RecipeViewer />
      <ItemListPanel />
      <ToastHost />
    </div>
  );
}
