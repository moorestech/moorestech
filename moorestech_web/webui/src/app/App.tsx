import { lazy, Suspense } from "react";
import { Button, Loader, Overlay, Stack, Text } from "@mantine/core";
import { InventoryPanel, HotbarPanel } from "@/features/inventory";
import { RecipeViewer, ItemListPanel, clearSelectedItem } from "@/features/recipe";
import { ToastHost } from "@/features/toast";
import { ModalHost } from "@/features/modal";
import { ProgressBar } from "@/features/progress";
import { BlockInventoryPanel } from "@/features/blockInventory";
import { ResearchTreePanel } from "@/features/research";
import { BuildMenuPanel } from "@/features/buildMenu";
import { dispatchAction, useConnectionStatus, useTopicSelector, Topics } from "@/bridge";
import { screenForUiState, useGameLayerKeydown } from "@/shared/uiState";
import styles from "./App.module.css";

// dev 専用。static import すると本番バンドルに残るため import.meta.env.DEV 内で lazy 化
// Dev-only; a static import would ship to prod, so lazy-load it inside the import.meta.env.DEV guard
const DebugActionButton = import.meta.env.DEV ? lazy(() => import("./DebugActionButton")) : null;

// uGUI のインベントリ画面準拠の3カラム+下段ホットバーレイアウト
// Three-column layout with a bottom hotbar row, matching the uGUI inventory screen
export default function App() {
  // 一度接続した後の切断中のみオーバーレイを出す（初回接続前は各 panel の connecting... 表示に任せる）
  // Show the overlay only when disconnected after a prior connect (before first connect, panels show connecting...)
  const disconnected = useConnectionStatus() === "reconnecting";

  // ui_state.current による画面ルーティング（C# UIStateControl が正。セレクタはプリミティブを返す）
  // Screen routing by ui_state.current (C# UIStateControl is authoritative; the selector returns a primitive)
  const screen = useTopicSelector(Topics.uiState, (d) => screenForUiState(d?.state ?? null));

  // Esc でアイテム選択を解除する。modal 等のオーバーレイは自前で Esc を処理するため game レイヤーのみ
  // Esc clears item selection; overlays like the modal handle Esc themselves, so only at the game layer
  useGameLayerKeydown((e) => {
    if (e.key !== "Escape") return;
    clearSelectedItem();
  });

  return (
    <div className={styles.layout}>
      {screen !== "none" && <div className={styles.backdrop} data-testid="screen-backdrop" />}
      {/* 整理操作と開発用操作を画面右上へ独立配置する */}
      {/* Float sorting and development controls independently at the top-right */}
      {screen !== "none" && (
        <div style={{ position: "fixed", top: 12, right: 12, zIndex: 10, display: "flex", gap: 8 }}>
          <Button variant="default" size="compact-sm" onClick={() => void dispatchAction("inventory.sort", {})}>
            整理
          </Button>
          {DebugActionButton ? (
            <Suspense fallback={null}>
              <DebugActionButton />
            </Suspense>
          ) : null}
        </div>
      )}
      {/* 左下の常時キーヒント */}
      {/* Always-on key hints at the bottom-left */}
      {screen !== "none" && (
        <div className={styles.keyHints} data-testid="key-hints">
          <div><kbd>Tab/ESC</kbd>: インベントリを閉じる</div>
          <div><kbd>R</kbd>: リサーチツリー</div>
        </div>
      )}
      {screen !== "none" && <InventoryPanel />}
      {/* ホットバーは uGUI GameStateController 準拠の常時表示HUD（GameScreen中も出す） */}
      {/* The hotbar is an always-on HUD mirroring uGUI GameStateController (shown during GameScreen too) */}
      <HotbarPanel />
      {screen === "playerInventory" && <RecipeViewer />}
      {screen === "playerInventory" && <ItemListPanel />}
      {/* オーバーレイ系（grid セルでなく fixed/center 配置） */}
      {/* Overlays (fixed/centered, not grid cells) */}
      {screen === "researchTree" && <ResearchTreePanel />}
      {screen === "buildMenu" && <BuildMenuPanel />}
      <BlockInventoryPanel />
      <ModalHost />
      <ProgressBar />
      <ToastHost />
      {/* 再接続中は全面オーバーレイで操作をブロックする（Overlay 自体が pointer を捕捉する） */}
      {/* While reconnecting, a full-screen overlay blocks input (the Overlay itself captures pointers) */}
      {disconnected && (
        <Overlay fixed center backgroundOpacity={0.6} blur={2} zIndex={2000} data-testid="reconnect-overlay">
          <Stack align="center" gap="sm">
            <Loader color="gray" />
            <Text c="white" fw={500}>再接続中...</Text>
          </Stack>
        </Overlay>
      )}
    </div>
  );
}
