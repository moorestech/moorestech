import type { HotbarData } from "../../../src/bridge/contract/payloadTypes";
import { blockIconUrl } from "../../../src/bridge/transport/httpEndpoints";
import { buildMenuEntryIds } from "./buildMenuFixtures";

// slot0はbuildMenuと同一idの木箱で選択済み。slot4は割当済みだがホストが解決できない枠(未解放・削除済みBP)
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
