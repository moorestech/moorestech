import { Topics } from "../transport/protocol";
import { sendAction } from "../transport/webSocketClient";
import { DEFAULT_ACTION_TIMEOUT_MS } from "../transport/actions";
import { initPlaytestTopicResponder } from "./playtestTopicResponder";
import { findByTestId, isElementVisible, readStateFlags } from "./testIdElement";

type DomQueryRequest = {
  requestId: string;
  testid: string;
};

type DomQueryResult = {
  requestId: string;
  found: boolean;
  visible: boolean;
  x: number;
  y: number;
  width: number;
  height: number;
  devicePixelRatio: number;
  hitTestPassed: boolean;
  text: string;
  matchCount: number;
  stateFlags: string[];
};

// DOM問い合わせの受信を一度だけ登録し、アプリ終了時に破棄できるようにする
// Register DOM query reception once and allow teardown at application shutdown
export function initDomQueryResponder(): () => void {
  return initPlaytestTopicResponder(Topics.playtestDomQuery, respond);
}

function respond(request: unknown) {
  if (!isDomQueryRequest(request)) {
    if (request !== undefined) console.warn("[playtest.dom_query] invalid request", request);
    return;
  }

  // DOMは読み取るだけに留め、クリックやスクロールなどの操作を行わない
  // Read the DOM only; never perform interactions such as clicks or scrolling
  const result = queryElement(request);
  void sendAction("playtest.dom_query_result", result, DEFAULT_ACTION_TIMEOUT_MS).then((actionResult) => {
    // Unity側が拒否した応答はerror codeを残し、契約不整合を追跡可能にする
    // Preserve the error code for Unity-side rejections so contract mismatches remain diagnosable
    if (!actionResult.ok) console.warn(`[playtest.dom_query_result] rejected: ${actionResult.error ?? "unknown_error"}`);
  }).catch((error: unknown) => {
    // WS境界の失敗を記録し、Unity側では要求タイムアウトとして検出する
    // Record WS boundary failures; Unity detects the request timeout
    console.warn("[playtest.dom_query_result] send failed", error);
  });
}

function isDomQueryRequest(value: unknown): value is DomQueryRequest {
  if (typeof value !== "object" || value === null) return false;
  const candidate = value as Record<string, unknown>;
  return typeof candidate.requestId === "string" && typeof candidate.testid === "string";
}

function queryElement(request: DomQueryRequest): DomQueryResult {
  const { element, matchCount } = findByTestId(request.testid);
  const devicePixelRatio = window.devicePixelRatio;

  if (element === null) {
    return {
      requestId: request.requestId,
      found: false,
      visible: false,
      x: 0,
      y: 0,
      width: 0,
      height: 0,
      devicePixelRatio,
      hitTestPassed: false,
      text: "",
      matchCount,
      stateFlags: [],
    };
  }

  const rect = element.getBoundingClientRect();
  let hitTestPassed = false;
  const visible = isElementVisible(element, rect);
  if (visible) {
    // 矩形中心の実ヒット要素を調べ、遮蔽物による空クリックを検出する
    // Inspect the actual hit at the rectangle center to detect blocked clicks
    const hitElement = document.elementFromPoint(rect.x + rect.width / 2, rect.y + rect.height / 2);
    hitTestPassed = hitElement !== null && (
      hitElement === element || element.contains(hitElement)
    );
  }

  return {
    requestId: request.requestId,
    found: true,
    visible,
    x: rect.x,
    y: rect.y,
    width: rect.width,
    height: rect.height,
    devicePixelRatio,
    hitTestPassed,
    text: element.innerText,
    matchCount,
    stateFlags: readStateFlags(element),
  };
}
