import type { ClientMsg } from "./protocol";
import { clearTopic } from "../store/topicStore";

// topic 毎の参照カウントで購読を一元管理し、0→1 で subscribe、1→0 で unsubscribe を送る
// Ref-counts subscriptions per topic and sends subscribe on 0→1, unsubscribe on 1→0
export class SubscriptionManager {
  private readonly counts = new Map<string, number>();
  private send: (msg: ClientMsg) => void;

  constructor(send: (msg: ClientMsg) => void) {
    this.send = send;
  }

  // 送信トランスポートを差し替える（実 socket の sendRaw を起動時に注入する）
  // Swap the send transport (the real socket's sendRaw is injected at startup)
  setSend(send: (msg: ClientMsg) => void) {
    this.send = send;
  }

  acquire(topic: string) {
    const next = (this.counts.get(topic) ?? 0) + 1;
    this.counts.set(topic, next);
    // 初回参照のみ subscribe を送る（切断中は送信が no-op で、resubscribe が再接続時に補う）
    // Send subscribe only on the first reference (no-op while disconnected; resubscribe covers it on reconnect)
    if (next === 1) this.send({ op: "subscribe", topics: [topic] });
  }

  release(topic: string) {
    const current = this.counts.get(topic) ?? 0;
    if (current > 1) {
      this.counts.set(topic, current - 1);
      return;
    }
    // 最終参照の解除で残値削除と unsubscribe。カウント削除で再接続時の再購読対象からも外れる
    // Last release clears retained data and unsubscribes; deleting the count also excludes reconnect resubscription
    this.counts.delete(topic);
    if (current === 1) {
      clearTopic(topic);
      this.send({ op: "unsubscribe", topics: [topic] });
    }
  }

  // 再接続時、参照カウント>0 の topic をまとめて再購読する
  // On reconnect, resubscribe every topic with refcount > 0 in a single batch
  resubscribe() {
    const topics = [...this.counts.keys()];
    if (topics.length > 0) this.send({ op: "subscribe", topics });
  }

  subscribedTopics(): string[] {
    return [...this.counts.keys()];
  }
}

// アプリ用シングルトン。transport は webSocketClient が起動時に注入する
// App singleton; the transport is injected by webSocketClient at startup
export const subscriptions = new SubscriptionManager(() => {});
