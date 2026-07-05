import { useEffect } from "react";
import { Button, Group, Stack, Text, Title } from "@mantine/core";
import { useTopic, readTopic, dispatchAction, Topics } from "@/bridge";
import { useItemMaster } from "@/bridge/useItemMaster";
import { readActiveLayer } from "@/app/activeLayer";
import { ItemSlot, SlotGrid } from "@/shared/ui";
import type { InventoryArea, SlotData, SlotRef } from "@/bridge/payloadTypes";
import { resolveDirectMoveTarget } from "../inventoryLogic";
import { keyToHotbarIndex, cycleHotbar } from "../hotbarLogic";
import GrabOverlay from "./GrabOverlay";
import styles from "./style.module.css";

const GRAB: SlotRef = { area: "grab", slot: 0 };

// プレイヤーインベントリ（メイン4行+ホットバー1行+grab）の表示と操作
// Player inventory view & interactions: 4 main rows, 1 hotbar row, and the grab stack
// uGUI 準拠で inv 領域（メイン+Sort）と hotbar 領域（下段中央）の2要素を Fragment で返す
// Returns two grid children via Fragment, matching uGUI: inv area (main+Sort) and bottom-center hotbar area
export default function InventoryPanel() {
  const inventory = useTopic(Topics.inventory);
  const itemMaster = useItemMaster();

  // 1-9 キーでホットバー選択。リスナーは1回だけ張り、最新値は readTopic(getState) で読む
  // Keys 1-9 select a hotbar slot; attach the listener once and read the latest value via readTopic (getState)
  useEffect(() => {
    const onKeyDown = (e: KeyboardEvent) => {
      // 入力欄フォーカス中はゲーム操作を奪わない
      // Don't hijack typing while an input/textarea is focused
      const tag = document.activeElement?.tagName;
      if (tag === "INPUT" || tag === "TEXTAREA") return;
      // オーバーレイ表示中はゲーム系入力を止める（レイヤーが game のときのみ発火）
      // Suppress game inputs while an overlay is up (fires only when the layer is game)
      if (readActiveLayer() !== "game") return;
      const latest = readTopic(Topics.inventory);
      if (!latest) return;
      const index = keyToHotbarIndex(e.key);
      if (index === null || index >= latest.hotbarSlots.length) return;
      // 実際に選択が変わるときだけ送信する（uGUI 同様）
      // Dispatch only when the selection actually changes, matching uGUI
      if (index === latest.selectedHotbar) return;
      void dispatchAction("inventory.select_hotbar", { index });
    };
    window.addEventListener("keydown", onKeyDown);
    return () => window.removeEventListener("keydown", onKeyDown);
  }, []);

  // ホイールでホットバー選択を循環。変化時のみ送信し、オーバーレイ表示中は無効化する
  // Cycle the hotbar selection on wheel; dispatch only on change and suppress while an overlay is up
  const onHotbarWheel = (e: { deltaY: number }) => {
    if (readActiveLayer() !== "game") return;
    if (!inventory || inventory.hotbarSlots.length === 0) return;
    const delta = e.deltaY > 0 ? 1 : -1;
    const index = cycleHotbar(inventory.selectedHotbar, delta, inventory.hotbarSlots.length);
    if (index === inventory.selectedHotbar) return;
    void dispatchAction("inventory.select_hotbar", { index });
  };

  if (!inventory) {
    return <Text size="sm" c="dimmed" style={{ gridArea: "inv" }}>connecting...</Text>;
  }

  const grabHeld = inventory.grab.count > 0;

  // dispatchAction の true は「受理」であり topic 反映完了ではない。表示更新は event 駆動に任せる
  // dispatchAction's true means accepted, not topic-updated; rendering follows topic events
  const onLeftDown = (ref: SlotRef, slot: SlotData, shiftKey: boolean) => {
    if (grabHeld) {
      void dispatchAction("inventory.move_item", { from: GRAB, to: ref, count: inventory.grab.count });
      return;
    }
    if (slot.count === 0) return;
    if (shiftKey) {
      directMove(ref, slot);
      return;
    }
    void dispatchAction("inventory.move_item", { from: ref, to: GRAB, count: slot.count });
  };

  const onRightDown = (ref: SlotRef, slot: SlotData) => {
    if (grabHeld) {
      void dispatchAction("inventory.move_item", { from: GRAB, to: ref, count: 1 });
      return;
    }
    if (slot.count === 0) return;
    void dispatchAction("inventory.split", { from: ref });
  };

  // 収集先（grab か クリックスロットか）は host が自身の現在 grab 状態で決める。
  // Web はクリックされたスロットを送るだけ。dblclick 時点の grab 表示は古く信用できない
  // The host decides the target (grab vs clicked slot) from its own current grab state.
  // The web only sends the clicked slot; the grab view at dblclick time is stale and untrustworthy
  const onDoubleClick = (ref: SlotRef) => {
    void dispatchAction("inventory.collect", { slot: ref });
  };

  // Shift+クリック: 反対エリアの同種スタック→空スロットの順で移動先を探す
  // Shift-click: prefer a same-item stack in the opposite area, then an empty slot
  const directMove = (from: SlotRef, slot: SlotData) => {
    const targetArea: InventoryArea = from.area === "hotbar" ? "main" : "hotbar";
    const targetSlots = targetArea === "main" ? inventory.mainSlots : inventory.hotbarSlots;
    // マスタ未ロード時は maxStack 不明のため同種スタック探索をスキップし、空スロットのみ探す
    // Skip the same-item stack search while the item master is unloaded; fall back to empty slots
    const maxStack = itemMaster?.get(slot.itemId)?.maxStack;
    const target = resolveDirectMoveTarget(targetSlots, slot.itemId, maxStack);
    if (target < 0) return;
    void dispatchAction("inventory.move_item", { from, to: { area: targetArea, slot: target }, count: slot.count });
  };

  const renderSlot = (area: InventoryArea, index: number, slot: SlotData) => {
    const ref: SlotRef = { area, slot: index };
    // ホットバーのみ選択ハイライトを付与する
    // Highlight selection only for hotbar slots
    const selected = area === "hotbar" && index === inventory.selectedHotbar;
    return (
      <ItemSlot
        key={`${area}-${index}`}
        itemId={slot.itemId}
        count={slot.count}
        name={itemMaster?.get(slot.itemId)?.name}
        selected={selected}
        onLeftDown={(shiftKey) => onLeftDown(ref, slot, shiftKey)}
        onRightDown={() => onRightDown(ref, slot)}
        onDoubleClick={() => onDoubleClick(ref)}
      />
    );
  };

  return (
    <>
      <Stack gap="sm" style={{ gridArea: "inv" }}>
        <Group gap="sm">
          <Title order={2} size="h4">Inventory</Title>
          <Button variant="default" size="compact-sm" onClick={() => void dispatchAction("inventory.sort", {})}>
            Sort
          </Button>
        </Group>
        <SlotGrid testId="main-grid">
          {inventory.mainSlots.map((s, i) => renderSlot("main", i, s))}
        </SlotGrid>
      </Stack>
      {/* ホットバーは uGUI と同様に画面下段の中央へ独立配置 */}
      {/* The hotbar sits independently at the bottom center, matching uGUI */}
      <div className={styles.hotbarArea}>
        <SlotGrid testId="hotbar-grid" className={styles.hotbarFrame} onWheel={onHotbarWheel}>
          {inventory.hotbarSlots.map((s, i) => renderSlot("hotbar", i, s))}
        </SlotGrid>
      </div>
      <GrabOverlay grab={inventory.grab} />
    </>
  );
}
