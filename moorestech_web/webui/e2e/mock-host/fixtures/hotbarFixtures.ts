import type { HotbarData } from "../../../src/bridge/contract/payloadTypes";
import { blockIconUrl } from "../../../src/bridge/transport/httpEndpoints";
import { buildMenuEntryIds } from "./buildMenuFixtures";

// slot0選択済み、slot4は未解決
// Slot 0 carries the same wood-chest id as the build menu and is selected; slot 4 is assigned but unresolvable on the host (locked target or deleted blueprint)
export const hotbar: HotbarData = {
  slots: [
    { id: buildMenuEntryIds.woodChest, kind: "block", iconUrl: blockIconUrl(1) },
    ...Array.from({ length: 3 }, () => null),
    { id: buildMenuEntryIds.beltConveyor, kind: "unresolved" },
    ...Array.from({ length: 4 }, () => null),
  ],
  selectedSlot: 0,
};
