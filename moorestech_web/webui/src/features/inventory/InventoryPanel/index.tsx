import type { CSSProperties } from "react";
import { useTopic, Topics } from "@/bridge";
import { ConnectingPlaceholder, ItemSlot, SlotGrid, GamePanel } from "@/shared/ui";
import type { SlotRef } from "@/bridge";
import { slotActions } from "../slotActions";
import { useI18n } from "@/shared/i18n";

// 固定pxでピッチの端数ドリフトを防ぐ
// Use fixed-pixel slots and gaps to prevent fractional drift from the 140px screenshot pitch
// 正本の占有率へ寄せるため持ち物だけ1pxへ縮め、inv-white面隅プローブの合格を維持する
// Tighten inventory padding to 1px for the reference occupancy while preserving the inv-white corner probe
const GRID_STYLE = { "--slot-size": "45.617px", "--slot-grid-gap": "9.183px", "--filled-face-inset": "1.565749px", "--face-inset-color": "rgb(50 52 67)", "--icon-pad": "1px", "--count-bottom": "-1px", "--count-font-size": "16px", "--count-letter-spacing": "0.12em", marginTop: "12px", marginLeft: "-0.549px" } as CSSProperties;

// メイン4行を操作する。grab追従とホットバーは常時別表示
// Handle four main rows; grab tracking and the hotbar render separately
export default function InventoryPanel() {
  const { t } = useI18n();
  const inventory = useTopic(Topics.inventory);
  if (!inventory) {
    return <ConnectingPlaceholder style={{ gridArea: "inv" }} />;
  }

  return (
    <GamePanel gridArea="inv" title={t("持ち物")} style={{ justifySelf: "start", alignSelf: "start", width: 378, minHeight: 452.391, transform: "translate(0.783px, 0.783px)", "--panel-left": "-2.22px", "--panel-right": "-2.22px", "--title-shift-x": "-1.96px", "--title-scale-x": 0.919, "--title-scale-y": 0.924 } as CSSProperties}>
      <SlotGrid testId="main-grid" cols={6} style={GRID_STYLE}>
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
