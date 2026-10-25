import { useTopicStore } from "../store/topicStore";
import { subscriptions } from "../transport/subscriptionManager";
import { sendAction } from "../transport/webSocketClient";
import { DEFAULT_ACTION_TIMEOUT_MS } from "../transport/actions";

// UITK段階0のS15実測専用（poc/uitk-stage0-measure）。委譲中を模して document 全体の pointer-events を切り替える
// Stage-0 S15 measurement only (poc/uitk-stage0-measure): toggles pointer-events on the whole document to mimic delegation
const ProbeTopic = "poc.pointer_events";
const StyleElementId = "poc-pointer-events-none";

type ProbeRequest = { requestId: string; mode: "root-none" | "all-none-important" | "restore" };

export function initPocPointerEventsProbe() {
  let acquired = false;
  useTopicStore.subscribe((state, previousState) => {
    if (state.status === "open" && !acquired) {
      acquired = true;
      subscriptions.acquire(ProbeTopic);
    }
    if (state.status !== "open" && acquired) {
      acquired = false;
      subscriptions.release(ProbeTopic);
    }
    const request = state.topics[ProbeTopic] as ProbeRequest | undefined;
    if (request === undefined || request === previousState.topics[ProbeTopic]) return;
    apply(request);
  });
}

function apply(request: ProbeRequest) {
  // root-none は html 要素だけ、all-none-important は全要素へ !important で効かせる（子の pointer-events:auto も潰す）
  // root-none touches only <html>; all-none-important forces every element with !important (also beating child pointer-events:auto)
  document.documentElement.style.pointerEvents = request.mode === "root-none" ? "none" : "";
  document.getElementById(StyleElementId)?.remove();
  if (request.mode === "all-none-important") {
    const style = document.createElement("style");
    style.id = StyleElementId;
    style.textContent = "*, *::before, *::after { pointer-events: none !important; }";
    document.head.appendChild(style);
  }
  const autoCount = Array.from(document.querySelectorAll("*")).filter((element) => getComputedStyle(element).pointerEvents !== "none").length;
  void sendAction("poc.pointer_events_ack", { requestId: request.requestId, mode: request.mode, elementsStillHittable: autoCount }, DEFAULT_ACTION_TIMEOUT_MS);
}
