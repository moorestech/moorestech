import type { MachineInputStallData } from "@/bridge";
import { L, useI18n, useItemDisplayName } from "@/shared/i18n";
import BlockInfoUnavailableLine from "../blockInfoFetch/BlockInfoUnavailableLine";
import LackHighlightText from "../LackHighlightText";
import { machineInputStallMessageKey } from "./machineInputStallLogic";

// 搬入停止欄: レベルの違う品・製作上限を超える品が届いて止まっていることを、待っている品の名前つきで赤字で出す
// Inflow-stall section: in red, says an item of another level or above the craft cap arrived and stalled, naming the waiting item
export default function MachineInputStallSection({ stall }: { stall: MachineInputStallData }) {
  const { t } = useI18n();
  const itemDisplayName = useItemDisplayName();
  if (stall.fetchState !== "fetched") return <BlockInfoUnavailableLine unavailable={stall} targetKey={L.ui.blockInventory.machineInputStallInfoLabel} testId="machine-input-stall-fetch-state" />;
  return (
    <LackHighlightText insufficient={true} size="xs" testId="machine-input-stall">
      {t(machineInputStallMessageKey(stall), { itemName: itemDisplayName(stall.itemId) })}
    </LackHighlightText>
  );
}
