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
const GRID_STYLE = { "--slot-size": "3.08rem", "--slot-grid-gap": "0.36rem", marginTop: "12px" } as CSSProperties;

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
