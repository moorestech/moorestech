import { createElement } from "react";
import { act, create } from "react-test-renderer";
import { describe, it, expect, beforeEach } from "vitest";
import { useTopicStore, deliverTopicPayload } from "./topicStore";
import { useTopicEvents } from "./useTopic";
import { Topics } from "../transport/protocol";

// 本番のフックをマウントして配信条件そのものを検証する
// Mounts the production hook so the delivery condition itself is under test
function Harness({ onEvent }: { onEvent: (count: number) => void }) {
  useTopicEvents(Topics.notification, (payload) => {
    if (!("count" in payload)) return;
    onEvent(payload.count);
  });
  return null;
}

function earn(seq: number, count: number) {
  return deliverTopicPayload(Topics.notification, seq, {
    seq, category: "itemEarned", messageId: "itemEarned.mined", messageParams: [], itemId: 5, count,
  });
}

describe("useTopicEvents delivery", () => {
  beforeEach(() => {
    useTopicStore.setState({ topics: {}, revisions: {}, restoringTopics: new Set() });
  });

  it("同一tickの連続配信を順に全て受け取る", () => {
    const observed: number[] = [];
    act(() => { create(createElement(Harness, { onEvent: (count) => observed.push(count) })); });

    act(() => { earn(1, 5); earn(2, 3); });

    // 最新値だけ読むと5が失われる
    // Reading only the latest value loses the 5
    expect(observed).toEqual([5, 3]);
  });

  it("購読前に届いていた値は配られない", () => {
    earn(1, 5);
    const observed: number[] = [];
    act(() => { create(createElement(Harness, { onEvent: (count) => observed.push(count) })); });

    expect(observed).toEqual([]);
  });

  it("unmount後は配られない", () => {
    const observed: number[] = [];
    let renderer: ReturnType<typeof create> | null = null;
    act(() => { renderer = create(createElement(Harness, { onEvent: (count) => observed.push(count) })); });
    act(() => { renderer!.unmount(); });

    act(() => { earn(1, 5); });
    expect(observed).toEqual([]);
  });

  it("再接続でrevisionが巻き戻っても配信は止まらない", () => {
    const observed: number[] = [];
    act(() => { create(createElement(Harness, { onEvent: (count) => observed.push(count) })); });
    act(() => { earn(9, 5); });

    // beginRestoreはrevisionを0から振り直す。高水位で待つと以後の通知が恒久的に止まる
    // beginRestore restarts revisions from zero; a high-water gate would stall every later notification forever
    act(() => { useTopicStore.getState().beginRestore([Topics.notification]); });
    act(() => { earn(1, 3); });

    expect(observed).toEqual([5, 3]);
  });
});
