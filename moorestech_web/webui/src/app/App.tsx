import { lazy, Suspense } from "react";
import { InventoryPanel } from "@/features/inventory";
import { RecipeViewer, ItemListPanel } from "@/features/recipe";
import { ToastHost } from "@/features/toast";
import styles from "./App.module.css";

// dev 専用。static import すると本番バンドルに残るため import.meta.env.DEV 内で lazy 化
// Dev-only; a static import would ship to prod, so lazy-load it inside the import.meta.env.DEV guard
const DebugActionButton = import.meta.env.DEV ? lazy(() => import("./DebugActionButton")) : null;

// uGUI のインベントリ画面準拠の3カラム+下段ホットバーレイアウト
// Three-column layout with a bottom hotbar row, matching the uGUI inventory screen
export default function App() {
  return (
    <div className={`p-4 min-h-screen ${styles.layout}`}>
      <div className="flex items-center gap-4 [grid-area:header]">
        <h1 className="text-2xl font-bold">moorestech Web UI</h1>
        {DebugActionButton ? (
          <Suspense fallback={null}>
            <DebugActionButton />
          </Suspense>
        ) : null}
      </div>
      <InventoryPanel />
      <RecipeViewer />
      <ItemListPanel />
      <ToastHost />
    </div>
  );
}
