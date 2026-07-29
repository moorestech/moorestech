import { useRef } from "react";
import { useTopic, readTopic, dispatchAction, Topics } from "@/bridge";
import { isPointerOverWebUi, isWheelPassthrough, useGameLayerWheel, useScreenInteractive } from "@/shared/uiState";
import { ItemSlot } from "@/shared/ui";
import type { SlotRef } from "@/bridge";
import { accumulateWheelSteps, cycleEquipment } from "./equipmentLogic";
import { slotActions } from "../slotActions";
import styles from "./style.module.css";

// 装備スロットの常時表示HUD。枠数はトピックの equipment 長がそのまま正となる
// Always-on equipment HUD; the topic's equipment array length is authoritative for the slot count
export default function EquipmentPanel() {
  const wheelRemainder = useRef(0);
  const inventory = useTopic(Topics.inventory);
  // GameScreen 中はカーソルロックでクリックできないため、選択操作はホイールだけになる
  // The cursor is locked during GameScreen, so the wheel is the only selection input there
  const interactive = useScreenInteractive();

  // ホイールで素手を含む装備選択を循環。変化時のみ送信し、オーバーレイ表示中は共有フックが抑止する
  // Cycle the equipment selection (bare hands included) on wheel; dispatch only on change, with the shared hook suppressing overlays
  useGameLayerWheel((e) => {
    // Web UI の上のホイールは一覧スクロール等その画面の操作であり、装備切替へ二重発火させない
    // A wheel over Web UI is that screen's own gesture (list scrolling etc.), so it must not also switch equipment
    // ただし常時表示HUD自身はスクロールを持たずゲーム操作の場なので、その上のホイールは装備切替へ通す
    // Always-on HUDs have no scrolling of their own and belong to the game, so a wheel over them still switches equipment
    if (isPointerOverWebUi(e.target) && !isWheelPassthrough(e.target)) return;
    const latest = readTopic(Topics.inventory);
    if (!latest || latest.equipment.length === 0) return;
    const accumulated = accumulateWheelSteps(wheelRemainder.current, e.deltaY);
    wheelRemainder.current = accumulated.remainder;
    if (accumulated.steps === 0) return;
    const index = cycleEquipment(latest.selectedEquipment, accumulated.steps, latest.equipment.length);
    if (index === latest.selectedEquipment) return;
    void dispatchAction("inventory.select_equipment", { index });
  });

  // snapshot 未受信の間は HUD ごと出さない（HotbarPanel と同じ判断）
  // Hide the whole HUD until the first snapshot, matching HotbarPanel
  if (!inventory) return null;

  // クリックは他のスロットと同じアイテム移動。装備の選択はホイール専用にする
  // Clicks are ordinary item moves like every other slot; equipment selection belongs to the wheel alone
  return (
    <div className={styles.equipmentArea} data-testid="equipment-slots" data-wheel-passthrough>
      {inventory.equipment.map((slot, i) => {
        const ref: SlotRef = { area: "equipment", slot: i };
        return (
          <ItemSlot
            key={`equipment-${i}`}
            itemId={slot.itemId}
            count={slot.count}
            selected={i === inventory.selectedEquipment}
            onLeftDown={interactive ? (shiftKey) => slotActions.onLeftDown(ref, shiftKey) : undefined}
            onRightDown={interactive ? () => slotActions.onRightDown(ref) : undefined}
            onRightEnter={interactive ? () => slotActions.onRightEnter(ref) : undefined}
            onLeftEnter={interactive ? () => slotActions.onLeftEnter(ref) : undefined}
            onDoubleClick={interactive ? () => slotActions.onDoubleClick(ref) : undefined}
          />
        );
      })}
    </div>
  );
}
