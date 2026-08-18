import { describe, it, expect, beforeEach } from "vitest";
import { useTopicStore, deliverTopicPayload } from "./topicStore";
import { Topics } from "../transport/protocol";

// useTopicEventsが依存する不変条件を固定する。最新値1枠の読み出しでは1件目が失われる
// Locks the invariant useTopicEvents relies on; reading the single latest-value slot loses the first write
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

    // 最新値だけを読むと5が失われて合計が過少になる
    // Reading only the latest value would lose the 5 and undercount the total
    expect(observed).toEqual([5, 3]);
  });
});
