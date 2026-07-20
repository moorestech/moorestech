import { describe, expect, it } from "vitest";
import { SplitDragSession } from "./splitDrag";

describe("SplitDragSession", () => {
  it("collects unique slots and sends one host-calculated distribution on release", () => {
    const sent: unknown[] = [];
    const session = new SplitDragSession((slots) => sent.push(slots));
    session.begin({ area: "main", slot: 1 }, true);
    session.enter({ area: "main", slot: 2 });
    session.enter({ area: "main", slot: 2 });
    session.end();
    expect(sent).toEqual([[{ area: "main", slot: 1 }, { area: "main", slot: 2 }]]);
  });

  it("does not start without a grabbed item", () => {
    const sent: unknown[] = [];
    const session = new SplitDragSession((slots) => sent.push(slots));
    session.begin({ area: "main", slot: 1 }, false);
    session.end();
    expect(sent).toEqual([]);
  });
});
