import type { CSSProperties } from "react";
import { dispatchAction, useTopic, Topics } from "@/bridge";
import { ConnectingPlaceholder, ItemSlot, PanelActionButton, SlotGrid, GamePanel } from "@/shared/ui";
import type { SlotRef } from "@/bridge";
import { slotActions } from "../slotActions";
import { L, useI18n } from "@/shared/i18n";

// 固定pxでピッチの端数ドリフトを防ぐ
// Use fixed-pixel slots and gaps to prevent fractional drift from the 140px screenshot pitch
// 正本の占有率へ寄せるため持ち物だけ1pxへ縮め、inv-white面隅プローブの合格を維持する
// Tighten inventory padding to 1px for the reference occupancy while preserving the inv-white corner probe
// 基準寸法・倍率はtokens.cssの--inventory-slot-base-*/--inventory-slot-scaleが正本。ここでは二重著述しない
// Base dimensions and scale live in tokens.css's --inventory-slot-base-*/--inventory-slot-scale; not duplicated here
const GRID_STYLE = {
  "--slot-size": "calc(var(--inventory-slot-base-size) * var(--inventory-slot-scale))",
  "--slot-grid-gap": "calc(var(--inventory-slot-base-gap) * var(--inventory-slot-scale))",
  "--filled-face-inset": "1.565749px",
  "--face-inset-color": "rgb(50 52 67)",
  "--icon-pad": "1px",
  "--count-bottom": "-1px",
  "--count-font-size": "calc(var(--inventory-count-base-font-size) * var(--inventory-slot-scale))",
  "--count-letter-spacing": "0.12em",
  marginTop: "12px",
  marginLeft: "-0.549px",
} as CSSProperties;

// 全スロットを操作。grabは別表示
// Handle every main-inventory slot; grab tracking renders separately
export default function InventoryPanel() {
  const { t } = useI18n();
  const inventory = useTopic(Topics.inventory);
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

  return (
    <GamePanel gridArea="inv" title={t(L.ui.inventory.title)} titleAction={sortAction} style={{ justifySelf: "start", alignSelf: "start", width: "var(--inventory-panel-width)", minHeight: 452.391, transform: "translate(0.783px, 0.783px)", "--panel-left": "-2.22px", "--panel-right": "-2.22px", "--title-shift-x": "-1.96px", "--title-scale-x": 0.919, "--title-scale-y": 0.924 } as CSSProperties}>
      <SlotGrid testId="main-grid" cols={9} style={GRID_STYLE}>
        {inventory.mainSlots.map((slot, i) => {
          const ref: SlotRef = { area: "main", slot: i };
          return (
            <ItemSlot
              key={`main-${i}`}
              itemId={slot.itemId}
              count={slot.count}
              onLeftDown={(shiftKey) => slotActions.onLeftDown(ref, shiftKey)}
              onRightDown={() => slotActions.onRightDown(ref)}
              onRightEnter={() => slotActions.onRightEnter(ref)}
              onLeftEnter={() => slotActions.onLeftEnter(ref)}
              onDoubleClick={() => slotActions.onDoubleClick(ref)}
            />
          );
        })}
      </SlotGrid>
    </GamePanel>
  );
}
