import { useEffect, useRef } from "react";
import { sendInputState } from "@/bridge";
import { browserDefaultSuppressionFor, isPointerOverWebUi, isTextInputElement, reduceWebInputState, type WebInputState } from "./activeLayer";
import { watchPointerHitOnDomChange } from "./watchPointerHitOnDomChange";

type SentStateRef = { current: WebInputState };

// 送信済みと異なるときだけUnityへ送る
// Send to Unity only when the state differs from what was last sent
function sendIfChanged(sentStateRef: SentStateRef, change: Partial<WebInputState>) {
  const state = sentStateRef.current;
  const next = reduceWebInputState(state, change);
  if (next.pointerOverUi === state.pointerOverUi && next.textInputFocused === state.textInputFocused) return;
  sentStateRef.current = next;
  sendInputState(next.pointerOverUi, next.textInputFocused);
}

function stopHitWatch(stopHitWatchRef: { current: (() => void) | null }) {
  stopHitWatchRef.current?.();
  stopHitWatchRef.current = null;
}

// DOMのヒットテストとテキストフォーカスをUnityへ差分通知する
// Report DOM hit testing and text focus changes to Unity only when state changes
export function useWebInputExclusivity(webUiSuspended: boolean) {
  // 休止解除で捨てる送信済みキャッシュと、解除時の再判定に使う最後のポインタ位置
  // Sent-state cache dropped on resume, and the last pointer position used to re-hit-test then
  const sentStateRef = useRef<WebInputState>({ pointerOverUi: false, textInputFocused: false });
  const lastPointerRef = useRef<{ x: number; y: number } | null>(null);
  const wasSuspendedRef = useRef(webUiSuspended);
  const stopHitWatchRef = useRef<(() => void) | null>(null);

  // 休止中Unityは両値をfalseへ捨てるので、解除時はキャッシュを使わず実状態を必ず送り直す
  // Unity drops both values while suspended, so on resume always resend the real state, bypassing the cache
  useEffect(() => {
    const resumed = wasSuspendedRef.current && !webUiSuspended;
    wasSuspendedRef.current = webUiSuspended;
    if (!resumed) return;
    const pointer = lastPointerRef.current;
    const pointerOverUi = pointer != null && isPointerOverWebUi(document.elementFromPoint(pointer.x, pointer.y));
    sentStateRef.current = { pointerOverUi, textInputFocused: isTextInputElement(document.activeElement) };
    sendInputState(sentStateRef.current.pointerOverUi, sentStateRef.current.textInputFocused);

    // topic待ちで後から描かれるHUDに追従するため、次の実pointermoveまでDOM変化ごとに当たり判定し直す
    // Follow HUDs rendered later once their topic arrives by re-hit-testing on DOM changes until the next real pointermove
    if (pointer != null) {
      stopHitWatchRef.current = watchPointerHitOnDomChange(pointer.x, pointer.y, (hit) => sendIfChanged(sentStateRef, { pointerOverUi: hit }));
    }
    // 再休止・unmountで監視を外す
    // Stop watching on re-suspension or unmount
    return () => stopHitWatch(stopHitWatchRef);
  }, [webUiSuspended]);

  useEffect(() => {
    const update = (change: Partial<WebInputState>) => sendIfChanged(sentStateRef, change);

    // 実ポインタ移動が来たらイベント側の判定が正になるので、DOM監視は外す
    // Once a real pointer move arrives the event target is authoritative, so drop the DOM watch
    const onPointerMove = (event: PointerEvent) => {
      stopHitWatch(stopHitWatchRef);
      lastPointerRef.current = { x: event.clientX, y: event.clientY };
      update({ pointerOverUi: isPointerOverWebUi(event.target) });
    };
    const onPointerLeave = () => {
      stopHitWatch(stopHitWatchRef);
      lastPointerRef.current = null;
      update({ pointerOverUi: false });
    };
    const onFocusIn = (event: FocusEvent) => update({ textInputFocused: isTextInputElement(event.target) });
    const onFocusOut = () => queueMicrotask(() => update({ textInputFocused: isTextInputElement(document.activeElement) }));
    // keydown/keyupが同じ判別値を引くので、封じ方の非対称が生まれない
    // Both keydown and keyup read the same discriminant, so the two can never drift apart
    const applySuppression = (event: KeyboardEvent): boolean => {
      const kind = browserDefaultSuppressionFor(event.key, document.activeElement);
      switch (kind) {
        case "allow":
          return false;
        case "preventDefault":
          event.preventDefault();
          return true;
        case "preventDefaultAndStopPropagation":
          event.preventDefault();
          event.stopPropagation();
          return true;
        default: {
          const exhaustive: never = kind;
          return exhaustive;
        }
      }
    };

    const onKeyUp = (event: KeyboardEvent) => {
      applySuppression(event);
    };
    const onKeyDown = (event: KeyboardEvent) => {
      if (applySuppression(event)) return;
      if (event.key !== "Escape" || !sentStateRef.current.textInputFocused) return;
      (document.activeElement as HTMLElement | null)?.blur();
      event.preventDefault();
      event.stopPropagation();
      update({ textInputFocused: false });
    };

    document.addEventListener("pointermove", onPointerMove, true);
    document.documentElement.addEventListener("pointerleave", onPointerLeave);
    document.addEventListener("focusin", onFocusIn, true);
    document.addEventListener("focusout", onFocusOut, true);
    document.addEventListener("keydown", onKeyDown, true);
    document.addEventListener("keyup", onKeyUp, true);
    return () => {
      document.removeEventListener("pointermove", onPointerMove, true);
      document.documentElement.removeEventListener("pointerleave", onPointerLeave);
      document.removeEventListener("focusin", onFocusIn, true);
      document.removeEventListener("focusout", onFocusOut, true);
      document.removeEventListener("keydown", onKeyDown, true);
      document.removeEventListener("keyup", onKeyUp, true);
      sentStateRef.current = { pointerOverUi: false, textInputFocused: false };
      sendInputState(false, false);
    };
  }, []);
}
