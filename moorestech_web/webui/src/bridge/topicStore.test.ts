import { describe, it, expect, vi, beforeEach } from "vitest";

// notify は sink 未注入で no-op のため、呼び出しを検証できるようモックする
// Mock notify (a no-op without an injected sink) so its calls can be asserted
vi.mock("./notify", () => ({ notify: vi.fn() }));

import { useTopicStore, deliverTopicPayload } from "./topicStore";
import { notify } from "./notify";
import { Topics } from "./protocol";

const validInventory = {
  mainSlots: [{ itemId: 1, count: 2 }],
  hotbarSlots: [{ itemId: 0, count: 0 }],
  grab: { itemId: 0, count: 0 },
  selectedHotbar: 0,
};

describe("deliverTopicPayload の validator 連携", () => {
  beforeEach(() => {
    useTopicStore.setState({ topics: {}, status: "connecting" });
    vi.mocked(notify).mockClear();
  });

  it("契約違反 payload はストアに届かず notify する", () => {
    const warn = vi.spyOn(console, "warn").mockImplementation(() => {});
    const ok = deliverTopicPayload(Topics.inventory, { mainSlots: "nope" });
    expect(ok).toBe(false);
    expect(useTopicStore.getState().topics[Topics.inventory]).toBeUndefined();
    expect(notify).toHaveBeenCalledOnce();
    warn.mockRestore();
  });

  it("妥当な payload はストアへ反映し notify しない", () => {
    const ok = deliverTopicPayload(Topics.inventory, validInventory);
    expect(ok).toBe(true);
    expect(useTopicStore.getState().topics[Topics.inventory]).toEqual(validInventory);
    expect(notify).not.toHaveBeenCalled();
  });
});

describe("topic 毎キー保持による stale 値の解消", () => {
  beforeEach(() => useTopicStore.setState({ topics: {}, status: "connecting" }));

  it("topic を変えて読むと前 topic の値が残らない", () => {
    // useTopic のセレクタ読み出しと同じロジックで検証する
    // Verify with the same read logic as useTopic's selector
    const read = (topic: string) => useTopicStore.getState().topics[topic] ?? null;

    deliverTopicPayload(Topics.inventory, validInventory);
    expect(read(Topics.inventory)).toEqual(validInventory);
    // 別 topic は未着なので null（前 topic の値が漏れ出さない）
    // The other topic hasn't arrived, so it reads null (no leak of the previous topic's value)
    expect(read(Topics.progress)).toBeNull();

    deliverTopicPayload(Topics.progress, { visible: true, progress: 0.5 });
    expect(read(Topics.progress)).toEqual({ visible: true, progress: 0.5 });
    // inventory は依然保持される（再接続を跨いだ保持の担保）
    // inventory is still retained (guarantees values survive across reconnects)
    expect(read(Topics.inventory)).toEqual(validInventory);
  });
});
