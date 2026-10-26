import { Topics } from "../transport/protocol";
import { sendAction } from "../transport/webSocketClient";
import { DEFAULT_ACTION_TIMEOUT_MS } from "../transport/actions";
import { initPlaytestTopicResponder } from "./playtestTopicResponder";
import { findByTestId, isElementVisible } from "./testIdElement";

type DomSetTextRequest = { requestId: string; testid: string; text: string };
type DomSetTextStatus = "applied" | "notFound" | "notTextField" | "notInteractable" | "valueMismatch";
type DomSetTextResult = {
  requestId: string;
  status: DomSetTextStatus;
  matchCount: number;
  actualValue: string;
};

// プレイテストの文字入力要求を一度だけ受け付ける
// Register playtest text-input reception once
export function initDomSetTextResponder(): () => void {
  return initPlaytestTopicResponder(Topics.playtestDomSetText, respond);
}

function respond(request: unknown) {
  if (!isDomSetTextRequest(request)) {
    if (request !== undefined) console.warn("[playtest.dom_set_text] invalid request", request);
    return;
  }
  void applyText(request)
    .then((result) => sendAction("playtest.dom_set_text_result", result, DEFAULT_ACTION_TIMEOUT_MS))
    .then((actionResult) => {
      // Unity側の拒否を記録して契約不整合を追跡する
      // Record Unity rejections to trace contract mismatches
      if (!actionResult.ok) console.warn(`[playtest.dom_set_text_result] rejected: ${actionResult.error ?? "unknown_error"}`);
    })
    .catch((error: unknown) => {
      // WS境界の失敗を記録し、Unity側では要求タイムアウトとして検出する
      // Record WS boundary failures; Unity detects the request timeout
      console.warn("[playtest.dom_set_text_result] failed", error);
    });
}

function isDomSetTextRequest(value: unknown): value is DomSetTextRequest {
  if (typeof value !== "object" || value === null) return false;
  const candidate = value as Record<string, unknown>;
  return typeof candidate.requestId === "string" && typeof candidate.testid === "string" && typeof candidate.text === "string";
}

async function applyText(request: DomSetTextRequest): Promise<DomSetTextResult> {
  let { element, matchCount } = findByTestId(request.testid);
  const reply = (status: DomSetTextStatus, actualValue: string): DomSetTextResult =>
    ({ requestId: request.requestId, status, matchCount, actualValue });
  if (element === null) return reply("notFound", "");
  if (!isTextField(element)) return reply("notTextField", "");
  if (element.matches(":disabled") || element.readOnly || !isElementVisible(element, element.getBoundingClientRect())) {
    return reply("notInteractable", element.value);
  }

  // React制御入力へネイティブsetterを使い、inputイベントで変更を通知する
  // Use the native setter and input event to update React controlled fields
  const prototype = element instanceof HTMLInputElement ? HTMLInputElement.prototype : HTMLTextAreaElement.prototype;
  const nativeSetter = Object.getOwnPropertyDescriptor(prototype, "value")?.set;
  if (nativeSetter === undefined) {
    console.warn("[playtest.dom_set_text] native value setter missing");
    return reply("notTextField", element.value);
  }
  nativeSetter.call(element, request.text);
  element.dispatchEvent(new Event("input", { bubbles: true }));

  // 次のタスクで再描画による値の巻き戻りを確認する
  // Check on the next task whether a re-render reverted the value
  await new Promise<void>((resolve) => setTimeout(resolve, 0));
  ({ element, matchCount } = findByTestId(request.testid));
  if (element === null) return reply("notFound", "");
  if (!isTextField(element)) return reply("notTextField", "");
  return reply(element.value === request.text ? "applied" : "valueMismatch", element.value);
}

// 文字置換の契約に合う入力種だけを許可する
// Accept only input types that support the text replacement contract
function isTextField(element: Element): element is HTMLInputElement | HTMLTextAreaElement {
  return element instanceof HTMLTextAreaElement || (element instanceof HTMLInputElement &&
    ["text", "search", "tel", "url", "email", "password"].includes(element.type));
}
