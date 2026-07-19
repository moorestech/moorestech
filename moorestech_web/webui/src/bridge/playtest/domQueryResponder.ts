import { useTopicStore } from "../store/topicStore";
import { Topics } from "../transport/protocol";
import { subscriptions } from "../transport/subscriptionManager";
import { sendAction } from "../transport/webSocketClient";

type DomQueryRequest = {
  requestId: string;
  testid: string;
};

type DomQueryResult = {
  requestId: string;
  found: boolean;
  x: number;
  y: number;
  width: number;
  height: number;
  devicePixelRatio: number;
  hitTestPassed: boolean;
};

type TopicState = ReturnType<typeof useTopicStore.getState>;

let disposeResponder: (() => void) | null = null;

// DOM問い合わせの受信を一度だけ登録し、アプリ終了時に破棄できるようにする
// Register DOM query reception once and allow teardown at application shutdown
export function initDomQueryResponder(): () => void {
  if (disposeResponder !== null) return disposeResponder;

  let topicAcquired = false;
  const unsubscribeStore = useTopicStore.subscribe((state, previousState) => {
    syncTopicSubscription(state.status);
    respondToNewRequest(state, previousState);
  });

  const dispose = () => {
    if (disposeResponder !== dispose) return;
    unsubscribeStore();
    if (topicAcquired) subscriptions.release(Topics.playtestDomQuery);
    disposeResponder = null;
  };

  disposeResponder = dispose;
  syncTopicSubscription(useTopicStore.getState().status);
  return dispose;

  function syncTopicSubscription(status: TopicState["status"]) {
    // snapshotのないevent topicは通常topicの復元完了後だけ購読する
    // Subscribe to the snapshot-less event topic only after regular topics restore
    if (status === "open" && !topicAcquired) {
      topicAcquired = true;
      subscriptions.acquire(Topics.playtestDomQuery);
      return;
    }

    // 切断時に解除し、次回のrestoringTopicsへ混ざることを防ぐ
    // Release on disconnect so it never enters the next restoringTopics set
    if (status !== "open" && topicAcquired) {
      topicAcquired = false;
      subscriptions.release(Topics.playtestDomQuery);
    }
  }
}

function respondToNewRequest(state: TopicState, previousState: TopicState) {
  const request = state.topics[Topics.playtestDomQuery];
  if (request === previousState.topics[Topics.playtestDomQuery]) return;
  if (!isDomQueryRequest(request)) return;

  // DOMは読み取るだけに留め、クリックやスクロールなどの操作を行わない
  // Read the DOM only; never perform interactions such as clicks or scrolling
  const result = queryElement(request);
  void sendAction("playtest.dom_query_result", result).then((actionResult) => {
    // Unity側が拒否した応答はerror codeを残し、契約不整合を追跡可能にする
    // Preserve the error code for Unity-side rejections so contract mismatches remain diagnosable
    if (!actionResult.ok) console.warn(`[playtest.dom_query_result] rejected: ${actionResult.error ?? "unknown_error"}`);
  }).catch(() => {
    // 切断競合はWS境界で吸収し、Unity側の問い合わせタイムアウトへ委ねる
    // Absorb disconnect races at the WS boundary and let the Unity query time out
  });
}

function isDomQueryRequest(value: unknown): value is DomQueryRequest {
  if (typeof value !== "object" || value === null) return false;
  const candidate = value as Record<string, unknown>;
  return typeof candidate.requestId === "string" && typeof candidate.testid === "string";
}

function queryElement(request: DomQueryRequest): DomQueryResult {
  const selector = `[data-testid="${CSS.escape(request.testid)}"]`;
  const element = document.querySelector<HTMLElement>(selector);
  const devicePixelRatio = window.devicePixelRatio;

  if (element === null) {
    return {
      requestId: request.requestId,
      found: false,
      x: 0,
      y: 0,
      width: 0,
      height: 0,
      devicePixelRatio,
      hitTestPassed: false,
    };
  }

  const rect = element.getBoundingClientRect();
  let hitTestPassed = false;
  if (isElementVisible(element, rect)) {
    // 矩形中心の実ヒット要素を調べ、遮蔽物による空クリックを検出する
    // Inspect the actual hit at the rectangle center to detect blocked clicks
    const hitElement = document.elementFromPoint(rect.x + rect.width / 2, rect.y + rect.height / 2);
    hitTestPassed = hitElement !== null && (
      hitElement === element || element.contains(hitElement) || hitElement.contains(element)
    );
  }

  return {
    requestId: request.requestId,
    found: true,
    x: rect.x,
    y: rect.y,
    width: rect.width,
    height: rect.height,
    devicePixelRatio,
    hitTestPassed,
  };
}

function isElementVisible(element: HTMLElement, rect: DOMRect): boolean {
  if (element.hidden || element.closest('[aria-hidden="true"]')) return false;
  const style = getComputedStyle(element);
  if (style.display === "none" || style.visibility === "hidden") return false;

  // 面積があり、viewportと交差する要素だけをクリック可能とみなす
  // Treat only non-empty elements intersecting the viewport as clickable
  if (rect.width <= 0 || rect.height <= 0) return false;
  return rect.bottom > 0 && rect.right > 0 && rect.top < innerHeight && rect.left < innerWidth;
}
