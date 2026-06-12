import InventoryView from "./components/InventoryView";
import ToastHost from "./components/ToastHost";
import DebugActionButton from "./components/DebugActionButton";

export default function App() {
  return (
    <div className="p-4 space-y-4">
      <h1 className="text-2xl font-bold">moorestech Web UI</h1>
      <DebugActionButton />
      <InventoryView />
      <ToastHost />
    </div>
  );
}
