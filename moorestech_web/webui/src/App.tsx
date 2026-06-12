import InventoryPanel from "./components/InventoryPanel";
import CraftPanel from "./components/CraftPanel";
import ToastHost from "./components/ToastHost";
import DebugActionButton from "./components/DebugActionButton";

export default function App() {
  return (
    <div className="p-4 space-y-6">
      <h1 className="text-2xl font-bold">moorestech Web UI</h1>
      <DebugActionButton />
      <div className="flex gap-10 flex-wrap">
        <InventoryPanel />
        <CraftPanel />
      </div>
      <ToastHost />
    </div>
  );
}
