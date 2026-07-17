import type { CSSProperties } from "react";
import { Text } from "@mantine/core";
import { useTopic, Topics, useItemMaster } from "@/bridge";
import { ItemSlot, SlotGrid, GamePanel } from "@/shared/ui";
import type { SlotRef } from "@/bridge/contract/payloadTypes";
import { createSlotActions } from "../slotActions";

// 正本のスロット外形123px・間隔16pxへ寄せる局所上書き（他グリッドの既定値は変えない）。marginTopはiter2で
// ヘッダーを固定高(19px)にした際に格子が上へずれた分の補正（レシピ側のScrollArea marginTopと揃える）
// Local override toward the reference's 123px slot face / 16px gap (other grids keep their defaults). marginTop
// corrects the grid shift introduced when the header became a fixed 19px height in iter2 (matches the recipe side's ScrollArea marginTop)
// slot-sizeを0.08rem縮めgapへ同量足したのはiter4のベベル(box-shadowリング)追加分の補正。ピッチ(size+gap)は
// 不変のまま、枠込みの外形検出(border-to-border)だけを正本の123pxへ戻す
// slot-size is trimmed 0.08rem and gap grows by the same amount to offset iter4's new bevel (box-shadow ring)
// thickness; pitch (size+gap) stays put while the border-to-border footprint detection returns to the reference's 123px
// iter6: :rootの既定ベベルをサブピクセル(0.22px)から可視な明灰リング(1.57px)へ太らせた分、border-to-border
// 検出(bevel込み)がさらに膨らむため、slot-sizeをもう一段(約0.154rem)縮めgapへ回して123pxへ再収束させる
// iter6: The default :root bevel grew from sub-pixel (0.22px) to a genuinely visible light-gray ring (1.57px),
// which inflates the border-to-border (bevel-inclusive) detection further; trim slot-size another ~0.154rem
// and hand it to the gap to re-converge on the 123px target
// 正本の占有率へ寄せるため持ち物だけ1pxへ縮め、inv-white面隅プローブの合格を維持する
// Tighten inventory padding to 1px for the reference occupancy while preserving the inv-white corner probe
const GRID_STYLE = { "--slot-size": "2.9rem", "--slot-grid-gap": "0.54rem", "--icon-pad": "1px", "--count-bottom": "-1px", "--count-font-size": "16px", "--count-letter-spacing": "0.12em", marginTop: "12px", marginLeft: "1px" } as CSSProperties;

// メイン4行を操作する。grab追従とホットバーは常時別表示
// Handle four main rows; grab tracking and the hotbar render separately
export default function InventoryPanel() {
  const inventory = useTopic(Topics.inventory);
  const itemMaster = useItemMaster();

  if (!inventory) {
    return <Text size="sm" c="dimmed" style={{ gridArea: "inv" }}>connecting...</Text>;
  }

  // クリック操作は HotbarPanel と共通のファクトリで生成する
  // Click interactions come from the factory shared with HotbarPanel
  const actions = createSlotActions(inventory, itemMaster);

  return (
    <GamePanel gridArea="inv" title="持ち物" style={{ justifySelf: "start", alignSelf: "start", width: 378, minHeight: 452 }}>
      <SlotGrid testId="main-grid" cols={6} style={GRID_STYLE}>
        {inventory.mainSlots.map((slot, i) => {
          const ref: SlotRef = { area: "main", slot: i };
          return (
            <ItemSlot
              key={`main-${i}`}
              itemId={slot.itemId}
              count={slot.count}
              name={itemMaster?.get(slot.itemId)?.name}
              onLeftDown={(shiftKey) => actions.onLeftDown(ref, slot, shiftKey)}
              onRightDown={() => actions.onRightDown(ref, slot)}
              onDoubleClick={() => actions.onDoubleClick(ref)}
            />
          );
        })}
      </SlotGrid>
    </GamePanel>
  );
}
