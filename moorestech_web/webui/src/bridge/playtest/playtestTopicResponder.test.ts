import { afterEach, expect, it, vi } from "vitest";
import { useTopicStore } from "../store/topicStore";
import { subscriptions } from "../transport/subscriptionManager";
import { initPlaytestTopicResponder } from "./playtestTopicResponder";

vi.mock("../transport/subscriptionManager", () => ({ subscriptions: { acquire: vi.fn(), release: vi.fn() } }));
const disposers: Array<() => void> = [];

afterEach(() => {
  disposers.forEach((dispose) => dispose());
  disposers.length = 0;
  vi.clearAllMocks();
});

it("同一topicを一度だけ登録し、古い解除は再登録を壊さない", () => {
  useTopicStore.setState({ topics: {}, status: "open" });
  const first = vi.fn();
  const second = vi.fn();
  const oldDispose = initPlaytestTopicResponder("query", first);
  disposers.push(oldDispose);
  expect(initPlaytestTopicResponder("query", first)).toBe(oldDispose);
  expect(subscriptions.acquire).toHaveBeenCalledTimes(1);
  oldDispose();
  const newDispose = initPlaytestTopicResponder("query", second);
  disposers.push(newDispose);
  oldDispose();
  useTopicStore.setState({ topics: { query: { id: "new" } } });
  expect(first).not.toHaveBeenCalled();
  expect(second).toHaveBeenCalledOnce();
  expect(subscriptions.release).toHaveBeenCalledTimes(1);
  newDispose();
  expect(subscriptions.release).toHaveBeenCalledTimes(2);
});

it("topicごとに管理し、再接続時は両方を再購読する", () => {
  useTopicStore.setState({ topics: {}, status: "open" });
  const query = vi.fn();
  const text = vi.fn();
  disposers.push(initPlaytestTopicResponder("query", query), initPlaytestTopicResponder("text", text));
  useTopicStore.setState({ status: "reconnecting" });
  expect(subscriptions.release).toHaveBeenCalledTimes(2);
  useTopicStore.setState({ status: "open" });
  expect(subscriptions.acquire).toHaveBeenCalledTimes(4);
  useTopicStore.setState({ topics: { query: { id: "q" }, text: { id: "t" } } });
  expect(query).toHaveBeenCalledOnce();
  expect(text).toHaveBeenCalledOnce();
});
