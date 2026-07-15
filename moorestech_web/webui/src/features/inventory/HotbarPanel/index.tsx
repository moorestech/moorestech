import { useTopic, useTopicSelector, readTopic, dispatchAction, Topics, useItemMaster } from "@/bridge";
import { readActiveLayer, screenForUiState, useGameLayerKeydown } from "@/shared/uiState";
import { ItemSlot } from "@/shared/ui";
import type { SlotRef } from "@/bridge/contract/payloadTypes";
import { keyToHotbarIndex, cycleHotbar } from "../hotbarLogic";
import { createSlotActions } from "../slotActions";
import styles from "./style.module.css";

// uGUI GameStateController 準拠の常時表示ホットバーHUD（UIState には依存しない）
// Always-on hotbar HUD mirroring uGUI GameStateController (independent of the UIState)
export default function HotbarPanel() {
  const inventory = useTopic(Topics.inventory);
  const itemMaster = useItemMaster();

  // GameScreen 中は表示+キー/ホイール選択のみ（uGUI はカーソルロックでクリック不能）
  // Display + key/wheel selection only during GameScreen (uGUI locks the cursor, so no clicks)
  const interactive = useTopicSelector(Topics.uiState, (d) => screenForUiState(d?.state ?? null) !== "none");

  // 1-9 キーでホットバー選択。ゲートは共有フックが担い、最新値は readTopic で読む
  // Keys 1-9 select a hotbar slot; the shared hook gates it and the latest value comes via readTopic
  useGameLayerKeydown((e) => {
    const latest = readTopic(Topics.inventory);
    if (!latest) return;
    const index = keyToHotbarIndex(e.key);
    if (index === null || index >= latest.hotbarSlots.length) return;
    // 実際に選択が変わるときだけ送信する（uGUI 同様）
    // Dispatch only when the selection actually changes, matching uGUI
    if (index === latest.selectedHotbar) return;
    void dispatchAction("inventory.select_hotbar", { index });
  });

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

  // snapshot 未受信の間は HUD ごと出さない（connecting... 表示は InventoryPanel が担う）
  // Hide the whole HUD until the first snapshot (InventoryPanel owns the connecting... text)
  if (!inventory) return null;

  const actions = interactive ? createSlotActions(inventory, itemMaster) : null;

  return (
    <div className={styles.hotbarArea}>
      <div className={styles.hotbarFrame} data-testid="hotbar-grid" onWheel={onHotbarWheel}>
        {inventory.hotbarSlots.map((slot, i) => {
          const ref: SlotRef = { area: "hotbar", slot: i };
          return (
            <div key={`hotbar-${i}`} className={styles.cell}>
              <span className={styles.num}>{i + 1}</span>
              <ItemSlot
                itemId={slot.itemId}
                count={slot.count}
                name={itemMaster?.get(slot.itemId)?.name}
                selected={i === inventory.selectedHotbar}
                onLeftDown={actions ? (shiftKey) => actions.onLeftDown(ref, slot, shiftKey) : undefined}
                onRightDown={actions ? () => actions.onRightDown(ref, slot) : undefined}
                onDoubleClick={actions ? () => actions.onDoubleClick(ref) : undefined}
              />
            </div>
          );
        })}
      </div>
    </div>
  );
}
