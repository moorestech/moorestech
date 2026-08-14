import type { CSSProperties } from "react";
import { useTopic, Topics } from "@/bridge";
import { ConnectingPlaceholder, ItemSlot, SlotGrid, GamePanel } from "@/shared/ui";
import type { SlotRef } from "@/bridge";
import { slotActions } from "../slotActions";
import { L, useI18n } from "@/shared/i18n";

// 固定pxでピッチの端数ドリフトを防ぐ
// Use fixed-pixel slots and gaps to prevent fractional drift from the 140px screenshot pitch
// 正本の占有率へ寄せるため持ち物だけ1pxへ縮め、inv-white面隅プローブの合格を維持する
// Tighten inventory padding to 1px for the reference occupancy while preserving the inv-white corner probe
// メイン45枠=9列(PlayerInventoryConst.MainInventoryColumns)。パネル幅378pxは3カラム構成が使い切っており広げられないため、
// 6列時のグリッド外寸319.6pxへ9列を収める等比縮小(×0.6603)を実測値へ適用する。枠:間隔の比と左右余白は正本のまま
// The main inventory is 45 slots in 9 columns (PlayerInventoryConst.MainInventoryColumns). The 378px panel cannot grow — the
// three-column stage is fully used — so the measured pitch is uniformly scaled (×0.6603) to fit 9 columns into the same
// 319.6px grid footprint the 6-column layout had, preserving the slot:gap ratio and the reference side margins
const GRID_STYLE = { "--slot-size": "30.123px", "--slot-grid-gap": "6.064px", "--filled-face-inset": "1.565749px", "--face-inset-color": "rgb(50 52 67)", "--icon-pad": "1px", "--count-bottom": "-1px", "--count-font-size": "10.565px", "--count-letter-spacing": "0.12em", marginTop: "12px", marginLeft: "-0.549px" } as CSSProperties;

// メインインベントリ全スロットを操作する。grab追従は常時別表示
// Handle every main-inventory slot; grab tracking renders separately
export default function InventoryPanel() {
  const { t } = useI18n();
  const inventory = useTopic(Topics.inventory);
  if (!inventory) {
    return <ConnectingPlaceholder style={{ gridArea: "inv" }} />;
  }

  return (
    <GamePanel gridArea="inv" title={t(L.ui.inventory.title)} style={{ justifySelf: "start", alignSelf: "start", width: 378, minHeight: 452.391, transform: "translate(0.783px, 0.783px)", "--panel-left": "-2.22px", "--panel-right": "-2.22px", "--title-shift-x": "-1.96px", "--title-scale-x": 0.919, "--title-scale-y": 0.924 } as CSSProperties}>
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
