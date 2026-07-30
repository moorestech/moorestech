import { useTopic, readTopic, dispatchAction, Topics } from "@/bridge";
import { useGameLayerKeydown, useGrabInteractive } from "@/shared/uiState";
import { ItemSlot } from "@/shared/ui";
import type { SlotRef } from "@/bridge";
import { keyToHotbarIndex } from "./hotbarLogic";
import { slotActions } from "../slotActions";
import styles from "./style.module.css";

// uGUI GameStateController 準拠の常時表示ホットバーHUD（UIState には依存しない）
// Always-on hotbar HUD mirroring uGUI GameStateController (independent of the UIState)
export default function HotbarPanel() {
  const inventory = useTopic(Topics.inventory);
  // 掴んだ絵が出ない画面では表示+キー選択のみ。クリックを許すのは grab が成立する画面だけ
  // Display + key selection where the held item cannot be seen; clicks are allowed only where a grab holds
  const grabInteractive = useGrabInteractive();

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

  // snapshot 未受信の間は HUD ごと出さない（connecting... 表示は InventoryPanel が担う）
  // Hide the whole HUD until the first snapshot (InventoryPanel owns the connecting... text)
  if (!inventory) return null;

  // 装備切替のホイールはスロット列の上でも生かす（列はスクロールを持たずゲーム操作の場のため）
  // Keep the equipment wheel alive over the slot row too: the row has no scrolling and belongs to the game
  return (
    <div className={styles.hotbarArea}>
      <div className={styles.hotbarFrame} data-testid="hotbar-grid" data-wheel-passthrough>
        {inventory.hotbarSlots.map((slot, i) => {
          const ref: SlotRef = { area: "hotbar", slot: i };
          return (
            <div key={`hotbar-${i}`} className={styles.cell}>
              <span className={styles.num}>{i + 1}</span>
              <ItemSlot
                itemId={slot.itemId}
                count={slot.count}
                selected={i === inventory.selectedHotbar}
                onLeftDown={grabInteractive ? (shiftKey) => slotActions.onLeftDown(ref, shiftKey) : undefined}
                onRightDown={grabInteractive ? () => slotActions.onRightDown(ref) : undefined}
                onRightEnter={grabInteractive ? () => slotActions.onRightEnter(ref) : undefined}
                onLeftEnter={grabInteractive ? () => slotActions.onLeftEnter(ref) : undefined}
                onDoubleClick={grabInteractive ? () => slotActions.onDoubleClick(ref) : undefined}
              />
            </div>
          );
        })}
      </div>
    </div>
  );
}
