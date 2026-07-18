import { useLayoutEffect, useRef } from "react";
import { Loader, Overlay, Portal, Stack, Text } from "@mantine/core";
import { InventoryPanel, HotbarPanel, GrabOverlay, InventoryScreenChrome } from "@/features/inventory";
import { RecipeViewer, ItemListPanel, RecipeSelectionKeyHandler } from "@/features/recipe";
import { ToastHost } from "@/features/toast";
import { ModalHost } from "@/features/modal";
import { ProgressBar } from "@/features/progress";
import { BlockInventoryKeyHandler, BlockInventoryPanel } from "@/features/blockInventory";
import { ResearchTreePanel } from "@/features/research";
import { BuildMenuPanel } from "@/features/buildMenu";
import { useConnectionStatus, useTopicSelector, Topics } from "@/bridge";
import { screenForUiState } from "@/shared/uiState";
import styles from "./App.module.css";

// 基準stageをviewportへ収める一様拡縮を同期する
// Synchronize uniform scaling that fits the reference stage in the viewport
function useUiScale() {
  const stageRef = useRef<HTMLDivElement>(null);

  useLayoutEffect(() => {
    const updateScale = () => {
      const stage = stageRef.current;
      if (!stage) return;
      const scale = Math.min(window.innerWidth / stage.offsetWidth, window.innerHeight / stage.offsetHeight);
      document.documentElement.style.setProperty("--ui-scale", String(scale));
    };

    updateScale();
    window.addEventListener("resize", updateScale);
    return () => window.removeEventListener("resize", updateScale);
  }, []);

  return stageRef;
}

// uGUI のインベントリ画面準拠の3カラム+下段ホットバーレイアウト
// Three-column layout with a bottom hotbar row, matching the uGUI inventory screen
export default function App() {
  const stageRef = useUiScale();

  // 一度接続した後の切断中のみオーバーレイを出す（初回接続前は各 panel の connecting... 表示に任せる）
  // Show the overlay only when disconnected after a prior connect (before first connect, panels show connecting...)
  const connectionStatus = useConnectionStatus();
  const disconnected = connectionStatus === "reconnecting" || connectionStatus === "restoring";

  // ui_state.current による画面ルーティング（C# UIStateControl が正。セレクタはプリミティブを返す）
  // Screen routing by ui_state.current (C# UIStateControl is authoritative; the selector returns a primitive)
  const screen = useTopicSelector(Topics.uiState, (d) => screenForUiState(d?.state ?? null));

  return (
    <div className={styles.viewport}>
      {screen !== "none" && <div className={styles.backdrop} data-testid="screen-backdrop" />}
      <div ref={stageRef} className={styles.stage}>
        {screen !== "none" && <InventoryScreenChrome />}
        {screen !== "none" && <InventoryPanel />}
        {/* ホットバーは uGUI GameStateController 準拠の常時表示HUD（GameScreen中も出す） */}
        {/* The hotbar is an always-on HUD mirroring uGUI GameStateController (shown during GameScreen too) */}
        <HotbarPanel />
        {screen === "playerInventory" && <RecipeViewer />}
        {screen === "playerInventory" && <ItemListPanel />}
        {/* stage内オーバーレイを一様拡縮し、ModalはPortalでviewportへ描画する */}
        {/* Scale stage overlays uniformly while the Modal renders into the viewport via its portal */}
        {screen === "researchTree" && <ResearchTreePanel />}
        {screen === "buildMenu" && <BuildMenuPanel />}
        <BlockInventoryPanel />
        <ModalHost />
        <ProgressBar />
        <BlockInventoryKeyHandler />
        <RecipeSelectionKeyHandler />
      </div>
      {screen !== "none" && <GrabOverlay />}
      <Portal>
        <ToastHost />
      </Portal>
      {/* 再接続中は全面オーバーレイで操作をブロックする（Overlay 自体が pointer を捕捉する） */}
      {/* While reconnecting, a full-screen overlay blocks input (the Overlay itself captures pointers) */}
      {disconnected && (
        <Portal>
          <Overlay fixed center backgroundOpacity={0.6} blur={2} zIndex="var(--z-reconnect)" data-testid="reconnect-overlay">
            <Stack align="center" gap="sm">
              <Loader color="gray" />
              <Text c="white" fw={500}>再接続中...</Text>
            </Stack>
          </Overlay>
        </Portal>
      )}
    </div>
  );
}
