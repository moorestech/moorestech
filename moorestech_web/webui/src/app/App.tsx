import { lazy, Suspense } from "react";
import { Group, Loader, Overlay, Stack, Text, Title } from "@mantine/core";
import { InventoryPanel } from "@/features/inventory";
import { RecipeViewer, ItemListPanel } from "@/features/recipe";
import { ToastHost } from "@/features/toast";
import { ModalHost } from "@/features/modal";
import { ProgressBar } from "@/features/progress";
import { BlockInventoryPanel } from "@/features/blockInventory";
import { useTopicStore } from "@/bridge/topicStore";
import styles from "./App.module.css";

// dev 専用。static import すると本番バンドルに残るため import.meta.env.DEV 内で lazy 化
// Dev-only; a static import would ship to prod, so lazy-load it inside the import.meta.env.DEV guard
const DebugActionButton = import.meta.env.DEV ? lazy(() => import("./DebugActionButton")) : null;

// uGUI のインベントリ画面準拠の3カラム+下段ホットバーレイアウト
// Three-column layout with a bottom hotbar row, matching the uGUI inventory screen
export default function App() {
  // 一度接続した後の切断中のみオーバーレイを出す（初回接続前は各 panel の connecting... 表示に任せる）
  // Show the overlay only when disconnected after a prior connect (before first connect, panels show connecting...)
  const disconnected = useTopicStore((s) => s.status === "reconnecting");

  return (
    <div className={styles.layout}>
      <Group gap="md" style={{ gridArea: "header" }}>
        <Title order={1} size="h3">moorestech Web UI</Title>
        {DebugActionButton ? (
          <Suspense fallback={null}>
            <DebugActionButton />
          </Suspense>
        ) : null}
      </Group>
      <InventoryPanel />
      <RecipeViewer />
      <ItemListPanel />
      {/* オーバーレイ系（grid セルでなく fixed/center 配置） */}
      {/* Overlays (fixed/centered, not grid cells) */}
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
