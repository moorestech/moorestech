import { describe, it, expect, vi } from "vitest";
import { SubscriptionManager } from "./subscriptionManager";
import type { ClientMsg } from "./protocol";
import { deliverTopicPayload, useTopicStore } from "../store/topicStore";
import { readTopic } from "../store/useTopic";

// 送信された op を型付きで取り出すヘルパ
// Helper to pull sent ops out with their type
function opsOf(send: ReturnType<typeof vi.fn>, op: ClientMsg["op"]): ClientMsg[] {
  return send.mock.calls.map((c) => c[0] as ClientMsg).filter((m) => m.op === op);
}

describe("SubscriptionManager 参照カウント", () => {
  it("同一 topic への subscribe は初回参照時に1回だけ送る", () => {
    const send = vi.fn();
    const m = new SubscriptionManager(send);
    m.acquire("t");
    m.acquire("t");
    m.acquire("t");
    const subs = opsOf(send, "subscribe");
    expect(subs).toHaveLength(1);
    expect(subs[0]).toEqual({ op: "subscribe", topics: ["t"] });
  });

  it("最終参照の解除でのみ unsubscribe を送る", () => {
    const send = vi.fn();
    const m = new SubscriptionManager(send);
    m.acquire("t");
    m.acquire("t");
    m.release("t");
    expect(opsOf(send, "unsubscribe")).toHaveLength(0);
    m.release("t");
    const unsubs = opsOf(send, "unsubscribe");
    expect(unsubs).toHaveLength(1);
    expect(unsubs[0]).toEqual({ op: "unsubscribe", topics: ["t"] });
    expect(m.subscribedTopics()).toEqual([]);
  });

  it("解除後に再取得すると subscribe を再送する", () => {
    const send = vi.fn();
    const m = new SubscriptionManager(send);
    m.acquire("t");
    m.release("t");
    m.acquire("t");
    expect(opsOf(send, "subscribe")).toHaveLength(2);
  });

  it("acquire されていない topic の release は unsubscribe を送らない", () => {
    const send = vi.fn();
    const m = new SubscriptionManager(send);
    m.release("t");
    expect(opsOf(send, "unsubscribe")).toHaveLength(0);
  });

  it("最終参照の解除で topic の読み値を null に戻す", () => {
    const send = vi.fn();
    const m = new SubscriptionManager(send);
    useTopicStore.setState({ topics: {}, status: "connecting" });
    m.acquire("test.topic");
    deliverTopicPayload("test.topic", 1, { value: "latest" });

    m.release("test.topic");

    expect(readTopic("test.topic" as keyof import("./protocol").TopicPayloads)).toBeNull();
  });
});

describe("SubscriptionManager 再接続再購読", () => {
  it("resubscribe は参照カウント>0 の topic のみ一括で再購読する", () => {
    const send = vi.fn();
    const m = new SubscriptionManager(send);
    m.acquire("a");
    m.acquire("b");
    m.acquire("c");
    m.release("c"); // c は refcount 0 になり再購読対象から外れる
    send.mockClear();
    m.resubscribe();
    expect(send).toHaveBeenCalledTimes(1);
    const msg = send.mock.calls[0][0] as Extract<ClientMsg, { op: "subscribe" }>;
    expect(msg.op).toBe("subscribe");
    expect(new Set(msg.topics)).toEqual(new Set(["a", "b"]));
  });

  it("購読が空なら resubscribe は何も送らない", () => {
    const send = vi.fn();
    const m = new SubscriptionManager(send);
    m.resubscribe();
    expect(send).not.toHaveBeenCalled();
  });
});
