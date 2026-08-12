import type { HotbarData } from "../../../src/bridge/contract/payloadTypes";
import { blockIconUrl } from "../../../src/bridge/transport/httpEndpoints";
import { buildMenuEntryIds } from "./buildMenuFixtures";

// 未割当は9枠すべてnull。slot0だけbuildMenuと同一idの木箱を割り当てて選択済みにする
// All 9 slots unassigned; only slot 0 carries the same wood-chest id as the build menu and is selected
export const hotbar: HotbarData = {
  slots: [
    { id: buildMenuEntryIds.woodChest, kind: "block", label: "Wood Chest", iconUrl: blockIconUrl(1) },
    ...Array.from({ length: 8 }, () => null),
  ],
  selectedSlot: 0,
};
