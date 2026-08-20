import { useEffect, useRef } from "react";
import { useTopic, useTopicSelector, readTopic, dispatchAction, Topics, useConnectionStatus } from "@/bridge";
import { isPointerOverWebUi, isWheelPassthrough, useGameLayerWheel, useGrabInteractive } from "@/shared/uiState";
import { ItemSlot } from "@/shared/ui";
import type { SlotRef } from "@/bridge";
import { accumulateWheelSteps, cycleEquipment } from "./equipmentLogic";
import { slotActions } from "../slotActions";
import { equipmentSlotAnchorId, tutorialAnchor, TutorialAnchorIds } from "@/shared/tutorialAnchor";
import styles from "./style.module.css";

// 装備スロットの常時表示HUD。枠数はトピックの equipment 長がそのまま正となる
// Always-on equipment HUD; the topic's equipment array length is authoritative for the slot count
export default function EquipmentPanel() {
  const wheelRemainder = useRef(0);
  const pendingSelections = useRef<{ index: number }[]>([]);
  const lastConfirmationRevision = useRef<number | null>(null);
  const inventory = useTopic(Topics.inventory);
  const connectionStatus = useConnectionStatus();
  // 掴んだ絵が出ない画面ではクリックを受けず、選択操作はホイールだけになる
  // Where the held item cannot be seen, clicks are refused and the wheel is the only selection input
  const grabInteractive = useGrabInteractive();
  // ホイールを占有中かはC#が判定して配る値をそのまま読む（種別からの再導出はしない）
  // Read the occupancy flag C# publishes as-is; never re-derive it from the target kind
  const wheelOwnedByBuildTool = useTopicSelector(
    Topics.placementMode,
    (data) => data?.wheelOwnedByTool === true,
  );

  // サーバー適用だけが進めるrevisionの差分だけ、送信順FIFOから確認済み要求を除く
  // Remove only the FIFO requests confirmed by server-only revision advances
  useEffect(() => {
    const revision = inventory?.equipmentSelectionConfirmationRevision;
    if (revision === undefined) return;

    const previousRevision = lastConfirmationRevision.current;
    lastConfirmationRevision.current = revision;
    if (previousRevision === null) return;
    if (revision < previousRevision) {
      pendingSelections.current = [];
      return;
    }

    const confirmedCount = revision - previousRevision;
    pendingSelections.current.splice(0, confirmedCount);
  }, [inventory?.equipmentSelectionConfirmationRevision]);

  // 切断後のsnapshotを権威状態として受け直すため、未確認要求は接続断で破棄する
  // Drop unconfirmed requests on disconnect so the restored snapshot becomes authoritative
  useEffect(() => {
    if (connectionStatus !== "open") pendingSelections.current = [];
  }, [connectionStatus]);

  // 装備を循環しオーバーレイ中は抑止
  // Cycle equipment; suppress during overlays
  useGameLayerWheel((e) => {
    // Web UI の上のホイールは一覧スクロール等その画面の操作であり、装備切替へ二重発火させない
    // A wheel over Web UI is that screen's own gesture (list scrolling etc.), so it must not also switch equipment
    // ただし常時表示HUD自身はスクロールを持たずゲーム操作の場なので、その上のホイールは装備切替へ通す
    // Always-on HUDs have no scrolling of their own and belong to the game, so a wheel over them still switches equipment
    if (isPointerOverWebUi(e.target) && !isWheelPassthrough(e.target)) return;

    // ホイールを占有する建築ツール中は装備切替へ流さない（購読値なので購読が無い間もnull扱いにならない）
    // Build tools that own the wheel suppress equipment switching; the subscribed value never degrades to null
    if (wheelOwnedByBuildTool) return;

    const latest = readTopic(Topics.inventory);
    if (!latest || latest.equipment.length === 0) return;
    const accumulated = accumulateWheelSteps(wheelRemainder.current, e.deltaY, e.deltaMode);
    wheelRemainder.current = accumulated.remainder;
    if (accumulated.steps === 0) return;
    // 未反映の送信値を起点にし、高速な連続ノッチが古いtopicへ巻き戻されないようにする
    // Start from the unacknowledged value so rapid notches never rewind to a stale topic
    const pending = pendingSelections.current;
    const currentIndex = pending.length === 0 ? latest.selectedEquipment : pending[pending.length - 1].index;
    const index = cycleEquipment(currentIndex, accumulated.steps, latest.equipment.length);
    const request = { index };
    pending.push(request);
    void dispatchAction("inventory.select_equipment", { index }).then((accepted) => {
      if (accepted) return;
      const requestIndex = pendingSelections.current.indexOf(request);
      if (requestIndex >= 0) pendingSelections.current.splice(requestIndex, 1);
    });
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
        // 選択中の枠はホイールで動くため、固定枠IDに加えて選択アンカーも同じ要素へ名乗らせる
        // The selected slot moves with the wheel, so that element declares the selection anchor alongside its fixed slot ID
        const selected = i === inventory.selectedEquipment;
        const anchor = selected
          ? tutorialAnchor(equipmentSlotAnchorId(i), TutorialAnchorIds.equipmentSelectedSlot)
          : tutorialAnchor(equipmentSlotAnchorId(i));
        return (
          <div key={`equipment-${i}`} {...anchor}>
            <ItemSlot
              itemId={slot.itemId}
              count={slot.count}
              selected={selected}
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
  );
}
