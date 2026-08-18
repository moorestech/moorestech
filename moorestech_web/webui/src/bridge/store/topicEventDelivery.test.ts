import { describe, it, expect, beforeEach } from "vitest";
import { useTopicStore, deliverTopicPayload } from "./topicStore";
import { Topics } from "../transport/protocol";

// useTopicEventsの前提を固定
// The invariant useTopicEvents relies on
describe("topic event delivery", () => {
  beforeEach(() => {
    useTopicStore.setState({ topics: {}, revisions: {} });
  });

  it("同一レンダー内の連続書き込みを購読者は全て観測できる", () => {
    const observed: number[] = [];
    let lastRevision = -1;
    const unsubscribe = useTopicStore.subscribe((state) => {
      const revision = state.revisions[Topics.notification] ?? -1;
      if (revision <= lastRevision) return;
      lastRevision = revision;
      const payload = state.topics[Topics.notification] as { count: number };
      observed.push(payload.count);
    });

    const earned = (seq: number, count: number) => deliverTopicPayload(Topics.notification, seq, {
      seq, category: "itemEarned", messageId: "itemEarned.mined", messageParams: [], itemId: 5, count,
    });
    earned(1, 5);
    earned(2, 3);
    unsubscribe();

    // 最新値だけ読むと5が失われる
    // Reading only the latest value loses the 5
    expect(observed).toEqual([5, 3]);
  });
});
