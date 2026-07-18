import { describe, it, expect, vi, beforeEach } from "vitest";

// notify は sink 未注入で no-op のため、呼び出しを検証できるようモックする
// Mock notify (a no-op without an injected sink) so its calls can be asserted
vi.mock("../transport/notify", () => ({ notify: vi.fn() }));

import { useTopicStore, deliverTopicPayload } from "./topicStore";
import { notify } from "../transport/notify";
import { Topics } from "../transport/protocol";

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
    const ok = deliverTopicPayload(Topics.inventory, 1, { mainSlots: "nope" });
    expect(ok).toBe(false);
    expect(useTopicStore.getState().topics[Topics.inventory]).toBeUndefined();
    expect(notify).toHaveBeenCalledOnce();
    warn.mockRestore();
  });

  it("妥当な payload はストアへ反映し notify しない", () => {
    const ok = deliverTopicPayload(Topics.inventory, 1, validInventory);
    expect(ok).toBe(true);
    expect(useTopicStore.getState().topics[Topics.inventory]).toEqual(validInventory);
    expect(notify).not.toHaveBeenCalled();
  });
});

describe("topic 毎キー保持による stale 値の解消", () => {
  beforeEach(() => useTopicStore.setState({ topics: {}, revisions: {}, restoringTopics: new Set(), status: "connecting" }));

  it("topic を変えて読むと前 topic の値が残らない", () => {
    // useTopic のセレクタ読み出しと同じロジックで検証する
    // Verify with the same read logic as useTopic's selector
    const read = (topic: string) => useTopicStore.getState().topics[topic] ?? null;

    deliverTopicPayload(Topics.inventory, 1, validInventory);
    expect(read(Topics.inventory)).toEqual(validInventory);
    // 別 topic は未着なので null（前 topic の値が漏れ出さない）
    // The other topic hasn't arrived, so it reads null (no leak of the previous topic's value)
    expect(read(Topics.progress)).toBeNull();

    deliverTopicPayload(Topics.progress, 1, { visible: true, progress: 0.5 });
    expect(read(Topics.progress)).toEqual({ visible: true, progress: 0.5 });
    // inventory は依然保持される（再接続を跨いだ保持の担保）
    // inventory is still retained (guarantees values survive across reconnects)
    expect(read(Topics.inventory)).toEqual(validInventory);
  });
});

describe("topic revision ordering", () => {
  beforeEach(() => useTopicStore.setState({ topics: {}, revisions: {}, status: "connecting" }));

  it("drops an older snapshot after a newer event", () => {
    expect(deliverTopicPayload(Topics.progress, 8, { visible: true, progress: 0.8 })).toBe(true);
    expect(deliverTopicPayload(Topics.progress, 7, { visible: true, progress: 0.7 })).toBe(false);
    expect(useTopicStore.getState().topics[Topics.progress]).toEqual({ visible: true, progress: 0.8 });
  });

  it("accepts revision zero after reconnect generation reset", () => {
    deliverTopicPayload(Topics.progress, 8, { visible: true, progress: 0.8 });
    useTopicStore.getState().beginRestore([Topics.progress]);
    expect(deliverTopicPayload(Topics.progress, 0, { visible: false, progress: 0 })).toBe(true);
    expect(useTopicStore.getState().status).toBe("open");
  });
});
