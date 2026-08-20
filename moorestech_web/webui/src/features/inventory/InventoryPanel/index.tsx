import type { CSSProperties } from "react";
import { dispatchAction, useItemMaster, useTopic, Topics } from "@/bridge";
import { ConnectingPlaceholder, ItemSlot, PanelActionButton, SlotGrid, GamePanel } from "@/shared/ui";
import type { SlotRef } from "@/bridge";
import { slotActions } from "../slotActions";
import { L, useI18n } from "@/shared/i18n";
import { inventoryItemAnchorId, tutorialAnchor } from "@/shared/tutorialAnchor";
import { firstSlotIndexByItemId } from "../inventoryItemAnchors";

// 固定pxでピッチの端数ドリフトを防ぐ
// Use fixed-pixel slots and gaps to prevent fractional drift from the 140px screenshot pitch
// 正本の占有率へ寄せるため持ち物だけ1pxへ縮め、inv-white面隅プローブの合格を維持する
// Tighten inventory padding to 1px for the reference occupancy while preserving the inv-white corner probe
// 9列を等比縮小し実測値へ適用
// 枠:間隔比と余白は正本のまま
// The main inventory is 45 slots in 9 columns (PlayerInventoryConst.MainInventoryColumns). The 378px panel cannot grow — the
// three-column stage is fully used — so the measured pitch is uniformly scaled (×0.6603) to fit 9 columns into the same
// 319.6px grid footprint the 6-column layout had, preserving the slot:gap ratio and the reference side margins
const GRID_STYLE = { "--slot-size": "30.123px", "--slot-grid-gap": "6.064px", "--filled-face-inset": "1.565749px", "--face-inset-color": "rgb(50 52 67)", "--icon-pad": "1px", "--count-bottom": "-1px", "--count-font-size": "10.565px", "--count-letter-spacing": "0.12em", marginTop: "12px", marginLeft: "-0.549px" } as CSSProperties;

// 全スロットを操作。grabは別表示
// Handle every main-inventory slot; grab tracking renders separately
export default function InventoryPanel() {
  const { t } = useI18n();
  const inventory = useTopic(Topics.inventory);
  const itemMaster = useItemMaster();
  if (!inventory) {
    return <ConnectingPlaceholder style={{ gridArea: "inv" }} />;
  }
  // 整理は持ち物そのものへの副次アクションなので、パネルのタイトル行右端に置く
  // Sorting acts on the inventory itself, so it lives at the right end of this panel's title row
  const sortAction = (
    <PanelActionButton testId="inventory-sort" onClick={() => void dispatchAction("inventory.sort", {})}>
      {t(L.ui.inventory.sort)}
    </PanelActionButton>
  );

  // 所持スロットを指すアンカー
  // Anchors for "the slot holding this item"
  const firstSlots = firstSlotIndexByItemId(inventory.mainSlots);

  return (
    <GamePanel gridArea="inv" title={t(L.ui.inventory.title)} titleAction={sortAction} style={{ justifySelf: "start", alignSelf: "start", width: "var(--inventory-panel-width)", minHeight: 452.391, transform: "translate(0.783px, 0.783px)", "--panel-left": "-2.22px", "--panel-right": "-2.22px", "--title-shift-x": "-1.96px", "--title-scale-x": 0.919, "--title-scale-y": 0.924 } as CSSProperties}>
      <SlotGrid testId="main-grid" cols={9} style={GRID_STYLE}>
        {inventory.mainSlots.map((slot, i) => {
          const ref: SlotRef = { area: "main", slot: i };
          const itemGuid = firstSlots.get(slot.itemId) === i ? itemMaster?.get(slot.itemId)?.itemGuid : undefined;
          return (
            <div key={`main-${i}`} {...(itemGuid ? tutorialAnchor(inventoryItemAnchorId(itemGuid)) : {})}>
              <ItemSlot
                itemId={slot.itemId}
                count={slot.count}
                onLeftDown={(shiftKey) => slotActions.onLeftDown(ref, shiftKey)}
                onRightDown={() => slotActions.onRightDown(ref)}
                onRightEnter={() => slotActions.onRightEnter(ref)}
                onLeftEnter={() => slotActions.onLeftEnter(ref)}
                onDoubleClick={() => slotActions.onDoubleClick(ref)}
              />
            </div>
          );
        })}
      </SlotGrid>
    </GamePanel>
  );
}
