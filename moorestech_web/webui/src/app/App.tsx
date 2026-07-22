import { useLayoutEffect, useRef } from "react";
import { Loader, Overlay, Portal, Stack, Text } from "@mantine/core";
import { InventoryPanel, HotbarPanel, GrabOverlay, InventoryScreenChrome } from "@/features/inventory";
import { RecipeViewer, ItemListPanel, RecipeSelectionKeyHandler } from "@/features/recipe";
import { ToastHost } from "@/features/toast";
import { NotificationHost } from "@/features/notification";
import { ModalHost } from "@/features/modal";
import { ProgressBar } from "@/features/progress";
import { BlockInventoryKeyHandler, BlockInventoryPanel } from "@/features/blockInventory";
import { ResearchTreePanel, ResearchScreenChrome } from "@/features/research";
import { BuildMenuPanel } from "@/features/buildMenu";
import { ChallengePanel, CurrentChallengeHud } from "@/features/challenge";
import { PauseMenuPanel } from "@/features/pauseMenu";
import { DeleteModeHud, PlacementModeHud } from "@/features/modeHud";
import { Crosshair } from "@/features/commonHud";
import { MiningHud } from "@/features/miningHud";
import { TrainRidingHud } from "@/features/trainHud";
import { CursorTooltip } from "@/shared/tooltip";
import { useI18n } from "@/shared/i18n";
import { SkitPresentation } from "@/features/skit";
import { TutorialOverlay } from "@/features/tutorial";
import { useConnectionStatus, useTopicSelector, Topics, UiStateNames } from "@/bridge";
import { screenForUiState } from "@/shared/uiState";
import { useWebInputExclusivity } from "@/shared/uiState/useWebInputExclusivity";
import styles from "./App.module.css";

// 基準stageをviewportへ収める一様拡縮を同期する
// Synchronize uniform scaling that fits the reference stage in the viewport
function useUiScale(enabled: boolean) {
  const stageRef = useRef<HTMLDivElement>(null);

  useLayoutEffect(() => {
    const updateScale = () => {
      const stage = stageRef.current;
      if (!stage) return;
      const scale = Math.min(window.innerWidth / stage.offsetWidth, window.innerHeight / stage.offsetHeight);
      document.documentElement.style.setProperty("--ui-scale", String(scale));
    };

    if (enabled) updateScale();
    window.addEventListener("resize", updateScale);
    return () => window.removeEventListener("resize", updateScale);
  }, [enabled]);

  return stageRef;
}

// uGUI のインベントリ画面準拠の3カラム+下段ホットバーレイアウト
// Three-column layout with a bottom hotbar row, matching the uGUI inventory screen
export default function App() {
  useWebInputExclusivity();
  const { t } = useI18n();

  // 一度接続した後の切断中のみオーバーレイを出す（初回接続前は各 panel の connecting... 表示に任せる）
  // Show the overlay only when disconnected after a prior connect (before first connect, panels show connecting...)
  const connectionStatus = useConnectionStatus();
  const disconnected = connectionStatus === "reconnecting" || connectionStatus === "restoring";

  // ui_state.current による画面ルーティング（C# UIStateControl が正。セレクタはプリミティブを返す）
  // Screen routing by ui_state.current (C# UIStateControl is authoritative; the selector returns a primitive)
  const screen = useTopicSelector(Topics.uiState, (d) => screenForUiState(d?.state ?? null, d?.subState));
  const uiState = useTopicSelector(Topics.uiState, (d) => d?.state ?? null);
  const uiVisible = useTopicSelector(Topics.uiVisibility, (d) => d?.visible ?? true);
  const cutScene = useTopicSelector(Topics.gameState, (d) => d?.state === "CutScene");
  const stageRef = useUiScale(uiVisible);
  // プレイヤーインベントリ本体を出すのは uGUI 準拠で持ち物・サブインベントリ画面のみ
  // Show the player inventory itself only on the inventory / sub-inventory screens, matching uGUI
  const inventoryScreen = screen === "playerInventory" || screen === "subInventory";
  const researchScreen = screen === "researchTree";
  // ビルドメニュー等の独立メニューも背景ディムは共有するが、インベントリは重畳しない
  // Standalone menus (build menu, etc.) share the dim backdrop but do not overlay the inventory
  const modalScreen = inventoryScreen || screen === "researchTree" || screen === "buildMenu" || screen === "challengeList" || screen === "pauseMenu" || screen === "trainPause";

  // Ctrl+U中はPortalを含む全Web UIをunmountする
  // Unmount the entire Web UI, including portals, while Ctrl+U is active
  if (!uiVisible || cutScene) return <div className={styles.hidden} data-web-ui-transparent />;

  return (
    <div className={styles.viewport} data-web-ui-transparent>
      {modalScreen && <div className={styles.backdrop} data-testid="screen-backdrop" />}
      <div ref={stageRef} className={styles.stage} data-web-ui-transparent>
        {inventoryScreen && <InventoryScreenChrome />}
        {researchScreen && <ResearchScreenChrome />}
        {(inventoryScreen || researchScreen) && <InventoryPanel />}
        {/* ホットバーは uGUI GameStateController 準拠の常時表示HUD（GameScreen中も出す） */}
        {/* The hotbar is an always-on HUD mirroring uGUI GameStateController (shown during GameScreen too) */}
        <HotbarPanel />
        {screen === "playerInventory" && <RecipeViewer />}
        {screen === "playerInventory" && <ItemListPanel />}
        {/* stage内オーバーレイを一様拡縮し、ModalはPortalでviewportへ描画する */}
        {/* Scale stage overlays uniformly while the Modal renders into the viewport via its portal */}
        {screen === "researchTree" && <ResearchTreePanel />}
        {screen === "buildMenu" && <BuildMenuPanel />}
        {screen === "challengeList" && <ChallengePanel />}
        {screen === "pauseMenu" && <PauseMenuPanel />}
        {screen === "trainPause" && <PauseMenuPanel />}
        {(screen === "trainHud" || screen === "trainPause") && <TrainRidingHud />}
        {uiState === UiStateNames.placeBlock && <PlacementModeHud />}
        {uiState === UiStateNames.deleteBar && <DeleteModeHud />}
        <Crosshair />
        <MiningHud />
        <CursorTooltip />
        <BlockInventoryPanel />
        <ModalHost />
        <ProgressBar />
        <BlockInventoryKeyHandler />
        <RecipeSelectionKeyHandler />
      </div>
      {(inventoryScreen || researchScreen) && <GrabOverlay />}
      <Portal>
        <ToastHost />
        <NotificationHost />
        <CurrentChallengeHud />
        <SkitPresentation />
        <TutorialOverlay />
      </Portal>
      {/* 再接続中は全面オーバーレイで操作をブロックする（Overlay 自体が pointer を捕捉する） */}
      {/* While reconnecting, a full-screen overlay blocks input (the Overlay itself captures pointers) */}
      {disconnected && (
        <Portal>
          <Overlay fixed center backgroundOpacity={0.6} blur={2} zIndex="var(--z-reconnect)" data-testid="reconnect-overlay">
            <Stack align="center" gap="sm">
              <Loader color="gray" />
              <Text c="white" fw={500}>{t("再接続中...")}</Text>
            </Stack>
          </Overlay>
        </Portal>
      )}
    </div>
  );
}
